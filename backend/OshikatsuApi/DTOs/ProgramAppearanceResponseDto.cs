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

    /// <summary>媒体名（FEAT-036）。DB に保存されていない場合はタイトル末尾から取り出す。</summary>
    public string? SourceName { get; set; }

    /// <summary>まとめた他媒体の件数（FEAT-035）。同一番組が他媒体に転載されている数。</summary>
    public int OtherSourceCount { get; set; }

    /// <summary>まとめた他媒体のリンク一覧（FEAT-035）。</summary>
    public IReadOnlyList<OtherSourceDto> OtherSources { get; set; } = [];
}
