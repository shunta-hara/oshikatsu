using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>
/// FEAT-035/036: FeedService の転載重複まとめ・媒体名表示のテスト
/// </summary>
public class FeedServiceDeduplicationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FeedService _sut;

    public FeedServiceDeduplicationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new FeedService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private async Task<Oshi> CreateOshi(int userId, string name)
    {
        var now = DateTime.UtcNow;
        var oshi = new Oshi { UserId = userId, Name = name, CreatedAt = now, UpdatedAt = now };
        _db.Oshis.Add(oshi);

        // FEAT-039: Talent + UserOshi も作成して talent_id ベースのクエリが機能するようにする
        var normalizedName = TalentNameNormalizer.Normalize(name);
        var existingTalent = _db.Talents.Local.FirstOrDefault(t => t.NormalizedName == normalizedName)
            ?? await _db.Talents.FirstOrDefaultAsync(t => t.NormalizedName == normalizedName);
        if (existingTalent is null)
        {
            existingTalent = new Talent { DisplayName = name, NormalizedName = normalizedName, CreatedAt = now };
            _db.Talents.Add(existingTalent);
        }
        await _db.SaveChangesAsync();

        var existingUserOshi = await _db.UserOshis.FirstOrDefaultAsync(o => o.UserId == userId && o.TalentId == existingTalent.Id);
        if (existingUserOshi is null)
        {
            _db.UserOshis.Add(new UserOshi { UserId = userId, TalentId = existingTalent.Id, CreatedAt = now });
            await _db.SaveChangesAsync();
        }

        return oshi;
    }

    // Oshi から TalentId を取得するヘルパー
    private async Task<int> GetTalentId(Oshi oshi)
    {
        var normalizedName = TalentNameNormalizer.Normalize(oshi.Name);
        var talent = await _db.Talents.FirstAsync(t => t.NormalizedName == normalizedName);
        return talent.Id;
    }

    private async Task<NewsItem> CreateNews(
        Oshi oshi,
        string title,
        string? sourceName = null,
        DateTimeOffset? publishedAt = null,
        string? sourceUrl = null)
    {
        var now = DateTimeOffset.UtcNow;
        var talentId = await GetTalentId(oshi);
        var item = new NewsItem
        {
            OshiId = oshi.Id,
            TalentId = talentId,
            Title = title,
            SourceUrl = sourceUrl ?? $"https://example.com/news/{Guid.NewGuid()}",
            CollectedAt = now,
            PublishedAt = publishedAt ?? now,
            SourceName = sourceName,
            CreatedAt = now,
        };
        _db.NewsItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<ProgramAppearance> CreateProgram(
        Oshi oshi,
        string programName,
        string? sourceName = null,
        DateTimeOffset? airedAt = null,
        string? sourceUrl = null)
    {
        var now = DateTimeOffset.UtcNow;
        var talentId = await GetTalentId(oshi);
        var item = new ProgramAppearance
        {
            OshiId = oshi.Id,
            TalentId = talentId,
            ProgramName = programName,
            SourceUrl = sourceUrl ?? $"https://example.com/prog/{Guid.NewGuid()}",
            CollectedAt = now,
            AiredAt = airedAt ?? now,
            SourceName = sourceName,
            CreatedAt = now,
        };
        _db.ProgramAppearances.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    private async Task<User> CreateUser(int seed)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = $"u{seed}@example.com",
            PasswordHash = "hash",
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── FEAT-036: SourceName のレスポンス含有 ─────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_SourceNameFromDb_ReturnedInResponse()
    {
        var oshi = await CreateOshi(1, "推しA");
        await CreateNews(oshi, "記事タイトル - スポニチ", sourceName: "スポニチ");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("スポニチ", result.Items[0].SourceName);
    }

    [Fact]
    public async Task GetNewsAsync_SourceNameNotInDb_ExtractedFromTitle()
    {
        var oshi = await CreateOshi(1, "推しA");
        // SourceName は DB に保存されていない（既存データ）
        await CreateNews(oshi, "記事タイトル - モデルプレス", sourceName: null);

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("モデルプレス", result.Items[0].SourceName);
    }

    [Fact]
    public async Task GetNewsAsync_NoSourceInTitle_SourceNameIsNull()
    {
        var oshi = await CreateOshi(1, "推しA");
        await CreateNews(oshi, "媒体名なし記事", sourceName: null);

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Null(result.Items[0].SourceName);
    }

    [Fact]
    public async Task GetProgramsAsync_SourceNameFromDb_ReturnedInResponse()
    {
        var oshi = await CreateOshi(1, "推しA");
        await CreateProgram(oshi, "番組名 - Weverse", sourceName: "Weverse");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal("Weverse", result.Items[0].SourceName);
    }

    // ── FEAT-035: 転載重複まとめ ──────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_TwoSameTitleWithin48h_ReturnedAsSingleGroup()
    {
        var oshi = await CreateOshi(1, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-10);
        await CreateNews(oshi, "推しAの記事 - スポニチ Sponichi Annex",
            sourceName: "スポニチ Sponichi Annex", publishedAt: base_);
        await CreateNews(oshi, "推しAの記事 - Yahoo!ニュース",
            sourceName: "Yahoo!ニュース", publishedAt: base_.AddHours(2));

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        // 2件が1グループにまとまる
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetNewsAsync_DeduplicatedGroup_HasOtherSourceCount()
    {
        var oshi = await CreateOshi(1, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-10);
        await CreateNews(oshi, "推しAの記事 - スポニチ", sourceName: "スポニチ", publishedAt: base_);
        await CreateNews(oshi, "推しAの記事 - Yahoo!ニュース", sourceName: "Yahoo!ニュース", publishedAt: base_.AddHours(1));
        await CreateNews(oshi, "推しAの記事 - Infoseek", sourceName: "Infoseek", publishedAt: base_.AddHours(2));

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(2, result.Items[0].OtherSourceCount);
        Assert.Equal(2, result.Items[0].OtherSources.Count);
    }

    [Fact]
    public async Task GetNewsAsync_DeduplicatedGroup_RepresentativeIsNonRepostMedia()
    {
        var oshi = await CreateOshi(1, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-10);
        // Yahoo!ニュースが転載先リストに入っているので、スポニチが代表になるべき
        await CreateNews(oshi, "推しAの記事 - Yahoo!ニュース",
            sourceName: "Yahoo!ニュース", publishedAt: base_.AddHours(-1));  // 早い方だが転載先
        await CreateNews(oshi, "推しAの記事 - スポニチ",
            sourceName: "スポニチ", publishedAt: base_);                     // 遅い方だが非転載先

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Contains("スポニチ", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_Over48h_NotGrouped_CountsAsSeparate()
    {
        var oshi = await CreateOshi(1, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-100);
        await CreateNews(oshi, "推しAの記事 - スポニチ", publishedAt: base_);
        await CreateNews(oshi, "推しAの記事 - Yahoo!ニュース", publishedAt: base_.AddHours(49));

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        // 48h超なのでまとめられない
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetNewsAsync_Pagination_UsesDeduplicatedCount()
    {
        // 4記事が2グループ → ページサイズ1で取得すると TotalCount=2
        var oshi = await CreateOshi(1, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-20);
        await CreateNews(oshi, "記事X - スポニチ", publishedAt: base_);
        await CreateNews(oshi, "記事X - Yahoo!ニュース", publishedAt: base_.AddHours(1));
        await CreateNews(oshi, "記事Y - モデルプレス", publishedAt: base_.AddHours(-5));
        await CreateNews(oshi, "記事Y - Infoseek", publishedAt: base_.AddHours(-4));

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 1);

        Assert.Equal(2, result.TotalCount);  // グループ数
        Assert.Single(result.Items);          // 1ページに1グループ
    }

    [Fact]
    public async Task GetNewsAsync_BookmarkOnOtherInGroup_RepresentativeMarkedAsBookmarked()
    {
        var user = await CreateUser(1);
        var oshi = await CreateOshi(user.Id, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-5);

        // 転載先（Yahoo!ニュース）がブックマーク済み、代表はスポニチ
        var yahooItem = await CreateNews(oshi, "推しAの記事 - Yahoo!ニュース",
            sourceName: "Yahoo!ニュース", publishedAt: base_);
        var sponichi = await CreateNews(oshi, "推しAの記事 - スポニチ",
            sourceName: "スポニチ", publishedAt: base_.AddHours(1));

        // Yahoo!ニュースをブックマーク
        _db.Bookmarks.Add(new Bookmark
        {
            UserId = user.Id,
            NewsItemId = yahooItem.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.GetNewsAsync(userId: user.Id, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(true, result.Items[0].IsBookmarked);
    }

    [Fact]
    public async Task GetNewsAsync_NoBookmarkInGroup_NotMarkedAsBookmarked()
    {
        var user = await CreateUser(1);
        var oshi = await CreateOshi(user.Id, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-5);

        await CreateNews(oshi, "推しAの記事 - スポニチ", sourceName: "スポニチ", publishedAt: base_);
        await CreateNews(oshi, "推しAの記事 - Yahoo!ニュース", sourceName: "Yahoo!ニュース", publishedAt: base_.AddHours(1));

        var result = await _sut.GetNewsAsync(userId: user.Id, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(false, result.Items[0].IsBookmarked);
    }

    // ── FEAT-035: programs のまとめ ──────────────────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_TwoSameTitleWithin48h_ReturnedAsSingleGroup()
    {
        var oshi = await CreateOshi(1, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-10);
        await CreateProgram(oshi, "番組A - Weverse", sourceName: "Weverse", airedAt: base_);
        await CreateProgram(oshi, "番組A - Yahoo!ニュース", sourceName: "Yahoo!ニュース", airedAt: base_.AddHours(2));

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(1, result.Items[0].OtherSourceCount);
    }

    [Fact]
    public async Task GetProgramsAsync_Over48h_NotGrouped()
    {
        var oshi = await CreateOshi(1, "推しA");
        var base_ = DateTimeOffset.UtcNow.AddHours(-100);
        await CreateProgram(oshi, "番組A - Weverse", airedAt: base_);
        await CreateProgram(oshi, "番組A - YouTube", airedAt: base_.AddHours(49));

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(2, result.TotalCount);
    }

    // ── 異常系・境界値 ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNewsAsync_SingleItem_OtherSourceCountIsZero()
    {
        var oshi = await CreateOshi(1, "推しA");
        await CreateNews(oshi, "単独記事 - スポニチ", sourceName: "スポニチ");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Equal(0, result.Items[0].OtherSourceCount);
        Assert.Empty(result.Items[0].OtherSources);
    }

    [Fact]
    public async Task GetNewsAsync_DifferentOshiSameTitle_NotGrouped()
    {
        var oshi1 = await CreateOshi(1, "推しA");
        var oshi2 = await CreateOshi(1, "推しB");
        var base_ = DateTimeOffset.UtcNow.AddHours(-5);

        await CreateNews(oshi1, "同じタイトル - スポニチ", publishedAt: base_);
        await CreateNews(oshi2, "同じタイトル - Yahoo!ニュース", publishedAt: base_.AddHours(1));

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        // 別推しなのでまとめない
        Assert.Equal(2, result.TotalCount);
    }
}
