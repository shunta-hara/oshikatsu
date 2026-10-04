using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class InternalCollectorServiceBatchTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly InternalCollectorService _sut;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public InternalCollectorServiceBatchTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new InternalCollectorService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private async Task<Talent> AddTalentAsync(int userId = 1, string displayName = "テストタレント")
    {
        var talent = new Talent
        {
            DisplayName = displayName,
            NormalizedName = displayName.ToLowerInvariant().Replace(" ", ""),
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();
        return talent;
    }

    private BatchNewsItemDto BuildNewsItem(string sourceUrl, string title = "ニュースタイトル") => new()
    {
        Title = title,
        Summary = "要約",
        SourceUrl = sourceUrl,
        PublishedAt = _now.AddHours(-1),
        CollectedAt = _now,
    };

    private BatchProgramItemDto BuildProgramItem(string sourceUrl, string programName = "テスト番組") => new()
    {
        ProgramName = programName,
        Broadcaster = "テストTV",
        AiredAt = _now.AddHours(-1),
        SourceUrl = sourceUrl,
        CollectedAt = _now,
    };

    // ── BatchCreateNewsItemsAsync: 正常系 ────────────────────────────────────

    [Fact]
    public async Task BatchCreateNewsItemsAsync_ValidItems_SavesAll()
    {
        var talent = await AddTalentAsync();
        var dto = new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [
                BuildNewsItem("https://example.com/1"),
                BuildNewsItem("https://example.com/2"),
                BuildNewsItem("https://example.com/3"),
            ],
        };

        var result = await _sut.BatchCreateNewsItemsAsync(dto);

        Assert.Equal(3, result.ReceivedCount);
        Assert.Equal(3, result.SavedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(3, await _db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task BatchCreateNewsItemsAsync_ValidItems_ReturnsCorrectCounts()
    {
        var talent = await AddTalentAsync();
        var dto = new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [BuildNewsItem("https://example.com/news/a")],
        };

        var result = await _sut.BatchCreateNewsItemsAsync(dto);

        Assert.Equal(1, result.ReceivedCount);
        Assert.Equal(1, result.SavedCount);
        Assert.Equal(0, result.SkippedCount);
    }

    [Fact]
    public async Task BatchCreateNewsItemsAsync_NullPublishedAt_SetsEstimatedFlag()
    {
        var talent = await AddTalentAsync();
        var item = new BatchNewsItemDto
        {
            Title = "タイトル",
            SourceUrl = "https://example.com/news/est",
            PublishedAt = null,
            CollectedAt = _now,
        };
        var dto = new BatchCreateNewsRequestDto { TalentId = talent.Id, Items = [item] };

        await _sut.BatchCreateNewsItemsAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.True(saved.IsPublishedAtEstimated);
        Assert.Equal(_now.UtcDateTime, saved.PublishedAt!.Value.UtcDateTime);
    }

    [Fact]
    public async Task BatchCreateNewsItemsAsync_WithPublishedAt_StoresActualDateNotEstimated()
    {
        var talent = await AddTalentAsync();
        var publishedAt = _now.AddHours(-2);
        var item = new BatchNewsItemDto
        {
            Title = "タイトル",
            SourceUrl = "https://example.com/news/real",
            PublishedAt = publishedAt,
            CollectedAt = _now,
        };
        var dto = new BatchCreateNewsRequestDto { TalentId = talent.Id, Items = [item] };

        await _sut.BatchCreateNewsItemsAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.False(saved.IsPublishedAtEstimated);
        Assert.Equal(publishedAt.UtcDateTime, saved.PublishedAt!.Value.UtcDateTime);
    }

    [Fact]
    public async Task BatchCreateNewsItemsAsync_JstPublishedAt_NormalizesToUtc()
    {
        var talent = await AddTalentAsync();
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.FromHours(9));
        var item = new BatchNewsItemDto
        {
            Title = "タイトル",
            SourceUrl = "https://example.com/news/jst",
            PublishedAt = jstTime,
            CollectedAt = _now,
        };
        var dto = new BatchCreateNewsRequestDto { TalentId = talent.Id, Items = [item] };

        await _sut.BatchCreateNewsItemsAsync(dto);

        var saved = await _db.NewsItems.FirstAsync();
        Assert.Equal(TimeSpan.Zero, saved.PublishedAt!.Value.Offset);
        Assert.Equal(jstTime.UtcDateTime, saved.PublishedAt.Value.UtcDateTime);
    }

    // ── BatchCreateNewsItemsAsync: 重複排除 ──────────────────────────────────

    [Fact]
    public async Task BatchCreateNewsItemsAsync_DuplicateWithinRequest_OnlySavesFirst()
    {
        var talent = await AddTalentAsync();
        var dto = new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [
                BuildNewsItem("https://example.com/dup"),
                BuildNewsItem("https://example.com/dup"),
            ],
        };

        var result = await _sut.BatchCreateNewsItemsAsync(dto);

        Assert.Equal(2, result.ReceivedCount);
        Assert.Equal(1, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, await _db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task BatchCreateNewsItemsAsync_DuplicateWithExisting_SkipsExisting()
    {
        var talent = await AddTalentAsync();
        // 事前に1件保存
        var initial = new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [BuildNewsItem("https://example.com/existing")],
        };
        await _sut.BatchCreateNewsItemsAsync(initial);

        // 同じURLを含む一括登録
        var dto = new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [
                BuildNewsItem("https://example.com/existing"),
                BuildNewsItem("https://example.com/new"),
            ],
        };

        var result = await _sut.BatchCreateNewsItemsAsync(dto);

        Assert.Equal(2, result.ReceivedCount);
        Assert.Equal(1, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(2, await _db.NewsItems.CountAsync());
    }

    [Fact]
    public async Task BatchCreateNewsItemsAsync_SameUrlDifferentTalent_SavesBoth()
    {
        var talent1 = await AddTalentAsync(userId: 1, displayName: "タレント1");
        var talent2 = await AddTalentAsync(userId: 2, displayName: "タレント2");
        var url = "https://example.com/shared";

        await _sut.BatchCreateNewsItemsAsync(new BatchCreateNewsRequestDto
        {
            TalentId = talent1.Id,
            Items = [BuildNewsItem(url)],
        });

        var result = await _sut.BatchCreateNewsItemsAsync(new BatchCreateNewsRequestDto
        {
            TalentId = talent2.Id,
            Items = [BuildNewsItem(url)],
        });

        Assert.Equal(1, result.SavedCount);
        Assert.Equal(2, await _db.NewsItems.CountAsync());
    }

    // ── BatchCreateNewsItemsAsync: 異常系 ───────────────────────────────────

    [Fact]
    public async Task BatchCreateNewsItemsAsync_TalentNotFound_ThrowsTalentNotFoundException()
    {
        var dto = new BatchCreateNewsRequestDto
        {
            TalentId = 99999,
            Items = [BuildNewsItem("https://example.com/x")],
        };

        await Assert.ThrowsAsync<TalentNotFoundException>(() => _sut.BatchCreateNewsItemsAsync(dto));
    }

    // ── BatchCreateProgramAppearancesAsync: 正常系 ───────────────────────────

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_ValidItems_SavesAll()
    {
        var talent = await AddTalentAsync();
        var dto = new BatchCreateProgramsRequestDto
        {
            TalentId = talent.Id,
            Items = [
                BuildProgramItem("https://tv.example.com/1"),
                BuildProgramItem("https://tv.example.com/2"),
            ],
        };

        var result = await _sut.BatchCreateProgramAppearancesAsync(dto);

        Assert.Equal(2, result.ReceivedCount);
        Assert.Equal(2, result.SavedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(2, await _db.ProgramAppearances.CountAsync());
    }

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_NullAiredAt_SetsEstimatedFlag()
    {
        var talent = await AddTalentAsync();
        var item = new BatchProgramItemDto
        {
            ProgramName = "テスト番組",
            SourceUrl = "https://tv.example.com/est",
            AiredAt = null,
            CollectedAt = _now,
        };
        var dto = new BatchCreateProgramsRequestDto { TalentId = talent.Id, Items = [item] };

        await _sut.BatchCreateProgramAppearancesAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.True(saved.IsAiredAtEstimated);
    }

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_JstAiredAt_NormalizesToUtc()
    {
        var talent = await AddTalentAsync();
        var jstTime = new DateTimeOffset(2026, 10, 5, 20, 0, 0, TimeSpan.FromHours(9));
        var item = new BatchProgramItemDto
        {
            ProgramName = "テスト番組",
            SourceUrl = "https://tv.example.com/jst",
            AiredAt = jstTime,
            CollectedAt = _now,
        };
        var dto = new BatchCreateProgramsRequestDto { TalentId = talent.Id, Items = [item] };

        await _sut.BatchCreateProgramAppearancesAsync(dto);

        var saved = await _db.ProgramAppearances.FirstAsync();
        Assert.Equal(TimeSpan.Zero, saved.AiredAt!.Value.Offset);
        Assert.Equal(jstTime.UtcDateTime, saved.AiredAt.Value.UtcDateTime);
    }

    // ── BatchCreateProgramAppearancesAsync: 重複排除 ─────────────────────────

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_DuplicateWithinRequest_OnlySavesFirst()
    {
        var talent = await AddTalentAsync();
        var dto = new BatchCreateProgramsRequestDto
        {
            TalentId = talent.Id,
            Items = [
                BuildProgramItem("https://tv.example.com/dup"),
                BuildProgramItem("https://tv.example.com/dup"),
            ],
        };

        var result = await _sut.BatchCreateProgramAppearancesAsync(dto);

        Assert.Equal(2, result.ReceivedCount);
        Assert.Equal(1, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, await _db.ProgramAppearances.CountAsync());
    }

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_DuplicateWithExistingProgram_SkipsExisting()
    {
        var talent = await AddTalentAsync();
        await _sut.BatchCreateProgramAppearancesAsync(new BatchCreateProgramsRequestDto
        {
            TalentId = talent.Id,
            Items = [BuildProgramItem("https://tv.example.com/existing")],
        });

        var dto = new BatchCreateProgramsRequestDto
        {
            TalentId = talent.Id,
            Items = [
                BuildProgramItem("https://tv.example.com/existing"),
                BuildProgramItem("https://tv.example.com/new"),
            ],
        };

        var result = await _sut.BatchCreateProgramAppearancesAsync(dto);

        Assert.Equal(1, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    // ── BatchCreateProgramAppearancesAsync: FEAT-024 ニュースとの重複排除 ────

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_UrlAlreadyInNews_SkipsProgram()
    {
        var talent = await AddTalentAsync();
        // ニュースに同URLを先に保存
        await _sut.BatchCreateNewsItemsAsync(new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [BuildNewsItem("https://example.com/both")],
        });

        // 同URLで番組を登録しようとする
        var dto = new BatchCreateProgramsRequestDto
        {
            TalentId = talent.Id,
            Items = [BuildProgramItem("https://example.com/both")],
        };

        var result = await _sut.BatchCreateProgramAppearancesAsync(dto);

        Assert.Equal(0, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, await _db.ProgramAppearances.CountAsync());
    }

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_UrlInNewsOfOtherTalent_SavesProgram()
    {
        // 別のタレントのニュースに同URLがある場合はスキップしない
        var talent1 = await AddTalentAsync(userId: 1, displayName: "タレント1");
        var talent2 = await AddTalentAsync(userId: 2, displayName: "タレント2");

        await _sut.BatchCreateNewsItemsAsync(new BatchCreateNewsRequestDto
        {
            TalentId = talent1.Id,
            Items = [BuildNewsItem("https://example.com/cross")],
        });

        var dto = new BatchCreateProgramsRequestDto
        {
            TalentId = talent2.Id,
            Items = [BuildProgramItem("https://example.com/cross")],
        };

        var result = await _sut.BatchCreateProgramAppearancesAsync(dto);

        Assert.Equal(1, result.SavedCount);
        Assert.Equal(0, result.SkippedCount);
    }

    // ── BatchCreateProgramAppearancesAsync: 異常系 ───────────────────────────

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_TalentNotFound_ThrowsTalentNotFoundException()
    {
        var dto = new BatchCreateProgramsRequestDto
        {
            TalentId = 99999,
            Items = [BuildProgramItem("https://tv.example.com/x")],
        };

        await Assert.ThrowsAsync<TalentNotFoundException>(() => _sut.BatchCreateProgramAppearancesAsync(dto));
    }

    // ── 境界値 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchCreateNewsItemsAsync_AllItemsDuplicated_Returns100PercentSkipped()
    {
        var talent = await AddTalentAsync();
        await _sut.BatchCreateNewsItemsAsync(new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [BuildNewsItem("https://example.com/a"), BuildNewsItem("https://example.com/b")],
        });

        var dto = new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [BuildNewsItem("https://example.com/a"), BuildNewsItem("https://example.com/b")],
        };

        var result = await _sut.BatchCreateNewsItemsAsync(dto);

        Assert.Equal(2, result.ReceivedCount);
        Assert.Equal(0, result.SavedCount);
        Assert.Equal(2, result.SkippedCount);
    }

    // ── IsUniqueConstraintViolation: 23505 のみ catch, それ以外は rethrow ──────

    [Fact]
    public async Task BatchCreateNewsItemsAsync_UniqueConstraintViolation_TreatsAsSkip()
    {
        // 23505 相当の DbUpdateException が発生した場合はスキップ扱い（500 にならない）
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // Talent を通常 context で事前登録
        Talent talent;
        using (var setupDb = new AppDbContext(options))
        {
            talent = new Talent
            {
                DisplayName = "テストタレント",
                NormalizedName = "テストタレント",
                CreatedAt = DateTime.UtcNow,
            };
            setupDb.Talents.Add(talent);
            await setupDb.SaveChangesAsync();
        }

        // SaveChangesAsync が 23505 相当の DbUpdateException を投げる ThrowingDbContext
        var uniqueEx = new DbUpdateException("unique violation",
            new InvalidOperationException("duplicate key"));
        using var throwingDb = new ThrowingDbContext(options, uniqueEx);

        // IsUniqueConstraintViolation が常に true を返す TestableService（= 23505 扱い）
        var svc = new TestableInternalCollectorService(throwingDb, _ => true);

        var dto = new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [new BatchNewsItemDto
            {
                Title = "タイトル", SourceUrl = "https://example.com/uniq", CollectedAt = _now,
            }],
        };

        // 23505 扱いなので例外は発生しない
        var result = await svc.BatchCreateNewsItemsAsync(dto);

        Assert.Equal(1, result.ReceivedCount);
        Assert.Equal(0, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task BatchCreateNewsItemsAsync_NonUniqueConstraintDbException_Rethrows()
    {
        // 23505 以外の DbUpdateException は再スローされる（スキップ扱いにならない）
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Talent talent;
        using (var setupDb = new AppDbContext(options))
        {
            talent = new Talent
            {
                DisplayName = "テストタレント",
                NormalizedName = "テストタレント",
                CreatedAt = DateTime.UtcNow,
            };
            setupDb.Talents.Add(talent);
            await setupDb.SaveChangesAsync();
        }

        // 接続エラー相当（非 23505）の DbUpdateException
        var otherEx = new DbUpdateException("connection error", new InvalidOperationException("other"));
        using var throwingDb = new ThrowingDbContext(options, otherEx);

        // IsUniqueConstraintViolation が常に false を返す（= 非 23505 扱い）
        var svc = new TestableInternalCollectorService(throwingDb, _ => false);

        var dto = new BatchCreateNewsRequestDto
        {
            TalentId = talent.Id,
            Items = [new BatchNewsItemDto
            {
                Title = "タイトル", SourceUrl = "https://example.com/err", CollectedAt = _now,
            }],
        };

        // 非 23505 なので DbUpdateException が再スローされる
        await Assert.ThrowsAsync<DbUpdateException>(() => svc.BatchCreateNewsItemsAsync(dto));
    }

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_UniqueConstraintViolation_TreatsAsSkip()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Talent talent;
        using (var setupDb = new AppDbContext(options))
        {
            talent = new Talent
            {
                DisplayName = "テストタレント",
                NormalizedName = "テストタレント",
                CreatedAt = DateTime.UtcNow,
            };
            setupDb.Talents.Add(talent);
            await setupDb.SaveChangesAsync();
        }

        var uniqueEx = new DbUpdateException("unique violation", new InvalidOperationException());
        using var throwingDb = new ThrowingDbContext(options, uniqueEx);
        var svc = new TestableInternalCollectorService(throwingDb, _ => true);

        var dto = new BatchCreateProgramsRequestDto
        {
            TalentId = talent.Id,
            Items = [new BatchProgramItemDto
            {
                ProgramName = "番組", SourceUrl = "https://tv.example.com/uniq", CollectedAt = _now,
            }],
        };

        var result = await svc.BatchCreateProgramAppearancesAsync(dto);

        Assert.Equal(0, result.SavedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public async Task BatchCreateProgramAppearancesAsync_NonUniqueConstraintDbException_Rethrows()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        Talent talent;
        using (var setupDb = new AppDbContext(options))
        {
            talent = new Talent
            {
                DisplayName = "テストタレント",
                NormalizedName = "テストタレント",
                CreatedAt = DateTime.UtcNow,
            };
            setupDb.Talents.Add(talent);
            await setupDb.SaveChangesAsync();
        }

        var otherEx = new DbUpdateException("other error", new InvalidOperationException());
        using var throwingDb = new ThrowingDbContext(options, otherEx);
        var svc = new TestableInternalCollectorService(throwingDb, _ => false);

        var dto = new BatchCreateProgramsRequestDto
        {
            TalentId = talent.Id,
            Items = [new BatchProgramItemDto
            {
                ProgramName = "番組", SourceUrl = "https://tv.example.com/err", CollectedAt = _now,
            }],
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => svc.BatchCreateProgramAppearancesAsync(dto));
    }

    // ── テスト用ヘルパークラス ─────────────────────────────────────────────────

    /// <summary>
    /// SaveChangesAsync が常に指定の DbUpdateException を投げる AppDbContext。
    /// IsUniqueConstraintViolation の動作テストで使用する。
    /// </summary>
    private sealed class ThrowingDbContext : AppDbContext
    {
        private readonly DbUpdateException _exToThrow;

        public ThrowingDbContext(DbContextOptions<AppDbContext> options, DbUpdateException ex)
            : base(options)
        {
            _exToThrow = ex;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw _exToThrow;
    }

    /// <summary>
    /// IsUniqueConstraintViolation を任意の関数でオーバーライドできる InternalCollectorService。
    /// 23505 例外ハンドリングの単体テストに使用する。
    /// </summary>
    private sealed class TestableInternalCollectorService : InternalCollectorService
    {
        private readonly Func<DbUpdateException, bool> _isUniqueViolation;

        public TestableInternalCollectorService(
            AppDbContext db,
            Func<DbUpdateException, bool> isUniqueViolation)
            : base(db)
        {
            _isUniqueViolation = isUniqueViolation;
        }

        protected override bool IsUniqueConstraintViolation(DbUpdateException ex)
            => _isUniqueViolation(ex);
    }
}
