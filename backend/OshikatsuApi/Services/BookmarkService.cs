using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class BookmarkService : IBookmarkService
{
    private readonly AppDbContext _db;

    public BookmarkService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task AddBookmarkAsync(int userId, int newsItemId)
    {
        // ニュースアイテムの存在確認
        var newsExists = await _db.NewsItems.AnyAsync(n => n.Id == newsItemId);
        if (!newsExists)
        {
            throw new NewsItemNotFoundException();
        }

        // 重複ブックマークの確認
        var alreadyExists = await _db.Bookmarks
            .AnyAsync(b => b.UserId == userId && b.NewsItemId == newsItemId);
        if (alreadyExists)
        {
            throw new BookmarkAlreadyExistsException();
        }

        var bookmark = new Bookmark
        {
            UserId = userId,
            NewsItemId = newsItemId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Bookmarks.Add(bookmark);
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task RemoveBookmarkAsync(int userId, int newsItemId)
    {
        var bookmark = await _db.Bookmarks
            .FirstOrDefaultAsync(b => b.UserId == userId && b.NewsItemId == newsItemId);

        if (bookmark is null)
        {
            throw new BookmarkNotFoundException();
        }

        _db.Bookmarks.Remove(bookmark);
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<BookmarkResponseDto>> GetBookmarksAsync(
        int userId, int page, int pageSize,
        int? oshiId = null, string? query = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        // FEAT-039: user_oshis テーブルからユーザーのタレント一覧を取得する
        // 他ユーザーの推し ID を指定しても他人のデータは返さない
        var userOshisData = await _db.UserOshis
            .Where(o => o.UserId == userId)
            .Include(o => o.Talent)
            .ToListAsync();

        var talentIdToUserOshiId = userOshisData.ToDictionary(o => o.TalentId, o => o.Id);
        var talentIdToName = userOshisData.ToDictionary(o => o.TalentId, o => o.Talent.DisplayName);

        // oshiId フィルタが指定された場合、user_oshi_id → talent_id に解決する
        int? talentIdFilter = null;
        if (oshiId.HasValue)
        {
            var targetUserOshi = userOshisData.FirstOrDefault(o => o.Id == oshiId.Value);
            if (targetUserOshi is null)
            {
                return new PaginatedResponseDto<BookmarkResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                };
            }
            talentIdFilter = targetUserOshi.TalentId;
        }

        var bookmarkQuery = _db.Bookmarks
            .Where(b => b.UserId == userId)
            .Include(b => b.NewsItem)
            .ThenInclude(n => n.Talent)
            .AsQueryable();

        // 推し ID フィルタ（talent_id 経由）
        if (talentIdFilter.HasValue)
        {
            bookmarkQuery = bookmarkQuery.Where(b => b.NewsItem.TalentId == talentIdFilter.Value);
        }

        // キーワード検索（FEAT-031）: タイトルまたは要約に対して大文字小文字を区別しない部分一致
        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLowerInvariant();
            bookmarkQuery = bookmarkQuery.Where(b =>
                b.NewsItem.Title.ToLower().Contains(lowerQuery) ||
                (b.NewsItem.Summary != null && b.NewsItem.Summary.ToLower().Contains(lowerQuery)));
        }

        // 日付範囲フィルタ: 掲載日（PublishedAt）で絞り込む（FEAT-031）
        // Npgsql は UTC 以外のオフセット付き DateTimeOffset をクエリパラメータとして使えないため UTC に正規化する
        if (from.HasValue)
        {
            var fromUtc = from.Value.ToUniversalTime();
            bookmarkQuery = bookmarkQuery.Where(b => b.NewsItem.PublishedAt != null && b.NewsItem.PublishedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = to.Value.ToUniversalTime();
            bookmarkQuery = bookmarkQuery.Where(b => b.NewsItem.PublishedAt != null && b.NewsItem.PublishedAt <= toUtc);
        }

        // 並び順: ブックマーク登録日時の新しい順（FEAT-029 対象外）
        var orderedQuery = bookmarkQuery.OrderByDescending(b => b.CreatedAt);

        var total = await orderedQuery.CountAsync();
        var rawItems = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = rawItems.Select(b =>
        {
            var newsItem = b.NewsItem;
            // FEAT-039: OshiId は user_oshi_id（talent_id 経由で解決）
            var userOshiId = newsItem.TalentId.HasValue
                ? talentIdToUserOshiId.GetValueOrDefault(newsItem.TalentId.Value)
                : 0;
            var oshiName = newsItem.TalentId.HasValue && newsItem.Talent is not null
                ? newsItem.Talent.DisplayName
                : (newsItem.TalentId.HasValue
                    ? talentIdToName.GetValueOrDefault(newsItem.TalentId.Value, string.Empty)
                    : string.Empty);
            return new BookmarkResponseDto
            {
                Id = b.Id,
                NewsItemId = b.NewsItemId,
                OshiId = userOshiId,
                OshiName = oshiName,
                Title = newsItem.Title,
                Summary = newsItem.Summary,
                SourceUrl = newsItem.SourceUrl,
                PublishedAt = newsItem.PublishedAt,
                IsPublishedAtEstimated = newsItem.IsPublishedAtEstimated,
                CollectedAt = newsItem.CollectedAt,
                BookmarkedAt = b.CreatedAt,
                // Sprint 34 (FEAT-036): 媒体名。DB に保存済みの値を優先し、ない場合はタイトル末尾から取り出す
                SourceName = !string.IsNullOrEmpty(newsItem.SourceName)
                    ? newsItem.SourceName
                    : ArticleDeduplicator.ExtractSourceNameFromTitle(newsItem.Title),
            };
        }).ToList();

        return new PaginatedResponseDto<BookmarkResponseDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }
}
