using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class DashboardServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new DashboardService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── 正常系 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_ReturnsOneCardPerOshi()
    {
        await CreateOshi(userId: 1, name: "推しA");
        await CreateOshi(userId: 1, name: "推しB");

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_CardContainsOshiIdAndName()
    {
        var oshi = await CreateOshi(userId: 1, name: "テスト推し");

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.Single(result);
        Assert.Equal(oshi.Id, result[0].OshiId);
        Assert.Equal("テスト推し", result[0].OshiName);
    }

    [Fact]
    public async Task GetSummaryAsync_CardsOrderedByOshiCreatedAt()
    {
        var now = DateTime.UtcNow;
        var oshi1 = await CreateOshi(userId: 1, name: "推しA", createdAt: now.AddMinutes(-2));
        var oshi2 = await CreateOshi(userId: 1, name: "推しB", createdAt: now.AddMinutes(-1));
        var oshi3 = await CreateOshi(userId: 1, name: "推しC", createdAt: now);

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.Equal(oshi1.Id, result[0].OshiId);
        Assert.Equal(oshi2.Id, result[1].OshiId);
        Assert.Equal(oshi3.Id, result[2].OshiId);
    }

    // FEAT-029: 最新ニュースは PublishedAt が最も新しいものを返す
    [Fact]
    public async Task GetSummaryAsync_LatestNewsIsTheMostRecentByPublishedAt()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "古いニュース", publishedAt: now.AddHours(-2));
        await CreateNewsItem(oshi, "最新ニュース", publishedAt: now);
        await CreateNewsItem(oshi, "中間ニュース", publishedAt: now.AddHours(-1));

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.NotNull(result[0].LatestNews);
        Assert.Equal("最新ニュース", result[0].LatestNews!.Title);
    }

    [Fact]
    public async Task GetSummaryAsync_LatestNewsContainsSourceUrlAndCollectedAt()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi, "ニュース1", sourceUrl: "https://example.com/news/1", collectedAt: now);

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.NotNull(result[0].LatestNews);
        Assert.Equal("https://example.com/news/1", result[0].LatestNews!.SourceUrl);
        Assert.Equal(now.ToUnixTimeSeconds(), result[0].LatestNews!.CollectedAt.ToUnixTimeSeconds());
    }

    // FEAT-029: 最新出演情報は AiredAt が最も新しいものを返す
    [Fact]
    public async Task GetSummaryAsync_LatestProgramIsTheMostRecentByAiredAt()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateProgramAppearance(oshi, "古い番組", airedAt: now.AddHours(-2));
        await CreateProgramAppearance(oshi, "最新番組", airedAt: now);
        await CreateProgramAppearance(oshi, "中間番組", airedAt: now.AddHours(-1));

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.NotNull(result[0].LatestProgram);
        Assert.Equal("最新番組", result[0].LatestProgram!.ProgramName);
    }

    [Fact]
    public async Task GetSummaryAsync_LatestProgramContainsBroadcasterAndSourceUrl()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateProgramAppearance(oshi, "テスト番組", broadcaster: "テストTV", sourceUrl: "https://example.com/prog/1");

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.NotNull(result[0].LatestProgram);
        Assert.Equal("テストTV", result[0].LatestProgram!.Broadcaster);
        Assert.Equal("https://example.com/prog/1", result[0].LatestProgram!.SourceUrl);
    }

    // ── 境界値 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_NoOshis_ReturnsEmptyList()
    {
        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSummaryAsync_OshiWithNoNews_LatestNewsIsNull()
    {
        await CreateOshi(userId: 1, name: "推しA");

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.Null(result[0].LatestNews);
    }

    [Fact]
    public async Task GetSummaryAsync_OshiWithNoProgram_LatestProgramIsNull()
    {
        await CreateOshi(userId: 1, name: "推しA");

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.Null(result[0].LatestProgram);
    }

    [Fact]
    public async Task GetSummaryAsync_MultipleOshis_EachCardHasOwnLatestNews()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "推しA");
        var oshi2 = await CreateOshi(userId: 1, name: "推しB");
        var now = DateTimeOffset.UtcNow;
        await CreateNewsItem(oshi1, "推しAのニュース", collectedAt: now);
        await CreateNewsItem(oshi2, "推しBのニュース", collectedAt: now.AddHours(-1));

        var result = await _sut.GetSummaryAsync(userId: 1);

        var cardA = result.First(c => c.OshiId == oshi1.Id);
        var cardB = result.First(c => c.OshiId == oshi2.Id);

        Assert.Equal("推しAのニュース", cardA.LatestNews!.Title);
        Assert.Equal("推しBのニュース", cardB.LatestNews!.Title);
    }

    [Fact]
    public async Task GetSummaryAsync_OshiWithNewsButNoProgram_CorrectNulls()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "ニュースあり");

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.NotNull(result[0].LatestNews);
        Assert.Null(result[0].LatestProgram);
    }

    // ── 異常系（他ユーザーの隔離） ───────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_ReturnsOnlyCurrentUsersOshis()
    {
        await CreateOshi(userId: 1, name: "ユーザー1の推し");
        await CreateOshi(userId: 2, name: "ユーザー2の推し");

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.Single(result);
        Assert.Equal("ユーザー1の推し", result[0].OshiName);
    }

    [Fact]
    public async Task GetSummaryAsync_OtherUsersNewsIsNotIncluded()
    {
        var oshi1 = await CreateOshi(userId: 1, name: "ユーザー1の推し");
        var oshi2 = await CreateOshi(userId: 2, name: "ユーザー2の推し");
        await CreateNewsItem(oshi2, "ユーザー2のニュース");

        var result = await _sut.GetSummaryAsync(userId: 1);

        // ユーザー1のカードのニュースは null（ユーザー2のニュースは混入しない）
        Assert.Single(result);
        Assert.Null(result[0].LatestNews);
    }

    // ── 画像箱フィルタ（FEAT-030）────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_GalleryNewsExcluded_LatestNewsIsNonGallery()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        // 画像箱ニュース（最新）と通常ニュース
        await CreateNewsItem(oshi, "(画像1/5) フォト", publishedAt: now);
        await CreateNewsItem(oshi, "通常ニュース", publishedAt: now.AddHours(-1));

        var result = await _sut.GetSummaryAsync(userId: 1);

        // 画像箱は除外されて通常ニュースが選ばれる
        Assert.NotNull(result[0].LatestNews);
        Assert.Equal("通常ニュース", result[0].LatestNews!.Title);
    }

    [Fact]
    public async Task GetSummaryAsync_OnlyGalleryNews_LatestNewsIsNull()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        await CreateNewsItem(oshi, "写真：推しA 笑顔で登場");
        await CreateNewsItem(oshi, "(画像2/8) フォトギャラリー");

        var result = await _sut.GetSummaryAsync(userId: 1);

        // 画像箱しかない場合は null
        Assert.Null(result[0].LatestNews);
    }

    [Fact]
    public async Task GetSummaryAsync_GalleryProgram_LatestProgramIsNonGallery()
    {
        var oshi = await CreateOshi(userId: 1, name: "推しA");
        var now = DateTimeOffset.UtcNow;
        await CreateProgramAppearance(oshi, "推しAの写真一覧", airedAt: now);
        await CreateProgramAppearance(oshi, "出演番組A", airedAt: now.AddHours(-1));

        var result = await _sut.GetSummaryAsync(userId: 1);

        Assert.NotNull(result[0].LatestProgram);
        Assert.Equal("出演番組A", result[0].LatestProgram!.ProgramName);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private async Task<Oshi> CreateOshi(int userId, string name, DateTime? createdAt = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
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
        string? sourceUrl = null,
        DateTimeOffset? collectedAt = null,
        DateTimeOffset? publishedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new NewsItem
        {
            OshiId = oshi.Id,
            Title = title,
            SourceUrl = sourceUrl ?? $"https://example.com/news/{Guid.NewGuid()}",
            CollectedAt = collectedAt ?? now,
            PublishedAt = publishedAt ?? collectedAt ?? now,
            CreatedAt = now,
        };
        _db.NewsItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<ProgramAppearance> CreateProgramAppearance(
        Oshi oshi,
        string programName,
        string? broadcaster = null,
        string? sourceUrl = null,
        DateTimeOffset? collectedAt = null,
        DateTimeOffset? airedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new ProgramAppearance
        {
            OshiId = oshi.Id,
            ProgramName = programName,
            Broadcaster = broadcaster,
            SourceUrl = sourceUrl ?? $"https://example.com/programs/{Guid.NewGuid()}",
            CollectedAt = collectedAt ?? now,
            AiredAt = airedAt ?? collectedAt ?? now,
            CreatedAt = now,
        };
        _db.ProgramAppearances.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }
}
