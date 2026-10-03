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

    private static JobLogResponseDto MapToDto(CollectorJobLog log) => new()
    {
        Id = log.Id,
        JobType = log.JobType,
        ExecutedAt = log.ExecutedAt,
        SuccessCount = log.SuccessCount,
        FailureCount = log.FailureCount,
        CreatedAt = log.CreatedAt,
    };
}
