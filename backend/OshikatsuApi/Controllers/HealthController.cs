using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;

namespace OshikatsuApi.Controllers;

/// <summary>ヘルスチェックエンドポイント</summary>
[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// サービスの稼働状態を確認する
    /// </summary>
    /// <returns>稼働中なら 200 OK と現在時刻を返す</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponseDto), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new HealthResponseDto
        {
            Status = "ok",
            Timestamp = DateTime.UtcNow,
        });
    }
}
