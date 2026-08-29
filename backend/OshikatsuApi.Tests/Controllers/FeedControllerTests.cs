using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;
using System.Security.Claims;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class FeedControllerTests
{
    private readonly IFeedService _feedService;
    private readonly FeedController _sut;

    public FeedControllerTests()
    {
        _feedService = Substitute.For<IFeedService>();
        _sut = new FeedController(_feedService);
        SetupAuthenticatedUser(userId: 1);
    }

    // ── GET /api/v1/news: 正常系 ────────────────────────────────────────────

    [Fact]
    public async Task GetNews_AuthenticatedUser_Returns200()
    {
        _feedService.GetNewsAsync(1, 1, 20)
            .Returns(EmptyNewsPaged());

        var result = await _sut.GetNews();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetNews_WithItems_ReturnsPaginatedResponse()
    {
        var items = new List<NewsItemResponseDto>
        {
            new()
            {
                Id = 1,
                OshiId = 1,
                OshiName = "推しA",
                Title = "テストニュース",
                SourceUrl = "https://example.com",
                CollectedAt = DateTimeOffset.UtcNow,
            },
        };
        _feedService.GetNewsAsync(1, 1, 20)
            .Returns(new PaginatedResponseDto<NewsItemResponseDto>
            {
                Items = items,
                TotalCount = 1,
                Page = 1,
                PageSize = 20,
            });

        var result = await _sut.GetNews();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<PaginatedResponseDto<NewsItemResponseDto>>(ok.Value);
        Assert.Single(body.Items);
        Assert.Equal("テストニュース", body.Items[0].Title);
    }

    [Fact]
    public async Task GetNews_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 42);
        _feedService.GetNewsAsync(42, 1, 20).Returns(EmptyNewsPaged());

        await _sut.GetNews();

        await _feedService.Received(1).GetNewsAsync(42, 1, 20);
    }

    [Fact]
    public async Task GetNews_EmptyResult_Returns200WithEmptyList()
    {
        _feedService.GetNewsAsync(1, 1, 20).Returns(EmptyNewsPaged());

        var result = await _sut.GetNews();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<PaginatedResponseDto<NewsItemResponseDto>>(ok.Value);
        Assert.Empty(body.Items);
        Assert.Equal(0, body.TotalCount);
    }

    // ── GET /api/v1/news: 異常系・境界値 ────────────────────────────────────

    [Fact]
    public async Task GetNews_PageLessThan1_NormalizesTo1()
    {
        _feedService.GetNewsAsync(1, 1, 20).Returns(EmptyNewsPaged());

        await _sut.GetNews(page: 0);

        await _feedService.Received(1).GetNewsAsync(1, 1, 20);
    }

    [Fact]
    public async Task GetNews_NegativePage_NormalizesTo1()
    {
        _feedService.GetNewsAsync(1, 1, 20).Returns(EmptyNewsPaged());

        await _sut.GetNews(page: -5);

        await _feedService.Received(1).GetNewsAsync(1, 1, 20);
    }

    [Fact]
    public async Task GetNews_PageSizeOver100_NormalizesTo20()
    {
        _feedService.GetNewsAsync(1, 1, 20).Returns(EmptyNewsPaged());

        await _sut.GetNews(pageSize: 200);

        await _feedService.Received(1).GetNewsAsync(1, 1, 20);
    }

    [Fact]
    public async Task GetNews_CustomPage_PassesToService()
    {
        _feedService.GetNewsAsync(1, 3, 20)
            .Returns(new PaginatedResponseDto<NewsItemResponseDto>
            {
                Items = [],
                TotalCount = 60,
                Page = 3,
                PageSize = 20,
            });

        await _sut.GetNews(page: 3);

        await _feedService.Received(1).GetNewsAsync(1, 3, 20);
    }

    // ── GET /api/v1/programs: 正常系 ────────────────────────────────────────

    [Fact]
    public async Task GetPrograms_AuthenticatedUser_Returns200()
    {
        _feedService.GetProgramsAsync(1, 1, 20).Returns(EmptyProgramsPaged());

        var result = await _sut.GetPrograms();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetPrograms_WithItems_ReturnsPaginatedResponse()
    {
        var items = new List<ProgramAppearanceResponseDto>
        {
            new()
            {
                Id = 1,
                OshiId = 1,
                OshiName = "推しA",
                ProgramName = "テスト番組",
                SourceUrl = "https://example.com",
                CollectedAt = DateTimeOffset.UtcNow,
            },
        };
        _feedService.GetProgramsAsync(1, 1, 20)
            .Returns(new PaginatedResponseDto<ProgramAppearanceResponseDto>
            {
                Items = items,
                TotalCount = 1,
                Page = 1,
                PageSize = 20,
            });

        var result = await _sut.GetPrograms();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<PaginatedResponseDto<ProgramAppearanceResponseDto>>(ok.Value);
        Assert.Single(body.Items);
        Assert.Equal("テスト番組", body.Items[0].ProgramName);
    }

    [Fact]
    public async Task GetPrograms_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 99);
        _feedService.GetProgramsAsync(99, 1, 20).Returns(EmptyProgramsPaged());

        await _sut.GetPrograms();

        await _feedService.Received(1).GetProgramsAsync(99, 1, 20);
    }

    // ── GET /api/v1/programs: 異常系・境界値 ────────────────────────────────

    [Fact]
    public async Task GetPrograms_EmptyResult_Returns200WithEmptyList()
    {
        _feedService.GetProgramsAsync(1, 1, 20).Returns(EmptyProgramsPaged());

        var result = await _sut.GetPrograms();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<PaginatedResponseDto<ProgramAppearanceResponseDto>>(ok.Value);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task GetPrograms_PageLessThan1_NormalizesTo1()
    {
        _feedService.GetProgramsAsync(1, 1, 20).Returns(EmptyProgramsPaged());

        await _sut.GetPrograms(page: -1);

        await _feedService.Received(1).GetProgramsAsync(1, 1, 20);
    }

    [Fact]
    public async Task GetPrograms_PageSizeZero_NormalizesTo20()
    {
        _feedService.GetProgramsAsync(1, 1, 20).Returns(EmptyProgramsPaged());

        await _sut.GetPrograms(pageSize: 0);

        await _feedService.Received(1).GetProgramsAsync(1, 1, 20);
    }

    // ── GET /api/v1/news: oshiId フィルタ ───────────────────────────────────

    [Fact]
    public async Task GetNews_WithOshiId_PassesOshiIdToService()
    {
        _feedService.GetNewsAsync(1, 1, 20, 42).Returns(EmptyNewsPaged());

        await _sut.GetNews(oshiId: 42);

        await _feedService.Received(1).GetNewsAsync(1, 1, 20, 42);
    }

    [Fact]
    public async Task GetNews_WithoutOshiId_PassesNullToService()
    {
        _feedService.GetNewsAsync(1, 1, 20, null).Returns(EmptyNewsPaged());

        await _sut.GetNews();

        await _feedService.Received(1).GetNewsAsync(1, 1, 20, null);
    }

    // ── GET /api/v1/news: q キーワード検索 ──────────────────────────────────

    [Fact]
    public async Task GetNews_WithQuery_PassesQueryToService()
    {
        _feedService.GetNewsAsync(1, 1, 20, null, "テスト").Returns(EmptyNewsPaged());

        await _sut.GetNews(q: "テスト");

        await _feedService.Received(1).GetNewsAsync(1, 1, 20, null, "テスト");
    }

    [Fact]
    public async Task GetNews_WithQueryAndOshiId_PassesBothToService()
    {
        _feedService.GetNewsAsync(1, 1, 20, 7, "キーワード").Returns(EmptyNewsPaged());

        await _sut.GetNews(oshiId: 7, q: "キーワード");

        await _feedService.Received(1).GetNewsAsync(1, 1, 20, 7, "キーワード");
    }

    [Fact]
    public async Task GetNews_WithQuery_Returns200()
    {
        _feedService.GetNewsAsync(1, 1, 20, null, "テスト").Returns(EmptyNewsPaged());

        var result = await _sut.GetNews(q: "テスト");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetNews_WithNullQuery_PassesNullQueryToService()
    {
        _feedService.GetNewsAsync(1, 1, 20, null, null).Returns(EmptyNewsPaged());

        await _sut.GetNews();

        await _feedService.Received(1).GetNewsAsync(1, 1, 20, null, null);
    }

    // ── GET /api/v1/news: 日付範囲フィルタ ──────────────────────────────────

    [Fact]
    public async Task GetNews_WithFromAndTo_PassesDateRangeToService()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero);
        _feedService.GetNewsAsync(1, 1, 20, null, null, from, to).Returns(EmptyNewsPaged());

        await _sut.GetNews(from: from, to: to);

        await _feedService.Received(1).GetNewsAsync(1, 1, 20, null, null, from, to);
    }

    [Fact]
    public async Task GetNews_WithOnlyFrom_PassesFromToService()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        _feedService.GetNewsAsync(1, 1, 20, null, null, from, null).Returns(EmptyNewsPaged());

        await _sut.GetNews(from: from);

        await _feedService.Received(1).GetNewsAsync(1, 1, 20, null, null, from, null);
    }

    [Fact]
    public async Task GetNews_WithOnlyTo_PassesToToService()
    {
        var to = new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero);
        _feedService.GetNewsAsync(1, 1, 20, null, null, null, to).Returns(EmptyNewsPaged());

        await _sut.GetNews(to: to);

        await _feedService.Received(1).GetNewsAsync(1, 1, 20, null, null, null, to);
    }

    [Fact]
    public async Task GetNews_ToBeforeFrom_Returns400BadRequest()
    {
        var from = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero); // to < from

        var result = await _sut.GetNews(from: from, to: to);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetNews_FromEqualsTo_Returns200()
    {
        var sameDate = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        _feedService.GetNewsAsync(1, 1, 20, null, null, sameDate, sameDate).Returns(EmptyNewsPaged());

        var result = await _sut.GetNews(from: sameDate, to: sameDate);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── GET /api/v1/programs: 日付範囲フィルタ ──────────────────────────────

    [Fact]
    public async Task GetPrograms_WithFromAndTo_PassesDateRangeToService()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero);
        _feedService.GetProgramsAsync(1, 1, 20, null, from, to).Returns(EmptyProgramsPaged());

        await _sut.GetPrograms(from: from, to: to);

        await _feedService.Received(1).GetProgramsAsync(1, 1, 20, null, from, to);
    }

    [Fact]
    public async Task GetPrograms_ToBeforeFrom_Returns400BadRequest()
    {
        var from = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero); // to < from

        var result = await _sut.GetPrograms(from: from, to: to);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPrograms_FromEqualsTo_Returns200()
    {
        var sameDate = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
        _feedService.GetProgramsAsync(1, 1, 20, null, sameDate, sameDate).Returns(EmptyProgramsPaged());

        var result = await _sut.GetPrograms(from: sameDate, to: sameDate);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── GET /api/v1/programs: oshiId フィルタ ───────────────────────────────

    [Fact]
    public async Task GetPrograms_WithOshiId_PassesOshiIdToService()
    {
        _feedService.GetProgramsAsync(1, 1, 20, 7).Returns(EmptyProgramsPaged());

        await _sut.GetPrograms(oshiId: 7);

        await _feedService.Received(1).GetProgramsAsync(1, 1, 20, 7);
    }

    [Fact]
    public async Task GetPrograms_WithoutOshiId_PassesNullToService()
    {
        _feedService.GetProgramsAsync(1, 1, 20, null).Returns(EmptyProgramsPaged());

        await _sut.GetPrograms();

        await _feedService.Received(1).GetProgramsAsync(1, 1, 20, null);
    }

    // ── GET /api/v1/feed/unread-count: 正常系 ─────────────────────────────────

    [Fact]
    public async Task GetUnreadCount_AuthenticatedUser_Returns200()
    {
        _feedService.GetUnreadCountAsync(1).Returns(0);

        var result = await _sut.GetUnreadCount();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetUnreadCount_WithUnreadItems_ReturnsCount()
    {
        _feedService.GetUnreadCountAsync(1).Returns(5);

        var result = await _sut.GetUnreadCount();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<UnreadCountResponseDto>(ok.Value);
        Assert.Equal(5, body.UnreadCount);
    }

    [Fact]
    public async Task GetUnreadCount_NoUnreadItems_ReturnsZero()
    {
        _feedService.GetUnreadCountAsync(1).Returns(0);

        var result = await _sut.GetUnreadCount();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<UnreadCountResponseDto>(ok.Value);
        Assert.Equal(0, body.UnreadCount);
    }

    [Fact]
    public async Task GetUnreadCount_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 42);
        _feedService.GetUnreadCountAsync(42).Returns(3);

        await _sut.GetUnreadCount();

        await _feedService.Received(1).GetUnreadCountAsync(42);
    }

    // ── POST /api/v1/feed/mark-as-read: 正常系 ────────────────────────────────

    [Fact]
    public async Task MarkAsRead_AuthenticatedUser_Returns204()
    {
        _feedService.MarkAsReadAsync(1).Returns(Task.CompletedTask);

        var result = await _sut.MarkAsRead();

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task MarkAsRead_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 7);
        _feedService.MarkAsReadAsync(7).Returns(Task.CompletedTask);

        await _sut.MarkAsRead();

        await _feedService.Received(1).MarkAsReadAsync(7);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private void SetupAuthenticatedUser(int userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
    }

    private static PaginatedResponseDto<NewsItemResponseDto> EmptyNewsPaged() =>
        new() { Items = [], TotalCount = 0, Page = 1, PageSize = 20 };

    private static PaginatedResponseDto<ProgramAppearanceResponseDto> EmptyProgramsPaged() =>
        new() { Items = [], TotalCount = 0, Page = 1, PageSize = 20 };
}
