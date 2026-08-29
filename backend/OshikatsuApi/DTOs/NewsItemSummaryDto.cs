namespace OshikatsuApi.DTOs;

/// <summary>ダッシュボードカード用の軽量ニュースサマリー</summary>
public class NewsItemSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset CollectedAt { get; set; }
}
