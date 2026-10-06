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

    // ── 正常系（通常モード: CustomMessage なし）──────────────────────────────

    [Fact]
    public async Task Notify_Returns200WithResult()
    {
        var expectedResult = new PushNotifyResult(3, 0, 1, 0);
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var result = await _sut.Notify(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var body = Assert.IsType<PushNotifyResponseDto>(ok.Value);
        Assert.Equal(3, body.SuccessCount);
        Assert.Equal(0, body.FailureCount);
        Assert.Equal(1, body.SkipCount);
        Assert.Equal(0, body.GoneRemovedCount);
    }

    [Fact]
    public async Task Notify_NullRequest_CallsNotifyAllUsersAsync()
    {
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(0, 0, 0, 0));

        await _sut.Notify(null, CancellationToken.None);

        await _pushNotificationService.Received(1).NotifyAllUsersAsync(Arg.Any<CancellationToken>());
        await _pushNotificationService.DidNotReceive().BroadcastMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notify_EmptyCustomMessage_CallsNotifyAllUsersAsync()
    {
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(0, 0, 0, 0));

        await _sut.Notify(new PushNotifyRequestDto { CustomMessage = "" }, CancellationToken.None);

        await _pushNotificationService.Received(1).NotifyAllUsersAsync(Arg.Any<CancellationToken>());
        await _pushNotificationService.DidNotReceive().BroadcastMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notify_WhitespaceCustomMessage_CallsNotifyAllUsersAsync()
    {
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(0, 0, 0, 0));

        await _sut.Notify(new PushNotifyRequestDto { CustomMessage = "   " }, CancellationToken.None);

        await _pushNotificationService.Received(1).NotifyAllUsersAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notify_AllZeroCounts_Returns200()
    {
        // 購読なし・送信なしの場合も 200 を返す（ジョブ自体は成功）
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(0, 0, 0, 0));

        var result = await _sut.Notify(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── 正常系（ブロードキャストモード: CustomMessage あり）（FEAT-053）────

    [Fact]
    public async Task Notify_WithCustomMessage_CallsBroadcastMessageAsync()
    {
        const string message = "DB 使用量が 450 MB に達しています（上限 500 MB）。ご確認ください。";
        _pushNotificationService.BroadcastMessageAsync(message, Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(5, 0, 0, 1));

        await _sut.Notify(new PushNotifyRequestDto { CustomMessage = message }, CancellationToken.None);

        await _pushNotificationService.Received(1).BroadcastMessageAsync(message, Arg.Any<CancellationToken>());
        await _pushNotificationService.DidNotReceive().NotifyAllUsersAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notify_WithCustomMessage_Returns200WithResult()
    {
        const string message = "DB 容量警告";
        _pushNotificationService.BroadcastMessageAsync(message, Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(3, 1, 0, 2));

        var result = await _sut.Notify(new PushNotifyRequestDto { CustomMessage = message }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<PushNotifyResponseDto>(ok.Value);
        Assert.Equal(3, body.SuccessCount);
        Assert.Equal(1, body.FailureCount);
        Assert.Equal(0, body.SkipCount);
        Assert.Equal(2, body.GoneRemovedCount);
    }

    // ── Gone 削除のカウント ────────────────────────────────────────────────

    [Fact]
    public async Task Notify_WithGoneRemovedCount_ReturnsCorrectCount()
    {
        _pushNotificationService.NotifyAllUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new PushNotifyResult(1, 0, 0, 2));

        var result = await _sut.Notify(null, CancellationToken.None);

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

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Notify(null, CancellationToken.None));
    }

    [Fact]
    public async Task Notify_BroadcastThrows_PropagatesException()
    {
        _pushNotificationService.BroadcastMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("ブロードキャストエラー"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Notify(new PushNotifyRequestDto { CustomMessage = "警告" }, CancellationToken.None));
    }
}
