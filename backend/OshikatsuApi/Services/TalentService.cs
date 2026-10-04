using Microsoft.EntityFrameworkCore;
using Npgsql;
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

        try
        {
            await _db.SaveChangesAsync();
            return talent;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 並行 INSERT でユニーク制約違反が発生した場合は既存レコードを再 SELECT して返す
            _db.Entry(talent).State = EntityState.Detached;
            var raceWinner = await _db.Talents
                .FirstOrDefaultAsync(t => t.NormalizedName == normalizedName);
            return raceWinner
                ?? throw new InvalidOperationException(
                    $"Talent with normalized_name '{normalizedName}' not found after UNIQUE constraint violation.");
        }
    }

    /// <summary>
    /// DB 更新例外がユニーク制約違反（PostgreSQL エラーコード 23505）かどうかを判定する。
    /// InMemory プロバイダーでは PostgresException が発生しないため false を返す。
    /// </summary>
    protected virtual bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";
}
