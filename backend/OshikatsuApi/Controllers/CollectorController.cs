using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>収集ジョブのステータスをユーザーに公開するエンドポイント</summary>
[ApiController]
[Route("api/v1/collector")]
[Authorize]
public class CollectorController : ControllerBase
{
    private readonly ICollectorJobLogService _jobLogService;

    public CollectorController(ICollectorJobLogService jobLogService)
    {
        _jobLogService = jobLogService;
    }

    /// <summary>各ジョブタイプの最新実行状況を返す</summary>
    /// <response code="200">最新ジョブ実行状況</response>
    /// <response code="401">未認証</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(CollectorStatusResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus()
    {
        var result = await _jobLogService.GetStatusAsync();
        return Ok(result);
    }
}
