namespace OshikatsuApi.Services;

/// <summary>古い記事の自動削除を担うサービス</summary>
public interface INewsCleanupService
{
    /// <summary>
    /// 保持期間（90日）を超えた未ブックマーク記事を一括削除する。
    /// PublishedAt が null の場合は CollectedAt を基準にする。
    /// ブックマーク済みの記事は削除しない。
    /// </summary>
    /// <param name="batchSize">1回の実行で削除する最大件数。デフォルト 1000。</param>
    /// <returns>削除した件数</returns>
    Task<int> DeleteExpiredNewsAsync(int batchSize = 1000);
}
