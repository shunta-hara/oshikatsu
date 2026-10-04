namespace OshikatsuApi.Entities;

/// <summary>
/// タレントエンティティ（talents テーブル）
/// ユーザーを横断する推しの名簿。同一人物は1レコードで管理する。
/// </summary>
public class Talent
{
    public int Id { get; set; }

    /// <summary>表示名（最初に登録したユーザーの入力値をそのまま保持）</summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>正規化済み名前（NFKC → 小文字 → 空白除去。一意制約あり）</summary>
    public string NormalizedName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<UserOshi> UserOshis { get; set; } = new List<UserOshi>();
    public ICollection<NewsItem> NewsItems { get; set; } = new List<NewsItem>();
    public ICollection<ProgramAppearance> ProgramAppearances { get; set; } = new List<ProgramAppearance>();
}
