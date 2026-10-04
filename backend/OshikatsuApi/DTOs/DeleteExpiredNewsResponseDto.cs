namespace OshikatsuApi.DTOs;

/// <summary>古い記事一括削除の結果</summary>
public class DeleteExpiredNewsResponseDto
{
    /// <summary>削除した記事件数</summary>
    public int DeletedCount { get; set; }
}
