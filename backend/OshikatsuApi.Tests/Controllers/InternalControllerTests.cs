using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class InternalControllerTests
{
    private readonly IInternalCollectorService _service;
    private readonly ICollectorJobLogService _jobLogService;
    private readonly INewsCleanupService _cleanupService;
    private readonly InternalController _sut;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public InternalControllerTests()
    {
        _service = Substitute.For<IInternalCollectorService>();
        _jobLogService = Substitute.For<ICollectorJobLogService>();
        _cleanupService = Substitute.For<INewsCleanupService>();
        _sut = new InternalController(_service, _jobLogService, _cleanupService);
    }

    // ── POST /api/v1/internal/news: 正常系 ──────────────────────────────────

    [Fact]
    public async Task CreateNews_ValidRequest_Returns200()
    {
        var dto = BuildNewsRequest();
        _service.CreateNewsItemAsync(dto).Returns(BuildNewsResponse(wasDuplicate: false));

        var result = await _sut.CreateNews(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task CreateNews_ValidRequest_ReturnsInternalNewsResponseDto()
    {
        var dto = BuildNewsRequest();
        var expected = BuildNewsResponse(wasDuplicate: false);
        _service.CreateNewsItemAsync(dto).Returns(expected);

        var result = await _sut.CreateNews(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalNewsResponseDto>(ok.Value);
        Assert.Equal("https://example.com/news/1", body.SourceUrl);
        Assert.False(body.WasDuplicate);
    }

    // ── POST /api/v1/internal/news: 重複 ────────────────────────────────────

    [Fact]
    public async Task CreateNews_DuplicateSourceUrl_Returns200WithDuplicateFlag()
    {
        var dto = BuildNewsRequest();
        _service.CreateNewsItemAsync(dto).Returns(BuildNewsResponse(wasDuplicate: true));

        var result = await _sut.CreateNews(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalNewsResponseDto>(ok.Value);
        Assert.True(body.WasDuplicate);
    }

    [Fact]
    public async Task CreateNews_ValidRequest_CallsServiceOnce()
    {
        var dto = BuildNewsRequest();
        _service.CreateNewsItemAsync(dto).Returns(BuildNewsResponse(wasDuplicate: false));

        await _sut.CreateNews(dto);

        await _service.Received(1).CreateNewsItemAsync(dto);
    }

    // ── POST /api/v1/internal/programs: 正常系 ──────────────────────────────

    [Fact]
    public async Task CreateProgram_ValidRequest_Returns200()
    {
        var dto = BuildProgramRequest();
        _service.CreateProgramAppearanceAsync(dto).Returns(BuildProgramResponse(wasDuplicate: false));

        var result = await _sut.CreateProgram(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task CreateProgram_ValidRequest_ReturnsInternalProgramResponseDto()
    {
        var dto = BuildProgramRequest();
        var expected = BuildProgramResponse(wasDuplicate: false);
        _service.CreateProgramAppearanceAsync(dto).Returns(expected);

        var result = await _sut.CreateProgram(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalProgramResponseDto>(ok.Value);
        Assert.Equal("https://tv.example.com/prog/1", body.SourceUrl);
        Assert.False(body.WasDuplicate);
    }

    // ── POST /api/v1/internal/programs: 重複 ────────────────────────────────

    [Fact]
    public async Task CreateProgram_DuplicateSourceUrl_Returns200WithDuplicateFlag()
    {
        var dto = BuildProgramRequest();
        _service.CreateProgramAppearanceAsync(dto).Returns(BuildProgramResponse(wasDuplicate: true));

        var result = await _sut.CreateProgram(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<InternalProgramResponseDto>(ok.Value);
        Assert.True(body.WasDuplicate);
    }

    [Fact]
    public async Task CreateProgram_ValidRequest_CallsServiceOnce()
    {
        var dto = BuildProgramRequest();
        _service.CreateProgramAppearanceAsync(dto).Returns(BuildProgramResponse(wasDuplicate: false));

        await _sut.CreateProgram(dto);

        await _service.Received(1).CreateProgramAppearanceAsync(dto);
    }

    // ── GET /api/v1/internal/oshis: 正常系 ──────────────────────────────────

    [Fact]
    public async Task GetOshis_ValidRequest_Returns200()
    {
        _service.GetAllOshisAsync().Returns(Array.Empty<InternalOshiDto>());

        var result = await _sut.GetOshis();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetOshis_WithOshis_ReturnsListOfInternalOshiDto()
    {
        var oshis = new List<InternalOshiDto>
        {
            new() { Id = 1, Name = "推しA" },
            new() { Id = 2, Name = "推しB" },
        };
        _service.GetAllOshisAsync().Returns(oshis);

        var result = await _sut.GetOshis();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<InternalOshiDto>>(ok.Value);
        Assert.Equal(2, body.Count);
        Assert.Equal("推しA", body[0].Name);
        Assert.Equal(1, body[0].Id);
    }

    // ── GET /api/v1/internal/oshis: 境界値 ──────────────────────────────────

    [Fact]
    public async Task GetOshis_NoOshis_ReturnsEmptyList()
    {
        _service.GetAllOshisAsync().Returns(Array.Empty<InternalOshiDto>());

        var result = await _sut.GetOshis();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<InternalOshiDto>>(ok.Value);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetOshis_CallsServiceOnce()
    {
        _service.GetAllOshisAsync().Returns(Array.Empty<InternalOshiDto>());

        await _sut.GetOshis();

        await _service.Received(1).GetAllOshisAsync();
    }

    // ── POST /api/v1/internal/job-logs: 正常系 ──────────────────────────────

    [Fact]
    public async Task CreateJobLog_ValidRequest_Returns200()
    {
        var dto = BuildJobLogRequest("news");
        _jobLogService.RecordJobLogAsync(dto).Returns(BuildJobLogResponse("news"));

        var result = await _sut.CreateJobLog(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task CreateJobLog_ValidRequest_ReturnsJobLogResponseDto()
    {
        var dto = BuildJobLogRequest("program");
        var expected = BuildJobLogResponse("program");
        _jobLogService.RecordJobLogAsync(dto).Returns(expected);

        var result = await _sut.CreateJobLog(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<JobLogResponseDto>(ok.Value);
        Assert.Equal("program", body.JobType);
        Assert.Equal(3, body.SuccessCount);
    }

    [Fact]
    public async Task CreateJobLog_ValidRequest_CallsJobLogServiceOnce()
    {
        var dto = BuildJobLogRequest("news");
        _jobLogService.RecordJobLogAsync(dto).Returns(BuildJobLogResponse("news"));

        await _sut.CreateJobLog(dto);

        await _jobLogService.Received(1).RecordJobLogAsync(dto);
    }

    // ── POST /api/v1/internal/job-logs: 境界値 ──────────────────────────────

    [Fact]
    public async Task CreateJobLog_ZeroSuccessAndFailure_Returns200()
    {
        var dto = new CreateJobLogRequestDto
        {
            JobType = "news",
            ExecutedAt = _now,
            SuccessCount = 0,
            FailureCount = 0,
        };
        _jobLogService.RecordJobLogAsync(dto).Returns(BuildJobLogResponse("news", successCount: 0, failureCount: 0));

        var result = await _sut.CreateJobLog(dto);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private CreateJobLogRequestDto BuildJobLogRequest(string jobType) => new()
    {
        JobType = jobType,
        ExecutedAt = _now,
        SuccessCount = 3,
        FailureCount = 1,
    };

    private JobLogResponseDto BuildJobLogResponse(
        string jobType,
        int successCount = 3,
        int failureCount = 1) => new()
        {
            Id = 1,
            JobType = jobType,
            ExecutedAt = _now,
            SuccessCount = successCount,
            FailureCount = failureCount,
            CreatedAt = _now,
        };

    private CreateNewsItemRequestDto BuildNewsRequest() => new()
    {
        OshiId = 1,
        Title = "テストニュース",
        SourceUrl = "https://example.com/news/1",
        CollectedAt = _now,
    };

    private InternalNewsResponseDto BuildNewsResponse(bool wasDuplicate) => new()
    {
        Id = 1,
        OshiId = 1,
        Title = "テストニュース",
        SourceUrl = "https://example.com/news/1",
        PublishedAt = _now,
        IsPublishedAtEstimated = false,
        CollectedAt = _now,
        CreatedAt = _now,
        WasDuplicate = wasDuplicate,
    };

    private CreateProgramAppearanceRequestDto BuildProgramRequest() => new()
    {
        OshiId = 1,
        ProgramName = "テスト番組",
        SourceUrl = "https://tv.example.com/prog/1",
        CollectedAt = _now,
    };

    private InternalProgramResponseDto BuildProgramResponse(bool wasDuplicate) => new()
    {
        Id = 1,
        OshiId = 1,
        ProgramName = "テスト番組",
        SourceUrl = "https://tv.example.com/prog/1",
        CollectedAt = _now,
        CreatedAt = _now,
        WasDuplicate = wasDuplicate,
    };
}
