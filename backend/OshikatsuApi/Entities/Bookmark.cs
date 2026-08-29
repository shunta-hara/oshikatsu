namespace OshikatsuApi.Entities;

/// <summary>
/// ブックマークエンティティ（bookmarks テーブル）。
/// UserId + NewsItemId に一意制約を持ち、ユーザーごとに記事をブックマーク管理する。
/// </summary>
public class Bookmark
{
    public int Id { get; set; }

    /// <summary>ブックマークしたユーザーID</summary>
    public int UserId { get; set; }

    /// <summary>ブックマークしたニュースアイテムID</summary>
    public int NewsItemId { get; set; }

    /// <summary>ブックマーク登録日時（UTC）</summary>
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public NewsItem NewsItem { get; set; } = null!;
}
