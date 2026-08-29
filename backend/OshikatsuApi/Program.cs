using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OshikatsuApi.Authentication;
using OshikatsuApi.Data;
using OshikatsuApi.Services;
using OshikatsuApi.Settings;

var builder = WebApplication.CreateBuilder(args);

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

// collector 内部APIサービス
builder.Services.AddScoped<IInternalCollectorService, InternalCollectorService>();

// ユーザー向けフィードサービス（ニュース・出演番組一覧）
builder.Services.AddScoped<IFeedService, FeedService>();

// collector ジョブログサービス
builder.Services.AddScoped<ICollectorJobLogService, CollectorJobLogService>();

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

// CORS（開発時: Vite dev server からのアクセスを許可）
const string FrontendCorsPolicy = "FrontendCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:5173"];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ── パイプライン構成 ──────────────────────────────────────────────────────────

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Oshikatsu API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// テストプロジェクトから WebApplicationFactory でアクセスできるようにする
public partial class Program { }
