using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

/// <summary>InternalController の DELETE /api/v1/internal/news/expired テスト</summary>
public class InternalControllerCleanupTests
{
    private readonly IInternalCollectorService _collectorService;
    private readonly ICollectorJobLogService _jobLogService;
    private readonly INewsCleanupService _cleanupService;
    private readonly ITalentSourceService _talentSourceService;
    private readonly InternalController _sut;

    public InternalControllerCleanupTests()
    {
        _collectorService = Substitute.For<IInternalCollectorService>();
        _jobLogService = Substitute.For<ICollectorJobLogService>();
        _cleanupService = Substitute.For<INewsCleanupService>();
        _talentSourceService = Substitute.For<ITalentSourceService>();
        _sut = new InternalController(_collectorService, _jobLogService, _cleanupService, _talentSourceService);
    }

    // ── 正常系 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteExpiredNews_Returns200()
    {
        _cleanupService.DeleteExpiredNewsAsync().Returns(5);

        var result = await _sut.DeleteExpiredNews();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task DeleteExpiredNews_ReturnsDeletedCount()
    {
        _cleanupService.DeleteExpiredNewsAsync().Returns(5);

        var result = await _sut.DeleteExpiredNews();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DeleteExpiredNewsResponseDto>(ok.Value);
        Assert.Equal(5, body.DeletedCount);
    }

    [Fact]
    public async Task DeleteExpiredNews_WhenNothingToDelete_Returns0()
    {
        _cleanupService.DeleteExpiredNewsAsync().Returns(0);

        var result = await _sut.DeleteExpiredNews();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DeleteExpiredNewsResponseDto>(ok.Value);
        Assert.Equal(0, body.DeletedCount);
    }

    [Fact]
    public async Task DeleteExpiredNews_CallsCleanupServiceOnce()
    {
        _cleanupService.DeleteExpiredNewsAsync().Returns(0);

        await _sut.DeleteExpiredNews();

        await _cleanupService.Received(1).DeleteExpiredNewsAsync();
    }

    // ── 異常系 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteExpiredNews_ServiceThrows_PropagatesException()
    {
        _cleanupService.DeleteExpiredNewsAsync().Returns<int>(
            _ => throw new InvalidOperationException("DB error"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteExpiredNews());
    }
}
