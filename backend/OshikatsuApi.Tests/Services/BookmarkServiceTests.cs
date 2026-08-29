using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class BookmarkServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly BookmarkService _sut;

    public BookmarkServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new BookmarkService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── AddBookmarkAsync: 正常系 ─────────────────────────────────────────────

    [Fact]
    public async Task AddBookmarkAsync_ValidNewsItem_CreatesBookmark()
    {
        var newsItem = await CreateNewsItem();

        await _sut.AddBookmarkAsync(userId: 1, newsItemId: newsItem.Id);

        var bookmark = await _db.Bookmarks
            .FirstOrDefaultAsync(b => b.UserId == 1 && b.NewsItemId == newsItem.Id);
        Assert.NotNull(bookmark);
    }

    [Fact]
    public async Task AddBookmarkAsync_ValidNewsItem_SetsCreatedAt()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var newsItem = await CreateNewsItem();

        await _sut.AddBookmarkAsync(userId: 1, newsItemId: newsItem.Id);

        var bookmark = await _db.Bookmarks
            .FirstAsync(b => b.UserId == 1 && b.NewsItemId == newsItem.Id);
        Assert.True(bookmark.CreatedAt >= before);
    }

    [Fact]
    public async Task AddBookmarkAsync_DifferentUsersCanBookmarkSameItem()
    {
        var newsItem = await CreateNewsItem();

        await _sut.AddBookmarkAsync(userId: 1, newsItemId: newsItem.Id);
        await _sut.AddBookmarkAsync(userId: 2, newsItemId: newsItem.Id);

        var count = await _db.Bookmarks.CountAsync(b => b.NewsItemId == newsItem.Id);
        Assert.Equal(2, count);
    }

    // ── AddBookmarkAsync: 異常系 ─────────────────────────────────────────────

    [Fact]
    public async Task AddBookmarkAsync_NewsItemNotFound_ThrowsNewsItemNotFoundException()
    {
        await Assert.ThrowsAsync<NewsItemNotFoundException>(
            () => _sut.AddBookmarkAsync(userId: 1, newsItemId: 999));
    }

    [Fact]
    public async Task AddBookmarkAsync_AlreadyBookmarked_ThrowsBookmarkAlreadyExistsException()
    {
        var newsItem = await CreateNewsItem();
        await _sut.AddBookmarkAsync(userId: 1, newsItemId: newsItem.Id);

        await Assert.ThrowsAsync<BookmarkAlreadyExistsException>(
            () => _sut.AddBookmarkAsync(userId: 1, newsItemId: newsItem.Id));
    }

    // ── RemoveBookmarkAsync: 正常系 ──────────────────────────────────────────

    [Fact]
    public async Task RemoveBookmarkAsync_ExistingBookmark_RemovesIt()
    {
        var newsItem = await CreateNewsItem();
        await _sut.AddBookmarkAsync(userId: 1, newsItemId: newsItem.Id);

        await _sut.RemoveBookmarkAsync(userId: 1, newsItemId: newsItem.Id);

        var bookmark = await _db.Bookmarks
            .FirstOrDefaultAsync(b => b.UserId == 1 && b.NewsItemId == newsItem.Id);
        Assert.Null(bookmark);
    }

    [Fact]
    public async Task RemoveBookmarkAsync_OnlyRemovesTargetUsersBookmark()
    {
        var newsItem = await CreateNewsItem();
        await _sut.AddBookmarkAsync(userId: 1, newsItemId: newsItem.Id);
        await _sut.AddBookmarkAsync(userId: 2, newsItemId: newsItem.Id);

        await _sut.RemoveBookmarkAsync(userId: 1, newsItemId: newsItem.Id);

        // user2 のブックマークは残っている
        var user2Bookmark = await _db.Bookmarks
            .FirstOrDefaultAsync(b => b.UserId == 2 && b.NewsItemId == newsItem.Id);
        Assert.NotNull(user2Bookmark);
    }

    // ── RemoveBookmarkAsync: 異常系 ──────────────────────────────────────────

    [Fact]
    public async Task RemoveBookmarkAsync_BookmarkNotFound_ThrowsBookmarkNotFoundException()
    {
        var newsItem = await CreateNewsItem();

        await Assert.ThrowsAsync<BookmarkNotFoundException>(
            () => _sut.RemoveBookmarkAsync(userId: 1, newsItemId: newsItem.Id));
    }

    [Fact]
    public async Task RemoveBookmarkAsync_OtherUsersBookmark_ThrowsBookmarkNotFoundException()
    {
        var newsItem = await CreateNewsItem();
        await _sut.AddBookmarkAsync(userId: 2, newsItemId: newsItem.Id);

        // userId=1 は userId=2 のブックマークを解除できない
        await Assert.ThrowsAsync<BookmarkNotFoundException>(
            () => _sut.RemoveBookmarkAsync(userId: 1, newsItemId: newsItem.Id));
    }

    // ── GetBookmarksAsync: 正常系 ────────────────────────────────────────────

    [Fact]
    public async Task GetBookmarksAsync_ReturnsOnlyCurrentUsersBookmarks()
    {
        var news1 = await CreateNewsItem("ニュース1");
        var news2 = await CreateNewsItem("ニュース2");
        await _sut.AddBookmarkAsync(userId: 1, newsItemId: news1.Id);
        await _sut.AddBookmarkAsync(userId: 2, newsItemId: news2.Id);

        var result = await _sut.GetBookmarksAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(news1.Id, result.Items[0].NewsItemId);
    }

    [Fact]
    public async Task GetBookmarksAsync_ContainsNewsItemDetails()
    {
        var newsItem = await CreateNewsItem("テストニュース");
        await _sut.AddBookmarkAsync(userId: 1, newsItemId: newsItem.Id);

        var result = await _sut.GetBookmarksAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(newsItem.Id, item.NewsItemId);
        Assert.Equal("テストニュース", item.Title);
    }

    [Fact]
    public async Task GetBookmarksAsync_OrderedByCreatedAtDesc()
    {
        var news1 = await CreateNewsItem("古いニュース");
        var news2 = await CreateNewsItem("新しいニュース");

        // 意図的に time を離して追加
        await AddBookmark(userId: 1, newsItemId: news1.Id, createdAt: DateTimeOffset.UtcNow.AddHours(-2));
        await AddBookmark(userId: 1, newsItemId: news2.Id, createdAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.GetBookmarksAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("新しいニュース", result.Items[0].Title);
        Assert.Equal("古いニュース", result.Items[1].Title);
    }

    [Fact]
    public async Task GetBookmarksAsync_EmptyWhenNoBookmarks()
    {
        var result = await _sut.GetBookmarksAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetBookmarksAsync_ReturnsPaginationMetadata()
    {
        for (var i = 0; i < 5; i++)
        {
            var news = await CreateNewsItem($"ニュース{i}");
            await _sut.AddBookmarkAsync(userId: 1, newsItemId: news.Id);
        }

        var result = await _sut.GetBookmarksAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    // ── GetBookmarksAsync: 境界値 ────────────────────────────────────────────

    [Fact]
    public async Task GetBookmarksAsync_Pagination_SecondPageHasRemainingItems()
    {
        for (var i = 0; i < 3; i++)
        {
            var news = await CreateNewsItem($"ニュース{i}");
            await _sut.AddBookmarkAsync(userId: 1, newsItemId: news.Id);
        }

        var page2 = await _sut.GetBookmarksAsync(userId: 1, page: 2, pageSize: 2);

        Assert.Single(page2.Items);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private async Task<NewsItem> CreateNewsItem(string title = "テストニュース")
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Email = $"test-{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var oshi = new Oshi
        {
            UserId = user.Id,
            Name = "推しA",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Oshis.Add(oshi);
        await _db.SaveChangesAsync();

        var item = new NewsItem
        {
            OshiId = oshi.Id,
            Title = title,
            SourceUrl = $"https://example.com/news/{Guid.NewGuid()}",
            CollectedAt = now,
            CreatedAt = now,
        };
        _db.NewsItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task AddBookmark(int userId, int newsItemId, DateTimeOffset? createdAt = null)
    {
        var bookmark = new Bookmark
        {
            UserId = userId,
            NewsItemId = newsItemId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };
        _db.Bookmarks.Add(bookmark);
        await _db.SaveChangesAsync();
    }
}
