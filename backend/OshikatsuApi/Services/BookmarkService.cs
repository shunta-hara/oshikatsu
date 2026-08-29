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
        int userId, int page, int pageSize)
    {
        var query = _db.Bookmarks
            .Where(b => b.UserId == userId)
            .Include(b => b.NewsItem)
            .ThenInclude(n => n.Oshi)
            .OrderByDescending(b => b.CreatedAt);

        var total = await query.CountAsync();
        var rawItems = await query
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
