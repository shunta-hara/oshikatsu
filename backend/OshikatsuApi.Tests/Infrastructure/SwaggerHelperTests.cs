using OshikatsuApi.Infrastructure;
using Xunit;

namespace OshikatsuApi.Tests.Infrastructure;

public class SwaggerHelperTests
{
    // ── 開発環境: 常に公開 ──────────────────────────────────────────────────

    [Fact]
    public void IsEnabled_Development_ReturnsTrue()
    {
        Assert.True(SwaggerHelper.IsEnabled("Development", null));
    }

    [Fact]
    public void IsEnabled_DevelopmentWithEnableSwaggerFalse_ReturnsTrue()
    {
        // Development は ENABLE_SWAGGER の値に関わらず常に公開
        Assert.True(SwaggerHelper.IsEnabled("Development", "false"));
    }

    [Fact]
    public void IsEnabled_DevelopmentLowercase_ReturnsTrue()
    {
        // 大文字小文字不問
        Assert.True(SwaggerHelper.IsEnabled("development", null));
    }

    // ── 本番環境・ENABLE_SWAGGER=true: 公開 ──────────────────────────────────

    [Fact]
    public void IsEnabled_ProductionWithEnableSwaggerTrue_ReturnsTrue()
    {
        Assert.True(SwaggerHelper.IsEnabled("Production", "true"));
    }

    [Fact]
    public void IsEnabled_ProductionWithEnableSwaggerTrueUppercase_ReturnsTrue()
    {
        // 大文字小文字不問
        Assert.True(SwaggerHelper.IsEnabled("Production", "TRUE"));
    }

    [Fact]
    public void IsEnabled_ProductionWithEnableSwaggerTrueMixedCase_ReturnsTrue()
    {
        Assert.True(SwaggerHelper.IsEnabled("Production", "True"));
    }

    // ── 本番環境・ENABLE_SWAGGER 未設定または false: 非公開 ──────────────────

    [Fact]
    public void IsEnabled_ProductionWithoutEnableSwagger_ReturnsFalse()
    {
        Assert.False(SwaggerHelper.IsEnabled("Production", null));
    }

    [Fact]
    public void IsEnabled_ProductionWithEnableSwaggerFalse_ReturnsFalse()
    {
        Assert.False(SwaggerHelper.IsEnabled("Production", "false"));
    }

    [Fact]
    public void IsEnabled_ProductionWithEnableSwaggerEmpty_ReturnsFalse()
    {
        Assert.False(SwaggerHelper.IsEnabled("Production", ""));
    }

    [Fact]
    public void IsEnabled_ProductionWithEnableSwaggerArbitraryValue_ReturnsFalse()
    {
        Assert.False(SwaggerHelper.IsEnabled("Production", "yes"));
    }

    // ── 境界値 ──────────────────────────────────────────────────────────────

    [Fact]
    public void IsEnabled_NullEnvironmentWithoutEnableSwagger_ReturnsFalse()
    {
        Assert.False(SwaggerHelper.IsEnabled(null, null));
    }

    [Fact]
    public void IsEnabled_NullEnvironmentWithEnableSwaggerTrue_ReturnsTrue()
    {
        Assert.True(SwaggerHelper.IsEnabled(null, "true"));
    }

    [Fact]
    public void IsEnabled_EmptyEnvironmentWithoutEnableSwagger_ReturnsFalse()
    {
        Assert.False(SwaggerHelper.IsEnabled("", null));
    }
}
