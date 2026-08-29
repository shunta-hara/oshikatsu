namespace OshikatsuApi.DTOs;

/// <summary>ユーザー向けニュースアイテムレスポンス</summary>
public class NewsItemResponseDto
{
    public int Id { get; set; }
    public int OshiId { get; set; }
    public string OshiName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Summary { get; set; }
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsPublishedAtEstimated { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
}
