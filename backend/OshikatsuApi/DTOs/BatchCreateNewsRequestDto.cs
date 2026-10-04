using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>一括登録API: ニュース一括登録リクエスト</summary>
public class BatchCreateNewsRequestDto
{
    /// <summary>紐づくタレントID（talents.id）</summary>
    [Required]
    public int TalentId { get; set; }

    /// <summary>登録するニュースアイテムのリスト（1〜200件）</summary>
    [Required]
    public List<BatchNewsItemDto> Items { get; set; } = null!;
}
