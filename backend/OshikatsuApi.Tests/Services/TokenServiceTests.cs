using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using OshikatsuApi.Settings;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class TokenServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TokenService _sut;
    private readonly JwtSettings _jwtSettings;

    public TokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _jwtSettings = new JwtSettings
        {
            SecretKey = "test_jwt_secret_key_for_unit_tests_must_be_at_least_256_bits",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60,
        };

        var jwtOptions = Options.Create(_jwtSettings);
        _sut = new TokenService(jwtOptions, _db);
    }

    public void Dispose() => _db.Dispose();

    // ── GenerateToken: 正常系 ───────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ValidInput_ReturnsNonEmptyToken()
    {
        var (token, jti, expiresAt) = _sut.GenerateToken(1, "test@example.com");

        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(jti));
        Assert.True(expiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_ValidInput_TokenContainsCorrectClaims()
    {
        const int userId = 42;
        const string email = "test@example.com";

        var (token, _, _) = _sut.GenerateToken(userId, email);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal(email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
    }

    [Fact]
    public void GenerateToken_ValidInput_TokenHasCorrectIssuer()
    {
        var (token, _, _) = _sut.GenerateToken(1, "test@example.com");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(_jwtSettings.Issuer, jwt.Issuer);
    }

    [Fact]
    public void GenerateToken_ValidInput_TokenExpiresAtCorrectTime()
    {
        var before = DateTime.UtcNow;
        var (_, _, expiresAt) = _sut.GenerateToken(1, "test@example.com");

        var expectedExpiry = before.AddMinutes(_jwtSettings.ExpiryMinutes);
        // 1秒の許容誤差
        Assert.True(Math.Abs((expiresAt - expectedExpiry).TotalSeconds) < 1);
    }

    [Fact]
    public void GenerateToken_CalledTwice_ProducesDistinctJti()
    {
        var (_, jti1, _) = _sut.GenerateToken(1, "test@example.com");
        var (_, jti2, _) = _sut.GenerateToken(1, "test@example.com");

        Assert.NotEqual(jti1, jti2);
    }

    // ── RevokeToken / IsTokenRevoked: 正常系 ────────────────────────────────

    [Fact]
    public async Task RevokeTokenAsync_ValidJti_PersistsRevocation()
    {
        const string jti = "test-jti-to-revoke";
        var expiresAt = DateTime.UtcNow.AddHours(1);

        await _sut.RevokeTokenAsync(jti, expiresAt);

        var stored = await _db.RevokedTokens.FirstOrDefaultAsync(t => t.Jti == jti);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task IsTokenRevokedAsync_RevokedJti_ReturnsTrue()
    {
        const string jti = "revoked-jti";
        await _sut.RevokeTokenAsync(jti, DateTime.UtcNow.AddHours(1));

        var result = await _sut.IsTokenRevokedAsync(jti);

        Assert.True(result);
    }

    [Fact]
    public async Task IsTokenRevokedAsync_NonRevokedJti_ReturnsFalse()
    {
        var result = await _sut.IsTokenRevokedAsync("non-existent-jti");

        Assert.False(result);
    }

    // ── IsTokenRevoked: 境界値 ───────────────────────────────────────────────

    [Fact]
    public async Task IsTokenRevokedAsync_ExpiredRevocation_ReturnsFalse()
    {
        // 有効期限が過去のトークンはリボーク済みとして扱わない（期限切れ）
        const string jti = "expired-revoked-jti";
        _db.RevokedTokens.Add(new RevokedToken
        {
            Jti = jti,
            RevokedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1), // すでに期限切れ
        });
        await _db.SaveChangesAsync();

        var result = await _sut.IsTokenRevokedAsync(jti);

        Assert.False(result);
    }
}
