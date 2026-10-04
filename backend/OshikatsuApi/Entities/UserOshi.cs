namespace OshikatsuApi.Entities;

/// <summary>
/// ユーザーの推し登録エンティティ（user_oshis テーブル）
/// ユーザーとタレントの多対多中間テーブル。
/// </summary>
public class UserOshi
{
    public int Id { get; set; }

    /// <summary>登録したユーザーID</summary>
    public int UserId { get; set; }

    /// <summary>登録したタレントID</summary>
    public int TalentId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Talent Talent { get; set; } = null!;
}
