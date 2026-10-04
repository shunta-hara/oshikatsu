using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>
/// Sprint 37 (FEAT-039) — FeedService の user_oshi_id → talent_id 解決テスト。
/// </summary>
public class FeedServiceSprint37Tests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FeedService _sut;

    public FeedServiceSprint37Tests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new FeedService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── GetNewsAsync: user_oshi_id → talent_id 解決 ──────────────────────────

    [Fact]
    public async Task GetNewsAsync_NoFilter_ReturnsNewsForAllUserTalents()
    {
        // セットアップ: ユーザー1 は talent1 を推し登録済み
        var (user1, talent1, userOshi1) = await SeedUserWithTalent(userId: 1, talentName: "タレントA");
        await SeedNewsItem(talent1, "ニュースA");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal("ニュースA", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithOshiIdFilter_ReturnsOnlyMatchingTalentNews()
    {
        // ユーザー1 が 2タレントを登録
        var (_, talent1, userOshi1) = await SeedUserWithTalent(userId: 1, talentName: "タレントA");
        var (_, talent2, userOshi2) = await SeedUserWithTalent(userId: 1, talentName: "タレントB");
        await SeedNewsItem(talent1, "タレントAのニュース");
        await SeedNewsItem(talent2, "タレントBのニュース");

        // user_oshi_id = userOshi1.Id でフィルタ → talent1 のニュースのみ
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: userOshi1.Id);

        Assert.Single(result.Items);
        Assert.Equal("タレントAのニュース", result.Items[0].Title);
    }

    [Fact]
    public async Task GetNewsAsync_WithInvalidOshiId_ReturnsEmpty()
    {
        var (_, talent1, userOshi1) = await SeedUserWithTalent(userId: 1, talentName: "タレントA");
        await SeedNewsItem(talent1, "ニュース");

        // 存在しない user_oshi_id でフィルタ
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: 99999);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetNewsAsync_ResponseOshiId_IsUserOshiId()
    {
        // FEAT-039: レスポンスの OshiId は user_oshis.id
        var (_, talent1, userOshi1) = await SeedUserWithTalent(userId: 1, talentName: "タレントA");
        await SeedNewsItem(talent1, "ニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal(userOshi1.Id, result.Items[0].OshiId);
    }

    [Fact]
    public async Task GetNewsAsync_ResponseOshiName_IsTalentDisplayName()
    {
        // FEAT-039: レスポンスの OshiName は talents.display_name
        var (_, talent1, userOshi1) = await SeedUserWithTalent(userId: 1, talentName: "テスト太郎");
        await SeedNewsItem(talent1, "ニュース");

        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal("テスト太郎", result.Items[0].OshiName);
    }

    [Fact]
    public async Task GetNewsAsync_OtherUserOshiId_ReturnsEmpty()
    {
        // 他のユーザーの user_oshi_id でフィルタしても結果は空
        var (_, talent2, userOshi2) = await SeedUserWithTalent(userId: 2, talentName: "他ユーザーのタレント");
        await SeedNewsItem(talent2, "他ユーザーのニュース");

        // ユーザー1 が ユーザー2 の userOshi_id でフィルタ
        var result = await _sut.GetNewsAsync(userId: 1, page: 1, pageSize: 20, oshiId: userOshi2.Id);

        Assert.Empty(result.Items);
    }

    // ── GetProgramsAsync: user_oshi_id → talent_id 解決 ─────────────────────

    [Fact]
    public async Task GetProgramsAsync_WithOshiIdFilter_ReturnsOnlyMatchingTalentPrograms()
    {
        var (_, talent1, userOshi1) = await SeedUserWithTalent(userId: 1, talentName: "タレントA");
        var (_, talent2, userOshi2) = await SeedUserWithTalent(userId: 1, talentName: "タレントB");
        await SeedProgramItem(talent1, "タレントAの番組");
        await SeedProgramItem(talent2, "タレントBの番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20, oshiId: userOshi1.Id);

        Assert.Single(result.Items);
        Assert.Equal("タレントAの番組", result.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetProgramsAsync_ResponseOshiId_IsUserOshiId()
    {
        var (_, talent1, userOshi1) = await SeedUserWithTalent(userId: 1, talentName: "タレントA");
        await SeedProgramItem(talent1, "番組");

        var result = await _sut.GetProgramsAsync(userId: 1, page: 1, pageSize: 20);

        Assert.Single(result.Items);
        Assert.Equal(userOshi1.Id, result.Items[0].OshiId);
    }

    // ── ヘルパー ────────────────────────────────────────────────────────────

    private async Task<(User, Talent, UserOshi)> SeedUserWithTalent(int userId, string talentName)
    {
        // User を必要に応じて作成
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
        {
            user = new User
            {
                Id = userId,
                Email = $"user{userId}@test.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.Users.Add(user);
        }

        var normalizedName = TalentNameNormalizer.Normalize(talentName);
        var talent = new Talent
        {
            DisplayName = talentName,
            NormalizedName = normalizedName,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();

        var userOshi = new UserOshi
        {
            UserId = userId,
            TalentId = talent.Id,
            CreatedAt = DateTime.UtcNow,
        };
        _db.UserOshis.Add(userOshi);
        await _db.SaveChangesAsync();

        return (user, talent, userOshi);
    }

    private async Task SeedNewsItem(Talent talent, string title)
    {
        // NewsItem はまだ oshi_id (FK) を必要とするが、Sprint 37 では talent_id を使う
        // InMemory DB では FK 制約がないため oshi_id=0 で OK
        var now = DateTimeOffset.UtcNow;
        _db.NewsItems.Add(new NewsItem
        {
            OshiId = 0, // FK 制約なし（InMemory）
            TalentId = talent.Id,
            Title = title,
            SourceUrl = $"https://example.com/{Guid.NewGuid()}",
            PublishedAt = now,
            CollectedAt = now,
        });
        await _db.SaveChangesAsync();
    }

    private async Task SeedProgramItem(Talent talent, string programName)
    {
        var now = DateTimeOffset.UtcNow;
        _db.ProgramAppearances.Add(new ProgramAppearance
        {
            OshiId = 0, // FK 制約なし（InMemory）
            TalentId = talent.Id,
            ProgramName = programName,
            SourceUrl = $"https://example.com/prog/{Guid.NewGuid()}",
            AiredAt = now,
            CollectedAt = now,
        });
        await _db.SaveChangesAsync();
    }
}
