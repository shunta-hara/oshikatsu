namespace OshikatsuApi.DTOs;

/// <summary>ダッシュボードカード用の軽量出演番組サマリー</summary>
public class ProgramAppearanceSummaryDto
{
    public int Id { get; set; }
    public string ProgramName { get; set; } = null!;
    public string? Broadcaster { get; set; }
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset CollectedAt { get; set; }
}
