using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>ヘルスチェックエンドポイント</summary>
[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private readonly IDbSizeService _dbSizeService;

    public HealthController(IDbSizeService dbSizeService)
    {
        _dbSizeService = dbSizeService;
    }

    /// <summary>
    /// サービスの稼働状態と DB 使用量を確認する（FEAT-053）
    /// </summary>
    /// <returns>稼働中なら 200 OK・現在時刻・DB使用量を返す</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var dbSizeBytes = await _dbSizeService.GetDbSizeBytesAsync();
        return Ok(new HealthResponseDto
        {
            Status = "ok",
            Timestamp = DateTime.UtcNow,
            DbSizeBytes = dbSizeBytes,
            DbSizeMb = dbSizeBytes.HasValue
                ? Math.Round(dbSizeBytes.Value / 1_048_576.0, 2)
                : null,
        });
    }
}
