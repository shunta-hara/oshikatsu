namespace OshikatsuApi.DTOs;

/// <summary>Push 通知ジョブ実行結果レスポンス</summary>
public class PushNotifyResponseDto
{
    /// <summary>Push 送信成功した購読数</summary>
    public int SuccessCount { get; set; }

    /// <summary>Push 送信失敗した購読数（410 Gone 削除を除く）</summary>
    public int FailureCount { get; set; }

    /// <summary>新着なしでスキップしたユーザー数</summary>
    public int SkipCount { get; set; }

    /// <summary>410 Gone で削除した購読数</summary>
    public int GoneRemovedCount { get; set; }

    /// <summary>収集失敗警告 Push を送信した購読数</summary>
    public int WarningSentCount { get; set; }
}
