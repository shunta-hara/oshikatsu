using System.Text.RegularExpressions;

namespace OshikatsuApi.Services;

/// <summary>
/// ニュース・出演情報タイトルが「画像箱」（フォトギャラリー記事）かどうかを判定する。
/// 判定ルール R1〜R5 はここに集中管理する。新しいパターンが必要な場合はここに追加する。
/// </summary>
/// <remarks>
/// R1: 括弧付き n/m —— 「(画像10/12)」「（写真2/5）」など
/// R2: ラベル(n/m) —— 「画像・写真(3/12)」「写真・画像（1/8）」など
/// R3: 先頭ラベル —— 「画像・写真 |」「○○の写真・画像 |」「画像・写真 タイトル」など
/// R4: 画像一覧 —— 「○○の画像一覧 - サイト名」「○○の写真一覧」など
/// R5: 先頭「写真：」「写真:」 —— 「写真：推しA 笑顔で登場」など
///
/// 注意: EF Core の LINQ クエリには変換できないため、メモリ上でのフィルタに使う。
/// .NET の Regex を使うことで、PostgreSQL との正規表現方言の差異を回避する。
/// </remarks>
public static class GalleryTitleDetector
{
    // タイムアウト付きで正規表現を定義（ReDoS 対策）
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>R1: 括弧付き n/m パターン — 「(画像10/12)」「（写真2/5）」</summary>
    private static readonly Regex PatternR1 = new(
        @"[（(]\s*(?:画像|写真)(?:・(?:画像|写真))?\s*\d+\s*[/／]\s*\d+\s*[）)]",
        RegexOptions.Compiled, RegexTimeout);

    /// <summary>R2: ラベル(n/m) — 「画像・写真(3/12)」「写真・画像（1/8）」</summary>
    private static readonly Regex PatternR2 = new(
        @"(?:画像・写真|写真・画像)\s*[（(]\s*\d+\s*[/／]\s*\d+\s*[）)]",
        RegexOptions.Compiled, RegexTimeout);

    /// <summary>R3: 先頭ラベル — 「画像・写真 |」「○○の写真・画像 |」「画像・写真 タイトル」</summary>
    private static readonly Regex PatternR3 = new(
        @"^(?:[^|｜]{1,40}の)?(?:画像・写真|写真・画像)(?:\s*[|｜]|\s)",
        RegexOptions.Compiled, RegexTimeout);

    /// <summary>R4: 画像一覧 — 「○○の画像一覧 - サイト名」「○○の写真一覧」</summary>
    private static readonly Regex PatternR4 = new(
        @"の(?:画像|写真)一覧(?:\s+-\s+[^-]+)?$",
        RegexOptions.Compiled, RegexTimeout);

    /// <summary>R5: 先頭「写真：」「写真:」 — 「写真：推しA 笑顔で登場」</summary>
    private static readonly Regex PatternR5 = new(
        @"^写真\s*[：:]",
        RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    /// タイトルが画像箱記事かどうかを判定する。
    /// R1〜R5 のいずれかに一致する場合 <c>true</c> を返す。
    /// </summary>
    public static bool IsGalleryTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return false;

        try
        {
            return PatternR1.IsMatch(title)
                || PatternR2.IsMatch(title)
                || PatternR3.IsMatch(title)
                || PatternR4.IsMatch(title)
                || PatternR5.IsMatch(title);
        }
        catch (RegexMatchTimeoutException)
        {
            // タイムアウトが発生した場合は画像箱と判定しない（false positive より false negative を優先）
            return false;
        }
    }
}
