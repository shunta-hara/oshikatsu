namespace OshikatsuApi.Services;

/// <summary>DB 使用量を取得するサービスのインターフェース（FEAT-053）</summary>
public interface IDbSizeService
{
    /// <summary>
    /// 現在のDB使用量をバイト単位で返す。
    /// PostgreSQL 以外のプロバイダや接続失敗時は null を返す。
    /// </summary>
    Task<long?> GetDbSizeBytesAsync();
}
