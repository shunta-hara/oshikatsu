using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>
/// Sprint 37 (FEAT-039) — TalentService の race condition 対処テスト。
/// IsUniqueConstraintViolation は PostgresException 依存のため InMemory では検証できない。
/// 代わりに「既存レコードが返ってくる」正常系と「新規作成」正常系を確認する。
/// </summary>
public class TalentServiceSprint37Tests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TalentService _sut;

    public TalentServiceSprint37Tests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new TalentService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── FindOrCreateTalentAsync: 正常系 ─────────────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_NewName_CreatesTalent()
    {
        var talent = await _sut.FindOrCreateTalentAsync("新しいタレント");

        Assert.NotNull(talent);
        Assert.Equal("新しいタレント", talent.DisplayName);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_NewName_SetsNormalizedName()
    {
        var talent = await _sut.FindOrCreateTalentAsync("津田 健次郎");

        // スペース除去・小文字変換
        Assert.Equal("津田健次郎", talent.NormalizedName);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_ExistingNormalizedName_ReturnsExistingTalent()
    {
        // 既存タレント（表示名「津田健次郎」、normalized「津田健次郎」）
        var existing = new Talent
        {
            DisplayName = "津田健次郎",
            NormalizedName = "津田健次郎",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(existing);
        await _db.SaveChangesAsync();

        // 別表記で検索（スペースあり → 正規化後は同じ）
        var talent = await _sut.FindOrCreateTalentAsync("津田 健次郎");

        // 既存レコードを返し新規作成しない
        Assert.Equal(existing.Id, talent.Id);
        Assert.Equal("津田健次郎", talent.DisplayName); // 表示名は最初の登録表記のまま
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_ExistingTalent_DoesNotCreateDuplicate()
    {
        var existing = new Talent
        {
            DisplayName = "テスト",
            NormalizedName = "テスト",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(existing);
        await _db.SaveChangesAsync();

        await _sut.FindOrCreateTalentAsync("テスト");
        await _sut.FindOrCreateTalentAsync("テスト");

        var count = await _db.Talents.CountAsync(t => t.NormalizedName == "テスト");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_DisplayNameIsFirstRegisteredName()
    {
        // 最初に「津田健次郎」で登録
        await _sut.FindOrCreateTalentAsync("津田健次郎");

        // 2回目は別表記で呼ぶ → 表示名は最初の登録表記のまま
        var result = await _sut.FindOrCreateTalentAsync("津田 健次郎");

        Assert.Equal("津田健次郎", result.DisplayName);
    }

    // ── FindOrCreateTalentAsync: 境界値 ─────────────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_FullWidthAlpha_NormalizesToHalfWidth()
    {
        var talent = await _sut.FindOrCreateTalentAsync("ＬＯＶＥ");

        // NFKC 正規化で全角 → 半角・小文字
        Assert.Equal("love", talent.NormalizedName);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_DifferentSpacingsSameNormalized_SameRecord()
    {
        // 半角スペースあり
        await _sut.FindOrCreateTalentAsync("津田 健次郎");
        // 全角スペースあり → 同じ normalized_name
        var result = await _sut.FindOrCreateTalentAsync("津田　健次郎");

        var count = await _db.Talents.CountAsync();
        Assert.Equal(1, count);
        Assert.Equal("津田健次郎", result.NormalizedName);
    }
}
