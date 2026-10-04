using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OshiDashboardCardDto>> GetSummaryAsync(int userId)
    {
        // クエリ 1: ユーザーの推し一覧を登録順で取得
        var oshis = await _db.Oshis
            .Where(o => o.UserId == userId)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        if (oshis.Count == 0)
        {
            return [];
        }

        var oshiIds = oshis.Select(o => o.Id).ToList();

        // クエリ 2: 全推しのニュースを PublishedAt 降順で一括取得し、メモリで推しごとに最新1件を抽出（N+1 回避）
        // FEAT-029: PublishedAt 降順、同一のとき ID 降順で並べる
        // FEAT-030: 画像箱を除外する
        var newsItems = await _db.NewsItems
            .Where(n => oshiIds.Contains(n.OshiId))
            .OrderByDescending(n => n.PublishedAt)
            .ThenByDescending(n => n.Id)
            .ToListAsync();

        var latestNewsByOshiId = newsItems
            .Where(n => !GalleryTitleDetector.IsGalleryTitle(n.Title))
            .GroupBy(n => n.OshiId)
            .ToDictionary(g => g.Key, g => g.First());

        // クエリ 3: 全推しの出演番組を AiredAt 降順で一括取得し、メモリで推しごとに最新1件を抽出（N+1 回避）
        // FEAT-024: 同推しのニュースと sourceUrl が重複する番組を除外
        // FEAT-029: AiredAt 降順、同一のとき ID 降順で並べる
        // FEAT-030: 画像箱を除外する
        var programItems = await _db.ProgramAppearances
            .Where(p => oshiIds.Contains(p.OshiId))
            .Where(p => !_db.NewsItems.Any(n => n.OshiId == p.OshiId && n.SourceUrl == p.SourceUrl))
            .OrderByDescending(p => p.AiredAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        var latestProgramByOshiId = programItems
            .Where(p => !GalleryTitleDetector.IsGalleryTitle(p.ProgramName))
            .GroupBy(p => p.OshiId)
            .ToDictionary(g => g.Key, g => g.First());

        // 3 クエリ合計（N+1 なし）でカードを組み立てて返す
        return oshis.Select(o => new OshiDashboardCardDto
        {
            OshiId = o.Id,
            OshiName = o.Name,
            LatestNews = latestNewsByOshiId.TryGetValue(o.Id, out var news)
                ? new NewsItemSummaryDto
                {
                    Id = news.Id,
                    Title = news.Title,
                    SourceUrl = news.SourceUrl,
                    CollectedAt = news.CollectedAt,
                }
                : null,
            LatestProgram = latestProgramByOshiId.TryGetValue(o.Id, out var program)
                ? new ProgramAppearanceSummaryDto
                {
                    Id = program.Id,
                    ProgramName = program.ProgramName,
                    Broadcaster = program.Broadcaster,
                    SourceUrl = program.SourceUrl,
                    CollectedAt = program.CollectedAt,
                }
                : null,
        }).ToList();
    }
}
