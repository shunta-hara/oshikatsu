namespace OshikatsuApi.DTOs;

/// <summary>ダッシュボードカード用の軽量ニュースサマリー</summary>
public class NewsItemSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>媒体名（FEAT-036）。</summary>
    public string? SourceName { get; set; }

    /// <summary>まとめた他媒体の件数（FEAT-035）。</summary>
    public int OtherSourceCount { get; set; }

    /// <summary>まとめた他媒体のリンク一覧（FEAT-035）。</summary>
    public IReadOnlyList<OtherSourceDto> OtherSources { get; set; } = [];
}
