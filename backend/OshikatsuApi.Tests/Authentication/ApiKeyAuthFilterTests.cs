using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using OshikatsuApi.Authentication;
using Xunit;

namespace OshikatsuApi.Tests.Authentication;

public class ApiKeyAuthFilterTests
{
    private const string ValidKey = "test-api-key-12345";

    private static ApiKeyAuthFilter CreateFilter(string? configuredKey = ValidKey)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["CollectorSettings:ApiKey"] = configuredKey,
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        return new ApiKeyAuthFilter(config);
    }

    private static AuthorizationFilterContext CreateContext(string? headerValue = null)
    {
        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[ApiKeyAuthFilter.HeaderName] = headerValue;
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    // ── 正常系 ───────────────────────────────────────────────────────────────

    [Fact]
    public void OnAuthorization_CorrectApiKey_DoesNotSetResult()
    {
        var filter = CreateFilter(ValidKey);
        var context = CreateContext(headerValue: ValidKey);

        filter.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    // ── 異常系 ───────────────────────────────────────────────────────────────

    [Fact]
    public void OnAuthorization_MissingHeader_Returns401()
    {
        var filter = CreateFilter(ValidKey);
        var context = CreateContext(headerValue: null);

        filter.OnAuthorization(context);

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_WrongApiKey_Returns401()
    {
        var filter = CreateFilter(ValidKey);
        var context = CreateContext(headerValue: "wrong-key");

        filter.OnAuthorization(context);

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_EmptyConfiguredKey_Returns401()
    {
        // 設定に空文字が入っていてもアクセス拒否
        var filter = CreateFilter(configuredKey: "");
        var context = CreateContext(headerValue: "");

        filter.OnAuthorization(context);

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    // ── 境界値 ───────────────────────────────────────────────────────────────

    [Fact]
    public void OnAuthorization_KeyWithExtraWhitespace_Returns401()
    {
        var filter = CreateFilter(ValidKey);
        var context = CreateContext(headerValue: $" {ValidKey} ");

        filter.OnAuthorization(context);

        // スペースが入っているため不一致 → 401
        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }
}
