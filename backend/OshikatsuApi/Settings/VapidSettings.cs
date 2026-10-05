namespace OshikatsuApi.Settings;

/// <summary>VAPID 設定（VapidSettings セクションにバインドする）。
/// 秘密鍵はコードに含めず、環境変数 VapidSettings__PrivateKey で設定する。</summary>
public class VapidSettings
{
    /// <summary>VAPID 公開鍵（Base64url）。frontend に返して PushManager.subscribe() に渡す。</summary>
    public string PublicKey { get; set; } = null!;

    /// <summary>VAPID 秘密鍵（Base64url）。環境変数からのみ設定する。</summary>
    public string PrivateKey { get; set; } = null!;

    /// <summary>VAPID Subject（mailto: または URL）</summary>
    public string Subject { get; set; } = null!;
}
