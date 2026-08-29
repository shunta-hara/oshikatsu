using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class OshiServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly OshiService _sut;

    public OshiServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new OshiService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── CreateAsync: 正常系 ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidInput_ReturnsOshiResponseDto()
    {
        var result = await _sut.CreateAsync(userId: 1, name: "鈴木一郎");

        Assert.NotNull(result);
        Assert.Equal("鈴木一郎", result.Name);
    }

    [Fact]
    public async Task CreateAsync_ValidInput_SavesOshiToDb()
    {
        await _sut.CreateAsync(userId: 1, name: "鈴木一郎");

        var saved = await _db.Oshis.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(1, saved!.UserId);
        Assert.Equal("鈴木一郎", saved.Name);
    }

    [Fact]
    public async Task CreateAsync_ReturnedDto_HasCorrectId()
    {
        var result = await _sut.CreateAsync(userId: 1, name: "田中花子");

        var saved = await _db.Oshis.FirstAsync();
        Assert.Equal(saved.Id, result.Id);
    }

    // ── CreateAsync: 境界値 ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_At19Oshis_Succeeds()
    {
        await SeedOshis(userId: 1, count: 19);

        var result = await _sut.CreateAsync(userId: 1, name: "20件目の推し");

        Assert.NotNull(result);
        Assert.Equal("20件目の推し", result.Name);
    }

    [Fact]
    public async Task CreateAsync_At20Oshis_ThrowsOshiLimitExceededException()
    {
        await SeedOshis(userId: 1, count: 20);

        await Assert.ThrowsAsync<OshiLimitExceededException>(() =>
            _sut.CreateAsync(userId: 1, name: "21件目の推し"));
    }

    [Fact]
    public async Task CreateAsync_LimitIsPerUser_OtherUsersCountDoesNotAffect()
    {
        // ユーザー2がすでに20件登録していても、ユーザー1の登録は影響を受けない
        await SeedOshis(userId: 2, count: 20);

        var result = await _sut.CreateAsync(userId: 1, name: "ユーザー1の推し");

        Assert.NotNull(result);
    }

    // ── GetAllByUserAsync: 正常系 ────────────────────────────────────────────

    [Fact]
    public async Task GetAllByUserAsync_ReturnsOnlyOwnOshis()
    {
        await SeedOshis(userId: 1, count: 3);
        await SeedOshis(userId: 2, count: 5);

        var result = await _sut.GetAllByUserAsync(userId: 1);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.NotNull(r.Name));
    }

    [Fact]
    public async Task GetAllByUserAsync_ReturnsOrderedByCreatedAt()
    {
        // 時間差を持たせて登録
        var now = DateTime.UtcNow;
        _db.Oshis.Add(new Oshi { UserId = 1, Name = "B", CreatedAt = now.AddMinutes(2), UpdatedAt = now });
        _db.Oshis.Add(new Oshi { UserId = 1, Name = "A", CreatedAt = now.AddMinutes(1), UpdatedAt = now });
        _db.Oshis.Add(new Oshi { UserId = 1, Name = "C", CreatedAt = now.AddMinutes(3), UpdatedAt = now });
        await _db.SaveChangesAsync();

        var result = await _sut.GetAllByUserAsync(userId: 1);

        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0].Name);
        Assert.Equal("B", result[1].Name);
        Assert.Equal("C", result[2].Name);
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
    public async Task DeleteAsync_OwnOshi_RemovesFromDb()
    {
        var oshi = await CreateOshi(userId: 1, name: "削除対象");

        await _sut.DeleteAsync(userId: 1, oshiId: oshi.Id);

        var remaining = await _db.Oshis.FindAsync(oshi.Id);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task DeleteAsync_OwnOshi_DoesNotDeleteOtherOshis()
    {
        var target = await CreateOshi(userId: 1, name: "削除対象");
        var other = await CreateOshi(userId: 1, name: "残す推し");

        await _sut.DeleteAsync(userId: 1, oshiId: target.Id);

        var remaining = await _db.Oshis.FindAsync(other.Id);
        Assert.NotNull(remaining);
    }

    // ── DeleteAsync: 異常系 ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_NotExistingOshi_ThrowsOshiNotFoundException()
    {
        await Assert.ThrowsAsync<OshiNotFoundException>(() =>
            _sut.DeleteAsync(userId: 1, oshiId: 9999));
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersOshi_ThrowsOshiAccessDeniedException()
    {
        var oshi = await CreateOshi(userId: 2, name: "他ユーザーの推し");

        await Assert.ThrowsAsync<OshiAccessDeniedException>(() =>
            _sut.DeleteAsync(userId: 1, oshiId: oshi.Id));
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersOshi_DoesNotDeleteIt()
    {
        var oshi = await CreateOshi(userId: 2, name: "他ユーザーの推し");

        try
        {
            await _sut.DeleteAsync(userId: 1, oshiId: oshi.Id);
        }
        catch (OshiAccessDeniedException) { /* expected */ }

        var still = await _db.Oshis.FindAsync(oshi.Id);
        Assert.NotNull(still);
    }

    // ── ヘルパー ────────────────────────────────────────────────────────────

    private async Task SeedOshis(int userId, int count)
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            _db.Oshis.Add(new Oshi
            {
                UserId = userId,
                Name = $"推し_{userId}_{i}",
                CreatedAt = now.AddSeconds(i),
                UpdatedAt = now,
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task<Oshi> CreateOshi(int userId, string name)
    {
        var now = DateTime.UtcNow;
        var oshi = new Oshi
        {
            UserId = userId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Oshis.Add(oshi);
        await _db.SaveChangesAsync();
        return oshi;
    }
}
