using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>一括登録API: ニュースアイテム1件（oshiId はリクエスト上位で指定）</summary>
public class BatchNewsItemDto
{
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
    /// 記事の公開日時。取得できなかった場合は null。
    /// null の場合は CollectedAt で代替し、IsPublishedAtEstimated が true になる。
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>collector が収集した日時</summary>
    [Required]
    public DateTimeOffset CollectedAt { get; set; }
}
