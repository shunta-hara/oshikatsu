using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>ニュース・出演番組一覧取得エンドポイント（ログインユーザー向け）</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class FeedController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService)
    {
        _feedService = feedService;
    }

    /// <summary>ログインユーザーの推し全員分のニュース一覧を取得する（収集日時の新着順）</summary>
    /// <param name="page">ページ番号（1起算）。1未満の値は1に正規化される</param>
    /// <param name="pageSize">1ページあたりの件数（デフォルト20、最大100）</param>
    /// <param name="oshiId">絞り込む推しのID（省略時は全推し）</param>
    /// <response code="200">ページネーション付きニュース一覧</response>
    /// <response code="401">未認証</response>
    [HttpGet("news")]
    [ProducesResponseType(typeof(PaginatedResponseDto<NewsItemResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] int? oshiId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > MaxPageSize) pageSize = DefaultPageSize;

        var userId = GetCurrentUserId();
        var result = await _feedService.GetNewsAsync(userId, page, pageSize, oshiId);
        return Ok(result);
    }

    /// <summary>ログインユーザーの推し全員分の出演番組一覧を取得する（収集日時の新着順）</summary>
    /// <param name="page">ページ番号（1起算）。1未満の値は1に正規化される</param>
    /// <param name="pageSize">1ページあたりの件数（デフォルト20、最大100）</param>
    /// <param name="oshiId">絞り込む推しのID（省略時は全推し）</param>
    /// <response code="200">ページネーション付き出演番組一覧</response>
    /// <response code="401">未認証</response>
    [HttpGet("programs")]
    [ProducesResponseType(typeof(PaginatedResponseDto<ProgramAppearanceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPrograms(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] int? oshiId = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > MaxPageSize) pageSize = DefaultPageSize;

        var userId = GetCurrentUserId();
        var result = await _feedService.GetProgramsAsync(userId, page, pageSize, oshiId);
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
