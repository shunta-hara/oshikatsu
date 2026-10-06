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

    /// <summary>失敗理由（失敗した場合のみ設定。例: "network_error", "parse_error"）</summary>
    public string? FailureReason { get; set; }

    /// <summary>エラーメッセージ（失敗した場合のみ設定）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>対象タレントID（タレント別収集ログの場合に設定、全体ジョブは null）</summary>
    public int? TalentId { get; set; }

    /// <summary>タレントナビゲーションプロパティ</summary>
    public Talent? Talent { get; set; }

    /// <summary>レコード作成日時</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
