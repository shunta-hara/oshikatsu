using System.Data;
using Microsoft.EntityFrameworkCore;
using OshikatsuApi.Data;

namespace OshikatsuApi.Services;

/// <summary>
/// PostgreSQL の pg_database_size() を使用して DB 使用量を取得するサービス（FEAT-053）。
/// InMemory プロバイダや接続失敗時は null を返す。
/// </summary>
public class DbSizeService : IDbSizeService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DbSizeService> _logger;

    public DbSizeService(AppDbContext db, ILogger<DbSizeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<long?> GetDbSizeBytesAsync()
    {
        try
        {
            var connection = _db.Database.GetDbConnection();
            var wasOpen = connection.State == ConnectionState.Open;
            if (!wasOpen)
                await connection.OpenAsync();

            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT pg_database_size(current_database())";
                var result = await cmd.ExecuteScalarAsync();
                return result is null or DBNull ? null : Convert.ToInt64(result);
            }
            finally
            {
                if (!wasOpen)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DB 使用量の取得に失敗しました（InMemory プロバイダまたは接続エラー）");
            return null;
        }
    }
}
