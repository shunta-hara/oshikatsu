using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;
using System.Security.Claims;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class DashboardControllerTests
{
    private readonly IDashboardService _dashboardService;
    private readonly DashboardController _sut;

    public DashboardControllerTests()
    {
        _dashboardService = Substitute.For<IDashboardService>();
        _sut = new DashboardController(_dashboardService);
        SetupAuthenticatedUser(userId: 1);
    }

    // ── GET /api/v1/dashboard/summary: 正常系 ────────────────────────────────

    [Fact]
    public async Task GetSummary_AuthenticatedUser_Returns200()
    {
        _dashboardService.GetSummaryAsync(1).Returns(new List<OshiDashboardCardDto>());

        var result = await _sut.GetSummary();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetSummary_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 42);
        _dashboardService.GetSummaryAsync(42).Returns(new List<OshiDashboardCardDto>());

        await _sut.GetSummary();

        await _dashboardService.Received(1).GetSummaryAsync(42);
    }

    [Fact]
    public async Task GetSummary_NoOshis_Returns200WithEmptyArray()
    {
        _dashboardService.GetSummaryAsync(1).Returns(new List<OshiDashboardCardDto>());

        var result = await _sut.GetSummary();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<OshiDashboardCardDto>>(ok.Value);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetSummary_WithCards_ReturnsAllCards()
    {
        var cards = new List<OshiDashboardCardDto>
        {
            new() { OshiId = 1, OshiName = "推しA", LatestNews = null, LatestProgram = null },
            new() { OshiId = 2, OshiName = "推しB", LatestNews = null, LatestProgram = null },
        };
        _dashboardService.GetSummaryAsync(1).Returns(cards);

        var result = await _sut.GetSummary();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<OshiDashboardCardDto>>(ok.Value);
        Assert.Equal(2, body.Count);
    }

    [Fact]
    public async Task GetSummary_WithNewsAndProgram_ReturnsCardData()
    {
        var cards = new List<OshiDashboardCardDto>
        {
            new()
            {
                OshiId = 1,
                OshiName = "推しA",
                LatestNews = new NewsItemSummaryDto
                {
                    Id = 10,
                    Title = "テストニュース",
                    SourceUrl = "https://example.com/news/1",
                    CollectedAt = DateTimeOffset.UtcNow,
                },
                LatestProgram = new ProgramAppearanceSummaryDto
                {
                    Id = 20,
                    ProgramName = "テスト番組",
                    Broadcaster = "テストTV",
                    SourceUrl = "https://example.com/prog/1",
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            },
        };
        _dashboardService.GetSummaryAsync(1).Returns(cards);

        var result = await _sut.GetSummary();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<OshiDashboardCardDto>>(ok.Value);
        Assert.Single(body);
        Assert.Equal("推しA", body[0].OshiName);
        Assert.NotNull(body[0].LatestNews);
        Assert.Equal("テストニュース", body[0].LatestNews!.Title);
        Assert.NotNull(body[0].LatestProgram);
        Assert.Equal("テスト番組", body[0].LatestProgram!.ProgramName);
    }

    // ── GET /api/v1/dashboard/summary: 境界値 ────────────────────────────────

    [Fact]
    public async Task GetSummary_CardWithNullNews_ReturnsCardWithNullLatestNews()
    {
        var cards = new List<OshiDashboardCardDto>
        {
            new() { OshiId = 1, OshiName = "推しA", LatestNews = null, LatestProgram = null },
        };
        _dashboardService.GetSummaryAsync(1).Returns(cards);

        var result = await _sut.GetSummary();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<OshiDashboardCardDto>>(ok.Value);
        Assert.Null(body[0].LatestNews);
        Assert.Null(body[0].LatestProgram);
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
}
