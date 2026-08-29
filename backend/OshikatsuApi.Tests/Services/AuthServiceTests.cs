using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _tokenService = Substitute.For<ITokenService>();

        // デフォルトのトークン生成モック
        _tokenService
            .GenerateToken(Arg.Any<int>(), Arg.Any<string>())
            .Returns(("mock-token", "mock-jti", DateTime.UtcNow.AddHours(1)));

        _sut = new AuthService(_db, _tokenService);
    }

    public void Dispose() => _db.Dispose();

    // ── Register: 正常系 ────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsAuthResponseDto()
    {
        var result = await _sut.RegisterAsync("user@example.com", "password123");

        Assert.NotNull(result);
        Assert.Equal("mock-token", result.Token);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUserInDb()
    {
        await _sut.RegisterAsync("user@example.com", "password123");

        var user = await _db.Users.FirstOrDefaultAsync();
        Assert.NotNull(user);
        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_StoresHashedPassword()
    {
        const string plainPassword = "password123";
        await _sut.RegisterAsync("user@example.com", plainPassword);

        var user = await _db.Users.FirstAsync();
        // パスワードそのままが格納されていないこと（ハッシュ化されていること）
        Assert.NotEqual(plainPassword, user.PasswordHash);
        // BCrypt ハッシュは $2a$ または $2b$ で始まる
        Assert.True(user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$"),
            $"Password hash should be BCrypt format, but was: {user.PasswordHash[..4]}");
    }

    [Fact]
    public async Task RegisterAsync_EmailIsStoredLowerCase()
    {
        await _sut.RegisterAsync("User@EXAMPLE.COM", "password123");

        var user = await _db.Users.FirstAsync();
        Assert.Equal("user@example.com", user.Email);
    }

    // ── Register: 異常系 ────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        await _sut.RegisterAsync("dup@example.com", "password123");

        await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            _sut.RegisterAsync("dup@example.com", "differentpassword"));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmailCaseInsensitive_ThrowsDuplicateEmailException()
    {
        await _sut.RegisterAsync("user@example.com", "password123");

        await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            _sut.RegisterAsync("USER@EXAMPLE.COM", "differentpassword"));
    }

    // ── Login: 正常系 ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponseDto()
    {
        await SeedUser("login@example.com", "correctpassword");

        var result = await _sut.LoginAsync("login@example.com", "correctpassword");

        Assert.NotNull(result);
        Assert.Equal("mock-token", result!.Token);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_CallsGenerateToken()
    {
        await SeedUser("login@example.com", "correctpassword");

        await _sut.LoginAsync("login@example.com", "correctpassword");

        _tokenService.Received(1).GenerateToken(Arg.Any<int>(), Arg.Any<string>());
    }

    // ── Login: 異常系 ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        await SeedUser("login@example.com", "correctpassword");

        var result = await _sut.LoginAsync("login@example.com", "wrongpassword");

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_NonExistentEmail_ReturnsNull()
    {
        var result = await _sut.LoginAsync("nonexistent@example.com", "password123");

        Assert.Null(result);
    }

    // ── Login: 境界値 ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_EmptyDatabase_ReturnsNull()
    {
        var result = await _sut.LoginAsync("any@example.com", "anypassword");

        Assert.Null(result);
    }

    // ── Logout: 正常系 ──────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_ValidJti_CallsRevokeToken()
    {
        const string jti = "test-jti";
        var expiresAt = DateTime.UtcNow.AddHours(1);

        await _sut.LogoutAsync(jti, expiresAt);

        await _tokenService.Received(1).RevokeTokenAsync(jti, expiresAt);
    }

    // ── ヘルパー ────────────────────────────────────────────────────────────

    private async Task SeedUser(string email, string password)
    {
        var now = DateTime.UtcNow;
        _db.Users.Add(new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await _db.SaveChangesAsync();
    }
}
