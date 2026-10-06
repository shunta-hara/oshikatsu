using System.ComponentModel.DataAnnotations;

namespace OshikatsuApi.DTOs;

/// <summary>collector がバッチ実行後に送信するジョブログ登録リクエスト</summary>
public class CreateJobLogRequestDto
{
    /// <summary>ジョブ種別: "news" または "program"</summary>
    [Required]
    [RegularExpression("^(news|program)$", ErrorMessage = "JobType は 'news' または 'program' でなければなりません。")]
    public string JobType { get; set; } = string.Empty;

    /// <summary>バッチが実行された日時（UTC）</summary>
    [Required]
    public DateTimeOffset ExecutedAt { get; set; }

    /// <summary>正常に保存できたアイテム数</summary>
    [Range(0, int.MaxValue)]
    public int SuccessCount { get; set; }

    /// <summary>送信に失敗したアイテム数</summary>
    [Range(0, int.MaxValue)]
    public int FailureCount { get; set; }

    /// <summary>失敗理由（失敗した場合のみ設定。例: "network_error", "parse_error"）</summary>
    public string? FailureReason { get; set; }

    /// <summary>エラーメッセージ（失敗した場合のみ設定）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>対象タレントID（タレント別収集ログの場合に設定、全体ジョブは null）</summary>
    public int? TalentId { get; set; }
}
