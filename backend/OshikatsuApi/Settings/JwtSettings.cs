namespace OshikatsuApi.Settings;

/// <summary>JWT 設定（appsettings.json の JwtSettings セクションにバインドする）</summary>
public class JwtSettings
{
    public string SecretKey { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;

    /// <summary>トークンの有効期限（分）</summary>
    public int ExpiryMinutes { get; set; }
}
