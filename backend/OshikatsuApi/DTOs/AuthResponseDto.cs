namespace OshikatsuApi.DTOs;

/// <summary>認証成功レスポンス（register / login 共用）</summary>
public class AuthResponseDto
{
    /// <summary>JWT アクセストークン</summary>
    public string Token { get; set; } = null!;

    /// <summary>トークンの有効期限（UTC）</summary>
    public DateTime ExpiresAt { get; set; }
}
