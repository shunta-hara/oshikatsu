using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

/// <summary>InternalController の talent-sources エンドポイントのテスト（FEAT-048）</summary>
public class InternalTalentSourceControllerTests
{
    private readonly ITalentSourceService _talentSourceService;
    private readonly InternalController _sut;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public InternalTalentSourceControllerTests()
    {
        var collectorService = Substitute.For<IInternalCollectorService>();
        var jobLogService = Substitute.For<ICollectorJobLogService>();
        var cleanupService = Substitute.For<INewsCleanupService>();
        _talentSourceService = Substitute.For<ITalentSourceService>();
        _sut = new InternalController(collectorService, jobLogService, cleanupService, _talentSourceService);
    }

    // ── GET /api/v1/internal/talent-sources: 正常系 ──────────────────────────

    [Fact]
    public async Task GetTalentSources_NoFilter_Returns200WithList()
    {
        var sources = new List<TalentSourceResponseDto>
        {
            BuildSourceResponse(id: 1, talentId: 10, sourceType: "youtube", sourceId: "UC_abc"),
            BuildSourceResponse(id: 2, talentId: 11, sourceType: "youtube", sourceId: "UC_def"),
        };
        _talentSourceService.GetSourcesAsync(null).Returns(sources);

        var result = await _sut.GetTalentSources(null);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var body = Assert.IsAssignableFrom<IReadOnlyList<TalentSourceResponseDto>>(ok.Value);
        Assert.Equal(2, body.Count);
    }

    [Fact]
    public async Task GetTalentSources_WithTalentIdFilter_CallsServiceWithTalentId()
    {
        _talentSourceService.GetSourcesAsync(10).Returns([]);

        await _sut.GetTalentSources(10);

        await _talentSourceService.Received(1).GetSourcesAsync(10);
    }

    [Fact]
    public async Task GetTalentSources_EmptyList_Returns200WithEmptyList()
    {
        _talentSourceService.GetSourcesAsync(null).Returns([]);

        var result = await _sut.GetTalentSources(null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<TalentSourceResponseDto>>(ok.Value);
        Assert.Empty(body);
    }

    // ── POST /api/v1/internal/talent-sources: 正常系 ─────────────────────────

    [Fact]
    public async Task CreateTalentSource_ValidRequest_Returns201()
    {
        var dto = BuildCreateRequest(talentId: 10, sourceType: "youtube", sourceId: "UC_abc");
        _talentSourceService.CreateSourceAsync(dto).Returns(BuildSourceResponse(id: 1, talentId: 10));

        var result = await _sut.CreateTalentSource(dto);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
    }

    [Fact]
    public async Task CreateTalentSource_ValidRequest_ReturnsTalentSourceResponseDto()
    {
        var dto = BuildCreateRequest(talentId: 10, sourceType: "youtube", sourceId: "UC_xyz");
        var expected = BuildSourceResponse(id: 5, talentId: 10, sourceType: "youtube", sourceId: "UC_xyz");
        _talentSourceService.CreateSourceAsync(dto).Returns(expected);

        var result = await _sut.CreateTalentSource(dto);

        var created = Assert.IsType<CreatedResult>(result);
        var body = Assert.IsType<TalentSourceResponseDto>(created.Value);
        Assert.Equal(5, body.Id);
        Assert.Equal("youtube", body.SourceType);
        Assert.Equal("UC_xyz", body.SourceId);
    }

    // ── POST /api/v1/internal/talent-sources: 異常系 ─────────────────────────

    [Fact]
    public async Task CreateTalentSource_TalentNotFound_Returns404()
    {
        var dto = BuildCreateRequest(talentId: 999, sourceType: "youtube", sourceId: "UC_abc");
        _talentSourceService.CreateSourceAsync(dto)
            .Returns<TalentSourceResponseDto>(_ => throw new TalentNotFoundException());

        var result = await _sut.CreateTalentSource(dto);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task CreateTalentSource_Duplicate_Returns409()
    {
        var dto = BuildCreateRequest(talentId: 10, sourceType: "youtube", sourceId: "UC_abc");
        _talentSourceService.CreateSourceAsync(dto)
            .Returns<TalentSourceResponseDto>(_ => throw new DuplicateTalentSourceException());

        var result = await _sut.CreateTalentSource(dto);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    // ── DELETE /api/v1/internal/talent-sources/{id}: 正常系 ──────────────────

    [Fact]
    public async Task DeleteTalentSource_Exists_Returns204()
    {
        _talentSourceService.DeleteSourceAsync(1).Returns(true);

        var result = await _sut.DeleteTalentSource(1);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteTalentSource_Exists_CallsServiceOnce()
    {
        _talentSourceService.DeleteSourceAsync(42).Returns(true);

        await _sut.DeleteTalentSource(42);

        await _talentSourceService.Received(1).DeleteSourceAsync(42);
    }

    // ── DELETE /api/v1/internal/talent-sources/{id}: 異常系 ──────────────────

    [Fact]
    public async Task DeleteTalentSource_NotFound_Returns404()
    {
        _talentSourceService.DeleteSourceAsync(999).Returns(false);

        var result = await _sut.DeleteTalentSource(999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private static CreateTalentSourceRequestDto BuildCreateRequest(
        int talentId,
        string sourceType,
        string sourceId) => new()
        {
            TalentId = talentId,
            SourceType = sourceType,
            SourceId = sourceId,
            IsEnabled = true,
        };

    private TalentSourceResponseDto BuildSourceResponse(
        int id = 1,
        int talentId = 10,
        string sourceType = "youtube",
        string sourceId = "UC_test") => new()
        {
            Id = id,
            TalentId = talentId,
            SourceType = sourceType,
            SourceId = sourceId,
            IsEnabled = true,
            CreatedAt = _now.UtcDateTime,
        };
}
