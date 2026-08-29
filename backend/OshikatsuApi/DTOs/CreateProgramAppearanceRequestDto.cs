using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>内部API: 出演番組情報登録リクエスト</summary>
public class CreateProgramAppearanceRequestDto
{
    /// <summary>紐づく推しID</summary>
    [Required]
    public int OshiId { get; set; }

    /// <summary>番組名</summary>
    [Required]
    [MaxLength(500)]
    public string ProgramName { get; set; } = null!;

    /// <summary>放送局（任意）</summary>
    [MaxLength(200)]
    public string? Broadcaster { get; set; }

    /// <summary>
    /// 放送日時（UTC）。取得できなかった場合は null。
    /// null の場合は CollectedAt で代替し、IsAiredAtEstimated が true になる。
    /// </summary>
    public DateTimeOffset? AiredAt { get; set; }

    /// <summary>収集元URL（重複排除キー）</summary>
    [Required]
    [MaxLength(2048)]
    public string SourceUrl { get; set; } = null!;

    /// <summary>collector が収集した日時（UTC）</summary>
    [Required]
    public DateTimeOffset CollectedAt { get; set; }
}
