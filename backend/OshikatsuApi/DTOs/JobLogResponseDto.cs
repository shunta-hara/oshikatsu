namespace OshikatsuApi.DTOs;

/// <summary>ジョブ実行ログの単一エントリレスポンス</summary>
public class JobLogResponseDto
{
    public int Id { get; set; }

    /// <summary>ジョブ種別: "news" または "program"</summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>バッチが実行された日時</summary>
    public DateTimeOffset ExecutedAt { get; set; }

    /// <summary>正常に保存できたアイテム数</summary>
    public int SuccessCount { get; set; }

    /// <summary>送信に失敗したアイテム数</summary>
    public int FailureCount { get; set; }

    /// <summary>失敗理由（失敗した場合のみ設定）</summary>
    public string? FailureReason { get; set; }

    /// <summary>エラーメッセージ（失敗した場合のみ設定）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>対象タレントID（タレント別収集ログの場合に設定）</summary>
    public int? TalentId { get; set; }

    /// <summary>レコード作成日時</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
