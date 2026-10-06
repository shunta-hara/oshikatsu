using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;
using System.Security.Claims;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class CollectorControllerTests
{
    private readonly ICollectorJobLogService _jobLogService;
    private readonly CollectorController _sut;

    public CollectorControllerTests()
    {
        _jobLogService = Substitute.For<ICollectorJobLogService>();
        _sut = new CollectorController(_jobLogService);
        SetupAuthenticatedUser(userId: 1);
    }

    // ── GET /api/v1/collector/status: 正常系 ─────────────────────────────────

    [Fact]
    public async Task GetStatus_AuthenticatedUser_Returns200()
    {
        _jobLogService.GetStatusAsync().Returns(EmptyStatus());

        var result = await _sut.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetStatus_NoJobsRun_ReturnsBothNull()
    {
        _jobLogService.GetStatusAsync().Returns(EmptyStatus());

        var result = await _sut.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<CollectorStatusResponseDto>(ok.Value);
        Assert.Null(body.LatestNewsJob);
        Assert.Null(body.LatestProgramJob);
    }

    [Fact]
    public async Task GetStatus_BothJobsRun_ReturnsBothLatest()
    {
        var now = DateTimeOffset.UtcNow;
        var status = new CollectorStatusResponseDto
        {
            LatestNewsJob = new JobLogResponseDto
            {
                Id = 1,
                JobType = "news",
                ExecutedAt = now.AddHours(-1),
                SuccessCount = 5,
                FailureCount = 0,
                CreatedAt = now.AddHours(-1),
            },
            LatestProgramJob = new JobLogResponseDto
            {
                Id = 2,
                JobType = "program",
                ExecutedAt = now.AddHours(-2),
                SuccessCount = 3,
                FailureCount = 1,
                CreatedAt = now.AddHours(-2),
            },
        };
        _jobLogService.GetStatusAsync().Returns(status);

        var result = await _sut.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<CollectorStatusResponseDto>(ok.Value);
        Assert.NotNull(body.LatestNewsJob);
        Assert.NotNull(body.LatestProgramJob);
        Assert.Equal(5, body.LatestNewsJob!.SuccessCount);
        Assert.Equal(1, body.LatestProgramJob!.FailureCount);
    }

    [Fact]
    public async Task GetStatus_CallsServiceOnce()
    {
        _jobLogService.GetStatusAsync().Returns(EmptyStatus());

        await _sut.GetStatus();

        await _jobLogService.Received(1).GetStatusAsync();
    }

    // ── 境界値 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_OnlyNewsJobRun_ReturnsProgramJobAsNull()
    {
        var status = new CollectorStatusResponseDto
        {
            LatestNewsJob = new JobLogResponseDto
            {
                Id = 1,
                JobType = "news",
                ExecutedAt = DateTimeOffset.UtcNow,
                SuccessCount = 10,
                FailureCount = 0,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            LatestProgramJob = null,
        };
        _jobLogService.GetStatusAsync().Returns(status);

        var result = await _sut.GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<CollectorStatusResponseDto>(ok.Value);
        Assert.NotNull(body.LatestNewsJob);
        Assert.Null(body.LatestProgramJob);
    }

    // ── GET /api/v1/collector/operations: 正常系 ─────────────────────────────

    [Fact]
    public async Task GetOperations_AuthenticatedUser_Returns200()
    {
        _jobLogService.GetOperationsDataAsync().Returns(EmptyOperations());

        var result = await _sut.GetOperations();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetOperations_NoRecentJobs_ReturnsEmptyCollections()
    {
        _jobLogService.GetOperationsDataAsync().Returns(EmptyOperations());

        var result = await _sut.GetOperations();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<CollectorOperationsResponseDto>(ok.Value);
        Assert.Empty(body.RecentJobs);
        Assert.Empty(body.FailingTalents);
        Assert.False(body.IsOverallJobFailing);
    }

    [Fact]
    public async Task GetOperations_WithRecentJobs_ReturnsJobs()
    {
        var now = DateTimeOffset.UtcNow;
        var ops = new CollectorOperationsResponseDto
        {
            RecentJobs = new List<JobLogResponseDto>
            {
                new() { Id = 1, JobType = "news", ExecutedAt = now.AddHours(-1), SuccessCount = 5, FailureCount = 0, CreatedAt = now.AddHours(-1) },
                new() { Id = 2, JobType = "program", ExecutedAt = now.AddHours(-2), SuccessCount = 3, FailureCount = 1, CreatedAt = now.AddHours(-2) },
            },
            IsOverallJobFailing = false,
            FailingTalents = [],
        };
        _jobLogService.GetOperationsDataAsync().Returns(ops);

        var result = await _sut.GetOperations();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<CollectorOperationsResponseDto>(ok.Value);
        Assert.Equal(2, body.RecentJobs.Count);
    }

    [Fact]
    public async Task GetOperations_WithFailingTalents_ReturnsFailingTalents()
    {
        var ops = new CollectorOperationsResponseDto
        {
            RecentJobs = [],
            IsOverallJobFailing = true,
            FailingTalents = new List<FailingTalentStatusDto>
            {
                new() { TalentId = 1, TalentName = "推しA", ConsecutiveFailureCount = 3 },
            },
        };
        _jobLogService.GetOperationsDataAsync().Returns(ops);

        var result = await _sut.GetOperations();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<CollectorOperationsResponseDto>(ok.Value);
        Assert.True(body.IsOverallJobFailing);
        Assert.Single(body.FailingTalents);
        Assert.Equal("推しA", body.FailingTalents[0].TalentName);
        Assert.Equal(3, body.FailingTalents[0].ConsecutiveFailureCount);
    }

    [Fact]
    public async Task GetOperations_CallsServiceOnce()
    {
        _jobLogService.GetOperationsDataAsync().Returns(EmptyOperations());

        await _sut.GetOperations();

        await _jobLogService.Received(1).GetOperationsDataAsync();
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

    private static CollectorStatusResponseDto EmptyStatus() => new()
    {
        LatestNewsJob = null,
        LatestProgramJob = null,
    };

    private static CollectorOperationsResponseDto EmptyOperations() => new()
    {
        RecentJobs = [],
        IsOverallJobFailing = false,
        FailingTalents = [],
    };
}
