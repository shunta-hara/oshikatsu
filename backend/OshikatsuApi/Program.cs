using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OshikatsuApi.Authentication;
using OshikatsuApi.Data;
using OshikatsuApi.Infrastructure;
using OshikatsuApi.Services;
using OshikatsuApi.Settings;

var builder = WebApplication.CreateBuilder(args);

// ── PORT 環境変数（Render がリッスンポートを指定する）─────────────────────────
// PORT が設定されているときだけ UseUrls を呼ぶ。
// 未設定のときは何もしないことで、--urls フラグや ASPNETCORE_URLS などの
// 既存の開発フローを壊さない。
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ── 起動時バリデーション ──────────────────────────────────────────────────────
// 必須設定が欠けていれば分かりやすいエラーで早期終了する（fast-fail）。
StartupValidator.ValidateRequiredConfiguration(builder.Configuration);

// ── サービス登録 ──────────────────────────────────────────────────────────────

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// JWT 設定のバインド
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

// Swagger / OpenAPI（JWT Bearer 認証を定義に含める）
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Oshikatsu API",
        Version = "v1",
        Description = "推し活トラッキングサービスのREST API",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer トークンを入力してください。例: Bearer {token}",
    });

    // collector 内部API用 API キー認証定義
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "collector 内部 API 用 API キー（環境変数 CollectorSettings__ApiKey）",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                }
            },
            Array.Empty<string>()
        }
    });
});

// EF Core + Npgsql（接続文字列は環境変数または appsettings.Development.json で設定）
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 認証サービス
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// 推し管理サービス
builder.Services.AddScoped<IOshiService, OshiService>();

// タレント正規化・検索・作成サービス
builder.Services.AddScoped<ITalentService, TalentService>();

// GitHub Actions workflow_dispatch サービス（シングルトン: IHttpClientFactory・ILogger に依存）
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IGitHubDispatchService, GitHubDispatchService>();

// collector 内部APIサービス
builder.Services.AddScoped<IInternalCollectorService, InternalCollectorService>();

// ユーザー向けフィードサービス（ニュース・出演番組一覧）
builder.Services.AddScoped<IFeedService, FeedService>();

// マルチ推しダッシュボード集計サービス
builder.Services.AddScoped<IDashboardService, DashboardService>();

// collector ジョブログサービス
builder.Services.AddScoped<ICollectorJobLogService, CollectorJobLogService>();

// ブックマークサービス
builder.Services.AddScoped<IBookmarkService, BookmarkService>();

// 古い記事の自動削除サービス
builder.Services.AddScoped<INewsCleanupService, NewsCleanupService>();

// collector 内部API認証フィルター（ServiceFilter で DI 経由で使う）
builder.Services.AddScoped<ApiKeyAuthFilter>();

// JWT Bearer 認証
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings?.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings?.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? string.Empty)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        // ログアウト済み（無効化済み）トークンのチェック
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = context.Principal?
                    .FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                if (string.IsNullOrEmpty(jti))
                {
                    context.Fail("Token is missing jti claim.");
                    return;
                }

                var tokenService = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenService>();

                if (await tokenService.IsTokenRevokedAsync(jti))
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

// CORS:
//   環境変数 CORS_ALLOWED_ORIGINS（カンマ区切り）を優先する。
//   未設定の場合は appsettings の Cors:AllowedOrigins → 開発用 localhost:5173 の順。
const string FrontendCorsPolicy = "FrontendCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        var allowedOrigins = CorsOriginResolver.Resolve(builder.Configuration);
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ── パイプライン構成 ──────────────────────────────────────────────────────────

var app = builder.Build();

// リバースプロキシ（Render）からの X-Forwarded-For / X-Forwarded-Proto を信頼する。
// KnownNetworks/KnownProxies をクリアすることで、Render のプロキシ IP（ループバック以外）
// からのヘッダーも信頼される。UseHttpsRedirection は除去済み（Render が TLS を終端するため
// 不要かつ無限リダイレクトの原因になる）。
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Swagger: Development では常に公開、Production では ENABLE_SWAGGER=true のときのみ公開。
// Render 上（ASPNETCORE_ENVIRONMENT=Production）では ENABLE_SWAGGER 未設定のとき非公開になる。
if (SwaggerHelper.IsEnabled(
        app.Environment.EnvironmentName,
        Environment.GetEnvironmentVariable("ENABLE_SWAGGER")))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Oshikatsu API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── マイグレーション自動適用 ──────────────────────────────────────────────────
// コンテナ起動時に未適用のマイグレーションを自動で適用する。
// InMemory プロバイダー（テスト用）では実行しない。
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
    {
        try
        {
            logger.LogInformation("データベースマイグレーションを適用しています...");
            db.Database.Migrate();
            logger.LogInformation("データベースマイグレーションの適用が完了しました。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "データベースマイグレーションの適用に失敗しました。");
            throw;
        }
    }
}

app.Run();

// テストプロジェクトから WebApplicationFactory でアクセスできるようにする
public partial class Program { }
