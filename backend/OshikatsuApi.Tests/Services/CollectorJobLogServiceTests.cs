using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class CollectorJobLogServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ILogger<CollectorJobLogService> _logger;
    private readonly CollectorJobLogService _sut;

    public CollectorJobLogServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _logger = Substitute.For<ILogger<CollectorJobLogService>>();
        _sut = new CollectorJobLogService(_db, _logger);
    }

    public void Dispose() => _db.Dispose();

    // ── RecordJobLogAsync: 正常系 ────────────────────────────────────────────

    [Fact]
    public async Task RecordJobLogAsync_ValidRequest_SavesLog()
    {
        var dto = BuildDto(jobType: "news", successCount: 5, failureCount: 0);

        await _sut.RecordJobLogAsync(dto);

        var saved = await _db.CollectorJobLogs.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("news", saved!.JobType);
        Assert.Equal(5, saved.SuccessCount);
        Assert.Equal(0, saved.FailureCount);
    }

    [Fact]
    public async Task RecordJobLogAsync_ValidRequest_ReturnsDto()
    {
        var dto = BuildDto(jobType: "program", successCount: 3, failureCount: 1);

        var result = await _sut.RecordJobLogAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("program", result.JobType);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task RecordJobLogAsync_MultipleJobTypes_SavesBoth()
    {
        await _sut.RecordJobLogAsync(BuildDto(jobType: "news", successCount: 2, failureCount: 0));
        await _sut.RecordJobLogAsync(BuildDto(jobType: "program", successCount: 1, failureCount: 0));

        var total = await _db.CollectorJobLogs.CountAsync();
        Assert.Equal(2, total);
    }

    // ── RecordJobLogAsync: 連続3回失敗の警告 ────────────────────────────────

    [Fact]
    public async Task RecordJobLogAsync_ThreeConsecutiveFailures_LogsWarning()
    {
        // 3回連続で FailureCount > 0
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 2));
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 3));
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 1));

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("連続3回")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RecordJobLogAsync_TwoConsecutiveFailures_DoesNotLogWarning()
    {
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 1));
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 1));

        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RecordJobLogAsync_ThreeFailuresWithSuccessInMiddle_DoesNotLogWarning()
    {
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 1));
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 5, failureCount: 0)); // 成功
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 1));

        // 連続3回ではないので WARNING は出力されない
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RecordJobLogAsync_DifferentJobTypes_CheckedSeparately()
    {
        // news が3回失敗、program は1回だけ
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 1));
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 1));
        await _sut.RecordJobLogAsync(BuildDto("program", successCount: 0, failureCount: 1));
        // program の3回目の記録がないので warning は出ない（newsは3回で出る）
        await _sut.RecordJobLogAsync(BuildDto("news", successCount: 0, failureCount: 1));

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("news")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── GetStatusAsync: 正常系 ───────────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_NoLogs_ReturnsBothNull()
    {
        var result = await _sut.GetStatusAsync();

        Assert.NotNull(result);
        Assert.Null(result.LatestNewsJob);
        Assert.Null(result.LatestProgramJob);
    }

    [Fact]
    public async Task GetStatusAsync_NewsLogExists_ReturnsLatestNewsJob()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-2), successCount: 3, failureCount: 0);
        await AddLog("news", now.AddHours(-1), successCount: 7, failureCount: 1); // 最新

        var result = await _sut.GetStatusAsync();

        Assert.NotNull(result.LatestNewsJob);
        Assert.Equal(7, result.LatestNewsJob!.SuccessCount);
        Assert.Null(result.LatestProgramJob);
    }

    [Fact]
    public async Task GetStatusAsync_BothJobTypesExist_ReturnsBothLatest()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now, successCount: 5, failureCount: 0);
        await AddLog("program", now, successCount: 2, failureCount: 1);

        var result = await _sut.GetStatusAsync();

        Assert.NotNull(result.LatestNewsJob);
        Assert.NotNull(result.LatestProgramJob);
        Assert.Equal("news", result.LatestNewsJob!.JobType);
        Assert.Equal("program", result.LatestProgramJob!.JobType);
    }

    // ── GetStatusAsync: 境界値 ───────────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_MultipleNewsLogs_ReturnsOnlyLatest()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-3), successCount: 1, failureCount: 0);
        await AddLog("news", now.AddHours(-2), successCount: 2, failureCount: 0);
        await AddLog("news", now.AddHours(-1), successCount: 10, failureCount: 0); // 最新

        var result = await _sut.GetStatusAsync();

        Assert.Equal(10, result.LatestNewsJob!.SuccessCount);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private static CreateJobLogRequestDto BuildDto(
        string jobType,
        int successCount,
        int failureCount) => new()
        {
            JobType = jobType,
            ExecutedAt = DateTimeOffset.UtcNow,
            SuccessCount = successCount,
            FailureCount = failureCount,
        };

    private async Task<CollectorJobLog> AddLog(
        string jobType,
        DateTimeOffset executedAt,
        int successCount,
        int failureCount)
    {
        var log = new CollectorJobLog
        {
            JobType = jobType,
            ExecutedAt = executedAt,
            SuccessCount = successCount,
            FailureCount = failureCount,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.CollectorJobLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }
}
