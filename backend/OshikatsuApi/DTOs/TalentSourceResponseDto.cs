namespace OshikatsuApi.DTOs;

/// <summary>内部API: タレントソースレスポンス</summary>
public class TalentSourceResponseDto
{
    public int Id { get; set; }
    public int TalentId { get; set; }
    public string SourceType { get; set; } = null!;
    public string SourceId { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}
