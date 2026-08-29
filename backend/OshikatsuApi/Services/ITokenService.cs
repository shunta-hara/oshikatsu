namespace OshikatsuApi.Services;

/// <summary>JWT トークンの生成・無効化・検証を担う</summary>
public interface ITokenService
{
    /// <summary>ユーザー情報から JWT を生成する</summary>
    /// <returns>(token文字列, jti, 有効期限UTC)</returns>
    (string Token, string Jti, DateTime ExpiresAt) GenerateToken(int userId, string email);

    /// <summary>指定した jti を revoked_tokens テーブルに保存する（ログアウト）</summary>
    Task RevokeTokenAsync(string jti, DateTime expiresAt);

    /// <summary>指定した jti が無効化されているかを確認する</summary>
    Task<bool> IsTokenRevokedAsync(string jti);
}
