using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class FeedServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FeedService _sut;

    public FeedServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new FeedService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── GetNewsAsync: 正常系 ────────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_ReturnsOnlyUsersOshisNews()
    {
        var user1Oshi = await CreateOshi(userId: 1, name: "推しA");
        var user2Oshi = await CreateOshi(userId: 2, name: "推しB");
        await CreateNewsItem(user1Oshi, "ユーザー1のニュース");
        await CreateNewsItem(user2Oshi, "ユーザー2のニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("ユーザー1のニュース", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_ReturnsOrderedByCollectedAtDesc()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "古いニュース", collectedAt: now.AddHours(-2));
        await CreateNewsItem(oshi, "新しいニュース", collectedAt: now.AddHours(-1));
        await CreateNewsItem(oshi, "最新ニュース", collectedAt: now);

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("最新ニュース", result.Items[0].Title);
        Assert.Equal("古いニュース", result.Items[2].Title);
    }

    [Fact]
    public async Task GetNewsAsync_IncludesOshiName()
    {
        var oshi = await CreateOshi(userId: 1, name: "テスト推し");
        await CreateNewsItem(oshi, "テストニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("テスト推し", result.Items[0].OshiName);
    }

    [Fact]
    public async Task GetNewsAsync_ReturnsPaginationMetadata()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await CreateNewsItem(oshi, $"ニュース{i}", collectedAt: now.AddMinutes(-i));
        }

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    // ── GetNewsAsync: 境界値 ────────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_NoNews_ReturnsEmptyResult()
    {
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_Pagination_FirstPageHas20Items()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await CreateNewsItem(oshi, $"ニュース{i}", collectedAt: now.AddMinutes(-i));
        }

        var page1 = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(2, page1.TotalPages);
    }

    [Fact]
    public async Task GetNewsAsync_Pagination_SecondPageHasRemainingItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await CreateNewsItem(oshi, $"ニュース{i}", collectedAt: now.AddMinutes(-i));
        }

        var page2 = await _sut.GetNewsAsync(userId: 1, page: 2, pageSize: 20);

        Assert.Equal(5, page2.Items.Count);
    }

    [Fact]
    public async Task GetNewsAsync_MultipleOshis_ReturnsAllUsersNews()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateNewsItem(oshi1, "推しAのニュース");
        await CreateNewsItem(oshi2, "推しBのニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(2, result.TotalCount);
    }

    // ── GetProgramsAsync: 正常系 ────────────────────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_ReturnsOnlyUsersOshisPrograms()
    {
        var user1Oshi = await CreateOshi(userId: 1, name: "推しA");
        var user2Oshi = await CreateOshi(userId: 2, name: "推しB");
        await CreateProgramAppearance(user1Oshi, "ユーザー1の番組");
        await CreateProgramAppearance(user2Oshi, "ユーザー2の番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("ユーザー1の番組", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_ReturnsOrderedByCollectedAtDesc()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateProgramAppearance(oshi, "古い番組", collectedAt: now.AddHours(-2));
        await CreateProgramAppearance(oshi, "新しい番組", collectedAt: now.AddHours(-1));
        await CreateProgramAppearance(oshi, "最新の番組", collectedAt: now);

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("最新の番組", result.Items[0].ProgramName);
        Assert.Equal("古い番組", result.Items[2].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_IncludesOshiName()
    {
        var oshi = await CreateOshi(userId: 1, name: "テスト推し");
        await CreateProgramAppearance(oshi, "テスト番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("テスト推し", result.Items[0].OshiName);
    }

    // ── GetProgramsAsync: 境界値 ────────────────────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_NoPrograms_ReturnsEmptyResult()
    {
        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetProgramsAsync_Pagination_FirstPageHas20Items()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await CreateProgramAppearance(oshi, $"番組{i}", collectedAt: now.AddMinutes(-i));
        }

        var page1 = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(2, page1.TotalPages);
    }

    [Fact]
    public async Task GetProgramsAsync_Pagination_SecondPageHasRemainingItems()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            await CreateProgramAppearance(oshi, $"番組{i}", collectedAt: now.AddMinutes(-i));
        }

        var page2 = await _sut.GetProgramsAsync(userId: 1, page: 2, pageSize: 20);

        Assert.Equal(5, page2.Items.Count);
    }

    // ── GetNewsAsync: oshiId フィルタ ────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_WithOshiId_ReturnsOnlyThatOshisNews()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateNewsItem(oshi1, "推しAのニュース");
        await CreateNewsItem(oshi2, "推しBのニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: oshi1.Id);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("推しAのニュース", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithOshiIdNotBelongingToUser_ReturnsEmpty()
    {
        var otherOshi = await CreateOshi(userId: 99, name: "他ユーザーの推し");
        await CreateNewsItem(otherOshi, "他ユーザーのニュース");

        // userId=1 のユーザーが userId=99 の oshiId でフィルタしても空になる
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: otherOshi.Id);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_WithNullOshiId_ReturnsAllUsersNews()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateNewsItem(oshi1, "推しAのニュース");
        await CreateNewsItem(oshi2, "推しBのニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: null);

        Assert.Equal(2, result.TotalCount);
    }

    // ── GetProgramsAsync: oshiId フィルタ ───────────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_WithOshiId_ReturnsOnlyThatOshisPrograms()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateProgramAppearance(oshi1, "推しAの番組");
        await CreateProgramAppearance(oshi2, "推しBの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, oshiId: oshi2.Id);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("推しBの番組", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_WithOshiIdNotBelongingToUser_ReturnsEmpty()
    {
        var otherOshi = await CreateOshi(userId: 99, name: "他ユーザーの推し");
        await CreateProgramAppearance(otherOshi, "他ユーザーの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, oshiId: otherOshi.Id);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetProgramsAsync_WithNullOshiId_ReturnsAllUsersPrograms()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        await CreateProgramAppearance(oshi1, "推しAの番組");
        await CreateProgramAppearance(oshi2, "推しBの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, oshiId: null);

        Assert.Equal(2, result.TotalCount);
    }

    // ── 異常系 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_UserWithNoOshis_ReturnsEmptyResult()
    {
        // 他ユーザーが推しを持っていても、対象ユーザーには返さない
        var otherOshi = await CreateOshi(userId: 99, name: "他ユーザーの推し");
        await CreateNewsItem(otherOshi, "他ユーザーのニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetProgramsAsync_UserWithNoOshis_ReturnsEmptyResult()
    {
        var otherOshi = await CreateOshi(userId: 99, name: "他ユーザーの推し");
        await CreateProgramAppearance(otherOshi, "他ユーザーの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private async Task<Oshi> CreateOshi(int userId, string name)
    {
        var now = DateTime.UtcNow;
        var oshi = new Oshi
        {
            UserId = userId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Oshis.Add(oshi);
        await _db.SaveChangesAsync();
        return oshi;
    }

    private async Task<NewsItem> CreateNewsItem(
        Oshi oshi,
        string title,
        DateTimeOffset? collectedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new NewsItem
        {
            OshiId = oshi.Id,
            Title = title,
            SourceUrl = $"https://example.com/news/{Guid.NewGuid()}",
            CollectedAt = collectedAt ?? now,
            CreatedAt = now,
        };
        _db.NewsItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<ProgramAppearance> CreateProgramAppearance(
        Oshi oshi,
        string programName,
        DateTimeOffset? collectedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new ProgramAppearance
        {
            OshiId = oshi.Id,
            ProgramName = programName,
            SourceUrl = $"https://example.com/programs/{Guid.NewGuid()}",
            CollectedAt = collectedAt ?? now,
            CreatedAt = now,
        };
        _db.ProgramAppearances.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }
}
