using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class FeedService : IFeedService
{
    private readonly AppDbContext _db;

    public FeedService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<NewsItemResponseDto>> GetNewsAsync(
        int userId, int page, int pageSize,
        int? oshiId = null, string? query = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        // ユーザーの推し ID 一覧を取得（サブクエリよりも InMemory・SQL 両対応しやすい）
        var oshiIds = await _db.Oshis
            .Where(o => o.UserId == userId)
            .Select(o => o.Id)
            .ToListAsync();

        // oshiId フィルタが指定された場合、ユーザーの推しに含まれるか検証して絞り込む
        if (oshiId.HasValue)
        {
            if (!oshiIds.Contains(oshiId.Value))
            {
                return new PaginatedResponseDto<NewsItemResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                };
            }
            oshiIds = [oshiId.Value];
        }

        var newsQuery = _db.NewsItems.Where(n => oshiIds.Contains(n.OshiId));

        // キーワード検索: タイトルまたは本文要約に対して大文字小文字を区別しない部分一致で絞り込む。
        // ToLower().Contains() を使うことで、PostgreSQL（Npgsql が lower()+strpos に変換）と
        // InMemory（C# の string.Contains() をそのまま使用）の両方で動作する。
        // EF Core が Contains() のパターン中の % / _ / \ を自動エスケープするため手動エスケープ不要。
        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLowerInvariant();
            newsQuery = newsQuery.Where(n =>
                n.Title.ToLower().Contains(lowerQuery) ||
                (n.Summary != null && n.Summary.ToLower().Contains(lowerQuery)));
        }

        // 日付範囲フィルタ: 公開日時（PublishedAt）で絞り込む。
        // PublishedAt が null のアイテムはフィルタ適用時に除外される。
        // Npgsql は UTC 以外のオフセット付き DateTimeOffset をクエリパラメータとして使えないため
        // from/to を UTC に正規化してから渡す。
        if (from.HasValue)
        {
            var fromUtc = from.Value.ToUniversalTime();
            newsQuery = newsQuery.Where(n => n.PublishedAt != null && n.PublishedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = to.Value.ToUniversalTime();
            newsQuery = newsQuery.Where(n => n.PublishedAt != null && n.PublishedAt <= toUtc);
        }

        // FEAT-029: PublishedAt 降順、同一のとき ID 降順で並べる
        var orderedQuery = newsQuery
            .OrderByDescending(n => n.PublishedAt)
            .ThenByDescending(n => n.Id);

        // FEAT-030: 画像箱（フォトギャラリー記事）を除外する（メモリでフィルタ）
        // DB にはバッチ登録時に既に弾いているが、過去データのために一覧取得時にも除外する
        var allItems = await orderedQuery.ToListAsync();
        var filteredItems = allItems
            .Where(n => !GalleryTitleDetector.IsGalleryTitle(n.Title))
            .ToList();

        // FEAT-035: 転載重複まとめ（メモリでグループ化）
        var groups = ArticleDeduplicator.GroupNewsItems(filteredItems);

        var total = groups.Count;
        var pageGroups = groups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 推し名を付与するために Oshi を一括ロード
        var oshiNameMap = await _db.Oshis
            .Where(o => oshiIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name);

        // ブックマーク済みIDセットを取得（現在ページのグループ内の全アイテム ID を対象）
        var allGroupItemIds = pageGroups
            .SelectMany(g => g.Others.Select(o => o.Id).Prepend(g.Representative.Id))
            .ToList();
        var bookmarkedIdsList = await _db.Bookmarks
            .Where(b => b.UserId == userId && allGroupItemIds.Contains(b.NewsItemId))
            .Select(b => b.NewsItemId)
            .ToListAsync();
        var bookmarkedIds = new HashSet<int>(bookmarkedIdsList);

        var items = pageGroups.Select(g =>
        {
            var n = g.Representative;
            var sourceName = !string.IsNullOrEmpty(n.SourceName)
                ? n.SourceName
                : ArticleDeduplicator.ExtractSourceNameFromTitle(n.Title);
            // グループ内のいずれかがブックマーク済みなら代表をブックマーク済みとする
            var isBookmarked = g.Others.Any(o => bookmarkedIds.Contains(o.Id))
                || bookmarkedIds.Contains(n.Id);
            return new NewsItemResponseDto
            {
                Id = n.Id,
                OshiId = n.OshiId,
                OshiName = oshiNameMap.GetValueOrDefault(n.OshiId) ?? string.Empty,
                Title = n.Title,
                Summary = n.Summary,
                SourceUrl = n.SourceUrl,
                PublishedAt = n.PublishedAt,
                IsPublishedAtEstimated = n.IsPublishedAtEstimated,
                CollectedAt = n.CollectedAt,
                IsBookmarked = isBookmarked,
                SourceName = sourceName,
                OtherSourceCount = g.Others.Count,
                OtherSources = g.Others.Select(o => new OtherSourceDto
                {
                    SourceName = !string.IsNullOrEmpty(o.SourceName)
                        ? o.SourceName
                        : ArticleDeduplicator.ExtractSourceNameFromTitle(o.Title),
                    SourceUrl = o.SourceUrl,
                }).ToList(),
            };
        }).ToList();

        return new PaginatedResponseDto<NewsItemResponseDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<ProgramAppearanceResponseDto>> GetProgramsAsync(
        int userId, int page, int pageSize,
        int? oshiId = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        var oshiIds = await _db.Oshis
            .Where(o => o.UserId == userId)
            .Select(o => o.Id)
            .ToListAsync();

        // oshiId フィルタが指定された場合、ユーザーの推しに含まれるか検証して絞り込む
        if (oshiId.HasValue)
        {
            if (!oshiIds.Contains(oshiId.Value))
            {
                return new PaginatedResponseDto<ProgramAppearanceResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                };
            }
            oshiIds = [oshiId.Value];
        }

        var programsQuery = _db.ProgramAppearances.Where(p => oshiIds.Contains(p.OshiId));

        // FEAT-024: 同推しのニュースと sourceUrl が重複する番組を除外（DBからは削除しない）
        programsQuery = programsQuery
            .Where(p => !_db.NewsItems.Any(n => n.OshiId == p.OshiId && n.SourceUrl == p.SourceUrl));

        // 日付範囲フィルタ: 放送日時（AiredAt）で絞り込む。
        // AiredAt が null のアイテムはフィルタ適用時に除外される。
        // Npgsql は UTC 以外のオフセット付き DateTimeOffset をクエリパラメータとして使えないため
        // from/to を UTC に正規化してから渡す。
        if (from.HasValue)
        {
            var fromUtc = from.Value.ToUniversalTime();
            programsQuery = programsQuery.Where(p => p.AiredAt != null && p.AiredAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = to.Value.ToUniversalTime();
            programsQuery = programsQuery.Where(p => p.AiredAt != null && p.AiredAt <= toUtc);
        }

        // FEAT-029: AiredAt 降順、同一のとき ID 降順で並べる
        var orderedProgramsQuery = programsQuery
            .OrderByDescending(p => p.AiredAt)
            .ThenByDescending(p => p.Id);

        // FEAT-030: 画像箱（フォトギャラリー記事）を除外する（メモリでフィルタ）
        var allPrograms = await orderedProgramsQuery.ToListAsync();
        var filteredPrograms = allPrograms
            .Where(p => !GalleryTitleDetector.IsGalleryTitle(p.ProgramName))
            .ToList();

        // FEAT-035: 転載重複まとめ（メモリでグループ化）
        var groups = ArticleDeduplicator.GroupProgramItems(filteredPrograms);

        var total = groups.Count;
        var pageGroups = groups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var oshiNameMap = await _db.Oshis
            .Where(o => oshiIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name);

        var items = pageGroups.Select(g =>
        {
            var p = g.Representative;
            var sourceName = !string.IsNullOrEmpty(p.SourceName)
                ? p.SourceName
                : ArticleDeduplicator.ExtractSourceNameFromTitle(p.ProgramName);
            return new ProgramAppearanceResponseDto
            {
                Id = p.Id,
                OshiId = p.OshiId,
                OshiName = oshiNameMap.GetValueOrDefault(p.OshiId) ?? string.Empty,
                ProgramName = p.ProgramName,
                Broadcaster = p.Broadcaster,
                AiredAt = p.AiredAt,
                IsAiredAtEstimated = p.IsAiredAtEstimated,
                SourceUrl = p.SourceUrl,
                CollectedAt = p.CollectedAt,
                SourceName = sourceName,
                OtherSourceCount = g.Others.Count,
                OtherSources = g.Others.Select(o => new OtherSourceDto
                {
                    SourceName = !string.IsNullOrEmpty(o.SourceName)
                        ? o.SourceName
                        : ArticleDeduplicator.ExtractSourceNameFromTitle(o.ProgramName),
                    SourceUrl = o.SourceUrl,
                }).ToList(),
            };
        }).ToList();

        return new PaginatedResponseDto<ProgramAppearanceResponseDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user?.LastViewedFeedAt is null)
        {
            // 未閲覧ユーザーは 0 を返す（比較基準がないため）
            return 0;
        }

        var lastViewed = user.LastViewedFeedAt.Value;

        var oshiIds = await _db.Oshis
            .Where(o => o.UserId == userId)
            .Select(o => o.Id)
            .ToListAsync();

        if (oshiIds.Count == 0)
        {
            return 0;
        }

        var newsCount = await _db.NewsItems
            .Where(n => oshiIds.Contains(n.OshiId) && n.CollectedAt > lastViewed)
            .CountAsync();

        var programCount = await _db.ProgramAppearances
            .Where(p => oshiIds.Contains(p.OshiId) && p.CollectedAt > lastViewed)
            .CountAsync();

        return newsCount + programCount;
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        user.LastViewedFeedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

}
