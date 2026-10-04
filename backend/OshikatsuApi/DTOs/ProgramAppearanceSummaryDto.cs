namespace OshikatsuApi.DTOs;

/// <summary>ダッシュボードカード用の軽量出演番組サマリー</summary>
public class ProgramAppearanceSummaryDto
{
    public int Id { get; set; }
    public string ProgramName { get; set; } = null!;
    public string? Broadcaster { get; set; }
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>媒体名（FEAT-036）。</summary>
    public string? SourceName { get; set; }

    /// <summary>まとめた他媒体の件数（FEAT-035）。</summary>
    public int OtherSourceCount { get; set; }

    /// <summary>まとめた他媒体のリンク一覧（FEAT-035）。</summary>
    public IReadOnlyList<OtherSourceDto> OtherSources { get; set; } = [];
}
