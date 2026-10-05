using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>Web Push 購読管理エンドポイント（VAPID 公開鍵取得・購読登録・購読削除）</summary>
[ApiController]
[Route("api/v1/push")]
public class PushController : ControllerBase
{
    private readonly IPushSubscriptionService _pushService;

    public PushController(IPushSubscriptionService pushService)
    {
        _pushService = pushService;
    }

    /// <summary>VAPID 公開鍵を返す（認証不要）</summary>
    /// <remarks>
    /// frontend はこの公開鍵を PushManager.subscribe() の applicationServerKey に渡す。
    /// </remarks>
    /// <response code="200">VAPID 公開鍵（Base64url）</response>
    [HttpGet("vapid-public-key")]
    [ProducesResponseType(typeof(VapidPublicKeyResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVapidPublicKey()
    {
        var key = await _pushService.GetVapidPublicKeyAsync();
        return Ok(new VapidPublicKeyResponseDto { PublicKey = key });
    }

    /// <summary>Push Subscription を登録する（認証必須）</summary>
    /// <remarks>
    /// 同一 endpoint が既に登録されている場合は上書き（Upsert）する。
    /// </remarks>
    /// <response code="201">登録成功</response>
    /// <response code="400">バリデーションエラー</response>
    /// <response code="401">未認証</response>
    [HttpPost("subscriptions")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterSubscription([FromBody] RegisterPushSubscriptionRequestDto dto)
    {
        var userId = GetCurrentUserId();
        await _pushService.UpsertSubscriptionAsync(userId, dto.Endpoint, dto.P256dh, dto.Auth);
        return StatusCode(StatusCodes.Status201Created, new MessageResponseDto { Message = "Subscription registered." });
    }

    /// <summary>Push Subscription を削除する（認証必須）</summary>
    /// <remarks>
    /// 対象が存在しない場合も 200 を返す（冪等）。
    /// </remarks>
    /// <response code="200">削除成功（または対象なし）</response>
    /// <response code="400">バリデーションエラー</response>
    /// <response code="401">未認証</response>
    [HttpDelete("subscriptions")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteSubscription([FromBody] DeletePushSubscriptionRequestDto dto)
    {
        var userId = GetCurrentUserId();
        await _pushService.DeleteSubscriptionAsync(userId, dto.Endpoint);
        return Ok(new MessageResponseDto { Message = "Subscription deleted." });
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    /// <summary>
    /// JWT の sub クレームからログインユーザーの ID を取得する。
    /// </summary>
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
