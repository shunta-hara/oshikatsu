namespace OshikatsuApi.Entities;

/// <summary>
/// 無効化されたJWTトークン（revoked_tokens テーブル）。
/// ログアウト時に jti（JWT ID）を保存し、以降のリクエストで検証する。
/// </summary>
public class RevokedToken
{
    public int Id { get; set; }

    /// <summary>JWT の jti クレーム</summary>
    public string Jti { get; set; } = null!;

    /// <summary>トークンが無効化された日時（UTC）</summary>
    public DateTime RevokedAt { get; set; }

    /// <summary>元のトークンの有効期限（UTC）。期限切れ後はDBから削除可能</summary>
    public DateTime ExpiresAt { get; set; }
}
