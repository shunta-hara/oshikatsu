namespace OshikatsuApi.Services;

/// <summary>Web Push 購読管理のビジネスロジックを担う</summary>
public interface IPushSubscriptionService
{
    /// <summary>
    /// VAPID 公開鍵を返す。frontend が PushManager.subscribe() に渡す applicationServerKey に使う。
    /// </summary>
    Task<string> GetVapidPublicKeyAsync();

    /// <summary>
    /// Push Subscription を登録または上書きする（Upsert）。
    /// 同一 endpoint が既に登録されている場合は userId・p256dh・auth を更新する。
    /// </summary>
    Task UpsertSubscriptionAsync(int userId, string endpoint, string p256dh, string auth);

    /// <summary>
    /// 指定したユーザーの Push Subscription を削除する。
    /// 対象が存在しない場合は何もしない（冪等）。
    /// </summary>
    Task DeleteSubscriptionAsync(int userId, string endpoint);
}
