using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class CollectorJobLogService : ICollectorJobLogService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CollectorJobLogService> _logger;

    public CollectorJobLogService(AppDbContext db, ILogger<CollectorJobLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JobLogResponseDto> RecordJobLogAsync(CreateJobLogRequestDto dto)
    {
        // Npgsql は UTC 以外のオフセット付き DateTimeOffset を timestamptz に書き込めないため
        // 受信した DateTimeOffset を UTC に正規化してから保存する
        var now = DateTimeOffset.UtcNow;
        var log = new CollectorJobLog
        {
            JobType = dto.JobType,
            ExecutedAt = dto.ExecutedAt.ToUniversalTime(),
            SuccessCount = dto.SuccessCount,
            FailureCount = dto.FailureCount,
            FailureReason = dto.FailureReason,
            ErrorMessage = dto.ErrorMessage,
            TalentId = dto.TalentId,
            CreatedAt = now,
        };

        _db.CollectorJobLogs.Add(log);
        await _db.SaveChangesAsync();

        // 連続3回 FailureCount > 0 の場合に WARNING を出力する
        await CheckConsecutiveFailuresAsync(dto.JobType);

        return MapToDto(log);
    }

    /// <inheritdoc />
    public async Task<CollectorStatusResponseDto> GetStatusAsync()
    {
        var latestNews = await _db.CollectorJobLogs
            .Where(l => l.JobType == "news")
            .OrderByDescending(l => l.ExecutedAt)
            .FirstOrDefaultAsync();

        var latestProgram = await _db.CollectorJobLogs
            .Where(l => l.JobType == "program")
            .OrderByDescending(l => l.ExecutedAt)
            .FirstOrDefaultAsync();

        return new CollectorStatusResponseDto
        {
            LatestNewsJob = latestNews != null ? MapToDto(latestNews) : null,
            LatestProgramJob = latestProgram != null ? MapToDto(latestProgram) : null,
        };
    }

    // ── プライベートヘルパー ─────────────────────────────────────────────────

    private async Task CheckConsecutiveFailuresAsync(string jobType)
    {
        var recent = await _db.CollectorJobLogs
            .Where(l => l.JobType == jobType)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(3)
            .ToListAsync();

        if (recent.Count == 3 && recent.All(l => l.FailureCount > 0))
        {
            _logger.LogWarning(
                "ジョブタイプ '{JobType}' が連続3回 FailureCount > 0 です（直近3件の失敗件数: {Failures}）。バッチの健全性を確認してください。",
                jobType,
                string.Join(", ", recent.Select(l => l.FailureCount)));
        }
    }

    /// <inheritdoc />
    public async Task<FailureAlertSummary> GetActiveFailureAlertsAsync()
    {
        // 全体ジョブ（talent_id IS NULL）の連続失敗チェック
        var overallRecent = await _db.CollectorJobLogs
            .Where(l => !l.TalentId.HasValue)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(4)
            .ToListAsync();

        bool isOverallFailing = IsThirdConsecutiveFailure(overallRecent);

        // タレント別の連続失敗チェック
        var talentIds = await _db.CollectorJobLogs
            .Where(l => l.TalentId.HasValue)
            .Select(l => l.TalentId!.Value)
            .Distinct()
            .ToListAsync();

        var failingTalents = new List<FailingTalentInfo>();
        foreach (var talentId in talentIds)
        {
            var talentRecent = await _db.CollectorJobLogs
                .Where(l => l.TalentId == talentId)
                .OrderByDescending(l => l.ExecutedAt)
                .Take(4)
                .ToListAsync();

            if (IsThirdConsecutiveFailure(talentRecent))
            {
                var talent = await _db.Talents.FindAsync(talentId);
                if (talent is not null)
                    failingTalents.Add(new FailingTalentInfo(talentId, talent.DisplayName));
            }
        }

        return new FailureAlertSummary(isOverallFailing, failingTalents);
    }

    // ── プライベートヘルパー ─────────────────────────────────────────────────

    /// <summary>
    /// 直近4件のログから「ちょうど3回目の連続失敗」を検知する。
    /// <list type="bullet">
    ///   <item>直近3件がすべて FailureCount &gt; 0</item>
    ///   <item>かつ4件目が存在しないか FailureCount == 0（= 初回検知）</item>
    /// </list>
    /// 4件目も失敗の場合は「既に3回目で警告済み」とみなして false を返す（重複送信防止）。
    /// </summary>
    private static bool IsThirdConsecutiveFailure(List<CollectorJobLog> recent4)
    {
        if (recent4.Count < 3) return false;
        if (!recent4.Take(3).All(l => l.FailureCount > 0)) return false;
        // 4件目が存在しないか成功 = 今回初めて3連続失敗に到達
        if (recent4.Count >= 4 && recent4[3].FailureCount > 0) return false;
        return true;
    }

    private static JobLogResponseDto MapToDto(CollectorJobLog log) => new()
    {
        Id = log.Id,
        JobType = log.JobType,
        ExecutedAt = log.ExecutedAt,
        SuccessCount = log.SuccessCount,
        FailureCount = log.FailureCount,
        FailureReason = log.FailureReason,
        ErrorMessage = log.ErrorMessage,
        TalentId = log.TalentId,
        CreatedAt = log.CreatedAt,
    };
}
