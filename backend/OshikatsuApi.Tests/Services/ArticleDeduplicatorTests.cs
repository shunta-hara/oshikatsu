using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>
/// FEAT-035: ArticleDeduplicator の正規化・グループ化・代表選択ロジックのテスト
/// </summary>
public class ArticleDeduplicatorTests
{
    // ── NormalizeTitle ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(
        "イコラブ大谷映美里「秋になったからニット着たの」 - Yahoo!ニュース",
        "イコラブ大谷映美里「秋になったからニット着たの」 - Infoseek",
        true)]
    [InlineData(
        "＝LOVE大谷映美里、前髪なしのかき上げヘア - モデルプレス",
        "LOVE大谷映美里、前髪なしのかき上げヘア - au Webポータル",
        true)]
    [InlineData(
        "【記事全文】イコラブ大谷映美里 大好物に衝撃 - スポニチ Sponichi Annex",
        "イコラブ大谷映美里 大好物に衝撃 - スポニチ Sponichi Annex",
        true)]
    [InlineData(
        "KAWAII LAB. Weverse LIVE - CS Kana Itak - Weverse",
        "KAWAII LAB. Weverse LIVE - CS Kana Itak - Weverse",
        true)]
    [InlineData(
        "完全に別記事A - Yahoo!ニュース",
        "完全に別記事B - Infoseek",
        false)]
    public void NormalizeTitle_RealWorldCases(string titleA, string titleB, bool expectedMatch)
    {
        var normA = ArticleDeduplicator.NormalizeTitle(titleA);
        var normB = ArticleDeduplicator.NormalizeTitle(titleB);

        if (expectedMatch)
            Assert.Equal(normA, normB);
        else
            Assert.NotEqual(normA, normB);
    }

    [Fact]
    public void NormalizeTitle_RemovesMediaSuffix()
    {
        var result = ArticleDeduplicator.NormalizeTitle("推しA最新情報 - スポニチ Sponichi Annex");
        Assert.Equal("推しa最新情報", result);
    }

    [Fact]
    public void NormalizeTitle_RemovesTrailingParenthetical()
    {
        var result = ArticleDeduplicator.NormalizeTitle("記事タイトル（スポニチアネックス）");
        Assert.Equal("記事タイトル", result);
    }

    [Fact]
    public void NormalizeTitle_RemovesFullTextPrefix()
    {
        var result = ArticleDeduplicator.NormalizeTitle("【記事全文】イコラブ大谷映美里");
        Assert.Equal("イコラブ大谷映美里", result);
    }

    [Fact]
    public void NormalizeTitle_RemovesPhotoPrefix()
    {
        var result = ArticleDeduplicator.NormalizeTitle("画像・写真 推しAのイベント");
        Assert.Equal("推しaのイベント", result);
    }

    [Fact]
    public void NormalizeTitle_NfkcNormalizesFullWidthChars()
    {
        // ＝LOVE → =LOVE（=は記号として除去、LOVE は love に変換）
        var result = ArticleDeduplicator.NormalizeTitle("＝LOVE大谷映美里 - 媒体名");
        Assert.Equal("love大谷映美里", result);
    }

    [Fact]
    public void NormalizeTitle_RemovesKatakanaProlong()
    {
        // ニットが含まれるタイトルでーは除去される
        var titleWith = "ニットー着用 - 媒体名";
        var titleWithout = "ニット着用 - 媒体名";
        Assert.Equal(
            ArticleDeduplicator.NormalizeTitle(titleWith),
            ArticleDeduplicator.NormalizeTitle(titleWithout));
    }

    [Fact]
    public void NormalizeTitle_LowercasesEnglish()
    {
        var result = ArticleDeduplicator.NormalizeTitle("LOVE大谷 - Media");
        Assert.Equal("love大谷", result);
    }

    [Fact]
    public void NormalizeTitle_HandlesLastDashMediaName()
    {
        // 媒体名に ` - ` を含む場合は最後の ` - ` の後ろが媒体名
        var result = ArticleDeduplicator.NormalizeTitle("KAWAII LAB. Weverse LIVE - CS Kana Itak - Weverse");
        Assert.Equal(ArticleDeduplicator.NormalizeTitle("KAWAII LAB. Weverse LIVE - CS Kana Itak - Another Media"), result);
    }

    // ── ExtractSourceNameFromTitle ────────────────────────────────────────────

    [Theory]
    [InlineData("記事タイトル - スポニチ Sponichi Annex", "スポニチ Sponichi Annex")]
    [InlineData("KAWAII LAB. Weverse LIVE - CS Kana Itak - Weverse", "Weverse")]
    [InlineData("タイトルのみ", null)]
    [InlineData("", null)]
    public void ExtractSourceNameFromTitle_VariousCases(string title, string? expected)
    {
        Assert.Equal(expected, ArticleDeduplicator.ExtractSourceNameFromTitle(title));
    }

    // ── IsRepostMedia ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Yahoo!ニュース", true)]
    [InlineData("Infoseek ニュース", true)]
    [InlineData("au Webポータル", true)]
    [InlineData("ライブドアニュース", true)]
    [InlineData("スポニチ Sponichi Annex", false)]
    [InlineData("モデルプレス", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsRepostMedia_KnownMediaNames(string? name, bool expected)
    {
        Assert.Equal(expected, ArticleDeduplicator.IsRepostMedia(name));
    }

    // ── GroupNewsItems ─────────────────────────────────────────────────────────

    private static DateTimeOffset T(int hours) => DateTimeOffset.UtcNow.AddHours(-1000 + hours);

    private static NewsItem MakeNews(int id, int oshiId, string title, int hours, string? sourceName = null) => new()
    {
        Id = id,
        OshiId = oshiId,
        Title = title,
        SourceUrl = $"https://example.com/{id}",
        PublishedAt = T(hours),
        IsPublishedAtEstimated = false,
        CollectedAt = T(hours),
        SourceName = sourceName,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void GroupNewsItems_SingleItem_ReturnsOneGroup()
    {
        var items = new[] { MakeNews(1, 1, "推しA最新情報 - スポニチ", 0) };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Single(groups);
        Assert.Equal(1, groups[0].Representative.Id);
        Assert.Empty(groups[0].Others);
    }

    [Fact]
    public void GroupNewsItems_SameTitleWithin48h_GroupedTogether()
    {
        // 同一推し・同一タイトル・24時間差 → まとめる
        var items = new[]
        {
            MakeNews(1, 1, "推しAの記事 - スポニチ", 0),
            MakeNews(2, 1, "推しAの記事 - Yahoo!ニュース", 20), // 20h差
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Single(groups);
        Assert.Single(groups[0].Others);
    }

    [Fact]
    public void GroupNewsItems_SameTitleExactly48h_GroupedTogether()
    {
        // ちょうど 48 時間差 → まとめる（境界値）
        // 固定時刻を使って epsilon の影響を排除する
        var anchor = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var items = new[]
        {
            new NewsItem { Id = 1, OshiId = 1, Title = "推しAの記事 - スポニチ",
                SourceUrl = "https://example.com/1", PublishedAt = anchor,
                CollectedAt = anchor, CreatedAt = DateTime.UtcNow },
            new NewsItem { Id = 2, OshiId = 1, Title = "推しAの記事 - Yahoo!ニュース",
                SourceUrl = "https://example.com/2", PublishedAt = anchor.AddHours(48),
                CollectedAt = anchor.AddHours(48), CreatedAt = DateTime.UtcNow },
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Single(groups);
    }

    [Fact]
    public void GroupNewsItems_SameTitleOver48h_NotGrouped()
    {
        // 48 時間超は別記事
        var items = new[]
        {
            MakeNews(1, 1, "推しAの記事 - スポニチ", 0),
            MakeNews(2, 1, "推しAの記事 - Infoseek", 49),
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void GroupNewsItems_RealWorldSample_SameArticleDifferentMedia()
    {
        // 2026-10-04 の実データに相当するケース
        var items = new[]
        {
            MakeNews(1, 1, "イコラブ大谷映美里「秋になったからニット着たの」悩殺ニットにファンくぎ付け「尊い」「メロいー！」（スポニチアネックス） - Yahoo!ニュース", 0, "Yahoo!ニュース"),
            MakeNews(2, 1, "イコラブ大谷映美里「秋になったからニット着たの」悩殺ニットにファンくぎ付け「尊い」「メロいー！」 - Infoseek", 1, "Infoseek"),
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Single(groups);
    }

    [Fact]
    public void GroupNewsItems_RealWorldSample_FullWidthEqualsSign()
    {
        // ＝LOVE と LOVE が一致する
        var items = new[]
        {
            MakeNews(1, 1, "＝LOVE大谷映美里、前髪なしのかき上げヘアで雰囲気ガラリ「めっちゃ大人っぽい」 - モデルプレス", 0, "モデルプレス"),
            MakeNews(2, 1, "LOVE大谷映美里、前髪なしのかき上げヘアで雰囲気ガラリ「めっちゃ大人っぽい」 - au Webポータル", 2, "au Webポータル"),
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Single(groups);
    }

    [Fact]
    public void GroupNewsItems_KawaiiLabSameTitle_Over48h_NotGrouped()
    {
        // 2026-08-02 と 2026-09-02 は 744h 差 → 別記事
        var base1 = DateTimeOffset.Parse("2026-08-02T00:00:00Z");
        var base2 = DateTimeOffset.Parse("2026-09-02T00:00:00Z");

        var items = new[]
        {
            new NewsItem { Id = 1, OshiId = 1, Title = "KAWAII LAB. Weverse LIVE - CS Kana Itak - Weverse",
                SourceUrl = "https://example.com/1", PublishedAt = base1, CollectedAt = base1, CreatedAt = DateTime.UtcNow },
            new NewsItem { Id = 2, OshiId = 1, Title = "KAWAII LAB. Weverse LIVE - CS Kana Itak - Weverse",
                SourceUrl = "https://example.com/2", PublishedAt = base2, CollectedAt = base2, CreatedAt = DateTime.UtcNow },
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void GroupNewsItems_ChainDoesNotTransitivelyGroup()
    {
        // A=anchor, B=anchor+40h, C=anchor+80h
        // anchor-based（非トランジティブ）の動作:
        //   - B は A から 40h → 同グループ（グループ1: A+B、anchor=A）
        //   - C は A から 80h > 48h → 別グループ（グループ2: C）
        // B-C の差が 40h でも「Aからの距離」で判定するため連鎖しない
        var anchor = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var items = new[]
        {
            new NewsItem { Id = 1, OshiId = 1, Title = "推しAの記事 - スポニチ",
                SourceUrl = "https://example.com/1", PublishedAt = anchor,
                CollectedAt = anchor, CreatedAt = DateTime.UtcNow },
            new NewsItem { Id = 2, OshiId = 1, Title = "推しAの記事 - Yahoo!ニュース",
                SourceUrl = "https://example.com/2", PublishedAt = anchor.AddHours(40),
                CollectedAt = anchor.AddHours(40), CreatedAt = DateTime.UtcNow },
            new NewsItem { Id = 3, OshiId = 1, Title = "推しAの記事 - Infoseek",
                SourceUrl = "https://example.com/3", PublishedAt = anchor.AddHours(80),
                CollectedAt = anchor.AddHours(80), CreatedAt = DateTime.UtcNow },
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Equal(2, groups.Count);
        // グループを Others 件数で識別：A+B グループが 1 件の Other を持つ
        var twoMemberGroup = groups.Single(g => g.Others.Count == 1);
        var oneMemberGroup = groups.Single(g => g.Others.Count == 0);
        // A+B グループの代表はスポニチ（非転載先）
        Assert.Contains("スポニチ", twoMemberGroup.Representative.Title);
        // C は単独（Infoseek は転載先リストにあるが単独なので代表になる）
        Assert.Contains("Infoseek", oneMemberGroup.Representative.Title);
    }

    [Fact]
    public void GroupNewsItems_DifferentOshiSameTitle_NotGrouped()
    {
        var items = new[]
        {
            MakeNews(1, 1, "推しAの記事 - スポニチ", 0),
            MakeNews(2, 2, "推しAの記事 - Yahoo!ニュース", 1), // 別推し
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void GroupNewsItems_RepresentativeIsNonRepostMedia()
    {
        // Yahoo!ニュース（転載先）よりスポニチ（オリジナル）が代表になる
        var items = new[]
        {
            MakeNews(1, 1, "推しAの記事 - Yahoo!ニュース", 0, "Yahoo!ニュース"),
            MakeNews(2, 1, "推しAの記事 - スポニチ Sponichi Annex", 1, "スポニチ Sponichi Annex"),
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Single(groups);
        // 代表は転載先でない「スポニチ」（Yahoo! は転載先）
        Assert.Equal(2, groups[0].Representative.Id);
        Assert.Equal(1, groups[0].Others[0].Id);
    }

    [Fact]
    public void GroupNewsItems_OutputSortedByRepresentativePublishedAtDesc()
    {
        // グループ A: 24h前（新しい）
        // グループ B: 48h前（古い）
        // → A が先に来るべき
        var items = new[]
        {
            MakeNews(1, 1, "グループA記事 - スポニチ", -24),     // 24h前
            MakeNews(2, 1, "グループB記事 - モデルプレス", -48), // 48h前
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Equal(2, groups.Count);
        Assert.True(groups[0].Representative.PublishedAt >= groups[1].Representative.PublishedAt);
    }

    [Fact]
    public void GroupNewsItems_TotalCountReflectsGroupCount()
    {
        // 4記事が2グループにまとまる → 件数は 2
        var items = new[]
        {
            MakeNews(1, 1, "記事X - スポニチ", 0),
            MakeNews(2, 1, "記事X - Yahoo!ニュース", 1),
            MakeNews(3, 1, "記事Y - モデルプレス", 0),
            MakeNews(4, 1, "記事Y - Infoseek", 2),
        };

        var groups = ArticleDeduplicator.GroupNewsItems(items);

        Assert.Equal(2, groups.Count);
        Assert.Single(groups[0].Others);
        Assert.Single(groups[1].Others);
    }

    [Fact]
    public void GroupNewsItems_EmptyInput_ReturnsEmpty()
    {
        var groups = ArticleDeduplicator.GroupNewsItems([]);
        Assert.Empty(groups);
    }

    // ── GroupProgramItems ──────────────────────────────────────────────────────

    private static ProgramAppearance MakeProgram(int id, int oshiId, string name, int hours, string? sourceName = null) => new()
    {
        Id = id,
        OshiId = oshiId,
        ProgramName = name,
        SourceUrl = $"https://example.com/prog/{id}",
        AiredAt = T(hours),
        IsAiredAtEstimated = false,
        CollectedAt = T(hours),
        SourceName = sourceName,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void GroupProgramItems_SameTitleWithin48h_GroupedTogether()
    {
        var items = new[]
        {
            MakeProgram(1, 1, "番組A - Weverse", 0),
            MakeProgram(2, 1, "番組A - YouTube", 10),
        };

        var groups = ArticleDeduplicator.GroupProgramItems(items);

        Assert.Single(groups);
    }

    [Fact]
    public void GroupProgramItems_Over48h_NotGrouped()
    {
        var items = new[]
        {
            MakeProgram(1, 1, "番組A - Weverse", 0),
            MakeProgram(2, 1, "番組A - Weverse", 49),
        };

        var groups = ArticleDeduplicator.GroupProgramItems(items);

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void GroupProgramItems_EmptyInput_ReturnsEmpty()
    {
        var groups = ArticleDeduplicator.GroupProgramItems([]);
        Assert.Empty(groups);
    }
}
