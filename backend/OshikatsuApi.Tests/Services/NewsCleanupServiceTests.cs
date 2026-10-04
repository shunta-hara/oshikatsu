using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>NewsCleanupService のテスト</summary>
public class NewsCleanupServiceTests
{
    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User CreateUser(AppDbContext db)
    {
        var user = new User
        {
            Email = $"user_{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static Oshi CreateOshi(AppDbContext db, User user)
    {
        var oshi = new Oshi
        {
            UserId = user.Id,
            Name = "テスト推し",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Oshis.Add(oshi);
        db.SaveChanges();
        return oshi;
    }

    private static NewsItem AddNewsItem(
        AppDbContext db,
        Oshi oshi,
        DateTimeOffset? publishedAt,
        DateTimeOffset? collectedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new NewsItem
        {
            OshiId = oshi.Id,
            Title = $"記事_{Guid.NewGuid()}",
            SourceUrl = $"https://example.com/{Guid.NewGuid()}",
            PublishedAt = publishedAt,
            CollectedAt = collectedAt ?? now,
            CreatedAt = now,
        };
        db.NewsItems.Add(item);
        db.SaveChanges();
        return item;
    }

    private static Bookmark AddBookmark(AppDbContext db, User user, NewsItem item)
    {
        var bookmark = new Bookmark
        {
            UserId = user.Id,
            NewsItemId = item.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Bookmarks.Add(bookmark);
        db.SaveChanges();
        return bookmark;
    }

    // ── 正常系 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteExpiredNewsAsync_OldPublishedAt_DeletesItem()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        var old = AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-91));
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync();

        Assert.Equal(1, deleted);
        Assert.False(await db.NewsItems.AnyAsync(n => n.Id == old.Id));
    }

    [Fact]
    public async Task DeleteExpiredNewsAsync_NullPublishedAtOldCollectedAt_DeletesItem()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        var old = AddNewsItem(
            db,
            oshi,
            publishedAt: null,
            collectedAt: DateTimeOffset.UtcNow.AddDays(-91));
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync();

        Assert.Equal(1, deleted);
        Assert.False(await db.NewsItems.AnyAsync(n => n.Id == old.Id));
    }

    [Fact]
    public async Task DeleteExpiredNewsAsync_NewItem_NotDeleted()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        var fresh = AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-1));
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync();

        Assert.Equal(0, deleted);
        Assert.True(await db.NewsItems.AnyAsync(n => n.Id == fresh.Id));
    }

    [Fact]
    public async Task DeleteExpiredNewsAsync_BookmarkedOldItem_NotDeleted()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        var old = AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-91));
        AddBookmark(db, user, old);
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync();

        Assert.Equal(0, deleted);
        Assert.True(await db.NewsItems.AnyAsync(n => n.Id == old.Id));
    }

    // ── 境界値 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteExpiredNewsAsync_ExactlyAtCutoff_Deleted()
    {
        // cutoff = now - 90 days, 90 日前は削除対象
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        var atCutoff = AddNewsItem(
            db,
            oshi,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-90));
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync();

        Assert.Equal(1, deleted);
    }

    [Fact]
    public async Task DeleteExpiredNewsAsync_89DaysOld_NotDeleted()
    {
        // 89日前は保持対象
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-89));
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync();

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteExpiredNewsAsync_BatchSize1_DeletesOnlyOne()
    {
        // batchSize=1 のとき1件しか削除しない
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-91));
        AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-92));
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync(batchSize: 1);

        Assert.Equal(1, deleted);
    }

    [Fact]
    public async Task DeleteExpiredNewsAsync_NoExpiredItems_Returns0()
    {
        using var db = CreateDb();
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync();

        Assert.Equal(0, deleted);
    }

    // ── ジョブログ記録 ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteExpiredNewsAsync_RecordsCleanupJobLog()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-91));
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        await sut.DeleteExpiredNewsAsync();

        var log = await db.CollectorJobLogs.FirstOrDefaultAsync(l => l.JobType == "cleanup");
        Assert.NotNull(log);
        Assert.Equal(1, log.SuccessCount);
        Assert.Equal(0, log.FailureCount);
    }

    [Fact]
    public async Task DeleteExpiredNewsAsync_NoExpiredItems_RecordsCleanupJobLogWith0()
    {
        using var db = CreateDb();
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        await sut.DeleteExpiredNewsAsync();

        var log = await db.CollectorJobLogs.FirstOrDefaultAsync(l => l.JobType == "cleanup");
        Assert.NotNull(log);
        Assert.Equal(0, log.SuccessCount);
    }

    // ── ブックマーク混在 ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteExpiredNewsAsync_MixedBookmarkedAndNot_DeletesOnlyUnbookmarked()
    {
        using var db = CreateDb();
        var user = CreateUser(db);
        var oshi = CreateOshi(db, user);

        var bookmarked = AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-91));
        var notBookmarked = AddNewsItem(db, oshi, publishedAt: DateTimeOffset.UtcNow.AddDays(-91));
        AddBookmark(db, user, bookmarked);
        var sut = new NewsCleanupService(db, NullLogger<NewsCleanupService>.Instance);

        var deleted = await sut.DeleteExpiredNewsAsync();

        Assert.Equal(1, deleted);
        Assert.True(await db.NewsItems.AnyAsync(n => n.Id == bookmarked.Id));
        Assert.False(await db.NewsItems.AnyAsync(n => n.Id == notBookmarked.Id));
    }
}
