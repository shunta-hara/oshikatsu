using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;
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
        await AddTalentAsync(talentId: 1);
        var dto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/1");

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("https://example.com/news/1", saved!.SourceUrl);
        Assert.Equal(1, saved.TalentId);
    }

    [Fact]
    public async Task CreateNewsItemAsync_ValidRequest_ReturnsDto()
    {
        await AddTalentAsync(talentId: 1);
        var dto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/2");

        var result = await _sut.CreateNewsItemAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("テストニュース", result.Title);
        Assert.Equal("https://example.com/news/2", result.SourceUrl);
        Assert.False(result.WasDuplicate);
    }

    [Fact]
    public async Task CreateNewsItemAsync_WithPublishedAt_StoresActualDateNotEstimated()
    {
        await AddTalentAsync(talentId: 1);
        var publishedAt = _now.AddHours(-2);
        var dto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/3",
            publishedAt: publishedAt);

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.Equal(publishedAt.ToUniversalTime(), saved.PublishedAt);
        Assert.False(saved.IsPublishedAtEstimated);
    }

    // ── CreateNewsItemAsync: 異常系（talentId 未存在） ────────────────────────

    [Fact]
    public async Task CreateNewsItemAsync_TalentNotFound_ThrowsTalentNotFoundException()
    {
        var dto = BuildNewsDto(talentId: 999, sourceUrl: "https://example.com/news/notalent");

        await Assert.ThrowsAsync<TalentNotFoundException>(() => _sut.CreateNewsItemAsync(dto));
    }

    // ── CreateNewsItemAsync: 公開日時推定 ────────────────────────────────────

    [Fact]
    public async Task CreateNewsItemAsync_NullPublishedAt_UsesCollectedAtAndSetsEstimatedFlag()
    {
        await AddTalentAsync(talentId: 1);
        var collectedAt = _now;
        var dto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/4",
            publishedAt: null, collectedAt: collectedAt);

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.Equal(collectedAt.ToUniversalTime(), saved.PublishedAt);
        Assert.True(saved.IsPublishedAtEstimated);
    }

    // ── CreateNewsItemAsync: 重複排除 ────────────────────────────────────────

    [Fact]
    public async Task CreateNewsItemAsync_DuplicateSourceUrl_DoesNotInsertNewRow()
    {
        await AddTalentAsync(talentId: 1);
        var dto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/dup");

        await _sut.CreateNewsItemAsync(dto);
        await _sut.CreateNewsItemAsync(dto);

        var count = await _db.NewsItems.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateNewsItemAsync_DuplicateSourceUrl_ReturnsDuplicateFlag()
    {
        await AddTalentAsync(talentId: 1);
        var dto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/dup2");

        await _sut.CreateNewsItemAsync(dto);
        var result = await _sut.CreateNewsItemAsync(dto);

        Assert.True(result.WasDuplicate);
    }

    [Fact]
    public async Task CreateNewsItemAsync_SameUrlDifferentTalent_InsertsBothRows()
    {
        await AddTalentAsync(talentId: 1);
        await AddTalentAsync(talentId: 2);
        var url = "https://example.com/news/shared";
        var dto1 = BuildNewsDto(talentId: 1, sourceUrl: url);
        var dto2 = BuildNewsDto(talentId: 2, sourceUrl: url);

        await _sut.CreateNewsItemAsync(dto1);
        await _sut.CreateNewsItemAsync(dto2);

        var count = await _db.NewsItems.CountAsync();
        Assert.Equal(2, count);
    }

    // ── CreateProgramAppearanceAsync: 正常系 ─────────────────────────────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_ValidRequest_SavesProgram()
    {
        await AddTalentAsync(talentId: 1);
        var dto = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/1");

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("テスト番組", saved!.ProgramName);
        Assert.Equal("https://tv.example.com/prog/1", saved.SourceUrl);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_ValidRequest_ReturnsDto()
    {
        await AddTalentAsync(talentId: 1);
        var dto = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/2");

        var result = await _sut.CreateProgramAppearanceAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("テスト番組", result.ProgramName);
        Assert.False(result.WasDuplicate);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_WithAiredAt_StoresActualDateNotEstimated()
    {
        await AddTalentAsync(talentId: 1);
        var airedAt = _now.AddHours(-3);
        var dto = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/3",
            airedAt: airedAt);

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(airedAt.ToUniversalTime(), saved.AiredAt);
        Assert.False(saved.IsAiredAtEstimated);
    }

    // ── CreateProgramAppearanceAsync: 異常系（talentId 未存在） ──────────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_TalentNotFound_ThrowsTalentNotFoundException()
    {
        var dto = BuildProgramDto(talentId: 999, sourceUrl: "https://tv.example.com/prog/notalent");

        await Assert.ThrowsAsync<TalentNotFoundException>(() => _sut.CreateProgramAppearanceAsync(dto));
    }

    // ── CreateProgramAppearanceAsync: 放送日時推定 ───────────────────────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_NullAiredAt_UsesCollectedAtAndSetsEstimatedFlag()
    {
        await AddTalentAsync(talentId: 1);
        var collectedAt = _now;
        var dto = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/4",
            airedAt: null, collectedAt: collectedAt);

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(collectedAt.ToUniversalTime(), saved.AiredAt);
        Assert.True(saved.IsAiredAtEstimated);
    }

    // ── CreateProgramAppearanceAsync: 重複排除 ───────────────────────────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_DuplicateSourceUrl_DoesNotInsertNewRow()
    {
        await AddTalentAsync(talentId: 1);
        var dto = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/dup");

        await _sut.CreateProgramAppearanceAsync(dto);
        await _sut.CreateProgramAppearanceAsync(dto);

        var count = await _db.ProgramAppearances.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_DuplicateSourceUrl_ReturnsDuplicateFlag()
    {
        await AddTalentAsync(talentId: 1);
        var dto = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/dup2");

        await _sut.CreateProgramAppearanceAsync(dto);
        var result = await _sut.CreateProgramAppearanceAsync(dto);

        Assert.True(result.WasDuplicate);
    }

    // ── CreateNewsItemAsync: UTC 正規化（FEAT-017）──────────────────────────

    [Fact]
    public async Task CreateNewsItemAsync_JstPublishedAt_NormalizesToUtc()
    {
        await AddTalentAsync(talentId: 1);
        // JST (UTC+09:00) のオフセット付き公開日時を送ると UTC で保存される
        var jstOffset = TimeSpan.FromHours(9);
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset); // 2026-10-05T20:00:00+09:00
        var dto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/jst-pub",
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
        await AddTalentAsync(talentId: 1);
        // CollectedAt に JST オフセットが来ても UTC で保存される
        var jstOffset = TimeSpan.FromHours(9);
        var jstCollectedAt = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset);
        var dto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/jst-col",
            collectedAt: jstCollectedAt);

        await _sut.CreateNewsItemAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.Equal(TimeSpan.Zero, saved.CollectedAt.Offset);
        Assert.Equal(jstCollectedAt.UtcDateTime, saved.CollectedAt.UtcDateTime);
    }

    [Fact]
    public async Task CreateNewsItemAsync_UtcAndJstEquivalent_StoresSameUtcInstant()
    {
        await AddTalentAsync(talentId: 1);
        await AddTalentAsync(talentId: 2);
        // UTC と JST の同じ瞬間を表す DateTimeOffset は同一の UTC 値として保存される
        var utcTime = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero);       // UTC
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.FromHours(9)); // JST (同じ瞬間)

        var dtoUtc = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news/utc", publishedAt: utcTime);
        var dtoJst = BuildNewsDto(talentId: 2, sourceUrl: "https://example.com/news/jst", publishedAt: jstTime);

        await _sut.CreateNewsItemAsync(dtoUtc);
        await _sut.CreateNewsItemAsync(dtoJst);

        var items = await _db.NewsItems.OrderBy(n => n.TalentId).ToListAsync();
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
        await AddTalentAsync(talentId: 1);
        // JST (UTC+09:00) のオフセット付き放送日時を送ると UTC で保存される
        var jstOffset = TimeSpan.FromHours(9);
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset); // 2026-10-05T20:00:00+09:00
        var dto = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/jst-aired",
            airedAt: jstTime, collectedAt: _now);

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(TimeSpan.Zero, saved.AiredAt!.Value.Offset);
        Assert.Equal(jstTime.UtcDateTime, saved.AiredAt.Value.UtcDateTime);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_JstCollectedAt_NormalizesToUtc()
    {
        await AddTalentAsync(talentId: 1);
        var jstOffset = TimeSpan.FromHours(9);
        var jstCollectedAt = new DateTimeOffset(2026, 10, 5, 20, 0, 0, jstOffset);
        var dto = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/jst-col",
            collectedAt: jstCollectedAt);

        await _sut.CreateProgramAppearanceAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(TimeSpan.Zero, saved.CollectedAt.Offset);
        Assert.Equal(jstCollectedAt.UtcDateTime, saved.CollectedAt.UtcDateTime);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_UtcAndJstEquivalent_StoresSameUtcInstant()
    {
        await AddTalentAsync(talentId: 1);
        await AddTalentAsync(talentId: 2);
        var utcTime = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero);
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.FromHours(9));

        var dtoUtc = BuildProgramDto(talentId: 1, sourceUrl: "https://tv.example.com/prog/utc", airedAt: utcTime);
        var dtoJst = BuildProgramDto(talentId: 2, sourceUrl: "https://tv.example.com/prog/jst", airedAt: jstTime);

        await _sut.CreateProgramAppearanceAsync(dtoUtc);
        await _sut.CreateProgramAppearanceAsync(dtoJst);

        var items = await _db.ProgramAppearances.OrderBy(p => p.TalentId).ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.Equal(items[0].AiredAt!.Value.UtcDateTime, items[1].AiredAt!.Value.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, items[0].AiredAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, items[1].AiredAt!.Value.Offset);
    }

    // ── GetAllOshisAsync: 正常系 ─────────────────────────────────────────────

    [Fact]
    public async Task GetAllOshisAsync_WithMultipleTalents_ReturnsRegisteredOnes()
    {
        // talentId=1 はユーザー登録あり、talentId=2 は登録なし
        await AddTalentWithUserOshiAsync(displayName: "推しA", userId: 1);
        await AddTalentWithUserOshiAsync(displayName: "推しB", userId: 2);

        var result = await _sut.GetAllOshisAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllOshisAsync_ReturnsCorrectTalentIdAndName()
    {
        var talent = await AddTalentWithUserOshiAsync(displayName: "鈴木一郎", userId: 1);

        var result = await _sut.GetAllOshisAsync();

        Assert.Single(result);
        Assert.Equal("鈴木一郎", result[0].Name);
        Assert.Equal(talent.Id, result[0].TalentId);
    }

    [Fact]
    public async Task GetAllOshisAsync_TalentWithoutUserOshi_IsNotReturned()
    {
        // UserOshi なしで Talent だけ作成
        _db.Talents.Add(new Talent
        {
            DisplayName = "未登録タレント",
            NormalizedName = "未登録タレント",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllOshisAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllOshisAsync_SpansAllUsers_ReturnsTalentsFromAllUsers()
    {
        // ユーザー1、2、3 それぞれに1タレントずつ登録
        await AddTalentWithUserOshiAsync(displayName: "推し1", userId: 1);
        await AddTalentWithUserOshiAsync(displayName: "推し2", userId: 2);
        await AddTalentWithUserOshiAsync(displayName: "推し3", userId: 3);

        var result = await _sut.GetAllOshisAsync();

        Assert.Equal(3, result.Count);
        var names = result.Select(o => o.Name).ToHashSet();
        Assert.Contains("推し1", names);
        Assert.Contains("推し2", names);
        Assert.Contains("推し3", names);
    }

    // ── GetAllOshisAsync: 境界値 ─────────────────────────────────────────────

    [Fact]
    public async Task GetAllOshisAsync_NoTalents_ReturnsEmptyList()
    {
        var result = await _sut.GetAllOshisAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── CreateProgramAppearanceAsync: FEAT-024 ニュース URL との重複 ──────────

    [Fact]
    public async Task CreateProgramAppearanceAsync_UrlAlreadyInNewsForSameTalent_ReturnsWasDuplicateTrue()
    {
        await AddTalentAsync(talentId: 1);
        // 事前に同 URL のニュースを保存
        var newsDto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/both");
        await _sut.CreateNewsItemAsync(newsDto);

        // 同 URL で番組を登録しようとする
        var progDto = BuildProgramDto(talentId: 1, sourceUrl: "https://example.com/both");
        var result = await _sut.CreateProgramAppearanceAsync(progDto);

        Assert.True(result.WasDuplicate);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_UrlAlreadyInNewsForSameTalent_DoesNotSaveProgram()
    {
        await AddTalentAsync(talentId: 1);
        var newsDto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/news-dup");
        await _sut.CreateNewsItemAsync(newsDto);

        var progDto = BuildProgramDto(talentId: 1, sourceUrl: "https://example.com/news-dup");
        await _sut.CreateProgramAppearanceAsync(progDto);

        var count = await _db.ProgramAppearances.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CreateProgramAppearanceAsync_UrlInNewsOfDifferentTalent_SavesProgram()
    {
        await AddTalentAsync(talentId: 1);
        await AddTalentAsync(talentId: 2);
        // talentId=1 のニュースと URL が同じでも talentId=2 の番組は保存される
        var newsDto = BuildNewsDto(talentId: 1, sourceUrl: "https://example.com/cross");
        await _sut.CreateNewsItemAsync(newsDto);

        // talentId=2 の番組（talentId=1 のニュースとは別タレント）
        var progDto = BuildProgramDto(talentId: 2, sourceUrl: "https://example.com/cross");
        var result = await _sut.CreateProgramAppearanceAsync(progDto);

        Assert.False(result.WasDuplicate);
        Assert.Equal(1, await _db.ProgramAppearances.CountAsync());
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    /// <summary>指定 ID で Talent レコードのみを作成する（UserOshi なし）</summary>
    private async Task AddTalentAsync(int talentId)
    {
        _db.Talents.Add(new Talent
        {
            Id = talentId,
            DisplayName = $"タレント{talentId}",
            NormalizedName = $"タレント{talentId}",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>Talent + UserOshi をセットで作成する（GetAllOshisAsync テスト用）</summary>
    private async Task<Talent> AddTalentWithUserOshiAsync(string displayName, int userId)
    {
        var talent = new Talent
        {
            DisplayName = displayName,
            NormalizedName = displayName.ToLowerInvariant().Replace(" ", ""),
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync(); // talent.Id が確定する

        _db.UserOshis.Add(new UserOshi
        {
            UserId = userId,
            TalentId = talent.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return talent;
    }

    private CreateNewsItemRequestDto BuildNewsDto(
        int talentId,
        string sourceUrl,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? collectedAt = null) => new()
        {
            TalentId = talentId,
            Title = "テストニュース",
            Summary = "テスト本文要約",
            SourceUrl = sourceUrl,
            PublishedAt = publishedAt,
            CollectedAt = collectedAt ?? _now,
        };

    private CreateProgramAppearanceRequestDto BuildProgramDto(
        int talentId,
        string sourceUrl,
        DateTimeOffset? airedAt = null,
        DateTimeOffset? collectedAt = null) => new()
        {
            TalentId = talentId,
            ProgramName = "テスト番組",
            Broadcaster = "テストTV",
            AiredAt = airedAt,
            SourceUrl = sourceUrl,
            CollectedAt = collectedAt ?? _now,
        };
}
