using Microsoft.Extensions.Configuration;
using OshikatsuApi.Infrastructure;
using Xunit;

namespace OshikatsuApi.Tests.Infrastructure;

public class StartupValidatorTests
{
    // ── ヘルパー ───────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(
        string? connectionString = "Host=localhost;Database=oshikatsu",
        string? jwtSecretKey = "test-secret-key-32chars-at-minimum!",
        string? collectorApiKey = "test-api-key")
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["JwtSettings:SecretKey"] = jwtSecretKey,
            ["CollectorSettings:ApiKey"] = collectorApiKey,
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    // ── 正常系 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateRequiredConfiguration_AllPresent_DoesNotThrow()
    {
        var config = BuildConfig();

        // 全必須項目が揃っていれば例外なし
        var ex = Record.Exception(() => StartupValidator.ValidateRequiredConfiguration(config));
        Assert.Null(ex);
    }

    // ── 異常系: 接続文字列 ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateRequiredConfiguration_MissingConnectionString_Throws()
    {
        var config = BuildConfig(connectionString: null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
        Assert.Contains("ConnectionStrings:DefaultConnection", ex.Message);
    }

    [Fact]
    public void ValidateRequiredConfiguration_EmptyConnectionString_Throws()
    {
        var config = BuildConfig(connectionString: "");

        Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
    }

    // ── 異常系: JWT 秘密鍵 ────────────────────────────────────────────────────

    [Fact]
    public void ValidateRequiredConfiguration_MissingJwtSecretKey_Throws()
    {
        var config = BuildConfig(jwtSecretKey: null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
        Assert.Contains("JwtSettings:SecretKey", ex.Message);
    }

    [Fact]
    public void ValidateRequiredConfiguration_WhitespaceJwtSecretKey_Throws()
    {
        var config = BuildConfig(jwtSecretKey: "   ");

        Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
    }

    // ── 異常系: collector API キー ───────────────────────────────────────────

    [Fact]
    public void ValidateRequiredConfiguration_MissingCollectorApiKey_Throws()
    {
        var config = BuildConfig(collectorApiKey: null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
        Assert.Contains("CollectorSettings:ApiKey", ex.Message);
    }

    [Fact]
    public void ValidateRequiredConfiguration_EmptyCollectorApiKey_Throws()
    {
        var config = BuildConfig(collectorApiKey: "");

        Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
    }

    // ── 境界値: エラーメッセージに環境変数名が含まれる ─────────────────────────

    [Fact]
    public void ValidateRequiredConfiguration_MissingConnectionString_MessageContainsEnvVarHint()
    {
        var config = BuildConfig(connectionString: null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
        // 環境変数名のヒントがメッセージに含まれること
        Assert.Contains("ConnectionStrings__DefaultConnection", ex.Message);
    }

    [Fact]
    public void ValidateRequiredConfiguration_MissingJwtSecretKey_MessageContainsEnvVarHint()
    {
        var config = BuildConfig(jwtSecretKey: null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
        Assert.Contains("JwtSettings__SecretKey", ex.Message);
    }

    [Fact]
    public void ValidateRequiredConfiguration_MissingCollectorApiKey_MessageContainsEnvVarHint()
    {
        var config = BuildConfig(collectorApiKey: null);

        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidator.ValidateRequiredConfiguration(config));
        Assert.Contains("CollectorSettings__ApiKey", ex.Message);
    }
}
