using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class InternalCollectorService : IInternalCollectorService
{
    private readonly AppDbContext _db;

    public InternalCollectorService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<InternalNewsResponseDto> CreateNewsItemAsync(CreateNewsItemRequestDto dto)
    {
        // 同一ソースURLが既に存在する場合は無視（冪等）
        var existing = await _db.NewsItems
            .FirstOrDefaultAsync(n => n.SourceUrl == dto.SourceUrl && n.OshiId == dto.OshiId);

        if (existing is not null)
        {
            return ToNewsDto(existing, wasDuplicate: true);
        }

        // 公開日時が未取得の場合は収集日時で代替し、推定フラグを立てる
        var isEstimated = dto.PublishedAt is null;
        var publishedAt = dto.PublishedAt ?? dto.CollectedAt;

        var now = DateTimeOffset.UtcNow;
        var item = new NewsItem
        {
            OshiId = dto.OshiId,
            Title = dto.Title,
            Summary = dto.Summary,
            SourceUrl = dto.SourceUrl,
            PublishedAt = publishedAt,
            IsPublishedAtEstimated = isEstimated,
            CollectedAt = dto.CollectedAt,
            CreatedAt = now,
        };

        _db.NewsItems.Add(item);
        await _db.SaveChangesAsync();

        return ToNewsDto(item, wasDuplicate: false);
    }

    /// <inheritdoc />
    public async Task<InternalProgramResponseDto> CreateProgramAppearanceAsync(CreateProgramAppearanceRequestDto dto)
    {
        // 同一ソースURLが既に存在する場合は無視（冪等）
        var existing = await _db.ProgramAppearances
            .FirstOrDefaultAsync(p => p.SourceUrl == dto.SourceUrl && p.OshiId == dto.OshiId);

        if (existing is not null)
        {
            return ToProgramDto(existing, wasDuplicate: true);
        }

        // 放送日時が未取得の場合は収集日時で代替し、推定フラグを立てる
        var isEstimated = dto.AiredAt is null;
        var airedAt = dto.AiredAt ?? dto.CollectedAt;

        var now = DateTimeOffset.UtcNow;
        var item = new ProgramAppearance
        {
            OshiId = dto.OshiId,
            ProgramName = dto.ProgramName,
            Broadcaster = dto.Broadcaster,
            AiredAt = airedAt,
            IsAiredAtEstimated = isEstimated,
            SourceUrl = dto.SourceUrl,
            CollectedAt = dto.CollectedAt,
            CreatedAt = now,
        };

        _db.ProgramAppearances.Add(item);
        await _db.SaveChangesAsync();

        return ToProgramDto(item, wasDuplicate: false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InternalOshiDto>> GetAllOshisAsync()
    {
        return await _db.Oshis
            .OrderBy(o => o.Id)
            .Select(o => new InternalOshiDto { Id = o.Id, Name = o.Name })
            .ToListAsync();
    }

    private static InternalNewsResponseDto ToNewsDto(NewsItem item, bool wasDuplicate) => new()
    {
        Id = item.Id,
        OshiId = item.OshiId,
        Title = item.Title,
        SourceUrl = item.SourceUrl,
        PublishedAt = item.PublishedAt,
        IsPublishedAtEstimated = item.IsPublishedAtEstimated,
        CollectedAt = item.CollectedAt,
        CreatedAt = item.CreatedAt,
        WasDuplicate = wasDuplicate,
    };

    private static InternalProgramResponseDto ToProgramDto(ProgramAppearance item, bool wasDuplicate) => new()
    {
        Id = item.Id,
        OshiId = item.OshiId,
        ProgramName = item.ProgramName,
        Broadcaster = item.Broadcaster,
        AiredAt = item.AiredAt,
        IsAiredAtEstimated = item.IsAiredAtEstimated,
        SourceUrl = item.SourceUrl,
        CollectedAt = item.CollectedAt,
        CreatedAt = item.CreatedAt,
        WasDuplicate = wasDuplicate,
    };
}
