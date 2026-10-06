namespace OshikatsuApi.Services;

/// <summary>各ユーザーへの Web Push 通知送信ジョブのインターフェース</summary>
public interface IPushNotificationService
{
    /// <summary>
    /// 直近の通知送信以降に新着アイテムがある全ユーザーへ Push 通知を送信する。
    /// </summary>
    /// <returns>送信成功・失敗・スキップ件数をまとめた結果</returns>
    Task<PushNotifyResult> NotifyAllUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 全購読者へカスタムメッセージをブロードキャスト送信する（DB容量警告等）。
    /// </summary>
    /// <param name="message">通知本文に使用するメッセージ</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>送信成功・失敗件数をまとめた結果（SkipCount は常に 0）</returns>
    Task<PushNotifyResult> BroadcastMessageAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>Push 通知ジョブの実行結果</summary>
/// <param name="SuccessCount">Push 送信成功した購読数</param>
/// <param name="FailureCount">Push 送信失敗した購読数（410 Gone 削除を除く）</param>
/// <param name="SkipCount">新着なしでスキップしたユーザー数</param>
/// <param name="GoneRemovedCount">410 Gone で削除した購読数</param>
public record PushNotifyResult(int SuccessCount, int FailureCount, int SkipCount, int GoneRemovedCount)
{
    /// <summary>収集失敗警告 Push を送信した購読数</summary>
    public int WarningSentCount { get; init; } = 0;
}
