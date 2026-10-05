using System.Net;
using Microsoft.Extensions.Options;
using OshikatsuApi.Settings;
using WebPush;

namespace OshikatsuApi.Services;

/// <summary>
/// IWebPushClient の実装。WebPush NuGet ライブラリを使って VAPID ベースの Push 通知を送信する。
/// VAPID 秘密鍵は環境変数 VapidSettings__PrivateKey から読み込む。
/// </summary>
public class WebPushClientWrapper : IWebPushClient
{
    private readonly VapidDetails _vapidDetails;

    public WebPushClientWrapper(IOptions<VapidSettings> vapidOptions)
    {
        var settings = vapidOptions.Value;
        _vapidDetails = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey);
    }

    /// <inheritdoc />
    public async Task SendNotificationAsync(
        string endpoint,
        string p256dh,
        string auth,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var client = new WebPushClient();
        var subscription = new PushSubscription(endpoint, p256dh, auth);

        try
        {
            await client.SendNotificationAsync(subscription, payload, _vapidDetails);
        }
        catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone)
        {
            throw new WebPushGoneException($"Push endpoint が無効になりました (410 Gone): {endpoint}");
        }
        catch (WebPushException ex)
        {
            throw new WebPushTransientException(
                $"Push 送信に失敗しました (HTTP {(int)ex.StatusCode}): {endpoint}",
                ex);
        }
        catch (Exception ex) when (ex is not WebPushGoneException and not WebPushTransientException)
        {
            throw new WebPushTransientException(
                $"Push 送信中に予期しないエラーが発生しました: {endpoint}",
                ex);
        }
    }
}
