using Microsoft.EntityFrameworkCore;
using NSubstitute;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>
/// OshiService のテスト（FEAT-039: タレントモデル対応後）。
/// OshiService は UserOshis + TalentService を使用するため、
/// ITalentService をモック化して各テストを構成する。
/// </summary>
public class OshiServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ITalentService _talentService;
    private readonly OshiService _sut;

    public OshiServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _talentService = Substitute.For<ITalentService>();
        _sut = new OshiService(_db, _talentService);
    }

    public void Dispose() => _db.Dispose();

    // ── CreateAsync: 正常系 ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidInput_ReturnsOshiResponseDto()
    {
        var talent = await SeedTalent("鈴木一郎");
        _talentService.FindOrCreateTalentAsync("鈴木一郎").Returns(talent);

        var result = await _sut.CreateAsync(userId: 1, name: "鈴木一郎");

        Assert.NotNull(result);
        Assert.Equal("鈴木一郎", result.Name);
    }

    [Fact]
    public async Task CreateAsync_ValidInput_SavesUserOshiToDb()
    {
        var talent = await SeedTalent("鈴木一郎");
        _talentService.FindOrCreateTalentAsync("鈴木一郎").Returns(talent);

        await _sut.CreateAsync(userId: 1, name: "鈴木一郎");

        var saved = await _db.UserOshis.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(1, saved!.UserId);
        Assert.Equal(talent.Id, saved.TalentId);
    }

    [Fact]
    public async Task CreateAsync_ReturnedDto_IdIsUserOshiId()
    {
        var talent = await SeedTalent("田中花子");
        _talentService.FindOrCreateTalentAsync("田中花子").Returns(talent);

        var result = await _sut.CreateAsync(userId: 1, name: "田中花子");

        var saved = await _db.UserOshis.FirstAsync();
        Assert.Equal(saved.Id, result.Id);
    }

    [Fact]
    public async Task CreateAsync_ReturnedDto_NameIsTalentDisplayName()
    {
        var talent = await SeedTalent("津田健次郎");
        _talentService.FindOrCreateTalentAsync("津田 健次郎").Returns(talent); // 入力は別表記

        var result = await _sut.CreateAsync(userId: 1, name: "津田 健次郎");

        // 返される name は talent.DisplayName（最初に登録した表記）
        Assert.Equal("津田健次郎", result.Name);
    }

    [Fact]
    public async Task CreateAsync_ReturnedDto_HasTalentId()
    {
        var talent = await SeedTalent("テスト太郎");
        _talentService.FindOrCreateTalentAsync("テスト太郎").Returns(talent);

        var result = await _sut.CreateAsync(userId: 1, name: "テスト太郎");

        Assert.Equal(talent.Id, result.TalentId);
    }

    // ── CreateAsync: 境界値・異常系 ─────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_At19UserOshis_Succeeds()
    {
        await SeedUserOshis(userId: 1, count: 19);
        var talent = await SeedTalent("20件目の推し");
        _talentService.FindOrCreateTalentAsync("20件目の推し").Returns(talent);

        var result = await _sut.CreateAsync(userId: 1, name: "20件目の推し");

        Assert.NotNull(result);
        Assert.Equal("20件目の推し", result.Name);
    }

    [Fact]
    public async Task CreateAsync_At20UserOshis_ThrowsOshiLimitExceededException()
    {
        await SeedUserOshis(userId: 1, count: 20);

        await Assert.ThrowsAsync<OshiLimitExceededException>(() =>
            _sut.CreateAsync(userId: 1, name: "21件目の推し"));
    }

    [Fact]
    public async Task CreateAsync_LimitIsPerUser_OtherUsersCountDoesNotAffect()
    {
        // ユーザー2がすでに20件登録していても、ユーザー1の登録は影響を受けない
        await SeedUserOshis(userId: 2, count: 20);
        var talent = await SeedTalent("ユーザー1の推し");
        _talentService.FindOrCreateTalentAsync("ユーザー1の推し").Returns(talent);

        var result = await _sut.CreateAsync(userId: 1, name: "ユーザー1の推し");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task CreateAsync_SameUserSameTalent_ThrowsOshiDuplicateException()
    {
        // FEAT-039: 同一ユーザーが同名タレントを再登録すると 409
        var talent = await SeedTalent("重複テスト");
        _talentService.FindOrCreateTalentAsync("重複テスト").Returns(talent);
        // 既にこのユーザーがこのタレントを登録済み
        _db.UserOshis.Add(new UserOshi
        {
            UserId = 1,
            TalentId = talent.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<OshiDuplicateException>(() =>
            _sut.CreateAsync(userId: 1, name: "重複テスト"));
    }

    [Fact]
    public async Task CreateAsync_DifferentUserSameTalent_Succeeds()
    {
        // FEAT-039: 異なるユーザーが同名タレントを登録しても成功する
        var talent = await SeedTalent("共有タレント");
        _talentService.FindOrCreateTalentAsync("共有タレント").Returns(talent);
        // ユーザー2は既に登録済み
        _db.UserOshis.Add(new UserOshi
        {
            UserId = 2,
            TalentId = talent.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        // ユーザー1が同じタレントを登録
        var result = await _sut.CreateAsync(userId: 1, name: "共有タレント");

        Assert.NotNull(result);
        Assert.Equal("共有タレント", result.Name);
    }

    // ── GetAllByUserAsync: 正常系 ────────────────────────────────────────────

    [Fact]
    public async Task GetAllByUserAsync_ReturnsOnlyOwnOshis()
    {
        await SeedUserOshis(userId: 1, count: 3);
        await SeedUserOshis(userId: 2, count: 5);

        var result = await _sut.GetAllByUserAsync(userId: 1);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.NotNull(r.Name));
    }

    [Fact]
    public async Task GetAllByUserAsync_ReturnsOrderedByCreatedAt()
    {
        // 時間差を持たせて登録
        var now = DateTime.UtcNow;
        var talentA = await SeedTalent("A");
        var talentB = await SeedTalent("B");
        var talentC = await SeedTalent("C");
        _db.UserOshis.Add(new UserOshi { UserId = 1, TalentId = talentB.Id, CreatedAt = now.AddMinutes(2) });
        _db.UserOshis.Add(new UserOshi { UserId = 1, TalentId = talentA.Id, CreatedAt = now.AddMinutes(1) });
        _db.UserOshis.Add(new UserOshi { UserId = 1, TalentId = talentC.Id, CreatedAt = now.AddMinutes(3) });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllByUserAsync(userId: 1);

        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0].Name);
        Assert.Equal("B", result[1].Name);
        Assert.Equal("C", result[2].Name);
    }

    [Fact]
    public async Task GetAllByUserAsync_ReturnsUserOshiIdAsId()
    {
        // FEAT-039: 返される id は user_oshis.id
        var talent = await SeedTalent("テスト");
        var userOshi = new UserOshi { UserId = 1, TalentId = talent.Id, CreatedAt = DateTime.UtcNow };
        _db.UserOshis.Add(userOshi);
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllByUserAsync(userId: 1);

        Assert.Single(result);
        Assert.Equal(userOshi.Id, result[0].Id);
    }

    // ── GetAllByUserAsync: 境界値 ────────────────────────────────────────────

    [Fact]
    public async Task GetAllByUserAsync_NoOshis_ReturnsEmptyList()
    {
        var result = await _sut.GetAllByUserAsync(userId: 1);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── DeleteAsync: 正常系 ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_OwnUserOshi_RemovesFromDb()
    {
        var talent = await SeedTalent("削除対象");
        var userOshi = await CreateUserOshi(userId: 1, talent: talent);

        await _sut.DeleteAsync(userId: 1, userOshiId: userOshi.Id);

        var remaining = await _db.UserOshis.FindAsync(userOshi.Id);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task DeleteAsync_OwnUserOshi_DoesNotDeleteOtherUserOshis()
    {
        var talentA = await SeedTalent("削除対象");
        var talentB = await SeedTalent("残す推し");
        var target = await CreateUserOshi(userId: 1, talent: talentA);
        var other = await CreateUserOshi(userId: 1, talent: talentB);

        await _sut.DeleteAsync(userId: 1, userOshiId: target.Id);

        var remaining = await _db.UserOshis.FindAsync(other.Id);
        Assert.NotNull(remaining);
    }

    [Fact]
    public async Task DeleteAsync_DeleteUserOshi_TalentStillExists()
    {
        // FEAT-039: UserOshi を削除しても Talent は残る
        var talent = await SeedTalent("タレント保持テスト");
        var userOshi = await CreateUserOshi(userId: 1, talent: talent);

        await _sut.DeleteAsync(userId: 1, userOshiId: userOshi.Id);

        var talentStillExists = await _db.Talents.FindAsync(talent.Id);
        Assert.NotNull(talentStillExists);
    }

    // ── DeleteAsync: 異常系 ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_NotExistingUserOshi_ThrowsOshiNotFoundException()
    {
        await Assert.ThrowsAsync<OshiNotFoundException>(() =>
            _sut.DeleteAsync(userId: 1, userOshiId: 9999));
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersUserOshi_ThrowsOshiAccessDeniedException()
    {
        var talent = await SeedTalent("他ユーザーの推し");
        var userOshi = await CreateUserOshi(userId: 2, talent: talent);

        await Assert.ThrowsAsync<OshiAccessDeniedException>(() =>
            _sut.DeleteAsync(userId: 1, userOshiId: userOshi.Id));
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersUserOshi_DoesNotDeleteIt()
    {
        var talent = await SeedTalent("他ユーザーの推し");
        var userOshi = await CreateUserOshi(userId: 2, talent: talent);

        try
        {
            await _sut.DeleteAsync(userId: 1, userOshiId: userOshi.Id);
        }
        catch (OshiAccessDeniedException) { /* expected */ }

        var still = await _db.UserOshis.FindAsync(userOshi.Id);
        Assert.NotNull(still);
    }

    // ── ヘルパー ────────────────────────────────────────────────────────────

    private async Task<Talent> SeedTalent(string name)
    {
        var normalizedName = OshikatsuApi.Services.TalentNameNormalizer.Normalize(name);
        var talent = new Talent
        {
            DisplayName = name,
            NormalizedName = normalizedName,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();
        return talent;
    }

    private async Task SeedUserOshis(int userId, int count)
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            var talentName = $"推し_{userId}_{i}";
            var talent = await SeedTalent(talentName);
            _db.UserOshis.Add(new UserOshi
            {
                UserId = userId,
                TalentId = talent.Id,
                CreatedAt = now.AddSeconds(i),
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task<UserOshi> CreateUserOshi(int userId, Talent talent)
    {
        var userOshi = new UserOshi
        {
            UserId = userId,
            TalentId = talent.Id,
            CreatedAt = DateTime.UtcNow,
        };
        _db.UserOshis.Add(userOshi);
        await _db.SaveChangesAsync();
        return userOshi;
    }
}
