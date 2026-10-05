namespace OshikatsuApi.Entities;

/// <summary>
/// Web Push 購読エンティティ（push_subscriptions テーブル）。
/// ユーザーがブラウザで通知を許可したときに生成される Push Subscription を保持する。
/// 同一 endpoint は一意であり、再登録時は上書き（Upsert）する。
/// </summary>
public class PushSubscription
{
    public int Id { get; set; }

    /// <summary>購読を登録したユーザーID</summary>
    public int UserId { get; set; }

    /// <summary>ブラウザが発行した Push Subscription の endpoint URL</summary>
    public string Endpoint { get; set; } = null!;

    /// <summary>P-256 DH 鍵（Base64url）</summary>
    public string P256dh { get; set; } = null!;

    /// <summary>Auth シークレット（Base64url）</summary>
    public string Auth { get; set; } = null!;

    /// <summary>購読登録日時（UTC）</summary>
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
