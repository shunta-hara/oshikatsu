using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>Web Push 購読削除リクエスト（DELETE /api/v1/push/subscriptions）</summary>
public class DeletePushSubscriptionRequestDto
{
    /// <summary>削除する Push Subscription の endpoint URL</summary>
    [Required]
    public string Endpoint { get; set; } = null!;
}
