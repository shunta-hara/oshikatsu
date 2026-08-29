using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using System.Security.Claims;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class BookmarkControllerTests
{
    private readonly IBookmarkService _bookmarkService;
    private readonly BookmarkController _sut;

    public BookmarkControllerTests()
    {
        _bookmarkService = Substitute.For<IBookmarkService>();
        _sut = new BookmarkController(_bookmarkService);
        SetupAuthenticatedUser(userId: 1);
    }

    // ── POST /api/v1/news/{id}/bookmark: 正常系 ──────────────────────────────

    [Fact]
    public async Task AddBookmark_ValidNewsItem_Returns201()
    {
        _bookmarkService.AddBookmarkAsync(1, 42).Returns(Task.CompletedTask);

        var result = await _sut.AddBookmark(id: 42);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, statusResult.StatusCode);
    }

    [Fact]
    public async Task AddBookmark_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 99);
        _bookmarkService.AddBookmarkAsync(99, 42).Returns(Task.CompletedTask);

        await _sut.AddBookmark(id: 42);

        await _bookmarkService.Received(1).AddBookmarkAsync(99, 42);
    }

    // ── POST /api/v1/news/{id}/bookmark: 異常系 ──────────────────────────────

    [Fact]
    public async Task AddBookmark_NewsItemNotFound_Returns404()
    {
        _bookmarkService.AddBookmarkAsync(1, 999)
            .Throws(new NewsItemNotFoundException());

        var result = await _sut.AddBookmark(id: 999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task AddBookmark_AlreadyBookmarked_Returns409()
    {
        _bookmarkService.AddBookmarkAsync(1, 42)
            .Throws(new BookmarkAlreadyExistsException());

        var result = await _sut.AddBookmark(id: 42);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    // ── DELETE /api/v1/news/{id}/bookmark: 正常系 ────────────────────────────

    [Fact]
    public async Task RemoveBookmark_ExistingBookmark_Returns204()
    {
        _bookmarkService.RemoveBookmarkAsync(1, 42).Returns(Task.CompletedTask);

        var result = await _sut.RemoveBookmark(id: 42);

        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);
    }

    [Fact]
    public async Task RemoveBookmark_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 7);
        _bookmarkService.RemoveBookmarkAsync(7, 42).Returns(Task.CompletedTask);

        await _sut.RemoveBookmark(id: 42);

        await _bookmarkService.Received(1).RemoveBookmarkAsync(7, 42);
    }

    // ── DELETE /api/v1/news/{id}/bookmark: 異常系 ────────────────────────────

    [Fact]
    public async Task RemoveBookmark_NotBookmarked_Returns404()
    {
        _bookmarkService.RemoveBookmarkAsync(1, 42)
            .Throws(new BookmarkNotFoundException());

        var result = await _sut.RemoveBookmark(id: 42);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    // ── GET /api/v1/bookmarks: 正常系 ────────────────────────────────────────

    [Fact]
    public async Task GetBookmarks_ReturnsOk()
    {
        _bookmarkService.GetBookmarksAsync(1, 1, 20)
            .Returns(EmptyBookmarkPaged());

        var result = await _sut.GetBookmarks();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetBookmarks_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 5);
        _bookmarkService.GetBookmarksAsync(5, 1, 20)
            .Returns(EmptyBookmarkPaged());

        await _sut.GetBookmarks();

        await _bookmarkService.Received(1).GetBookmarksAsync(5, 1, 20);
    }

    [Fact]
    public async Task GetBookmarks_WithItems_ReturnsPaginatedResponse()
    {
        var items = new List<BookmarkResponseDto>
        {
            new()
            {
                Id = 1,
                NewsItemId = 10,
                OshiId = 1,
                OshiName = "推しA",
                Title = "テストニュース",
                SourceUrl = "https://example.com",
                CollectedAt = DateTimeOffset.UtcNow,
                BookmarkedAt = DateTimeOffset.UtcNow,
            },
        };
        _bookmarkService.GetBookmarksAsync(1, 1, 20)
            .Returns(new PaginatedResponseDto<BookmarkResponseDto>
            {
                Items = items,
                TotalCount = 1,
                Page = 1,
                PageSize = 20,
            });

        var result = await _sut.GetBookmarks();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<PaginatedResponseDto<BookmarkResponseDto>>(ok.Value);
        Assert.Single(body.Items);
        Assert.Equal("テストニュース", body.Items[0].Title);
    }

    // ── ページネーション境界値 ────────────────────────────────────────────────

    [Fact]
    public async Task GetBookmarks_PageBelowOne_NormalizesToOne()
    {
        _bookmarkService.GetBookmarksAsync(1, 1, 20)
            .Returns(EmptyBookmarkPaged());

        await _sut.GetBookmarks(page: 0);

        await _bookmarkService.Received(1).GetBookmarksAsync(1, 1, 20);
    }

    [Fact]
    public async Task GetBookmarks_PageSizeAboveMax_NormalizesToDefault()
    {
        _bookmarkService.GetBookmarksAsync(1, 1, 20)
            .Returns(EmptyBookmarkPaged());

        await _sut.GetBookmarks(pageSize: 9999);

        await _bookmarkService.Received(1).GetBookmarksAsync(1, 1, 20);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private static PaginatedResponseDto<BookmarkResponseDto> EmptyBookmarkPaged() =>
        new()
        {
            Items = new List<BookmarkResponseDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 20,
        };

    private void SetupAuthenticatedUser(int userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
    }
}
