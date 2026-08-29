namespace OshikatsuApi.Entities;

/// <summary>
/// ユーザーエンティティ（users テーブル）
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>メールアドレス（一意）</summary>
    public string Email { get; set; } = null!;

    /// <summary>ハッシュ化済みパスワード</summary>
    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ICollection<Oshi> Oshis { get; set; } = new List<Oshi>();
}
