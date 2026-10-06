using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>内部API: タレントソース登録リクエスト</summary>
public class CreateTalentSourceRequestDto
{
    /// <summary>紐づくタレントID（talents.id）</summary>
    [Required]
    public int TalentId { get; set; }

    /// <summary>ソースタイプ（"youtube" / "x_post" / "event" 等）</summary>
    [Required]
    [MaxLength(50)]
    public string SourceType { get; set; } = null!;

    /// <summary>ソース識別子（YouTube チャンネルID・X ハンドル・RSS URL 等）</summary>
    [Required]
    [MaxLength(500)]
    public string SourceId { get; set; } = null!;

    /// <summary>有効フラグ（省略時 true）</summary>
    public bool IsEnabled { get; set; } = true;
}
