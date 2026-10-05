using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class InternalControllerBatchTests
{
    private readonly IInternalCollectorService _service;
    private readonly ICollectorJobLogService _jobLogService;
    private readonly INewsCleanupService _cleanupService;
    private readonly InternalController _sut;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public InternalControllerBatchTests()
    {
        _service = Substitute.For<IInternalCollectorService>();
        _jobLogService = Substitute.For<ICollectorJobLogService>();
        _cleanupService = Substitute.For<INewsCleanupService>();
        _sut = new InternalController(_service, _jobLogService, _cleanupService);
    }

    // ── POST /api/v1/internal/news/batch: 正常系 ────────────────────────────

    [Fact]
    public async Task BatchCreateNews_ValidRequest_Returns200()
    {
        var dto = BuildNewsBatchRequest(3);
        _service.BatchCreateNewsItemsAsync(dto).Returns(new BatchResultResponseDto
        {
            ReceivedCount = 3,
            SavedCount = 3,
            SkippedCount = 0,
        });

        var result = await _sut.BatchCreateNews(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task BatchCreateNews_ValidRequest_ReturnsBatchResultResponseDto()
    {
        var dto = BuildNewsBatchRequest(2);
        _service.BatchCreateNewsItemsAsync(dto).Returns(new BatchResultResponseDto
        {
            ReceivedCount = 2,
            SavedCount = 1,
            SkippedCount = 1,
        });

        var result = await _sut.BatchCreateNews(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BatchResultResponseDto>(ok.Value);
        Assert.Equal(2, body.ReceivedCount);
        Assert.Equal(1, body.SavedCount);
        Assert.Equal(1, body.SkippedCount);
    }

    [Fact]
    public async Task BatchCreateNews_ValidRequest_CallsServiceOnce()
    {
        var dto = BuildNewsBatchRequest(1);
        _service.BatchCreateNewsItemsAsync(dto).Returns(new BatchResultResponseDto
        {
            ReceivedCount = 1,
            SavedCount = 1,
            SkippedCount = 0,
        });

        await _sut.BatchCreateNews(dto);

        await _service.Received(1).BatchCreateNewsItemsAsync(dto);
    }

    // ── POST /api/v1/internal/news/batch: 異常系 ───────────────────────────

    [Fact]
    public async Task BatchCreateNews_EmptyItems_Returns400()
    {
        var dto = new BatchCreateNewsRequestDto
        {
            OshiId = 1,
            Items = [],
        };

        var result = await _sut.BatchCreateNews(dto);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task BatchCreateNews_Over200Items_Returns400()
    {
        var dto = new BatchCreateNewsRequestDto
        {
            OshiId = 1,
            Items = Enumerable.Range(0, 201)
                .Select(i => new BatchNewsItemDto
                {
                    Title = $"タイトル{i}",
                    SourceUrl = $"https://example.com/{i}",
                    CollectedAt = _now,
                }).ToList(),
        };

        var result = await _sut.BatchCreateNews(dto);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task BatchCreateNews_OshiNotFound_Returns404()
    {
        var dto = BuildNewsBatchRequest(1);
        _service.BatchCreateNewsItemsAsync(dto).Throws<OshiNotFoundException>();

        var result = await _sut.BatchCreateNews(dto);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    // ── POST /api/v1/internal/programs/batch: 正常系 ────────────────────────

    [Fact]
    public async Task BatchCreatePrograms_ValidRequest_Returns200()
    {
        var dto = BuildProgramBatchRequest(3);
        _service.BatchCreateProgramAppearancesAsync(dto).Returns(new BatchResultResponseDto
        {
            ReceivedCount = 3,
            SavedCount = 3,
            SkippedCount = 0,
        });

        var result = await _sut.BatchCreatePrograms(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task BatchCreatePrograms_ValidRequest_ReturnsBatchResultResponseDto()
    {
        var dto = BuildProgramBatchRequest(3);
        _service.BatchCreateProgramAppearancesAsync(dto).Returns(new BatchResultResponseDto
        {
            ReceivedCount = 3,
            SavedCount = 2,
            SkippedCount = 1,
        });

        var result = await _sut.BatchCreatePrograms(dto);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<BatchResultResponseDto>(ok.Value);
        Assert.Equal(3, body.ReceivedCount);
        Assert.Equal(2, body.SavedCount);
        Assert.Equal(1, body.SkippedCount);
    }

    [Fact]
    public async Task BatchCreatePrograms_ValidRequest_CallsServiceOnce()
    {
        var dto = BuildProgramBatchRequest(1);
        _service.BatchCreateProgramAppearancesAsync(dto).Returns(new BatchResultResponseDto
        {
            ReceivedCount = 1,
            SavedCount = 1,
            SkippedCount = 0,
        });

        await _sut.BatchCreatePrograms(dto);

        await _service.Received(1).BatchCreateProgramAppearancesAsync(dto);
    }

    // ── POST /api/v1/internal/programs/batch: 異常系 ──────────────────────

    [Fact]
    public async Task BatchCreatePrograms_EmptyItems_Returns400()
    {
        var dto = new BatchCreateProgramsRequestDto
        {
            OshiId = 1,
            Items = [],
        };

        var result = await _sut.BatchCreatePrograms(dto);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task BatchCreatePrograms_Over200Items_Returns400()
    {
        var dto = new BatchCreateProgramsRequestDto
        {
            OshiId = 1,
            Items = Enumerable.Range(0, 201)
                .Select(i => new BatchProgramItemDto
                {
                    ProgramName = $"番組{i}",
                    SourceUrl = $"https://tv.example.com/{i}",
                    CollectedAt = _now,
                }).ToList(),
        };

        var result = await _sut.BatchCreatePrograms(dto);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task BatchCreatePrograms_OshiNotFound_Returns404()
    {
        var dto = BuildProgramBatchRequest(1);
        _service.BatchCreateProgramAppearancesAsync(dto).Throws<OshiNotFoundException>();

        var result = await _sut.BatchCreatePrograms(dto);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    // ── 境界値: 200件ちょうど ────────────────────────────────────────────

    [Fact]
    public async Task BatchCreateNews_Exactly200Items_Returns200()
    {
        var dto = new BatchCreateNewsRequestDto
        {
            OshiId = 1,
            Items = Enumerable.Range(0, 200)
                .Select(i => new BatchNewsItemDto
                {
                    Title = $"タイトル{i}",
                    SourceUrl = $"https://example.com/{i}",
                    CollectedAt = _now,
                }).ToList(),
        };
        _service.BatchCreateNewsItemsAsync(dto).Returns(new BatchResultResponseDto
        {
            ReceivedCount = 200,
            SavedCount = 200,
            SkippedCount = 0,
        });

        var result = await _sut.BatchCreateNews(dto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task BatchCreatePrograms_Exactly200Items_Returns200()
    {
        var dto = new BatchCreateProgramsRequestDto
        {
            OshiId = 1,
            Items = Enumerable.Range(0, 200)
                .Select(i => new BatchProgramItemDto
                {
                    ProgramName = $"番組{i}",
                    SourceUrl = $"https://tv.example.com/{i}",
                    CollectedAt = _now,
                }).ToList(),
        };
        _service.BatchCreateProgramAppearancesAsync(dto).Returns(new BatchResultResponseDto
        {
            ReceivedCount = 200,
            SavedCount = 200,
            SkippedCount = 0,
        });

        var result = await _sut.BatchCreatePrograms(dto);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private BatchCreateNewsRequestDto BuildNewsBatchRequest(int count) => new()
    {
        OshiId = 1,
        Items = Enumerable.Range(1, count)
            .Select(i => new BatchNewsItemDto
            {
                Title = $"ニュース{i}",
                SourceUrl = $"https://example.com/news/{i}",
                CollectedAt = _now,
            }).ToList(),
    };

    private BatchCreateProgramsRequestDto BuildProgramBatchRequest(int count) => new()
    {
        OshiId = 1,
        Items = Enumerable.Range(1, count)
            .Select(i => new BatchProgramItemDto
            {
                ProgramName = $"番組{i}",
                SourceUrl = $"https://tv.example.com/prog/{i}",
                CollectedAt = _now,
            }).ToList(),
    };
}
