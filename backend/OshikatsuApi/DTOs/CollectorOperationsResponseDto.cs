namespace OshikatsuApi.DTOs;

/// <summary>失敗中のタレントの状態情報</summary>
public class FailingTalentStatusDto
{
    /// <summary>タレントID</summary>
    public int TalentId { get; set; }

    /// <summary>タレント表示名</summary>
    public string TalentName { get; set; } = string.Empty;

    /// <summary>現在の連続失敗回数（最新ジョブから連続している失敗の数）</summary>
    public int ConsecutiveFailureCount { get; set; }
}

/// <summary>GET /api/v1/collector/operations のレスポンス</summary>
public class CollectorOperationsResponseDto
{
    /// <summary>直近24時間に実行されたジョブの一覧（最新順）</summary>
    public IReadOnlyList<JobLogResponseDto> RecentJobs { get; set; } = [];

    /// <summary>全体収集ジョブ（タレント指定なし）が現在失敗中かどうか</summary>
    public bool IsOverallJobFailing { get; set; }

    /// <summary>現在連続失敗中のタレント一覧（連続失敗回数の降順）</summary>
    public IReadOnlyList<FailingTalentStatusDto> FailingTalents { get; set; } = [];
}
