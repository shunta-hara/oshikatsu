using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class InternalCollectorServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly InternalCollectorService _sut;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public InternalCollectorServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new InternalCollectorService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── CreateNewsItemAsync: 正常系 ──────────────────────────────────────────

    [Fact]
    public async Task CreateNewsItemAsync_ValidRequest_SavesNewsItem()
    {
        var dto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/1");

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("https://example.com/news/1", saved!.SourceUrl);
        Assert.Equal(1, saved.OshiId);
    }

    [Fact]
    public async Task CreateNewsItemAsync_ValidRequest_ReturnsDto()
    {
        var dto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/2");

        var result = await _sut.CreateNewsItemAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("テストニュース", result.Title);
        Assert.Equal("https://example.com/news/2", result.SourceUrl);
        Assert.False(result.WasDuplicate);
    }

    [Fact]
    public async Task CreateNewsItemAsync_WithPublishedAt_StoresActualDateNotEstimated()
    {
        var publishedAt = _now.AddHours(-2);
        var dto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/3",
            publishedAt: publishedAt);

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.Equal(publishedAt, saved.PublishedAt);
        Assert.False(saved.IsPublishedAtEstimated);
    }

    // ── CreateNewsItemAsync: 公開日時推定 ────────────────────────────────────

    [Fact]
    public async Task CreateNewsItemAsync_NullPublishedAt_UsesCollectedAtAndSetsEstimatedFlag()
    {
        var collectedAt = _now;
        var dto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/4",
            publishedAt: null, collectedAt: collectedAt);

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.Equal(collectedAt, saved.PublishedAt);
        Assert.True(saved.IsPublishedAtEstimated);
    }

    // ── CreateNewsItemAsync: 重複排除 ────────────────────────────────────────

    [Fact]
    public async Task CreateNewsItemAsync_DuplicateSourceUrl_DoesNotInsertNewRow()
    {
        var dto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/dup");

        await _sut.CreateNewsItemAsync(dto);
        await _sut.CreateNewsItemAsync(dto);

        var count = await _db.NewsItems.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateNewsItemAsync_DuplicateSourceUrl_ReturnsDuplicateFlag()
    {
        var dto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/dup2");

        await _sut.CreateNewsItemAsync(dto);
        var result = await _sut.CreateNewsItemAsync(dto);

        Assert.True(result.WasDuplicate);
    }

    [Fact]
    public async Task CreateNewsItemAsync_SameUrlDifferentOshi_InsertsBothRows()
    {
        var url = "https://example.com/news/shared";
        var dto1 = BuildNewsDto(oshiId: 1, sourceUrl: url);
        var dto2 = BuildNewsDto(oshiId: 2, sourceUrl: url);

        await _sut.CreateNewsItemAsync(dto1);
        await _sut.CreateNewsItemAsync(dto2);

        var count = await _db.NewsItems.CountAsync();
        Assert.Equal(2, count);
    }

    // ── CreateProgramAppearanceAsync: 正常系 ─────────────────────────────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_ValidRequest_SavesProgram()
    {
        var dto = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/1");

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("テスト番組", saved!.ProgramName);
        Assert.Equal("https://tv.example.com/prog/1", saved.SourceUrl);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_ValidRequest_ReturnsDto()
    {
        var dto = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/2");

        var result = await _sut.CreateProgramAppearanceAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("テスト番組", result.ProgramName);
        Assert.False(result.WasDuplicate);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_WithAiredAt_StoresActualDateNotEstimated()
    {
        var airedAt = _now.AddHours(-3);
        var dto = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/3",
            airedAt: airedAt);

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(airedAt, saved.AiredAt);
        Assert.False(saved.IsAiredAtEstimated);
    }

    // ── CreateProgramAppearanceAsync: 放送日時推定 ───────────────────────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_NullAiredAt_UsesCollectedAtAndSetsEstimatedFlag()
    {
        var collectedAt = _now;
        var dto = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/4",
            airedAt: null, collectedAt: collectedAt);

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(collectedAt, saved.AiredAt);
        Assert.True(saved.IsAiredAtEstimated);
    }

    // ── CreateProgramAppearanceAsync: 重複排除 ───────────────────────────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_DuplicateSourceUrl_DoesNotInsertNewRow()
    {
        var dto = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/dup");

        await _sut.CreateProgramAppearanceAsync(dto);
        await _sut.CreateProgramAppearanceAsync(dto);

        var count = await _db.ProgramAppearances.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_DuplicateSourceUrl_ReturnsDuplicateFlag()
    {
        var dto = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/dup2");

        await _sut.CreateProgramAppearanceAsync(dto);
        var result = await _sut.CreateProgramAppearanceAsync(dto);

        Assert.True(result.WasDuplicate);
    }

    // ── GetAllOshisAsync: 正常系 ─────────────────────────────────────────────

    [Fact]
    public async Task GetAllOshisAsync_WithMultipleOshis_ReturnsAll()
    {
        var now = DateTime.UtcNow;
        _db.Oshis.AddRange(
            new OshikatsuApi.Entities.Oshi { UserId = 1, Name = "推しA", CreatedAt = now, UpdatedAt = now },
            new OshikatsuApi.Entities.Oshi { UserId = 2, Name = "推しB", CreatedAt = now, UpdatedAt = now }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllOshisAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllOshisAsync_ReturnsCorrectIdAndName()
    {
        var now = DateTime.UtcNow;
        _db.Oshis.Add(new OshikatsuApi.Entities.Oshi { UserId = 1, Name = "鈴木一郎", CreatedAt = now, UpdatedAt = now });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllOshisAsync();

        Assert.Single(result);
        Assert.Equal("鈴木一郎", result[0].Name);
        Assert.True(result[0].Id > 0);
    }

    [Fact]
    public async Task GetAllOshisAsync_SpansAllUsers_ReturnsOshisFromAllUsers()
    {
        var now = DateTime.UtcNow;
        // ユーザー1、2、3 それぞれに1件ずつ
        _db.Oshis.AddRange(
            new OshikatsuApi.Entities.Oshi { UserId = 1, Name = "推し1", CreatedAt = now, UpdatedAt = now },
            new OshikatsuApi.Entities.Oshi { UserId = 2, Name = "推し2", CreatedAt = now, UpdatedAt = now },
            new OshikatsuApi.Entities.Oshi { UserId = 3, Name = "推し3", CreatedAt = now, UpdatedAt = now }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllOshisAsync();

        Assert.Equal(3, result.Count);
        var names = result.Select(o => o.Name).ToHashSet();
        Assert.Contains("推し1", names);
        Assert.Contains("推し2", names);
        Assert.Contains("推し3", names);
    }

    // ── GetAllOshisAsync: 境界値 ─────────────────────────────────────────────

    [Fact]
    public async Task GetAllOshisAsync_NoOshis_ReturnsEmptyList()
    {
        var result = await _sut.GetAllOshisAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private CreateNewsItemRequestDto BuildNewsDto(
        int oshiId,
        string sourceUrl,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? collectedAt = null) => new()
        {
            OshiId = oshiId,
            Title = "テストニュース",
            Summary = "テスト本文要約",
            SourceUrl = sourceUrl,
            PublishedAt = publishedAt,
            CollectedAt = collectedAt ?? _now,
        };

    private CreateProgramAppearanceRequestDto BuildProgramDto(
        int oshiId,
        string sourceUrl,
        DateTimeOffset? airedAt = null,
        DateTimeOffset? collectedAt = null) => new()
        {
            OshiId = oshiId,
            ProgramName = "テスト番組",
            Broadcaster = "テストTV",
            AiredAt = airedAt,
            SourceUrl = sourceUrl,
            CollectedAt = collectedAt ?? _now,
        };
}
