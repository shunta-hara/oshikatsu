using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>一括登録API: 出演番組一括登録リクエスト</summary>
public class BatchCreateProgramsRequestDto
{
    /// <summary>紐づくタレントID（talents.id）</summary>
    [Required]
    public int TalentId { get; set; }

    /// <summary>登録する出演番組アイテムのリスト（1〜200件）</summary>
    [Required]
    public List<BatchProgramItemDto> Items { get; set; } = null!;
}
