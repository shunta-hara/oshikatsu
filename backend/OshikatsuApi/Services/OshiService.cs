using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.DTOs;
using OshikatsuApi.Entities;
using OshikatsuApi.Exceptions;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class OshiService : IOshiService
{
    private const int MaxOshisPerUser = 20;

    private readonly AppDbContext _db;

    public OshiService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<OshiResponseDto> CreateAsync(int userId, string name)
    {
        var count = await _db.Oshis.CountAsync(o => o.UserId == userId);
        if (count >= MaxOshisPerUser)
        {
            throw new OshiLimitExceededException();
        }

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

        return ToDto(oshi);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OshiResponseDto>> GetAllByUserAsync(int userId)
    {
        return await _db.Oshis
            .Where(o => o.UserId == userId)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new OshiResponseDto
            {
                Id = o.Id,
                Name = o.Name,
                CreatedAt = o.CreatedAt,
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int userId, int oshiId)
    {
        var oshi = await _db.Oshis.FindAsync(oshiId);

        if (oshi is null)
        {
            throw new OshiNotFoundException();
        }

        if (oshi.UserId != userId)
        {
            throw new OshiAccessDeniedException();
        }

        _db.Oshis.Remove(oshi);
        await _db.SaveChangesAsync();
    }

    private static OshiResponseDto ToDto(Oshi oshi) =>
        new() { Id = oshi.Id, Name = oshi.Name, CreatedAt = oshi.CreatedAt };
}
