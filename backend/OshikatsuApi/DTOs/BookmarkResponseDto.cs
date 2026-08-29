namespace OshikatsuApi.DTOs;

/// <summary>ブックマーク一覧のレスポンスアイテム（ニュース記事情報を含む）</summary>
public class BookmarkResponseDto
{
    /// <summary>ブックマークID</summary>
    public int Id { get; set; }

    /// <summary>ブックマークしたニュースアイテムID</summary>
    public int NewsItemId { get; set; }

    public int OshiId { get; set; }
    public string OshiName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Summary { get; set; }
    public string SourceUrl { get; set; } = null!;
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsPublishedAtEstimated { get; set; }
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>ブックマーク登録日時（UTC）</summary>
    public DateTimeOffset BookmarkedAt { get; set; }
}
