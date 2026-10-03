namespace OshikatsuApi.DTOs;

/// <summary>一括登録API: 登録結果レスポンス</summary>
public class BatchResultResponseDto
{
    /// <summary>受信した件数（リクエスト内の Items 数）</summary>
    public int ReceivedCount { get; set; }

    /// <summary>新規保存した件数</summary>
    public int SavedCount { get; set; }

    /// <summary>重複によりスキップした件数（DB既存 + リクエスト内重複の合計）</summary>
    public int SkippedCount { get; set; }
}
