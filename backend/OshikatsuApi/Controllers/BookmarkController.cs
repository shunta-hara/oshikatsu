using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>ニュースのブックマーク管理エンドポイント</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class BookmarkController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IBookmarkService _bookmarkService;

    public BookmarkController(IBookmarkService bookmarkService)
    {
        _bookmarkService = bookmarkService;
    }

    /// <summary>指定したニュース記事をブックマークに登録する</summary>
    /// <param name="id">ブックマークするニュースアイテムのID</param>
    /// <response code="201">登録成功</response>
    /// <response code="401">未認証</response>
    /// <response code="404">ニュースアイテムが存在しない</response>
    /// <response code="409">すでにブックマーク済み</response>
    [HttpPost("news/{id:int}/bookmark")]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddBookmark(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _bookmarkService.AddBookmarkAsync(userId, id);
            return StatusCode(StatusCodes.Status201Created,
                new MessageResponseDto { Message = "Bookmarked." });
        }
        catch (NewsItemNotFoundException)
        {
            return NotFound(new MessageResponseDto { Message = "News item not found." });
        }
        catch (BookmarkAlreadyExistsException)
        {
            return Conflict(new MessageResponseDto { Message = "Already bookmarked." });
        }
    }

    /// <summary>指定したニュース記事のブックマークを解除する</summary>
    /// <param name="id">ブックマーク解除するニュースアイテムのID</param>
    /// <response code="204">解除成功</response>
    /// <response code="401">未認証</response>
    /// <response code="404">ブックマークが存在しない</response>
    [HttpDelete("news/{id:int}/bookmark")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveBookmark(int id)
    {
        var userId = GetCurrentUserId();
        try
        {
            await _bookmarkService.RemoveBookmarkAsync(userId, id);
            return NoContent();
        }
        catch (BookmarkNotFoundException)
        {
            return NotFound(new MessageResponseDto { Message = "Bookmark not found." });
        }
    }

    /// <summary>ログインユーザーのブックマーク一覧を取得する（登録日時の新しい順）</summary>
    /// <param name="page">ページ番号（1起算）</param>
    /// <param name="pageSize">1ページあたりの件数（デフォルト20、最大100）</param>
    /// <param name="oshiId">推しIDで絞り込む（任意）。自分の推し以外を指定しても空を返す</param>
    /// <param name="q">タイトル・要約のキーワード部分一致（英字の大小文字を区別しない）</param>
    /// <param name="from">掲載日の下限（任意・オフセット付き日時）</param>
    /// <param name="to">掲載日の上限（任意・オフセット付き日時）</param>
    /// <response code="200">ページネーション付きブックマーク一覧</response>
    /// <response code="401">未認証</response>
    [HttpGet("bookmarks")]
    [ProducesResponseType(typeof(PaginatedResponseDto<BookmarkResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBookmarks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] int? oshiId = null,
        [FromQuery] string? q = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > MaxPageSize) pageSize = DefaultPageSize;

        var userId = GetCurrentUserId();
        var result = await _bookmarkService.GetBookmarksAsync(userId, page, pageSize, oshiId, q, from, to);
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
