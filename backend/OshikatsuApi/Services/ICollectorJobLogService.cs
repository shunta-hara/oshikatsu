using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

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
}
