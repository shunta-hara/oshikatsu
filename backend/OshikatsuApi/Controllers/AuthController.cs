using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>ユーザー認証エンドポイント（登録・ログイン・ログアウト）</summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>新規ユーザー登録</summary>
    /// <response code="201">登録成功。JWT トークンを返す</response>
    /// <response code="400">バリデーションエラー（無効なメール形式・パスワード短すぎなど）</response>
    /// <response code="409">メールアドレスが既に登録済み</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        try
        {
            var result = await _authService.RegisterAsync(dto.Email, dto.Password);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (DuplicateEmailException)
        {
            return Conflict(new MessageResponseDto { Message = "The email address is already registered." });
        }
    }

    /// <summary>ログイン</summary>
    /// <response code="200">認証成功。JWT トークンを返す</response>
    /// <response code="400">バリデーションエラー</response>
    /// <response code="401">メールアドレスまたはパスワードが誤っている</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto.Email, dto.Password);
        if (result is null)
        {
            return Unauthorized(new MessageResponseDto { Message = "Invalid email or password." });
        }
        return Ok(result);
    }

    /// <summary>ログアウト（現在のトークンを無効化）</summary>
    /// <response code="200">ログアウト成功</response>
    /// <response code="401">未認証</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(expClaim))
        {
            return Unauthorized(new MessageResponseDto { Message = "Invalid token." });
        }

        var expiresAt = DateTimeOffset
            .FromUnixTimeSeconds(long.Parse(expClaim))
            .UtcDateTime;

        await _authService.LogoutAsync(jti, expiresAt);

        return Ok(new MessageResponseDto { Message = "Logged out successfully." });
    }
}
