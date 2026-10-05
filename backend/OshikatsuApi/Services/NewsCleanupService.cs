using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class NewsCleanupService : INewsCleanupService
{
    /// <summary>保持期間（日数）</summary>
    public const int RetentionDays = 90;

    private readonly AppDbContext _db;
    private readonly ILogger<NewsCleanupService> _logger;

    public NewsCleanupService(AppDbContext db, ILogger<NewsCleanupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> DeleteExpiredNewsAsync(int batchSize = 1000)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-RetentionDays);

        // ブックマーク済みの記事を除外しながら削除対象エンティティを取得する
        var itemsToDelete = await _db.NewsItems
            .Where(n =>
                (n.PublishedAt.HasValue && n.PublishedAt.Value <= cutoff) ||
                (!n.PublishedAt.HasValue && n.CollectedAt <= cutoff))
            .Where(n => !_db.Bookmarks.Any(b => b.NewsItemId == n.Id))
            .Take(batchSize)
            .ToListAsync();

        if (itemsToDelete.Count == 0)
        {
            _logger.LogInformation("削除対象の記事はありませんでした。");
            await RecordCleanupLogAsync(0);
            return 0;
        }

        _db.NewsItems.RemoveRange(itemsToDelete);
        await _db.SaveChangesAsync();
        var deletedCount = itemsToDelete.Count;

        _logger.LogInformation(
            "古い記事の削除完了: {DeletedCount}件削除（cutoff={Cutoff:O}）",
            deletedCount,
            cutoff);

        await RecordCleanupLogAsync(deletedCount);

        return deletedCount;
    }

    // ── プライベートヘルパー ──────────────────────────────────────────────────

    private async Task RecordCleanupLogAsync(int deletedCount)
    {
        var now = DateTimeOffset.UtcNow;
        var log = new CollectorJobLog
        {
            JobType = "cleanup",
            ExecutedAt = now,
            SuccessCount = deletedCount,
            FailureCount = 0,
            CreatedAt = now,
        };
        _db.CollectorJobLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}
