using Microsoft.Extensions.Configuration;
using OshikatsuApi.Settings;
using Xunit;

namespace OshikatsuApi.Tests.Settings;

/// <summary>
/// CorsOriginResolver のテスト。
/// 環境変数の操作を含むため、テスト間の干渉を避けるために IDisposable で後片付けする。
/// </summary>
public class CorsOriginResolverTests : IDisposable
{
    // テスト開始時の環境変数の値を保持しておき、終了時に復元する
    private readonly string? _originalEnvValue;

    public CorsOriginResolverTests()
    {
        _originalEnvValue = Environment.GetEnvironmentVariable(CorsOriginResolver.EnvVarName);
        // 各テストが独立して実行できるよう、まず環境変数をクリア
        Environment.SetEnvironmentVariable(CorsOriginResolver.EnvVarName, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CorsOriginResolver.EnvVarName, _originalEnvValue);
    }

    // ── ヘルパー ───────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(string[]? configOrigins = null)
    {
        var settings = new Dictionary<string, string?>();
        if (configOrigins is not null)
        {
            for (var i = 0; i < configOrigins.Length; i++)
            {
                settings[$"Cors:AllowedOrigins:{i}"] = configOrigins[i];
            }
        }
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    // ── 正常系: 環境変数が設定されている ──────────────────────────────────────

    [Fact]
    public void Resolve_WithSingleEnvVar_ReturnsOneOrigin()
    {
        Environment.SetEnvironmentVariable(
            CorsOriginResolver.EnvVarName, "https://app.example.com");

        var result = CorsOriginResolver.Resolve(BuildConfig());

        Assert.Single(result);
        Assert.Equal("https://app.example.com", result[0]);
    }

    [Fact]
    public void Resolve_WithMultipleEnvVar_ReturnsAllOrigins()
    {
        Environment.SetEnvironmentVariable(
            CorsOriginResolver.EnvVarName,
            "https://app.example.com,https://other.example.com");

        var result = CorsOriginResolver.Resolve(BuildConfig());

        Assert.Equal(2, result.Length);
        Assert.Contains("https://app.example.com", result);
        Assert.Contains("https://other.example.com", result);
    }

    [Fact]
    public void Resolve_EnvVarWithSpaces_TrimsWhitespace()
    {
        // カンマ区切りにスペースが混在していてもトリムされること
        Environment.SetEnvironmentVariable(
            CorsOriginResolver.EnvVarName,
            " https://app.example.com , https://other.example.com ");

        var result = CorsOriginResolver.Resolve(BuildConfig());

        Assert.Equal(2, result.Length);
        Assert.Contains("https://app.example.com", result);
        Assert.Contains("https://other.example.com", result);
    }

    // ── 正常系: 環境変数なし・appsettings から読む ────────────────────────────

    [Fact]
    public void Resolve_NoEnvVar_WithConfigOrigins_ReturnsConfigOrigins()
    {
        var configOrigins = new[] { "https://config.example.com" };
        var config = BuildConfig(configOrigins);

        var result = CorsOriginResolver.Resolve(config);

        Assert.Single(result);
        Assert.Equal("https://config.example.com", result[0]);
    }

    // ── 正常系: 環境変数なし・設定なし → デフォルト ─────────────────────────

    [Fact]
    public void Resolve_NoEnvVar_NoConfig_ReturnsDefaultLocalhostOrigin()
    {
        var result = CorsOriginResolver.Resolve(BuildConfig());

        Assert.Single(result);
        Assert.Equal("http://localhost:5173", result[0]);
    }

    // ── 異常系: 環境変数が空・whitespace の場合は fallback ─────────────────────

    [Fact]
    public void Resolve_EmptyEnvVar_FallsBackToConfig()
    {
        Environment.SetEnvironmentVariable(CorsOriginResolver.EnvVarName, "");
        var configOrigins = new[] { "https://config.example.com" };
        var config = BuildConfig(configOrigins);

        var result = CorsOriginResolver.Resolve(config);

        // 空の環境変数は無視して config を使う
        Assert.Single(result);
        Assert.Equal("https://config.example.com", result[0]);
    }

    [Fact]
    public void Resolve_WhitespaceEnvVar_FallsBackToDefault()
    {
        Environment.SetEnvironmentVariable(CorsOriginResolver.EnvVarName, "   ");

        var result = CorsOriginResolver.Resolve(BuildConfig());

        Assert.Single(result);
        Assert.Equal("http://localhost:5173", result[0]);
    }

    // ── 境界値: 環境変数がカンマのみ → エントリ 0 件 → デフォルトではなく空配列 ─

    [Fact]
    public void Resolve_EnvVarWithOnlyCommas_ReturnsEmptyArray()
    {
        // カンマのみが入っている場合、RemoveEmptyEntries で全エントリが除去される
        Environment.SetEnvironmentVariable(CorsOriginResolver.EnvVarName, ",,");

        // 環境変数に非空文字が入っていると認識されるが全エントリが空なので空配列
        var result = CorsOriginResolver.Resolve(BuildConfig());

        // 空文字列でも "非 null/whitespace" 扱いになる（値がある扱い）なので
        // RemoveEmptyEntries により空配列が返る
        Assert.Empty(result);
    }
}
