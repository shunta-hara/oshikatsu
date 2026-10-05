using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>推し登録・管理エンドポイント</summary>
[ApiController]
[Route("api/v1/oshis")]
[Authorize]
public class OshisController : ControllerBase
{
    private readonly IOshiService _oshiService;
    private readonly IGitHubDispatchService _githubDispatchService;

    public OshisController(
        IOshiService oshiService,
        IGitHubDispatchService githubDispatchService)
    {
        _oshiService = oshiService;
        _githubDispatchService = githubDispatchService;
    }

    /// <summary>推しを登録する</summary>
    /// <response code="201">登録成功。登録した推し情報（id=user_oshis.id, name=talent.display_name）を返す</response>
    /// <response code="400">バリデーションエラー（名前が空・100文字超など）</response>
    /// <response code="401">未認証</response>
    /// <response code="409">同一ユーザーが既に同名タレントを登録済み</response>
    /// <response code="422">推し登録数の上限（20件）を超えている</response>
    [HttpPost]
    [ProducesResponseType(typeof(OshiResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateOshiRequestDto dto)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _oshiService.CreateAsync(userId, dto.Name);
            // Fire-and-forget: dispatch failures must not block or fail oshi registration.
            // TriggerCollectAsync catches all exceptions internally and logs them.
            // talent_id を dispatch inputs に渡す（FEAT-039: oshi_id → talent_id）
            _ = _githubDispatchService.TriggerCollectAsync(result.TalentId);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (OshiDuplicateException)
        {
            return Conflict(
                new MessageResponseDto { Message = "This talent is already registered as your oshi." });
        }
        catch (OshiLimitExceededException)
        {
            return UnprocessableEntity(
                new MessageResponseDto { Message = "Maximum of 20 oshis allowed per user." });
        }
    }

    /// <summary>ログインユーザーの推し一覧を取得する</summary>
    /// <response code="200">推し一覧（登録日時昇順）。id=user_oshis.id, name=talent.display_name</response>
    /// <response code="401">未認証</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OshiResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetCurrentUserId();
        var result = await _oshiService.GetAllByUserAsync(userId);
        return Ok(result);
    }

    /// <summary>指定した推しを削除する</summary>
    /// <response code="204">削除成功（UserOshi レコードのみ削除。Talent・記事・番組は残る）</response>
    /// <response code="401">未認証</response>
    /// <response code="403">他のユーザーの推しへのアクセス</response>
    /// <response code="404">推しが存在しない</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _oshiService.DeleteAsync(userId, id);
            return NoContent();
        }
        catch (OshiNotFoundException)
        {
            return NotFound(new MessageResponseDto { Message = "Oshi not found." });
        }
        catch (OshiAccessDeniedException)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new MessageResponseDto { Message = "You do not have permission to delete this oshi." });
        }
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    /// <summary>
    /// JWT の sub クレームからログインユーザーの ID を取得する。
    /// </summary>
    private int GetCurrentUserId()
    {
        // JwtBearer ミドルウェアは sub → ClaimTypes.NameIdentifier にマップする
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (string.IsNullOrEmpty(sub) || !int.TryParse(sub, out var userId))
        {
            throw new InvalidOperationException("User ID not found in token.");
        }

        return userId;
    }
}
