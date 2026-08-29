using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OshikatsuApi.Authentication;

/// <summary>
/// X-Api-Key ヘッダーによる API キー認証フィルター。
/// 環境変数 CollectorSettings__ApiKey から期待するキーを読み込む。
/// </summary>
public class ApiKeyAuthFilter : IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";
    private const string ConfigKey = "CollectorSettings:ApiKey";

    private readonly IConfiguration _config;

    public ApiKeyAuthFilter(IConfiguration config)
    {
        _config = config;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var incomingKey))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "API key is missing." });
            return;
        }

        var expectedKey = _config[ConfigKey];

        if (string.IsNullOrEmpty(expectedKey) || incomingKey != expectedKey)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid API key." });
        }
    }
}
