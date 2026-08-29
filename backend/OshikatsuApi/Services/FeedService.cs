using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class FeedService : IFeedService
{
    private readonly AppDbContext _db;

    public FeedService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<NewsItemResponseDto>> GetNewsAsync(
        int userId, int page, int pageSize, int? oshiId = null)
    {
        // ユーザーの推し ID 一覧を取得（サブクエリよりも InMemory・SQL 両対応しやすい）
        var oshiIds = await _db.Oshis
            .Where(o => o.UserId == userId)
            .Select(o => o.Id)
            .ToListAsync();

        // oshiId フィルタが指定された場合、ユーザーの推しに含まれるか検証して絞り込む
        if (oshiId.HasValue)
        {
            if (!oshiIds.Contains(oshiId.Value))
            {
                return new PaginatedResponseDto<NewsItemResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                };
            }
            oshiIds = [oshiId.Value];
        }

        var query = _db.NewsItems
            .Where(n => oshiIds.Contains(n.OshiId))
            .OrderByDescending(n => n.CollectedAt);

        var total = await query.CountAsync();
        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 推し名を付与するために Oshi を一括ロード
        var oshiNameMap = await _db.Oshis
            .Where(o => oshiIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name);

        var items = rawItems.Select(n => new NewsItemResponseDto
        {
            Id = n.Id,
            OshiId = n.OshiId,
            OshiName = oshiNameMap.GetValueOrDefault(n.OshiId) ?? string.Empty,
            Title = n.Title,
            Summary = n.Summary,
            SourceUrl = n.SourceUrl,
            PublishedAt = n.PublishedAt,
            IsPublishedAtEstimated = n.IsPublishedAtEstimated,
            CollectedAt = n.CollectedAt,
        }).ToList();

        return new PaginatedResponseDto<NewsItemResponseDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <inheritdoc />
    public async Task<PaginatedResponseDto<ProgramAppearanceResponseDto>> GetProgramsAsync(
        int userId, int page, int pageSize, int? oshiId = null)
    {
        var oshiIds = await _db.Oshis
            .Where(o => o.UserId == userId)
            .Select(o => o.Id)
            .ToListAsync();

        // oshiId フィルタが指定された場合、ユーザーの推しに含まれるか検証して絞り込む
        if (oshiId.HasValue)
        {
            if (!oshiIds.Contains(oshiId.Value))
            {
                return new PaginatedResponseDto<ProgramAppearanceResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                };
            }
            oshiIds = [oshiId.Value];
        }

        var query = _db.ProgramAppearances
            .Where(p => oshiIds.Contains(p.OshiId))
            .OrderByDescending(p => p.CollectedAt);

        var total = await query.CountAsync();
        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var oshiNameMap = await _db.Oshis
            .Where(o => oshiIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name);

        var items = rawItems.Select(p => new ProgramAppearanceResponseDto
        {
            Id = p.Id,
            OshiId = p.OshiId,
            OshiName = oshiNameMap.GetValueOrDefault(p.OshiId) ?? string.Empty,
            ProgramName = p.ProgramName,
            Broadcaster = p.Broadcaster,
            AiredAt = p.AiredAt,
            IsAiredAtEstimated = p.IsAiredAtEstimated,
            SourceUrl = p.SourceUrl,
            CollectedAt = p.CollectedAt,
        }).ToList();

        return new PaginatedResponseDto<ProgramAppearanceResponseDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }
}
