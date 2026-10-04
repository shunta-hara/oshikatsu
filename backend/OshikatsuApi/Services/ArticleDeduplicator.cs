using System.Text;
using System.Text.RegularExpressions;
using OshikatsuApi.Entities;

namespace OshikatsuApi.Services;

/// <summary>
/// 転載記事の重複まとめロジック（FEAT-035）。
/// タイトル正規化・48時間判定・代表選択のルールをここに集約する。
/// </summary>
/// <remarks>
/// 【使い方】
/// 1. <see cref="NormalizeTitle"/> でタイトルを正規化して比較キーを得る。
/// 2. <see cref="GroupNewsItems"/> または <see cref="GroupProgramItems"/> でグループ化する。
/// 3. 呼び出し元はグループごとに代表 + 他媒体リストをレスポンスに組み立てる。
///
/// 【ルール（3層の契約）】
/// - タイトル末尾の ` - 媒体名` を除去する（最後の ` - ` で分割）。
/// - 末尾の括弧内の元媒体名 `（...）` または `(...)` を除去する。
/// - 先頭の `【記事全文】` を除去する。
/// - 先頭の `画像・写真 ` を除去する。
/// - Unicode NFKC 正規化（全角英数字・記号を半角に統一）。
/// - 記号・空白・カタカナ長音符（ー）を除去して英字を小文字にそろえる。
///
/// 【連鎖グループの扱い】
/// グループ内の最初の記事（最古）を起点として、その PublishedAt から 48 時間以内の記事を同一グループとする。
/// 連鎖（A-B=40h、B-C=40h）の場合、C は A から 80h 離れているため別グループになる（非トランジティブ）。
/// </remarks>
public static class ArticleDeduplicator
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MaxGroupSpan = TimeSpan.FromHours(48);

    /// <summary>
    /// 転載先として認識する媒体名リスト。
    /// 新しい媒体を追加する場合はここに追記する。
    /// </summary>
    private static readonly HashSet<string> RepostMediaNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Yahoo!ニュース",
        "Yahoo! JAPAN ニュース",
        "Infoseek ニュース",
        "Infoseek",
        "au Webポータル",
        "ライブドアニュース",
        "gooニュース",
        "エキサイトニュース",
        "BIGLOBEニュース",
        "dメニューニュース",
        "LINE NEWS",
        "MSN",
        "ニコニコニュース",
    };

    /// <summary>末尾の括弧内媒体名を除去する正規表現</summary>
    private static readonly Regex TrailingParenthetical = new(
        @"\s*[（(][^）)]{1,60}[）)]\s*$", RegexOptions.Compiled, Timeout);

    /// <summary>
    /// タイトルを正規化して重複比較用のキーを返す。
    /// </summary>
    public static string NormalizeTitle(string title)
    {
        // Step 1: 末尾の ` - 媒体名` を除去（最後の ` - ` で分割）
        var dashIndex = title.LastIndexOf(" - ", StringComparison.Ordinal);
        if (dashIndex >= 0)
            title = title[..dashIndex].TrimEnd();

        // Step 2: 末尾の括弧内の元媒体名 `（媒体名）` を除去
        title = TrailingParenthetical.Replace(title, "").TrimEnd();

        // Step 3: 先頭の `【記事全文】` を除去
        const string FullTextPrefix = "【記事全文】";
        if (title.StartsWith(FullTextPrefix, StringComparison.Ordinal))
            title = title[FullTextPrefix.Length..].TrimStart();

        // Step 4: 先頭の `画像・写真 ` を除去
        const string PhotoPrefix = "画像・写真 ";
        if (title.StartsWith(PhotoPrefix, StringComparison.Ordinal))
            title = title[PhotoPrefix.Length..].TrimStart();

        // Step 5: NFKC 正規化（全角→半角）
        title = title.Normalize(NormalizationForm.FormKC);

        // Step 6: 記号・空白・ー（カタカナ長音符 U+30FC）を除去し、英字を小文字に
        var sb = new StringBuilder(title.Length);
        foreach (var ch in title)
        {
            // ー（U+30FC、カテゴリ Lm）は明示的に除去
            if (ch == 'ー') continue;
            // 文字（\p{L}相当）または数字（\p{N}相当）のみ残す
            var cat = char.GetUnicodeCategory(ch);
            if (cat is System.Globalization.UnicodeCategory.UppercaseLetter
                    or System.Globalization.UnicodeCategory.LowercaseLetter
                    or System.Globalization.UnicodeCategory.TitlecaseLetter
                    or System.Globalization.UnicodeCategory.OtherLetter
                    or System.Globalization.UnicodeCategory.DecimalDigitNumber
                    or System.Globalization.UnicodeCategory.LetterNumber
                    or System.Globalization.UnicodeCategory.OtherNumber)
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 媒体名が転載先媒体リストに含まれるかどうかを返す。
    /// </summary>
    public static bool IsRepostMedia(string? sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return false;
        return RepostMediaNames.Contains(sourceName.Trim());
    }

    /// <summary>
    /// タイトル末尾の ` - 媒体名` から媒体名を取り出す。
    /// ` - ` が存在しない場合は null を返す。
    /// </summary>
    public static string? ExtractSourceNameFromTitle(string? title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        var idx = title.LastIndexOf(" - ", StringComparison.Ordinal);
        return idx >= 0 ? title[(idx + 3)..].Trim() : null;
    }

    // ── ニュースグループ ──────────────────────────────────────────────────────

    /// <summary>ニュースグループの代表 + 他媒体リスト</summary>
    public sealed class NewsGroup
    {
        public NewsItem Representative { get; init; } = null!;
        public IReadOnlyList<NewsItem> Others { get; init; } = [];
    }

    /// <summary>
    /// ニュースアイテムのリストを転載重複まとめしてグループに整理する。
    /// 入力は PublishedAt 降順、同一のとき Id 降順でソート済みであること。
    /// 出力は代表の PublishedAt 降順、同一のとき Id 降順で並ぶ。
    /// </summary>
    public static IReadOnlyList<NewsGroup> GroupNewsItems(IReadOnlyList<NewsItem> items)
    {
        if (items.Count == 0) return [];

        // 昇順に処理してグループを構築する（anchor = 最古）
        var ascending = items
            .OrderBy(n => n.PublishedAt)
            .ThenBy(n => n.Id)
            .ToList();

        // key: (oshiId, normalizedTitle) → (anchorPublishedAt, List<NewsItem>)
        var pendingGroups = new List<(int OshiId, string NKey, DateTimeOffset Anchor, List<NewsItem> Members)>();

        foreach (var item in ascending)
        {
            var nkey = NormalizeTitle(item.Title);
            var publishedAt = item.PublishedAt ?? DateTimeOffset.UtcNow;
            bool added = false;

            for (int i = 0; i < pendingGroups.Count; i++)
            {
                var g = pendingGroups[i];
                if (g.OshiId == item.OshiId && g.NKey == nkey
                    && (publishedAt - g.Anchor) <= MaxGroupSpan)
                {
                    g.Members.Add(item);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                pendingGroups.Add((item.OshiId, nkey, publishedAt, [item]));
            }
        }

        // グループごとに代表を選んで NewsGroup を作る
        var result = new List<NewsGroup>(pendingGroups.Count);
        foreach (var (_, _, _, members) in pendingGroups)
        {
            var (rep, others) = SelectRepresentative(members);
            result.Add(new NewsGroup { Representative = rep, Others = others });
        }

        // 代表の PublishedAt 降順・Id 降順で並べ直す（元の表示順に合わせる）
        result.Sort((a, b) =>
        {
            var cmp = Nullable.Compare(b.Representative.PublishedAt, a.Representative.PublishedAt);
            return cmp != 0 ? cmp : b.Representative.Id.CompareTo(a.Representative.Id);
        });

        return result;
    }

    private static (NewsItem Representative, IReadOnlyList<NewsItem> Others) SelectRepresentative(
        List<NewsItem> members)
    {
        if (members.Count == 1) return (members[0], []);

        // 転載先でない媒体を優先、同条件なら PublishedAt の早いものを代表とする
        var sorted = members
            .OrderBy(n => IsRepostMedia(n.SourceName ?? ExtractSourceNameFromTitle(n.Title)) ? 1 : 0)
            .ThenBy(n => n.PublishedAt)
            .ThenBy(n => n.Id)
            .ToList();

        var rep = sorted[0];
        var others = sorted.Skip(1).ToList();
        return (rep, others);
    }

    // ── 番組グループ ──────────────────────────────────────────────────────────

    /// <summary>出演番組グループの代表 + 他媒体リスト</summary>
    public sealed class ProgramGroup
    {
        public ProgramAppearance Representative { get; init; } = null!;
        public IReadOnlyList<ProgramAppearance> Others { get; init; } = [];
    }

    /// <summary>
    /// 出演番組アイテムのリストを転載重複まとめしてグループに整理する。
    /// 入力は AiredAt 降順、同一のとき Id 降順でソート済みであること。
    /// 出力は代表の AiredAt 降順、同一のとき Id 降順で並ぶ。
    /// </summary>
    public static IReadOnlyList<ProgramGroup> GroupProgramItems(IReadOnlyList<ProgramAppearance> items)
    {
        if (items.Count == 0) return [];

        // 昇順に処理してグループを構築する（anchor = 最古）
        var ascending = items
            .OrderBy(p => p.AiredAt)
            .ThenBy(p => p.Id)
            .ToList();

        var pendingGroups = new List<(int OshiId, string NKey, DateTimeOffset Anchor, List<ProgramAppearance> Members)>();

        foreach (var item in ascending)
        {
            var nkey = NormalizeTitle(item.ProgramName);
            var airedAt = item.AiredAt ?? DateTimeOffset.UtcNow;
            bool added = false;

            for (int i = 0; i < pendingGroups.Count; i++)
            {
                var g = pendingGroups[i];
                if (g.OshiId == item.OshiId && g.NKey == nkey
                    && (airedAt - g.Anchor) <= MaxGroupSpan)
                {
                    g.Members.Add(item);
                    added = true;
                    break;
                }
            }

            if (!added)
            {
                pendingGroups.Add((item.OshiId, nkey, airedAt, [item]));
            }
        }

        var result = new List<ProgramGroup>(pendingGroups.Count);
        foreach (var (_, _, _, members) in pendingGroups)
        {
            var (rep, others) = SelectProgramRepresentative(members);
            result.Add(new ProgramGroup { Representative = rep, Others = others });
        }

        // 代表の AiredAt 降順・Id 降順で並べ直す
        result.Sort((a, b) =>
        {
            var cmp = Nullable.Compare(b.Representative.AiredAt, a.Representative.AiredAt);
            return cmp != 0 ? cmp : b.Representative.Id.CompareTo(a.Representative.Id);
        });

        return result;
    }

    private static (ProgramAppearance Representative, IReadOnlyList<ProgramAppearance> Others) SelectProgramRepresentative(
        List<ProgramAppearance> members)
    {
        if (members.Count == 1) return (members[0], []);

        var sorted = members
            .OrderBy(p => IsRepostMedia(p.SourceName ?? ExtractSourceNameFromTitle(p.ProgramName)) ? 1 : 0)
            .ThenBy(p => p.AiredAt)
            .ThenBy(p => p.Id)
            .ToList();

        var rep = sorted[0];
        var others = sorted.Skip(1).ToList();
        return (rep, others);
    }
}
