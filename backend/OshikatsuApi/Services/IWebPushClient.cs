namespace OshikatsuApi.Services;

/// <summary>
/// Web Push 送信の抽象インターフェース。
/// テスト時はモックに差し替えられる。
/// </summary>
public interface IWebPushClient
{
    /// <summary>
    /// Push 通知を送信する。
    /// </summary>
    /// <param name="endpoint">Push Subscription の endpoint URL</param>
    /// <param name="p256dh">P-256 DH 鍵（Base64url）</param>
    /// <param name="auth">Auth シークレット（Base64url）</param>
    /// <param name="payload">通知ペイロード（JSON 文字列）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <exception cref="WebPushGoneException">endpoint が 410 Gone を返した場合</exception>
    /// <exception cref="WebPushTransientException">一時的なエラー（リトライ可能）の場合</exception>
    Task SendNotificationAsync(
        string endpoint,
        string p256dh,
        string auth,
        string payload,
        CancellationToken cancellationToken = default);
}

/// <summary>Push endpoint が 410 Gone を返したことを示す例外（購読削除が必要）</summary>
public sealed class WebPushGoneException : Exception
{
    public WebPushGoneException(string message) : base(message) { }
}

/// <summary>一時的なネットワークエラーを示す例外（リトライ可能）</summary>
public sealed class WebPushTransientException : Exception
{
    public WebPushTransientException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
