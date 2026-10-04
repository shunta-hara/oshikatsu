namespace OshikatsuApi.DTOs;

/// <summary>ユーザー向けニュースアイテムレスポンス</summary>
public class NewsItemResponseDto
{
    public int Id { get; set; }
    public int OshiId { get; set; }
    public string OshiName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Summary { get; set; }
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsPublishedAtEstimated { get; set; }
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>
    /// ログインユーザーがこの記事をブックマーク済みかどうか。
    /// フィード一覧では null の場合があるため nullable とする。
    /// </summary>
    public bool? IsBookmarked { get; set; }

    /// <summary>媒体名（FEAT-036）。DB に保存されていない場合はタイトル末尾から取り出す。</summary>
    public string? SourceName { get; set; }

    /// <summary>まとめた他媒体の件数（FEAT-035）。同一記事が他媒体に転載されている数。</summary>
    public int OtherSourceCount { get; set; }

    /// <summary>まとめた他媒体のリンク一覧（FEAT-035）。</summary>
    public IReadOnlyList<OtherSourceDto> OtherSources { get; set; } = [];
}
