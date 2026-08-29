using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>マルチ推しダッシュボード集計エンドポイント（ログインユーザー向け）</summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// 登録済み推しごとに最新ニュース1件・最新出演番組1件を含むダッシュボードカードの一覧を返す（推し登録順）。
    /// ニュースや番組がない推しのカードは latestNews / latestProgram が null になる。
    /// </summary>
    /// <response code="200">推しカードの配列（推し登録順）</response>
    /// <response code="401">未認証</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IReadOnlyList<OshiDashboardCardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSummary()
    {
        var userId = GetCurrentUserId();
        var result = await _dashboardService.GetSummaryAsync(userId);
        return Ok(result);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private int GetCurrentUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (string.IsNullOrEmpty(sub) || !int.TryParse(sub, out var userId))
        {
            throw new InvalidOperationException("User ID not found in token.");
        }

        return userId;
    }
}
