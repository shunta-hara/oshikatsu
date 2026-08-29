namespace OshikatsuApi.DTOs;

/// <summary>GET /api/v1/collector/status のレスポンス</summary>
public class CollectorStatusResponseDto
{
    /// <summary>ニュース収集ジョブの最新実行ログ（未実行の場合は null）</summary>
    public JobLogResponseDto? LatestNewsJob { get; set; }

    /// <summary>番組収集ジョブの最新実行ログ（未実行の場合は null）</summary>
    public JobLogResponseDto? LatestProgramJob { get; set; }
}
