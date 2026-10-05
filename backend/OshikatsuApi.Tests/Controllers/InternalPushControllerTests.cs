using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class InternalPushControllerTests
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly InternalPushController _sut;

    public InternalPushControllerTests()
    {
        _pushNotificationService = Substitute.For<IPushNotificationService>();
        _sut = new InternalPushController(
            _pushNotificationService,
            NullLogger<InternalPushController>.Instance);
    }

    // ── 正常系 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Notify_Returns200WithResult()
    {
        var expectedResult = new PushNotifyResult(3, 0, 1, 0);
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var result = await _sut.Notify(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var body = Assert.IsType<PushNotifyResponseDto>(ok.Value);
        Assert.Equal(3, body.SuccessCount);
        Assert.Equal(0, body.FailureCount);
        Assert.Equal(1, body.SkipCount);
        Assert.Equal(0, body.GoneRemovedCount);
    }

    [Fact]
    public async Task Notify_CallsNotifyAllUsersAsync()
    {
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(0, 0, 0, 0));

        await _sut.Notify(CancellationToken.None);

        await _pushNotificationService.Received(1).NotifyAllUsersAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notify_AllZeroCounts_Returns200()
    {
        // 購読なし・送信なしの場合も 200 を返す（ジョブ自体は成功）
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(0, 0, 0, 0));

        var result = await _sut.Notify(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Gone 削除のカウント ────────────────────────────────────────────────

    [Fact]
    public async Task Notify_WithGoneRemovedCount_ReturnsCorrectCount()
    {
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(1, 0, 0, 2));

        var result = await _sut.Notify(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<PushNotifyResponseDto>(ok.Value);
        Assert.Equal(2, body.GoneRemovedCount);
    }

    // ── 異常系: サービスがスローした場合 ─────────────────────────────────────

    [Fact]
    public async Task Notify_ServiceThrows_PropagatesException()
    {
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("予期しないエラー"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Notify(CancellationToken.None));
    }
}
