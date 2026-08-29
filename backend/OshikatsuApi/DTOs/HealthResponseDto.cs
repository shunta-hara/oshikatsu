namespace OshikatsuApi.DTOs;

/// <summary>GET /api/v1/health のレスポンスDTO</summary>
public class HealthResponseDto
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
