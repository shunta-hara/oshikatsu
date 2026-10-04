using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>TalentService の FindOrCreateTalentAsync をテストする</summary>
public class TalentServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TalentService _sut;

    public TalentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new TalentService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── 新規作成 ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_NewName_CreatesNewTalent()
    {
        var result = await _sut.FindOrCreateTalentAsync("鈴木一郎");

        Assert.NotNull(result);
        Assert.Equal("鈴木一郎", result.DisplayName);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_NewName_NormalizedNameIsNormalized()
    {
        var result = await _sut.FindOrCreateTalentAsync("津田 健次郎");

        // 正規化済み: スペース除去
        Assert.Equal("津田健次郎", result.NormalizedName);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_NewTalent_SavedToDb()
    {
        await _sut.FindOrCreateTalentAsync("鈴木一郎");

        var saved = await _db.Talents.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("鈴木一郎", saved!.DisplayName);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_NewTalent_HasPositiveId()
    {
        var result = await _sut.FindOrCreateTalentAsync("鈴木一郎");

        Assert.True(result.Id > 0);
    }

    // ── 同一名前の再登録（再利用） ────────────────────────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_SameName_ReturnsExistingTalent()
    {
        var first = await _sut.FindOrCreateTalentAsync("鈴木一郎");
        var second = await _sut.FindOrCreateTalentAsync("鈴木一郎");

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_SameName_DoesNotCreateDuplicate()
    {
        await _sut.FindOrCreateTalentAsync("鈴木一郎");
        await _sut.FindOrCreateTalentAsync("鈴木一郎");

        Assert.Equal(1, await _db.Talents.CountAsync());
    }

    // ── スペースありとなしで同一タレントを返す ───────────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_HalfWidthSpaceAndNoSpace_ReturnsSameTalent()
    {
        var withSpace = await _sut.FindOrCreateTalentAsync("津田 健次郎");
        var withoutSpace = await _sut.FindOrCreateTalentAsync("津田健次郎");

        Assert.Equal(withSpace.Id, withoutSpace.Id);
        Assert.Equal(1, await _db.Talents.CountAsync());
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_FullWidthSpaceAndHalfWidthSpace_ReturnsSameTalent()
    {
        var halfSpace = await _sut.FindOrCreateTalentAsync("津田 健次郎");
        var fullSpace = await _sut.FindOrCreateTalentAsync("津田　健次郎");

        Assert.Equal(halfSpace.Id, fullSpace.Id);
    }

    // ── display_name は最初の登録表記のまま変わらない ────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_SecondCallWithDifferentSpacing_DisplayNameUnchanged()
    {
        // 最初に「津田 健次郎」で登録
        await _sut.FindOrCreateTalentAsync("津田 健次郎");

        // 2回目はスペースなしで呼び出す
        var second = await _sut.FindOrCreateTalentAsync("津田健次郎");

        // display_name は最初の登録表記のまま
        Assert.Equal("津田 健次郎", second.DisplayName);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_MultipleCallsDifferentRepresentations_DisplayNameIsFirst()
    {
        await _sut.FindOrCreateTalentAsync("ＬＯＶＥ");
        var second = await _sut.FindOrCreateTalentAsync("love");

        // display_name は "ＬＯＶＥ" のまま（最初の登録表記）
        Assert.Equal("ＬＯＶＥ", second.DisplayName);
    }

    // ── 全角英数字の正規化 ────────────────────────────────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_FullWidthAlphaAndAscii_ReturnsSameTalent()
    {
        var fullWidth = await _sut.FindOrCreateTalentAsync("ＬＯＶＥ");
        var halfWidth = await _sut.FindOrCreateTalentAsync("love");

        Assert.Equal(fullWidth.Id, halfWidth.Id);
    }

    [Fact]
    public async Task FindOrCreateTalentAsync_FullWidthAlpha_NormalizedNameIsHalfWidthLower()
    {
        var result = await _sut.FindOrCreateTalentAsync("ＬＯＶＥ");

        Assert.Equal("love", result.NormalizedName);
    }

    // ── 異なる名前は別タレントを作成する ─────────────────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_DifferentNames_CreatesDifferentTalents()
    {
        var talent1 = await _sut.FindOrCreateTalentAsync("鈴木一郎");
        var talent2 = await _sut.FindOrCreateTalentAsync("田中花子");

        Assert.NotEqual(talent1.Id, talent2.Id);
        Assert.Equal(2, await _db.Talents.CountAsync());
    }

    // ── 複数ユーザーが同一名前を登録しても talent は1件だけ ─────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_MultipleUsersRegisterSameName_OnlyOneTalent()
    {
        // ユーザー1が登録
        var talentForUser1 = await _sut.FindOrCreateTalentAsync("津田健次郎");

        // ユーザー2が同じ名前を登録（サービス層は userId を受け取らず talent の検索のみ）
        var talentForUser2 = await _sut.FindOrCreateTalentAsync("津田健次郎");

        Assert.Equal(talentForUser1.Id, talentForUser2.Id);
        Assert.Equal(1, await _db.Talents.CountAsync());
    }

    // ── 境界値 ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindOrCreateTalentAsync_SingleCharName_CreatesNewTalent()
    {
        var result = await _sut.FindOrCreateTalentAsync("愛");

        Assert.NotNull(result);
        Assert.Equal("愛", result.DisplayName);
        Assert.Equal("愛", result.NormalizedName);
    }
}
