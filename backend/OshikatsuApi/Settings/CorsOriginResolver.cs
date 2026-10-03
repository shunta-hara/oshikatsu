namespace OshikatsuApi.Settings;

/// <summary>
/// CORS 許可オリジンを環境変数またはアプリ設定から解決するユーティリティ。
/// 環境変数 <c>CORS_ALLOWED_ORIGINS</c>（カンマ区切り）が設定されていればそちらを優先し、
/// なければ appsettings の <c>Cors:AllowedOrigins</c> を使う。
/// どちらも設定されていない場合は開発用デフォルト（Vite dev server）を返す。
/// </summary>
public static class CorsOriginResolver
{
    /// <summary>環境変数名</summary>
    public const string EnvVarName = "CORS_ALLOWED_ORIGINS";

    /// <summary>
    /// 許可オリジンの文字列配列を返す。
    /// </summary>
    public static string[] Resolve(IConfiguration configuration)
    {
        var envValue = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
               ?? ["http://localhost:5173"];
    }
}
