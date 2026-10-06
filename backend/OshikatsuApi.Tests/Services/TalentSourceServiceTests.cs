using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

/// <summary>TalentSourceService のテスト（FEAT-048）</summary>
public class TalentSourceServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TalentSourceService _sut;

    public TalentSourceServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new TalentSourceService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private async Task<Talent> SeedTalentAsync(string name = "テストタレント")
    {
        var talent = new Talent
        {
            DisplayName = name,
            NormalizedName = name,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Talents.Add(talent);
        await _db.SaveChangesAsync();
        return talent;
    }

    // ── GetSourcesAsync: 正常系 ───────────────────────────────────────────────

    [Fact]
    public async Task GetSourcesAsync_NoFilter_ReturnsAll()
    {
        var talent = await SeedTalentAsync();
        _db.TalentSources.AddRange(
            new TalentSource { TalentId = talent.Id, SourceType = "youtube", SourceId = "UC_a", IsEnabled = true, CreatedAt = DateTime.UtcNow },
            new TalentSource { TalentId = talent.Id, SourceType = "x_post", SourceId = "@handle", IsEnabled = true, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetSourcesAsync(null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetSourcesAsync_WithTalentIdFilter_ReturnsOnlyMatching()
    {
        var talent1 = await SeedTalentAsync("タレント1");
        var talent2 = await SeedTalentAsync("タレント2");
        _db.TalentSources.AddRange(
            new TalentSource { TalentId = talent1.Id, SourceType = "youtube", SourceId = "UC_a", IsEnabled = true, CreatedAt = DateTime.UtcNow },
            new TalentSource { TalentId = talent2.Id, SourceType = "youtube", SourceId = "UC_b", IsEnabled = true, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var result = await _sut.GetSourcesAsync(talent1.Id);

        Assert.Single(result);
        Assert.Equal(talent1.Id, result[0].TalentId);
    }

    [Fact]
    public async Task GetSourcesAsync_EmptyDb_ReturnsEmptyList()
    {
        var result = await _sut.GetSourcesAsync(null);

        Assert.Empty(result);
    }

    // ── CreateSourceAsync: 正常系 ─────────────────────────────────────────────

    [Fact]
    public async Task CreateSourceAsync_ValidData_ReturnsTalentSourceResponseDto()
    {
        var talent = await SeedTalentAsync();
        var dto = new OshikatsuApi.DTOs.CreateTalentSourceRequestDto
        {
            TalentId = talent.Id,
            SourceType = "youtube",
            SourceId = "UC_test",
            IsEnabled = true,
        };

        var result = await _sut.CreateSourceAsync(dto);

        Assert.Equal(talent.Id, result.TalentId);
        Assert.Equal("youtube", result.SourceType);
        Assert.Equal("UC_test", result.SourceId);
        Assert.True(result.IsEnabled);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task CreateSourceAsync_ValidData_SavesRecordToDb()
    {
        var talent = await SeedTalentAsync();
        var dto = new OshikatsuApi.DTOs.CreateTalentSourceRequestDto
        {
            TalentId = talent.Id,
            SourceType = "youtube",
            SourceId = "UC_saved",
            IsEnabled = true,
        };

        await _sut.CreateSourceAsync(dto);

        var saved = await _db.TalentSources.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal("UC_saved", saved!.SourceId);
    }

    // ── CreateSourceAsync: 異常系 ─────────────────────────────────────────────

    [Fact]
    public async Task CreateSourceAsync_TalentNotFound_ThrowsTalentNotFoundException()
    {
        var dto = new OshikatsuApi.DTOs.CreateTalentSourceRequestDto
        {
            TalentId = 9999,
            SourceType = "youtube",
            SourceId = "UC_nope",
            IsEnabled = true,
        };

        await Assert.ThrowsAsync<TalentNotFoundException>(
            () => _sut.CreateSourceAsync(dto));
    }

    [Fact]
    public async Task CreateSourceAsync_DuplicateSource_ThrowsDuplicateTalentSourceException()
    {
        var talent = await SeedTalentAsync();
        var dto = new OshikatsuApi.DTOs.CreateTalentSourceRequestDto
        {
            TalentId = talent.Id,
            SourceType = "youtube",
            SourceId = "UC_dup",
            IsEnabled = true,
        };
        await _sut.CreateSourceAsync(dto);

        // 同一ソースを再度登録
        await Assert.ThrowsAsync<DuplicateTalentSourceException>(
            () => _sut.CreateSourceAsync(dto));
    }

    // ── DeleteSourceAsync: 正常系 ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteSourceAsync_Exists_ReturnsTrue()
    {
        var talent = await SeedTalentAsync();
        var entity = new TalentSource
        {
            TalentId = talent.Id,
            SourceType = "youtube",
            SourceId = "UC_del",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.TalentSources.Add(entity);
        await _db.SaveChangesAsync();

        var result = await _sut.DeleteSourceAsync(entity.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteSourceAsync_Exists_RemovesFromDb()
    {
        var talent = await SeedTalentAsync();
        var entity = new TalentSource
        {
            TalentId = talent.Id,
            SourceType = "youtube",
            SourceId = "UC_rm",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.TalentSources.Add(entity);
        await _db.SaveChangesAsync();
        var id = entity.Id;

        await _sut.DeleteSourceAsync(id);

        var after = await _db.TalentSources.FindAsync(id);
        Assert.Null(after);
    }

    // ── DeleteSourceAsync: 異常系 ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteSourceAsync_NotFound_ReturnsFalse()
    {
        var result = await _sut.DeleteSourceAsync(99999);

        Assert.False(result);
    }
}
