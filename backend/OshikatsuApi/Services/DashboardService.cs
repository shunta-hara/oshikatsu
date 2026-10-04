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

        // FEAT-035: 転載重複まとめ後に推しごと最新1件を取得
        var filteredNewsItems = newsItems
            .Where(n => !GalleryTitleDetector.IsGalleryTitle(n.Title))
            .ToList();
        var newsGroups = ArticleDeduplicator.GroupNewsItems(filteredNewsItems);
        // 代表の推しIDごとに最新（最初）のグループを取得
        var latestNewsGroupByOshiId = newsGroups
            .GroupBy(g => g.Representative.OshiId)
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

        // FEAT-035: 転載重複まとめ後に推しごと最新1件を取得
        var filteredProgramItems = programItems
            .Where(p => !GalleryTitleDetector.IsGalleryTitle(p.ProgramName))
            .ToList();
        var programGroups = ArticleDeduplicator.GroupProgramItems(filteredProgramItems);
        var latestProgramGroupByOshiId = programGroups
            .GroupBy(g => g.Representative.OshiId)
            .ToDictionary(g => g.Key, g => g.First());

        // 3 クエリ合計（N+1 なし）でカードを組み立てて返す
        return oshis.Select(o => new OshiDashboardCardDto
        {
            OshiId = o.Id,
            OshiName = o.Name,
            LatestNews = latestNewsGroupByOshiId.TryGetValue(o.Id, out var newsGroup)
                ? new NewsItemSummaryDto
                {
                    Id = newsGroup.Representative.Id,
                    Title = newsGroup.Representative.Title,
                    SourceUrl = newsGroup.Representative.SourceUrl,
                    CollectedAt = newsGroup.Representative.CollectedAt,
                    SourceName = !string.IsNullOrEmpty(newsGroup.Representative.SourceName)
                        ? newsGroup.Representative.SourceName
                        : ArticleDeduplicator.ExtractSourceNameFromTitle(newsGroup.Representative.Title),
                    OtherSourceCount = newsGroup.Others.Count,
                    OtherSources = newsGroup.Others.Select(ot => new OshikatsuApi.DTOs.OtherSourceDto
                    {
                        SourceName = !string.IsNullOrEmpty(ot.SourceName)
                            ? ot.SourceName
                            : ArticleDeduplicator.ExtractSourceNameFromTitle(ot.Title),
                        SourceUrl = ot.SourceUrl,
                    }).ToList(),
                }
                : null,
            LatestProgram = latestProgramGroupByOshiId.TryGetValue(o.Id, out var programGroup)
                ? new ProgramAppearanceSummaryDto
                {
                    Id = programGroup.Representative.Id,
                    ProgramName = programGroup.Representative.ProgramName,
                    Broadcaster = programGroup.Representative.Broadcaster,
                    SourceUrl = programGroup.Representative.SourceUrl,
                    CollectedAt = programGroup.Representative.CollectedAt,
                    SourceName = !string.IsNullOrEmpty(programGroup.Representative.SourceName)
                        ? programGroup.Representative.SourceName
                        : ArticleDeduplicator.ExtractSourceNameFromTitle(programGroup.Representative.ProgramName),
                    OtherSourceCount = programGroup.Others.Count,
                    OtherSources = programGroup.Others.Select(ot => new OshikatsuApi.DTOs.OtherSourceDto
                    {
                        SourceName = !string.IsNullOrEmpty(ot.SourceName)
                            ? ot.SourceName
                            : ArticleDeduplicator.ExtractSourceNameFromTitle(ot.ProgramName),
                        SourceUrl = ot.SourceUrl,
                    }).ToList(),
                }
                : null,
        }).ToList();
    }
}
