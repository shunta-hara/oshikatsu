namespace OshikatsuApi.DTOs;

/// <summary>GET /api/v1/health のレスポンスDTO</summary>
public class HealthResponseDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    /// <summary>DB 使用量（バイト）。PostgreSQL 以外のプロバイダでは null</summary>
    public long? DbSizeBytes { get; set; }

    /// <summary>DB 使用量（MB、小数第2位まで）。PostgreSQL 以外のプロバイダでは null</summary>
    public double? DbSizeMb { get; set; }
}
