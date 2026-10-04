using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>
/// GalleryTitleDetector の単体テスト。
/// .NET の Regex を使って R1〜R5 の判定ルールが、仕様書に示された実例タイトルと
/// 「除外してはいけない例」の両方で期待どおりに動作することを確認する。
/// </summary>
public class GalleryTitleDetectorTests
{
    // ── R1: 括弧付き n/m パターン ────────────────────────────────────────────

    [Theory]
    [InlineData("(画像2/12) CUTIE STREET板倉可奈、グレーカラコン×王冠で雰囲気一変…【IDOL RUNWAY COLLECTION 2026】 - モデルプレス")]
    [InlineData("（画像5/10）アーティスト名 イベントレポート")]
    [InlineData("(写真3/8) テスト記事タイトル")]
    [InlineData("（写真・画像 2/5）テスト")]
    public void IsGalleryTitle_R1_BracketedNofM_ReturnsTrue(string title)
    {
        Assert.True(GalleryTitleDetector.IsGalleryTitle(title));
    }

    // ── R2: ラベル(n/m) パターン ─────────────────────────────────────────────

    [Theory]
    [InlineData("画像・写真 | CUTIE STREET、ロッテ新商品を\"即興\"食リポ … 13枚目 - オリコンニュース")]
    [InlineData("写真・画像 | テスト記事 … 5枚目 - サイト名")]
    [InlineData("画像・写真(3/12) テスト")]
    [InlineData("写真・画像（1/8）テスト")]
    public void IsGalleryTitle_R2orR3_LabelsWithPipeOrBracket_ReturnsTrue(string title)
    {
        Assert.True(GalleryTitleDetector.IsGalleryTitle(title));
    }

    // ── R3: 先頭ラベルパターン ────────────────────────────────────────────────

    [Theory]
    [InlineData("板倉可奈の画像・写真 | 【MAJ2026】Mrs. GREEN APPLE、レッドカーペットにサプライズ登場 … 2枚目 - オリコンニュース")]
    [InlineData("画像・写真 CUTIE STREET、新CM内での\"スプーンダンス\"を披露…(24/49) - WEBザテレビジョン")]
    [InlineData("写真・画像 テスト記事")]
    public void IsGalleryTitle_R3_LeadingLabel_ReturnsTrue(string title)
    {
        Assert.True(GalleryTitleDetector.IsGalleryTitle(title));
    }

    // ── R4: 画像一覧パターン ──────────────────────────────────────────────────

    [Theory]
    [InlineData("板倉可奈の画像一覧 - オリコンニュース")]
    [InlineData("津田健次郎さんの写真一覧")]
    [InlineData("○○アーティストの画像一覧")]
    public void IsGalleryTitle_R4_ImageList_ReturnsTrue(string title)
    {
        Assert.True(GalleryTitleDetector.IsGalleryTitle(title));
    }

    // ── R5: 先頭「写真：」「写真:」パターン ──────────────────────────────────

    [Theory]
    [InlineData("写真：CUTIE STREET・板倉可奈 上の世代のLINEに疑問… - 東スポWEB")]
    [InlineData("写真:テスト記事タイトル")]
    [InlineData("写真 ： テスト")]
    public void IsGalleryTitle_R5_LeadingPhotoColon_ReturnsTrue(string title)
    {
        Assert.True(GalleryTitleDetector.IsGalleryTitle(title));
    }

    // ── 括弧パターン変形（西日本新聞・産経ニュース形式）──────────────────────

    [Theory]
    [InlineData("津田健次郎さん - 声も「人格の象徴」初判断 AI声模倣動画訴訟、東京地裁 - 写真・画像(1/2) - 西日本新聞me")]
    [InlineData("声優の津田健次郎さんの請求退ける 「生成AIで声模倣」…訴訟（写真・画像 1/1） - 産経ニュース")]
    public void IsGalleryTitle_R2_InlineNofM_ReturnsTrue(string title)
    {
        Assert.True(GalleryTitleDetector.IsGalleryTitle(title));
    }

    // ── 除外してはいけない例 ──────────────────────────────────────────────────

    [Theory]
    [InlineData("第2話（10/5放送）")]
    [InlineData("【写真】推しAがイベントに登場")]
    [InlineData("【画像】新曲MVのワンシーン公開")]
    [InlineData("3枚目のシングル「タイトル」発売決定")]
    [InlineData("写真集発売決定 1/24")]
    [InlineData("フォトギャラリー公開 - ニュースサイト")]
    [InlineData("本日（9/30）20時より公開")]
    [InlineData("CUTIE STREET、NHK「紅白歌合戦」出場決定")]
    [InlineData("推しAのMV 1,200万再生を突破")]
    [InlineData("CUTIE STREET 写真：が途中に含まれる記事タイトル")]
    public void IsGalleryTitle_NonGalleryTitles_ReturnsFalse(string title)
    {
        Assert.False(GalleryTitleDetector.IsGalleryTitle(title));
    }

    // ── 境界値: null・空文字列 ────────────────────────────────────────────────

    [Fact]
    public void IsGalleryTitle_NullTitle_ReturnsFalse()
    {
        Assert.False(GalleryTitleDetector.IsGalleryTitle(null!));
    }

    [Fact]
    public void IsGalleryTitle_EmptyTitle_ReturnsFalse()
    {
        Assert.False(GalleryTitleDetector.IsGalleryTitle(string.Empty));
    }

    // ── 正常ニュースタイトル ──────────────────────────────────────────────────

    [Theory]
    [InlineData("CUTIE STREET、秋のコンサートツアーを発表")]
    [InlineData("板倉可奈、ドラマ出演が決定 - スポーツ報知")]
    [InlineData("津田健次郎、新アニメのキャスト発表")]
    public void IsGalleryTitle_NormalNewsTitle_ReturnsFalse(string title)
    {
        Assert.False(GalleryTitleDetector.IsGalleryTitle(title));
    }
}
