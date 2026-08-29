using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class AuthControllerTests
{
    private readonly IAuthService _authService;
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _authService = Substitute.For<IAuthService>();
        _sut = new AuthController(_authService);
    }

    // ── Register: 正常系 ────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns201()
    {
        var dto = new RegisterRequestDto { Email = "test@example.com", Password = "password123" };
        _authService.RegisterAsync(dto.Email, dto.Password)
            .Returns(new AuthResponseDto { Token = "jwt-token", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        var result = await _sut.Register(dto);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, status.StatusCode);
    }

    [Fact]
    public async Task Register_ValidRequest_ReturnsAuthResponseDto()
    {
        var dto = new RegisterRequestDto { Email = "test@example.com", Password = "password123" };
        var expected = new AuthResponseDto { Token = "jwt-token", ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _authService.RegisterAsync(dto.Email, dto.Password).Returns(expected);

        var result = await _sut.Register(dto);

        var status = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsType<AuthResponseDto>(status.Value);
        Assert.Equal(expected.Token, body.Token);
    }

    // ── Register: 異常系 ────────────────────────────────────────────────────

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var dto = new RegisterRequestDto { Email = "dup@example.com", Password = "password123" };
        _authService.RegisterAsync(dto.Email, dto.Password)
            .ThrowsAsync(new DuplicateEmailException());

        var result = await _sut.Register(dto);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsErrorMessage()
    {
        var dto = new RegisterRequestDto { Email = "dup@example.com", Password = "password123" };
        _authService.RegisterAsync(dto.Email, dto.Password)
            .ThrowsAsync(new DuplicateEmailException());

        var result = await _sut.Register(dto);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(conflict.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    // ── Login: 正常系 ───────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200()
    {
        var dto = new LoginRequestDto { Email = "test@example.com", Password = "password123" };
        _authService.LoginAsync(dto.Email, dto.Password)
            .Returns(new AuthResponseDto { Token = "jwt-token", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        var result = await _sut.Login(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var dto = new LoginRequestDto { Email = "test@example.com", Password = "password123" };
        var expected = new AuthResponseDto { Token = "jwt-token", ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _authService.LoginAsync(dto.Email, dto.Password).Returns(expected);

        var result = await _sut.Login(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<AuthResponseDto>(ok.Value);
        Assert.Equal("jwt-token", body.Token);
    }

    // ── Login: 異常系 ───────────────────────────────────────────────────────

    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        var dto = new LoginRequestDto { Email = "test@example.com", Password = "wrongpassword" };
        _authService.LoginAsync(dto.Email, dto.Password).Returns((AuthResponseDto?)null);

        var result = await _sut.Login(dto);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsErrorMessage()
    {
        var dto = new LoginRequestDto { Email = "test@example.com", Password = "wrongpassword" };
        _authService.LoginAsync(dto.Email, dto.Password).Returns((AuthResponseDto?)null);

        var result = await _sut.Login(dto);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(unauthorized.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    // ── Login: 境界値 ───────────────────────────────────────────────────────

    [Fact]
    public async Task Login_NonExistentEmail_Returns401()
    {
        var dto = new LoginRequestDto { Email = "nonexistent@example.com", Password = "password123" };
        _authService.LoginAsync(dto.Email, dto.Password).Returns((AuthResponseDto?)null);

        var result = await _sut.Login(dto);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── Logout: 正常系 ──────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_AuthenticatedUser_Returns200()
    {
        SetupAuthenticatedUser("test-jti-123", DateTime.UtcNow.AddHours(1));
        _authService.LogoutAsync(Arg.Any<string>(), Arg.Any<DateTime>())
            .Returns(Task.CompletedTask);

        var result = await _sut.Logout();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task Logout_AuthenticatedUser_CallsLogoutAsyncWithCorrectJti()
    {
        const string expectedJti = "specific-jti-value";
        SetupAuthenticatedUser(expectedJti, DateTime.UtcNow.AddHours(1));
        _authService.LogoutAsync(Arg.Any<string>(), Arg.Any<DateTime>())
            .Returns(Task.CompletedTask);

        await _sut.Logout();

        await _authService.Received(1).LogoutAsync(expectedJti, Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Logout_AuthenticatedUser_ReturnsSuccessMessage()
    {
        SetupAuthenticatedUser("test-jti-123", DateTime.UtcNow.AddHours(1));
        _authService.LogoutAsync(Arg.Any<string>(), Arg.Any<DateTime>())
            .Returns(Task.CompletedTask);

        var result = await _sut.Logout();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(ok.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    // ── Logout: 異常系 ──────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_MissingJtiClaim_Returns401()
    {
        // jti クレームが無いユーザーを設定
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Exp,
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()) };
        SetupUserWithClaims(claims);

        var result = await _sut.Logout();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── ヘルパー ────────────────────────────────────────────────────────────

    private void SetupAuthenticatedUser(string jti, DateTime expiresAt)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Exp,
                DateTimeOffset.FromFileTime(expiresAt.ToFileTimeUtc()).ToUnixTimeSeconds().ToString()),
        };
        SetupUserWithClaims(claims);
    }

    private void SetupUserWithClaims(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
