namespace OshikatsuApi.DTOs;

/// <summary>ユーザー向け出演番組レスポンス</summary>
public class ProgramAppearanceResponseDto
{
    public int Id { get; set; }
    public int OshiId { get; set; }
    public string OshiName { get; set; } = null!;
    public string ProgramName { get; set; } = null!;
    public string? Broadcaster { get; set; }
    public DateTimeOffset? AiredAt { get; set; }
    public bool IsAiredAtEstimated { get; set; }
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset CollectedAt { get; set; }
}
