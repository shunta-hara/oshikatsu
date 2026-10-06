using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>内部API: ニュースアイテム登録リクエスト</summary>
public class CreateNewsItemRequestDto
{
    /// <summary>紐づくタレントID（talents.id）</summary>
    [Required]
    public int TalentId { get; set; }

    /// <summary>記事タイトル</summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = null!;

    /// <summary>本文要約（任意）</summary>
    [MaxLength(2000)]
    public string? Summary { get; set; }

    /// <summary>収集元URL（重複排除キー）</summary>
    [Required]
    [MaxLength(2048)]
    public string SourceUrl { get; set; } = null!;

    /// <summary>
    /// 記事の公開日時（UTC）。取得できなかった場合は null。
    /// null の場合は CollectedAt で代替し、IsPublishedAtEstimated が true になる。
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>collector が収集した日時（UTC）</summary>
    [Required]
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>媒体名（任意）</summary>
    [MaxLength(200)]
    public string? SourceName { get; set; }

    /// <summary>ソースタイプ（任意）。"youtube" 等。未設定は通常のニュース記事扱い。</summary>
    [MaxLength(50)]
    public string? SourceType { get; set; }
}
