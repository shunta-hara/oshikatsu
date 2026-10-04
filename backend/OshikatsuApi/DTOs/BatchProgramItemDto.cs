using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>一括登録API: 出演番組アイテム1件（oshiId はリクエスト上位で指定）</summary>
public class BatchProgramItemDto
{
    /// <summary>番組名</summary>
    [Required]
    [MaxLength(500)]
    public string ProgramName { get; set; } = null!;

    /// <summary>放送局（任意）</summary>
    [MaxLength(200)]
    public string? Broadcaster { get; set; }

    /// <summary>
    /// 放送日時。取得できなかった場合は null。
    /// null の場合は CollectedAt で代替し、IsAiredAtEstimated が true になる。
    /// </summary>
    public DateTimeOffset? AiredAt { get; set; }

    /// <summary>収集元URL（重複排除キー）</summary>
    [Required]
    [MaxLength(2048)]
    public string SourceUrl { get; set; } = null!;

    /// <summary>collector が収集した日時</summary>
    [Required]
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>媒体名（FEAT-036）。RSS のタイトル末尾から抽出。取得できない場合は null。</summary>
    [MaxLength(200)]
    public string? SourceName { get; set; }
}
