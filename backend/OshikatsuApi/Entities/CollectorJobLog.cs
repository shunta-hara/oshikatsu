namespace OshikatsuApi.Entities;

/// <summary>collector バッチの実行ログエントリ</summary>
public class CollectorJobLog
{
    public int Id { get; set; }

    /// <summary>ジョブ種別: "news" または "program"</summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>バッチが実行された日時（UTC）</summary>
    public DateTimeOffset ExecutedAt { get; set; }

    /// <summary>正常に保存できたアイテム数</summary>
    public int SuccessCount { get; set; }

    /// <summary>送信に失敗したアイテム数</summary>
    public int FailureCount { get; set; }

    /// <summary>レコード作成日時</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
