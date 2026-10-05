using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>Web Push 購読登録リクエスト（POST /api/v1/push/subscriptions）</summary>
public class RegisterPushSubscriptionRequestDto
{
    /// <summary>ブラウザが発行した Push Subscription の endpoint URL</summary>
    [Required]
    public string Endpoint { get; set; } = null!;

    /// <summary>P-256 DH 鍵（Base64url）</summary>
    [Required]
    public string P256dh { get; set; } = null!;

    /// <summary>Auth シークレット（Base64url）</summary>
    [Required]
    public string Auth { get; set; } = null!;
}
