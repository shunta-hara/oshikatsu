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

    // ── RecordJobLogAsync: UTC 正規化（FEAT-017）────────────────────────────

    [Fact]
    public async Task RecordJobLogAsync_JstExecutedAt_NormalizesToUtc()
    {
        // JST (UTC+09:00) のオフセット付き日時を送ると UTC で保存される
        var jstOffset = TimeSpan.FromHours(9);
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset); // 2026-10-05T20:00:00+09:00
        var dto = new CreateJobLogRequestDto
        {
            JobType = "news",
            ExecutedAt = jstTime,
            SuccessCount = 1,
            FailureCount = 0,
        };

        await _sut.RecordJobLogAsync(dto);

        var saved = await _db.CollectorJobLogs.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(TimeSpan.Zero, saved!.ExecutedAt.Offset);
        Assert.Equal(jstTime.UtcDateTime, saved.ExecutedAt.UtcDateTime);
    }

    [Fact]
    public async Task RecordJobLogAsync_UtcAndJstEquivalent_StoresSameUtcInstant()
    {
        var utcTime = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero);
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.FromHours(9));

        await _sut.RecordJobLogAsync(new CreateJobLogRequestDto
        {
            JobType = "news",
            ExecutedAt = utcTime,
            SuccessCount = 1,
            FailureCount = 0,
        });
        await _sut.RecordJobLogAsync(new CreateJobLogRequestDto
        {
            JobType = "program",
            ExecutedAt = jstTime,
            SuccessCount = 1,
            FailureCount = 0,
        });

        var logs = await _db.CollectorJobLogs.OrderBy(l => l.JobType).ToListAsync();
        Assert.Equal(2, logs.Count);
        // UTC と JST の同じ瞬間 → 同一の UTC 値で保存される
        Assert.Equal(logs[0].ExecutedAt.UtcDateTime, logs[1].ExecutedAt.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, logs[0].ExecutedAt.Offset);
        Assert.Equal(TimeSpan.Zero, logs[1].ExecutedAt.Offset);
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

    // ── RecordJobLogAsync: 新規フィールド（FEAT-052）────────────────────────

    [Fact]
    public async Task RecordJobLogAsync_WithFailureReason_SavesReason()
    {
        var dto = BuildDto(jobType: "news", successCount: 0, failureCount: 2);
        dto.FailureReason = "network_error";
        dto.ErrorMessage = "Connection timeout after 30s";
        dto.TalentId = null;

        var result = await _sut.RecordJobLogAsync(dto);

        Assert.Equal("network_error", result.FailureReason);
        Assert.Equal("Connection timeout after 30s", result.ErrorMessage);
        Assert.Null(result.TalentId);
    }

    [Fact]
    public async Task RecordJobLogAsync_WithTalentId_SavesTalentId()
    {
        // タレントを事前作成
        var talent = new OshikatsuApi.Entities.Talent
        {
            DisplayName = "山田花子",
            NormalizedName = "山田花子",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();

        var dto = BuildDto(jobType: "news", successCount: 0, failureCount: 1);
        dto.TalentId = talent.Id;

        var result = await _sut.RecordJobLogAsync(dto);

        Assert.Equal(talent.Id, result.TalentId);
    }

    [Fact]
    public async Task RecordJobLogAsync_NullOptionalFields_SavesNulls()
    {
        var dto = BuildDto(jobType: "news", successCount: 5, failureCount: 0);
        // FailureReason, ErrorMessage, TalentId はデフォルト null

        var result = await _sut.RecordJobLogAsync(dto);

        Assert.Null(result.FailureReason);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.TalentId);
    }

    // ── GetActiveFailureAlertsAsync: 正常系 ─────────────────────────────────

    [Fact]
    public async Task GetActiveFailureAlertsAsync_NoLogs_ReturnsNoAlerts()
    {
        var result = await _sut.GetActiveFailureAlertsAsync();

        Assert.False(result.IsOverallJobFailing);
        Assert.Empty(result.FailingTalents);
    }

    [Fact]
    public async Task GetActiveFailureAlertsAsync_ThreeConsecutiveOverallFailures_ReturnsOverallAlert()
    {
        // 全体ジョブ（talent_id = null）が3回連続失敗
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-3), successCount: 0, failureCount: 2, talentId: null);
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: null);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 3, talentId: null);

        var result = await _sut.GetActiveFailureAlertsAsync();

        Assert.True(result.IsOverallJobFailing);
        Assert.Empty(result.FailingTalents);
    }

    [Fact]
    public async Task GetActiveFailureAlertsAsync_FourConsecutiveOverallFailures_ReturnsNoAlertDuplicateSuppressed()
    {
        // 4連続失敗 = すでに3回目で送信済み → 重複しない
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-4), successCount: 0, failureCount: 1, talentId: null);
        await AddLog("news", now.AddHours(-3), successCount: 0, failureCount: 1, talentId: null);
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: null);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 1, talentId: null);

        var result = await _sut.GetActiveFailureAlertsAsync();

        Assert.False(result.IsOverallJobFailing);
    }

    [Fact]
    public async Task GetActiveFailureAlertsAsync_SuccessAfterThreeFailures_NoAlert()
    {
        // 成功が挟まった場合はアラートなし
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-4), successCount: 0, failureCount: 1, talentId: null);
        await AddLog("news", now.AddHours(-3), successCount: 5, failureCount: 0, talentId: null); // 成功
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: null);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 1, talentId: null);

        var result = await _sut.GetActiveFailureAlertsAsync();

        Assert.False(result.IsOverallJobFailing);
    }

    [Fact]
    public async Task GetActiveFailureAlertsAsync_ThreeConsecutiveTalentFailures_ReturnsTalentAlert()
    {
        var talent = new OshikatsuApi.Entities.Talent
        {
            DisplayName = "佐藤太郎",
            NormalizedName = "佐藤太郎",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-3), successCount: 0, failureCount: 1, talentId: talent.Id);
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: talent.Id);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 1, talentId: talent.Id);

        var result = await _sut.GetActiveFailureAlertsAsync();

        Assert.False(result.IsOverallJobFailing);
        Assert.Single(result.FailingTalents);
        Assert.Equal(talent.Id, result.FailingTalents[0].TalentId);
        Assert.Equal("佐藤太郎", result.FailingTalents[0].TalentName);
    }

    [Fact]
    public async Task GetActiveFailureAlertsAsync_FourConsecutiveTalentFailures_NoTalentAlert()
    {
        var talent = new OshikatsuApi.Entities.Talent
        {
            DisplayName = "田中花子",
            NormalizedName = "田中花子",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-4), successCount: 0, failureCount: 1, talentId: talent.Id);
        await AddLog("news", now.AddHours(-3), successCount: 0, failureCount: 1, talentId: talent.Id);
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: talent.Id);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 1, talentId: talent.Id);

        var result = await _sut.GetActiveFailureAlertsAsync();

        Assert.Empty(result.FailingTalents);
    }

    [Fact]
    public async Task GetActiveFailureAlertsAsync_TalentSuccessAfterThreeFailures_AlertReset()
    {
        // 成功後に再び3連続失敗 → 新しいアラートが発生する
        var talent = new OshikatsuApi.Entities.Talent
        {
            DisplayName = "田中次郎",
            NormalizedName = "田中次郎",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        // 4番目は成功（3連続失敗の前に成功があった = 新しい3連続）
        await AddLog("news", now.AddHours(-4), successCount: 5, failureCount: 0, talentId: talent.Id); // 成功
        await AddLog("news", now.AddHours(-3), successCount: 0, failureCount: 1, talentId: talent.Id);
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: talent.Id);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 1, talentId: talent.Id);

        var result = await _sut.GetActiveFailureAlertsAsync();

        Assert.Single(result.FailingTalents);
        Assert.Equal(talent.Id, result.FailingTalents[0].TalentId);
    }

    [Fact]
    public async Task GetActiveFailureAlertsAsync_TwoFailures_NoAlert()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: null);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 1, talentId: null);

        var result = await _sut.GetActiveFailureAlertsAsync();

        Assert.False(result.IsOverallJobFailing);
    }

    // ── GetOperationsDataAsync: 正常系 ──────────────────────────────────────

    [Fact]
    public async Task GetOperationsDataAsync_NoLogs_ReturnsEmpty()
    {
        var result = await _sut.GetOperationsDataAsync();

        Assert.NotNull(result);
        Assert.Empty(result.RecentJobs);
        Assert.False(result.IsOverallJobFailing);
        Assert.Empty(result.FailingTalents);
    }

    [Fact]
    public async Task GetOperationsDataAsync_RecentJobsWithin24h_ReturnsThem()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-1), successCount: 5, failureCount: 0);
        await AddLog("program", now.AddHours(-2), successCount: 3, failureCount: 1);

        var result = await _sut.GetOperationsDataAsync();

        Assert.Equal(2, result.RecentJobs.Count);
    }

    [Fact]
    public async Task GetOperationsDataAsync_OldJobsBeyond24h_ExcludesThem()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-25), successCount: 5, failureCount: 0); // 24h超過
        await AddLog("news", now.AddHours(-1), successCount: 3, failureCount: 0);  // 24h以内

        var result = await _sut.GetOperationsDataAsync();

        Assert.Single(result.RecentJobs);
    }

    [Fact]
    public async Task GetOperationsDataAsync_OverallJobFailing_SetsIsOverallJobFailing()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 2, talentId: null);

        var result = await _sut.GetOperationsDataAsync();

        Assert.True(result.IsOverallJobFailing);
    }

    [Fact]
    public async Task GetOperationsDataAsync_OverallJobSucceeded_IsOverallJobFailingFalse()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-1), successCount: 5, failureCount: 0, talentId: null);

        var result = await _sut.GetOperationsDataAsync();

        Assert.False(result.IsOverallJobFailing);
    }

    [Fact]
    public async Task GetOperationsDataAsync_TalentWithConsecutiveFailures_ReturnsFailingTalent()
    {
        var talent = new OshikatsuApi.Entities.Talent
        {
            DisplayName = "テスト推し",
            NormalizedName = "テスト推し",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-3), successCount: 0, failureCount: 1, talentId: talent.Id);
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 2, talentId: talent.Id);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 1, talentId: talent.Id);

        var result = await _sut.GetOperationsDataAsync();

        Assert.Single(result.FailingTalents);
        Assert.Equal(talent.Id, result.FailingTalents[0].TalentId);
        Assert.Equal("テスト推し", result.FailingTalents[0].TalentName);
        Assert.Equal(3, result.FailingTalents[0].ConsecutiveFailureCount);
    }

    [Fact]
    public async Task GetOperationsDataAsync_TalentWithSuccessAfterFailures_NotInFailingList()
    {
        var talent = new OshikatsuApi.Entities.Talent
        {
            DisplayName = "成功した推し",
            NormalizedName = "成功した推し",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: talent.Id); // 失敗
        await AddLog("news", now.AddHours(-1), successCount: 5, failureCount: 0, talentId: talent.Id); // 最新は成功

        var result = await _sut.GetOperationsDataAsync();

        Assert.Empty(result.FailingTalents);
    }

    // ── GetOperationsDataAsync: 境界値 ──────────────────────────────────────

    [Fact]
    public async Task GetOperationsDataAsync_MultipleTalents_SortedByConsecutiveCountDesc()
    {
        var talent1 = new OshikatsuApi.Entities.Talent { DisplayName = "推し1", NormalizedName = "推し1", CreatedAt = DateTime.UtcNow };
        var talent2 = new OshikatsuApi.Entities.Talent { DisplayName = "推し2", NormalizedName = "推し2", CreatedAt = DateTime.UtcNow };
        _db.Talents.AddRange(talent1, talent2);
        await _db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        // talent1: 1回連続失敗
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 1, talentId: talent1.Id);
        // talent2: 3回連続失敗
        await AddLog("news", now.AddHours(-3), successCount: 0, failureCount: 1, talentId: talent2.Id);
        await AddLog("news", now.AddHours(-2), successCount: 0, failureCount: 1, talentId: talent2.Id);
        await AddLog("news", now.AddHours(-1), successCount: 0, failureCount: 2, talentId: talent2.Id);

        var result = await _sut.GetOperationsDataAsync();

        Assert.Equal(2, result.FailingTalents.Count);
        // 降順: talent2（3回）が先
        Assert.Equal(talent2.Id, result.FailingTalents[0].TalentId);
        Assert.Equal(3, result.FailingTalents[0].ConsecutiveFailureCount);
        Assert.Equal(talent1.Id, result.FailingTalents[1].TalentId);
        Assert.Equal(1, result.FailingTalents[1].ConsecutiveFailureCount);
    }

    [Fact]
    public async Task GetOperationsDataAsync_RecentJobsSortedNewestFirst()
    {
        var now = DateTimeOffset.UtcNow;
        await AddLog("news", now.AddHours(-3), successCount: 1, failureCount: 0);
        await AddLog("news", now.AddHours(-1), successCount: 5, failureCount: 0); // 最新

        var result = await _sut.GetOperationsDataAsync();

        Assert.True(result.RecentJobs[0].ExecutedAt > result.RecentJobs[1].ExecutedAt);
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
        int failureCount,
        int? talentId = null)
    {
        var log = new CollectorJobLog
        {
            JobType = jobType,
            ExecutedAt = executedAt,
            SuccessCount = successCount,
            FailureCount = failureCount,
            TalentId = talentId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.CollectorJobLogs.Add(log);
        await _db.SaveChangesAsync();
        return log;
    }
}
