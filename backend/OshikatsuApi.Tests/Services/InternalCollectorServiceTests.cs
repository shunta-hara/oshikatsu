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

    // ── CreateNewsItemAsync: UTC 正規化（FEAT-017）──────────────────────────

    [Fact]
    public async Task CreateNewsItemAsync_JstPublishedAt_NormalizesToUtc()
    {
        // JST (UTC+09:00) のオフセット付き公開日時を送ると UTC で保存される
        var jstOffset = TimeSpan.FromHours(9);
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset); // 2026-10-05T20:00:00+09:00
        var dto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/jst-pub",
            publishedAt: jstTime, collectedAt: _now);

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        // 保存後は UTC（オフセット0）で格納される
        Assert.Equal(TimeSpan.Zero, saved.PublishedAt!.Value.Offset);
        // UTC 変換後も同じ瞬間を指す
        Assert.Equal(jstTime.UtcDateTime, saved.PublishedAt.Value.UtcDateTime);
    }

    [Fact]
    public async Task CreateNewsItemAsync_JstCollectedAt_NormalizesToUtc()
    {
        // CollectedAt に JST オフセットが来ても UTC で保存される
        var jstOffset = TimeSpan.FromHours(9);
        var jstCollectedAt = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset);
        var dto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/jst-col",
            collectedAt: jstCollectedAt);

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.Equal(TimeSpan.Zero, saved.CollectedAt.Offset);
        Assert.Equal(jstCollectedAt.UtcDateTime, saved.CollectedAt.UtcDateTime);
    }

    [Fact]
    public async Task CreateNewsItemAsync_UtcAndJstEquivalent_StoresSameUtcInstant()
    {
        // UTC と JST の同じ瞬間を表す DateTimeOffset は同一の UTC 値として保存される
        var utcTime = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero);       // UTC
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.FromHours(9)); // JST (同じ瞬間)

        var dtoUtc = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news/utc", publishedAt: utcTime);
        var dtoJst = BuildNewsDto(oshiId: 2, sourceUrl: "https://example.com/news/jst", publishedAt: jstTime);

        await _sut.CreateNewsItemAsync(dtoUtc);
        await _sut.CreateNewsItemAsync(dtoJst);

        var items = await _db.NewsItems.OrderBy(n => n.OshiId).ToListAsync();
        Assert.Equal(2, items.Count);
        // 両方とも同じ UTC 瞬間で保存されている
        Assert.Equal(items[0].PublishedAt!.Value.UtcDateTime, items[1].PublishedAt!.Value.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, items[0].PublishedAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, items[1].PublishedAt!.Value.Offset);
    }

    // ── CreateProgramAppearanceAsync: UTC 正規化（FEAT-017）──────────────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_JstAiredAt_NormalizesToUtc()
    {
        // JST (UTC+09:00) のオフセット付き放送日時を送ると UTC で保存される
        var jstOffset = TimeSpan.FromHours(9);
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset); // 2026-10-05T20:00:00+09:00
        var dto = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/jst-aired",
            airedAt: jstTime, collectedAt: _now);

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(TimeSpan.Zero, saved.AiredAt!.Value.Offset);
        Assert.Equal(jstTime.UtcDateTime, saved.AiredAt.Value.UtcDateTime);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_JstCollectedAt_NormalizesToUtc()
    {
        var jstOffset = TimeSpan.FromHours(9);
        var jstCollectedAt = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset);
        var dto = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/jst-col",
            collectedAt: jstCollectedAt);

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(TimeSpan.Zero, saved.CollectedAt.Offset);
        Assert.Equal(jstCollectedAt.UtcDateTime, saved.CollectedAt.UtcDateTime);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_UtcAndJstEquivalent_StoresSameUtcInstant()
    {
        var utcTime = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero);
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.FromHours(9));

        var dtoUtc = BuildProgramDto(oshiId: 1, sourceUrl: "https://tv.example.com/prog/utc", airedAt: utcTime);
        var dtoJst = BuildProgramDto(oshiId: 2, sourceUrl: "https://tv.example.com/prog/jst", airedAt: jstTime);

        await _sut.CreateProgramAppearanceAsync(dtoUtc);
        await _sut.CreateProgramAppearanceAsync(dtoJst);

        var items = await _db.ProgramAppearances.OrderBy(p => p.OshiId).ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.Equal(items[0].AiredAt!.Value.UtcDateTime, items[1].AiredAt!.Value.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, items[0].AiredAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, items[1].AiredAt!.Value.Offset);
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

    // ── CreateProgramAppearanceAsync: FEAT-024 ニュース URL との重複 ──────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_UrlAlreadyInNewsForSameOshi_ReturnsWasDuplicateTrue()
    {
        // 事前に同 URL のニュースを保存
        var newsDto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/both");
        await _sut.CreateNewsItemAsync(newsDto);

        // 同 URL で番組を登録しようとする
        var progDto = BuildProgramDto(oshiId: 1, sourceUrl: "https://example.com/both");
        var result = await _sut.CreateProgramAppearanceAsync(progDto);

        Assert.True(result.WasDuplicate);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_UrlAlreadyInNewsForSameOshi_DoesNotSaveProgram()
    {
        var newsDto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/news-dup");
        await _sut.CreateNewsItemAsync(newsDto);

        var progDto = BuildProgramDto(oshiId: 1, sourceUrl: "https://example.com/news-dup");
        await _sut.CreateProgramAppearanceAsync(progDto);

        var count = await _db.ProgramAppearances.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_UrlInNewsOfDifferentOshi_SavesProgram()
    {
        // 別の推しのニュースと URL が同じでも保存される
        var newsDto = BuildNewsDto(oshiId: 1, sourceUrl: "https://example.com/cross");
        await _sut.CreateNewsItemAsync(newsDto);

        // oshi=2 の番組（oshi=1 のニュースとは別推し）
        var progDto = BuildProgramDto(oshiId: 2, sourceUrl: "https://example.com/cross");
        var result = await _sut.CreateProgramAppearanceAsync(progDto);

        Assert.False(result.WasDuplicate);
        Assert.Equal(1, await _db.ProgramAppearances.CountAsync());
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
