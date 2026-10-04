using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;
using OshikatsuApi.Entities;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class TalentService : ITalentService
{
    private readonly AppDbContext _db;

    public TalentService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Talent> FindOrCreateTalentAsync(string name)
    {
        var normalizedName = TalentNameNormalizer.Normalize(name);

        // 同じ normalized_name を持つ既存タレントを検索する
        var existing = await _db.Talents
            .FirstOrDefaultAsync(t => t.NormalizedName == normalizedName);

        if (existing is not null)
            return existing;

        // 存在しない場合のみ新規作成する。
        // display_name は入力値をそのまま保持する（以降の呼び出しでは変更しない）。
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
}
