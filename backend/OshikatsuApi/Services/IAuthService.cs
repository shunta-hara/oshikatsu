using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <summary>ユーザー認証のビジネスロジックを担う</summary>
public interface IAuthService
{
    /// <summary>
    /// 新規ユーザーを登録して JWT トークンを返す。
    /// </summary>
    /// <exception cref="Exceptions.DuplicateEmailException">メールアドレスが既に登録済みの場合</exception>
    Task<AuthResponseDto> RegisterAsync(string email, string password);

    /// <summary>
    /// メールアドレスとパスワードを検証して JWT トークンを返す。
    /// 認証失敗時は null を返す。
    /// </summary>
    Task<AuthResponseDto?> LoginAsync(string email, string password);

    /// <summary>
    /// 指定した jti と有効期限でトークンを無効化する（ログアウト）。
    /// </summary>
    Task LogoutAsync(string jti, DateTime expiresAt);
}
