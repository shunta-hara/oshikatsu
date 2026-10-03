using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>
/// FEAT-024: 番組一覧API で同推しのニュースと重複する sourceUrl を除外する動作のテスト
/// </summary>
public class FeedServiceProgramDeduplicationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FeedService _sut;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public FeedServiceProgramDeduplicationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new FeedService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private async Task<(User user, Oshi oshi)> SetupUserAndOshiAsync()
    {
        var dbNow = DateTime.UtcNow;
        var user = new User
        {
            Email = $"user_{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            CreatedAt = dbNow,
            UpdatedAt = dbNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var oshi = new Oshi { UserId = user.Id, Name = "テスト推し", CreatedAt = dbNow, UpdatedAt = dbNow };
        _db.Oshis.Add(oshi);
        await _db.SaveChangesAsync();

        return (user, oshi);
    }

    private async Task AddNewsItemAsync(int oshiId, string sourceUrl, string title = "ニュース")
    {
        _db.NewsItems.Add(new NewsItem
        {
            OshiId = oshiId,
            Title = title,
            SourceUrl = sourceUrl,
            PublishedAt = _now,
            IsPublishedAtEstimated = false,
            CollectedAt = _now,
            CreatedAt = _now,
        });
        await _db.SaveChangesAsync();
    }

    private async Task AddProgramAsync(int oshiId, string sourceUrl, string name = "番組")
    {
        _db.ProgramAppearances.Add(new ProgramAppearance
        {
            OshiId = oshiId,
            ProgramName = name,
            Broadcaster = "テストTV",
            AiredAt = _now,
            IsAiredAtEstimated = false,
            SourceUrl = sourceUrl,
            CollectedAt = _now,
            CreatedAt = _now,
        });
        await _db.SaveChangesAsync();
    }

    // ── GetProgramsAsync: 正常系（重複なし） ──────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_NoNewsOverlap_ReturnsAllPrograms()
    {
        var (user, oshi) = await SetupUserAndOshiAsync();
        await AddProgramAsync(oshi.Id, "https://tv.example.com/p1");
        await AddProgramAsync(oshi.Id, "https://tv.example.com/p2");

        var result = await _sut.GetProgramsAsync(user.Id, 1, 20);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    // ── GetProgramsAsync: FEAT-024 フィルタ ──────────────────────────────

    [Fact]
    public async Task GetProgramsAsync_ProgramUrlDuplicatesOwnNewsUrl_ExcludesProgram()
    {
        var (user, oshi) = await SetupUserAndOshiAsync();
        var dupUrl = "https://example.com/both";

        // 同URLのニュースと番組を両方登録
        await AddNewsItemAsync(oshi.Id, dupUrl);
        await AddProgramAsync(oshi.Id, dupUrl);

        var result = await _sut.GetProgramsAsync(user.Id, 1, 20);

        // 番組は除外される
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetProgramsAsync_ProgramUrlDuplicatesOwnNewsUrl_OtherProgramsNotAffected()
    {
        var (user, oshi) = await SetupUserAndOshiAsync();
        var dupUrl = "https://example.com/dup";
        var uniqueProgramUrl = "https://tv.example.com/unique";

        await AddNewsItemAsync(oshi.Id, dupUrl);
        await AddProgramAsync(oshi.Id, dupUrl);
        await AddProgramAsync(oshi.Id, uniqueProgramUrl);

        var result = await _sut.GetProgramsAsync(user.Id, 1, 20);

        // uniqueProgramUrl のみ返る
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(uniqueProgramUrl, result.Items[0].SourceUrl);
    }

    [Fact]
    public async Task GetProgramsAsync_ProgramUrlDuplicatesOtherOshisNews_NotExcluded()
    {
        // 別の推し（他ユーザー）のニュースと URL が被っても除外されない
        var dbNow = DateTime.UtcNow;
        var user1 = new User { Email = "u1@example.com", PasswordHash = "h", CreatedAt = dbNow, UpdatedAt = dbNow };
        var user2 = new User { Email = "u2@example.com", PasswordHash = "h", CreatedAt = dbNow, UpdatedAt = dbNow };
        _db.Users.AddRange(user1, user2);
        await _db.SaveChangesAsync();

        var oshi1 = new Oshi { UserId = user1.Id, Name = "推し1", CreatedAt = dbNow, UpdatedAt = dbNow };
        var oshi2 = new Oshi { UserId = user2.Id, Name = "推し2", CreatedAt = dbNow, UpdatedAt = dbNow };
        _db.Oshis.AddRange(oshi1, oshi2);
        await _db.SaveChangesAsync();

        var crossUrl = "https://example.com/cross";

        // oshi1 の番組に crossUrl
        await AddProgramAsync(oshi1.Id, crossUrl);
        // oshi2 のニュースに crossUrl
        await AddNewsItemAsync(oshi2.Id, crossUrl);

        // user1 の番組一覧には oshi1 の番組が含まれる（oshi2 のニュースは別推しなので除外対象外）
        var result = await _sut.GetProgramsAsync(user1.Id, 1, 20);

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetProgramsAsync_NoProgramsAtAll_ReturnsEmpty()
    {
        var (user, oshi) = await SetupUserAndOshiAsync();
        await AddNewsItemAsync(oshi.Id, "https://example.com/news-only");

        var result = await _sut.GetProgramsAsync(user.Id, 1, 20);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    // ── GetProgramsAsync: ページネーションが FEAT-024 フィルタ後に適用される ─

    [Fact]
    public async Task GetProgramsAsync_AfterDeduplication_PaginationIsCorrect()
    {
        var (user, oshi) = await SetupUserAndOshiAsync();
        var dupUrl = "https://example.com/dup";

        await AddNewsItemAsync(oshi.Id, dupUrl);
        // dup なし × 5件、dup あり × 1件
        await AddProgramAsync(oshi.Id, dupUrl);
        for (int i = 0; i < 5; i++)
            await AddProgramAsync(oshi.Id, $"https://tv.example.com/unique/{i}");

        var result = await _sut.GetProgramsAsync(user.Id, 1, 20);

        // dup を除いた5件のみ
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(5, result.Items.Count);
    }
}
