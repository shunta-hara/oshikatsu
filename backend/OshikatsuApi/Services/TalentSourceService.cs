using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class TalentSourceService : ITalentSourceService
{
    private readonly AppDbContext _db;

    public TalentSourceService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TalentSourceResponseDto>> GetSourcesAsync(int? talentId = null)
    {
        var query = _db.TalentSources.AsQueryable();
        if (talentId.HasValue)
            query = query.Where(s => s.TalentId == talentId.Value);

        return await query
            .OrderBy(s => s.TalentId)
            .ThenBy(s => s.SourceType)
            .ThenBy(s => s.Id)
            .Select(s => ToDto(s))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<TalentSourceResponseDto> CreateSourceAsync(CreateTalentSourceRequestDto dto)
    {
        // タレント存在確認
        var talentExists = await _db.Talents.AnyAsync(t => t.Id == dto.TalentId);
        if (!talentExists)
            throw new TalentNotFoundException();

        // 重複チェック
        var duplicate = await _db.TalentSources.AnyAsync(s =>
            s.TalentId == dto.TalentId &&
            s.SourceType == dto.SourceType &&
            s.SourceId == dto.SourceId);
        if (duplicate)
            throw new DuplicateTalentSourceException();

        var entity = new TalentSource
        {
            TalentId = dto.TalentId,
            SourceType = dto.SourceType,
            SourceId = dto.SourceId,
            IsEnabled = dto.IsEnabled,
            CreatedAt = DateTime.UtcNow,
        };

        _db.TalentSources.Add(entity);
        await _db.SaveChangesAsync();

        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSourceAsync(int id)
    {
        var entity = await _db.TalentSources.FindAsync(id);
        if (entity is null)
            return false;

        _db.TalentSources.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    private static TalentSourceResponseDto ToDto(TalentSource s) => new()
    {
        Id = s.Id,
        TalentId = s.TalentId,
        SourceType = s.SourceType,
        SourceId = s.SourceId,
        IsEnabled = s.IsEnabled,
        CreatedAt = s.CreatedAt,
    };
}
