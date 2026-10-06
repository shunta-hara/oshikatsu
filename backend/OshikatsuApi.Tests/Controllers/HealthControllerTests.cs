using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class HealthControllerTests
{
    private readonly IDbSizeService _mockDbSizeService;
    private readonly HealthController _sut;

    public HealthControllerTests()
    {
        _mockDbSizeService = Substitute.For<IDbSizeService>();
        // デフォルト: InMemory 環境を想定して null を返す
        _mockDbSizeService.GetDbSizeBytesAsync().Returns((long?)null);
        _sut = new HealthController(_mockDbSizeService);
    }

    // ── 正常系 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_Returns200Ok()
    {
        var result = await _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsHealthResponseDto()
    {
        var result = await _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<HealthResponseDto>(ok.Value);
    }

    [Fact]
    public async Task Get_StatusIsOk()
    {
        var result = await _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<HealthResponseDto>(ok.Value);
        Assert.Equal("ok", dto.Status);
    }

    [Fact]
    public async Task Get_TimestampIsRecentUtc()
    {
        var before = DateTime.UtcNow;

        var result = await _sut.Get();

        var after = DateTime.UtcNow;
        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<HealthResponseDto>(ok.Value);

        // タイムスタンプがリクエスト前後の UTC 時刻の範囲内であること
        Assert.True(dto.Timestamp >= before,
            $"Timestamp {dto.Timestamp:O} should be >= {before:O}");
        Assert.True(dto.Timestamp <= after,
            $"Timestamp {dto.Timestamp:O} should be <= {after:O}");
        Assert.Equal(DateTimeKind.Utc, dto.Timestamp.Kind);
    }

    // ── DB サイズ（FEAT-053）────────────────────────────────────────────────

    [Fact]
    public async Task Get_DbSizeNull_WhenServiceReturnsNull()
    {
        _mockDbSizeService.GetDbSizeBytesAsync().Returns((long?)null);

        var result = await _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<HealthResponseDto>(ok.Value);
        Assert.Null(dto.DbSizeBytes);
        Assert.Null(dto.DbSizeMb);
    }

    [Fact]
    public async Task Get_DbSizePopulated_WhenServiceReturnsValue()
    {
        // 200 MB ≈ 209_715_200 bytes
        const long sizeBytes = 209_715_200L;
        _mockDbSizeService.GetDbSizeBytesAsync().Returns(sizeBytes);

        var result = await _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<HealthResponseDto>(ok.Value);
        Assert.Equal(sizeBytes, dto.DbSizeBytes);
        Assert.NotNull(dto.DbSizeMb);
        // 200 MB ± 0.5
        Assert.InRange(dto.DbSizeMb!.Value, 199.5, 200.5);
    }

    [Fact]
    public async Task Get_DbSizeMb_400MbBoundary_IsCorrectlyCalculated()
    {
        // 419_430_400 bytes = 400.00 MB（1 MB = 1_048_576 bytes）
        const long sizeBytes = 419_430_400L;
        _mockDbSizeService.GetDbSizeBytesAsync().Returns(sizeBytes);

        var result = await _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<HealthResponseDto>(ok.Value);
        Assert.Equal(400.0, dto.DbSizeMb!.Value, 1);
    }

    [Fact]
    public async Task Get_DbSizeMb_1Byte_IsNearZero()
    {
        _mockDbSizeService.GetDbSizeBytesAsync().Returns(1L);

        var result = await _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<HealthResponseDto>(ok.Value);
        Assert.NotNull(dto.DbSizeMb);
        // 1 byte = 0 MB (切り捨て)
        Assert.Equal(0.0, dto.DbSizeMb!.Value, 5);
    }

    // ── 境界値 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_CalledMultipleTimes_AlwaysReturnsOk()
    {
        // 複数回呼んでも常に 200 を返す（冪等性）
        for (var i = 0; i < 5; i++)
        {
            var result = await _sut.Get();
            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<HealthResponseDto>(ok.Value);
            Assert.Equal("ok", dto.Status);
        }
    }

    [Fact]
    public async Task Get_ConsecutiveCalls_TimestampsAreNonDecreasing()
    {
        var result1 = await _sut.Get();
        var result2 = await _sut.Get();

        var dto1 = Assert.IsType<HealthResponseDto>(
            Assert.IsType<OkObjectResult>(result1).Value);
        var dto2 = Assert.IsType<HealthResponseDto>(
            Assert.IsType<OkObjectResult>(result2).Value);

        Assert.True(dto2.Timestamp >= dto1.Timestamp,
            "Later call should have equal or newer timestamp");
    }
}
