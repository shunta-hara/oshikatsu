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
        // 他ユーザーの推し ID を指定しても他人のデータは返さない
        // まず自分の推し ID 一覧を取得し、oshiId 指定がある場合は自分の推しに含まれるか検証する
        if (oshiId.HasValue)
        {
            var ownOshiIds = await _db.Oshis
                .Where(o => o.UserId == userId)
                .Select(o => o.Id)
                .ToListAsync();

            if (!ownOshiIds.Contains(oshiId.Value))
            {
                return new PaginatedResponseDto<BookmarkResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                };
            }
        }

        var bookmarkQuery = _db.Bookmarks
            .Where(b => b.UserId == userId)
            .Include(b => b.NewsItem)
            .ThenInclude(n => n.Oshi)
            .AsQueryable();

        // 推し ID フィルタ
        if (oshiId.HasValue)
        {
            bookmarkQuery = bookmarkQuery.Where(b => b.NewsItem.OshiId == oshiId.Value);
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

        var items = rawItems.Select(b => new BookmarkResponseDto
        {
            Id = b.Id,
            NewsItemId = b.NewsItemId,
            OshiId = b.NewsItem.OshiId,
            OshiName = b.NewsItem.Oshi.Name,
            Title = b.NewsItem.Title,
            Summary = b.NewsItem.Summary,
            SourceUrl = b.NewsItem.SourceUrl,
            PublishedAt = b.NewsItem.PublishedAt,
            IsPublishedAtEstimated = b.NewsItem.IsPublishedAtEstimated,
            CollectedAt = b.NewsItem.CollectedAt,
            BookmarkedAt = b.CreatedAt,
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
