namespace OshikatsuApi.DTOs;

/// <summary>VAPID 公開鍵レスポンス（GET /api/v1/push/vapid-public-key）</summary>
public class VapidPublicKeyResponseDto
{
    /// <summary>VAPID 公開鍵（Base64url）。PushManager.subscribe() の applicationServerKey に渡す。</summary>
    public string PublicKey { get; set; } = null!;
}
