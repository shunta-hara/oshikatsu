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
    private readonly ITalentService _talentService;

    public OshiService(AppDbContext db, ITalentService talentService)
    {
        _db = db;
        _talentService = talentService;
    }

    /// <inheritdoc />
    public async Task<OshiResponseDto> CreateAsync(int userId, string name)
    {
        // 上限チェック: user_oshis テーブルの件数で判断する
        var count = await _db.UserOshis.CountAsync(o => o.UserId == userId);
        if (count >= MaxOshisPerUser)
        {
            throw new OshiLimitExceededException();
        }

        // タレントを検索または新規作成する（FEAT-038 の正規化ロジックを内包）
        var talent = await _talentService.FindOrCreateTalentAsync(name);

        // 同一ユーザーが同じタレントを既に登録済みなら 409
        var alreadyExists = await _db.UserOshis
            .AnyAsync(o => o.UserId == userId && o.TalentId == talent.Id);
        if (alreadyExists)
        {
            throw new OshiDuplicateException();
        }

        var now = DateTime.UtcNow;
        var userOshi = new UserOshi
        {
            UserId = userId,
            TalentId = talent.Id,
            CreatedAt = now,
        };

        _db.UserOshis.Add(userOshi);
        await _db.SaveChangesAsync();

        return new OshiResponseDto
        {
            Id = userOshi.Id,
            Name = talent.DisplayName,
            CreatedAt = userOshi.CreatedAt,
            TalentId = talent.Id,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OshiResponseDto>> GetAllByUserAsync(int userId)
    {
        return await _db.UserOshis
            .Where(o => o.UserId == userId)
            .Include(o => o.Talent)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new OshiResponseDto
            {
                Id = o.Id,
                Name = o.Talent.DisplayName,
                CreatedAt = o.CreatedAt,
            })
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int userId, int userOshiId)
    {
        var userOshi = await _db.UserOshis.FindAsync(userOshiId);

        if (userOshi is null)
        {
            throw new OshiNotFoundException();
        }

        if (userOshi.UserId != userId)
        {
            throw new OshiAccessDeniedException();
        }

        // UserOshi のみ削除。Talent・NewsItem・ProgramAppearance は残す（FEAT-039）
        _db.UserOshis.Remove(userOshi);
        await _db.SaveChangesAsync();
    }
}
