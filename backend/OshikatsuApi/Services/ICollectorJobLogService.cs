using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <summary>収集失敗アラートの対象タレント情報</summary>
/// <param name="TalentId">タレントID</param>
/// <param name="TalentName">タレント表示名</param>
public record FailingTalentInfo(int TalentId, string TalentName);

/// <summary>収集失敗アラートのサマリ（連続3サイクル失敗が初めて検知された場合のみ非空）</summary>
/// <param name="IsOverallJobFailing">全体ジョブ（talent_id=null）が3サイクル連続失敗中（かつ4回目以降でない）</param>
/// <param name="FailingTalents">3サイクル連続失敗中のタレント一覧</param>
public record FailureAlertSummary(
    bool IsOverallJobFailing,
    IReadOnlyList<FailingTalentInfo> FailingTalents);

/// <summary>collector バッチのジョブ実行ログを管理するサービス</summary>
public interface ICollectorJobLogService
{
    /// <summary>
    /// ジョブ実行結果をDBに記録する。
    /// 同一ジョブタイプで連続3回 FailureCount &gt; 0 の場合、WARNING ログを出力する。
    /// </summary>
    Task<JobLogResponseDto> RecordJobLogAsync(CreateJobLogRequestDto dto);

    /// <summary>各ジョブタイプの最新実行状況を返す</summary>
    Task<CollectorStatusResponseDto> GetStatusAsync();

    /// <summary>
    /// 現在アクティブな収集失敗アラートを返す。
    /// 「アクティブ」= 直近3サイクルがすべて失敗かつ4サイクル目が失敗でない（初回検知）。
    /// 警告送信後にさらに連続失敗が続く場合は重複送信しない。
    /// </summary>
    Task<FailureAlertSummary> GetActiveFailureAlertsAsync();

    /// <summary>
    /// 運用パネル向けデータを返す。
    /// <list type="bullet">
    ///   <item>直近24時間のジョブ実行一覧（最新順・最大50件）</item>
    ///   <item>全体収集ジョブが現在失敗中かどうか</item>
    ///   <item>タレント別の現在連続失敗状況（連続失敗回数の降順）</item>
    /// </list>
    /// </summary>
    Task<CollectorOperationsResponseDto> GetOperationsDataAsync();
}
