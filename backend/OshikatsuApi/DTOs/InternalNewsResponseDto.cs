namespace OshikatsuApi.DTOs;

/// <summary>内部API: ニュースアイテム登録レスポンス</summary>
public class InternalNewsResponseDto
{
    public int Id { get; set; }
    public int TalentId { get; set; }
    public string Title { get; set; } = null!;
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsPublishedAtEstimated { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>true = 既に登録済みのため無視（冪等）</summary>
    public bool WasDuplicate { get; set; }
}
