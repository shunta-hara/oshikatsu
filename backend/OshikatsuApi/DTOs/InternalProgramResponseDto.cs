namespace OshikatsuApi.DTOs;

/// <summary>内部API: 出演番組情報登録レスポンス</summary>
public class InternalProgramResponseDto
{
    public int Id { get; set; }
    public int OshiId { get; set; }
    public string ProgramName { get; set; } = null!;
    public string? Broadcaster { get; set; }
    public DateTimeOffset? AiredAt { get; set; }
    public bool IsAiredAtEstimated { get; set; }
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset CollectedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>true = 既に登録済みのため無視（冪等）</summary>
    public bool WasDuplicate { get; set; }
}
