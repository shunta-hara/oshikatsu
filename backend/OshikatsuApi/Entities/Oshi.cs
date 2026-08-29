namespace OshikatsuApi.Entities;

/// <summary>
/// 推しエンティティ（oshis テーブル）
/// </summary>
public class Oshi
{
    public int Id { get; set; }

    /// <summary>紐づくユーザーID</summary>
    public int UserId { get; set; }

    /// <summary>推しの名前</summary>
    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<NewsItem> NewsItems { get; set; } = new List<NewsItem>();
    public ICollection<ProgramAppearance> ProgramAppearances { get; set; } = new List<ProgramAppearance>();
}
