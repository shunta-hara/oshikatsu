namespace OshikatsuApi.Infrastructure;

/// <summary>
/// Swagger UI / OpenAPI JSON の公開可否を判定するヘルパー。
/// - Development 環境: 常に公開
/// - それ以外（Production 等）: 環境変数 ENABLE_SWAGGER=true のときのみ公開
/// </summary>
public static class SwaggerHelper
{
    /// <summary>
    /// Swagger を公開すべきか判定する。
    /// </summary>
    /// <param name="environmentName">ASPNETCORE_ENVIRONMENT の値（例: "Development", "Production"）</param>
    /// <param name="enableSwaggerEnvVar">ENABLE_SWAGGER 環境変数の値。"true"（大文字小文字不問）のとき有効</param>
    /// <returns>Swagger を公開すべきなら <c>true</c></returns>
    public static bool IsEnabled(string? environmentName, string? enableSwaggerEnvVar) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(enableSwaggerEnvVar, "true", StringComparison.OrdinalIgnoreCase);
}
