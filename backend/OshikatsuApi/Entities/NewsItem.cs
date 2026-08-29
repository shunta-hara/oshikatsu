namespace OshikatsuApi.Entities;

/// <summary>
/// ニュースアイテムエンティティ（news_items テーブル）
/// </summary>
public class NewsItem
{
    public int Id { get; set; }

    /// <summary>紐づく推しID</summary>
    public int OshiId { get; set; }

    /// <summary>記事タイトル</summary>
    public string Title { get; set; } = null!;

    /// <summary>本文要約（任意）</summary>
    public string? Summary { get; set; }

    /// <summary>収集元URL（ソースURL）</summary>
    public string SourceUrl { get; set; } = null!;

    /// <summary>記事の公開日時（取得できた場合）</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// 公開日時が推定値かどうかのフラグ。
    /// true = 取得できなかったため収集日時で代替、false = 実際の公開日時
    /// </summary>
    public bool IsPublishedAtEstimated { get; set; }

    /// <summary>collector が収集した日時（UTC）</summary>
    public DateTimeOffset CollectedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Oshi Oshi { get; set; } = null!;
}
