using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class PushControllerTests
{
    private readonly IPushSubscriptionService _pushService;
    private readonly PushController _sut;

    public PushControllerTests()
    {
        _pushService = Substitute.For<IPushSubscriptionService>();
        _sut = new PushController(_pushService);
    }

    // ── GetVapidPublicKey: 正常系 ────────────────────────────────────────────

    [Fact]
    public async Task GetVapidPublicKey_Returns200WithPublicKey()
    {
        const string expectedKey = "BPl6e2X_test_public_key";
        _pushService.GetVapidPublicKeyAsync().Returns(expectedKey);

        var result = await _sut.GetVapidPublicKey();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var body = Assert.IsType<VapidPublicKeyResponseDto>(ok.Value);
        Assert.Equal(expectedKey, body.PublicKey);
    }

    [Fact]
    public async Task GetVapidPublicKey_DoesNotRequireAuthentication()
    {
        // 認証なし（HttpContext.User なし）でも呼び出し可能であることを確認
        _pushService.GetVapidPublicKeyAsync().Returns("test-key");

        // Authorize 属性は Controller レベルではなく Action レベルでないため、
        // サービスが呼ばれることを確認する
        var result = await _sut.GetVapidPublicKey();

        await _pushService.Received(1).GetVapidPublicKeyAsync();
        Assert.IsType<OkObjectResult>(result);
    }

    // ── RegisterSubscription: 正常系 ────────────────────────────────────────

    [Fact]
    public async Task RegisterSubscription_ValidRequest_Returns201()
    {
        SetupAuthenticatedUser(userId: 1);
        var dto = BuildRegisterDto("https://fcm.example.com/sub1", "p256dh_key", "auth_secret");
        _pushService.UpsertSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var result = await _sut.RegisterSubscription(dto);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, status.StatusCode);
    }

    [Fact]
    public async Task RegisterSubscription_ValidRequest_ReturnsSuccessMessage()
    {
        SetupAuthenticatedUser(userId: 1);
        var dto = BuildRegisterDto("https://fcm.example.com/sub1", "p256dh_key", "auth_secret");
        _pushService.UpsertSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var result = await _sut.RegisterSubscription(dto);

        var status = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(status.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    [Fact]
    public async Task RegisterSubscription_CallsUpsertWithCorrectUserId()
    {
        const int expectedUserId = 42;
        SetupAuthenticatedUser(userId: expectedUserId);
        var dto = BuildRegisterDto("https://fcm.example.com/sub1", "p256dh_key", "auth_secret");
        _pushService.UpsertSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        await _sut.RegisterSubscription(dto);

        await _pushService.Received(1).UpsertSubscriptionAsync(
            expectedUserId,
            dto.Endpoint,
            dto.P256dh,
            dto.Auth);
    }

    // ── RegisterSubscription: 境界値（同一 endpoint の再登録） ──────────────

    [Fact]
    public async Task RegisterSubscription_DuplicateEndpoint_StillReturns201()
    {
        // 同一 endpoint の再登録はエラーにならず Upsert される
        SetupAuthenticatedUser(userId: 1);
        var dto = BuildRegisterDto("https://fcm.example.com/same_endpoint", "new_p256dh", "new_auth");
        _pushService.UpsertSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var result = await _sut.RegisterSubscription(dto);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, status.StatusCode);
    }

    // ── DeleteSubscription: 正常系 ───────────────────────────────────────────

    [Fact]
    public async Task DeleteSubscription_ValidRequest_Returns200()
    {
        SetupAuthenticatedUser(userId: 1);
        var dto = new DeletePushSubscriptionRequestDto { Endpoint = "https://fcm.example.com/sub1" };
        _pushService.DeleteSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var result = await _sut.DeleteSubscription(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task DeleteSubscription_ValidRequest_ReturnsSuccessMessage()
    {
        SetupAuthenticatedUser(userId: 1);
        var dto = new DeletePushSubscriptionRequestDto { Endpoint = "https://fcm.example.com/sub1" };
        _pushService.DeleteSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var result = await _sut.DeleteSubscription(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(ok.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    [Fact]
    public async Task DeleteSubscription_CallsDeleteWithCorrectUserId()
    {
        const int expectedUserId = 7;
        SetupAuthenticatedUser(userId: expectedUserId);
        const string expectedEndpoint = "https://fcm.example.com/sub1";
        var dto = new DeletePushSubscriptionRequestDto { Endpoint = expectedEndpoint };
        _pushService.DeleteSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        await _sut.DeleteSubscription(dto);

        await _pushService.Received(1).DeleteSubscriptionAsync(expectedUserId, expectedEndpoint);
    }

    // ── DeleteSubscription: 境界値（存在しない endpoint の削除） ────────────

    [Fact]
    public async Task DeleteSubscription_NonExistentEndpoint_StillReturns200()
    {
        // 存在しない endpoint の削除は冪等として 200 を返す
        SetupAuthenticatedUser(userId: 1);
        var dto = new DeletePushSubscriptionRequestDto { Endpoint = "https://fcm.example.com/nonexistent" };
        _pushService.DeleteSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>())
            .Returns(Task.CompletedTask); // サービス側も冪等で何もしない

        var result = await _sut.DeleteSubscription(dto);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── ヘルパー ────────────────────────────────────────────────────────────

    private void SetupAuthenticatedUser(int userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static RegisterPushSubscriptionRequestDto BuildRegisterDto(
        string endpoint, string p256dh, string auth)
        => new() { Endpoint = endpoint, P256dh = p256dh, Auth = auth };
}
