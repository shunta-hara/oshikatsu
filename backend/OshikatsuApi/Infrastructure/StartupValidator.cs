namespace OshikatsuApi.Infrastructure;

/// <summary>
/// 起動時に必須の設定値が揃っているか検証するユーティリティ。
/// 設定が欠けている場合は分かりやすいメッセージで InvalidOperationException をスローし、
/// コンテナが早期に失敗する（fast-fail）ようにする。
/// </summary>
public static class StartupValidator
{
    /// <summary>
    /// 必須の設定値をまとめて検証する。いずれかが欠けていれば例外をスローする。
    /// </summary>
    /// <param name="configuration">アプリケーション設定</param>
    /// <exception cref="InvalidOperationException">必須設定が欠けている場合</exception>
    public static void ValidateRequiredConfiguration(IConfiguration configuration)
    {
        ValidateConnectionString(configuration);
        ValidateJwtSecretKey(configuration);
        ValidateCollectorApiKey(configuration);
        ValidateVapidSettings(configuration);
    }

    private static void ValidateConnectionString(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "必須設定 'ConnectionStrings:DefaultConnection' が見つからないか空です。" +
                " 環境変数 'ConnectionStrings__DefaultConnection' を設定してください。");
        }
    }

    private static void ValidateJwtSecretKey(IConfiguration configuration)
    {
        var value = configuration["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "必須設定 'JwtSettings:SecretKey' が見つからないか空です。" +
                " 環境変数 'JwtSettings__SecretKey' を設定してください。");
        }
    }

    private static void ValidateCollectorApiKey(IConfiguration configuration)
    {
        var value = configuration["CollectorSettings:ApiKey"];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "必須設定 'CollectorSettings:ApiKey' が見つからないか空です。" +
                " 環境変数 'CollectorSettings__ApiKey' を設定してください。");
        }
    }

    private static void ValidateVapidSettings(IConfiguration configuration)
    {
        var publicKey = configuration["VapidSettings:PublicKey"];
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new InvalidOperationException(
                "必須設定 'VapidSettings:PublicKey' が見つからないか空です。" +
                " 環境変数 'VapidSettings__PublicKey' を設定してください。");
        }

        var privateKey = configuration["VapidSettings:PrivateKey"];
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new InvalidOperationException(
                "必須設定 'VapidSettings:PrivateKey' が見つからないか空です。" +
                " 環境変数 'VapidSettings__PrivateKey' を設定してください。");
        }
    }
}
