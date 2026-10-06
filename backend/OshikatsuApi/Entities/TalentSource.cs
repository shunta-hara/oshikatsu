namespace OshikatsuApi.Entities;

/// <summary>
/// タレントのコンテンツソース（talent_sources テーブル）。
/// YouTube チャンネル・X ハンドル・イベント RSS 等を管理する。
/// </summary>
public class TalentSource
{
    public int Id { get; set; }

    /// <summary>紐づくタレントID（talents.id）</summary>
    public int TalentId { get; set; }

    /// <summary>ソースタイプ（"youtube" / "x_post" / "event" 等）</summary>
    public string SourceType { get; set; } = null!;

    /// <summary>ソース識別子（YouTube チャンネルID・X ハンドル・RSS URL 等）</summary>
    public string SourceId { get; set; } = null!;

    /// <summary>有効フラグ（false のときは収集をスキップする）</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    // Navigation
    public Talent Talent { get; set; } = null!;
}
