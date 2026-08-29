using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.Controllers;
using OshikatsuApi.DTOs;
using Xunit;

namespace OshikatsuApi.Tests.Controllers;

public class HealthControllerTests
{
    private readonly HealthController _sut;

    public HealthControllerTests()
    {
        _sut = new HealthController();
    }

    // ── 正常系 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Get_Returns200Ok()
    {
        var result = _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public void Get_ReturnsHealthResponseDto()
    {
        var result = _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<HealthResponseDto>(ok.Value);
    }

    [Fact]
    public void Get_StatusIsOk()
    {
        var result = _sut.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<HealthResponseDto>(ok.Value);
        Assert.Equal("ok", dto.Status);
    }

    [Fact]
    public void Get_TimestampIsRecentUtc()
    {
        var before = DateTime.UtcNow;

        var result = _sut.Get();

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

    // ── 境界値 ──────────────────────────────────────────────────────────────

    [Fact]
    public void Get_CalledMultipleTimes_AlwaysReturnsOk()
    {
        // 複数回呼んでも常に 200 を返す（冪等性）
        for (var i = 0; i < 5; i++)
        {
            var result = _sut.Get();
            var ok = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<HealthResponseDto>(ok.Value);
            Assert.Equal("ok", dto.Status);
        }
    }

    [Fact]
    public void Get_ConsecutiveCalls_TimestampsAreNonDecreasing()
    {
        var result1 = _sut.Get();
        var result2 = _sut.Get();

        var dto1 = Assert.IsType<HealthResponseDto>(
            Assert.IsType<OkObjectResult>(result1).Value);
        var dto2 = Assert.IsType<HealthResponseDto>(
            Assert.IsType<OkObjectResult>(result2).Value);

        Assert.True(dto2.Timestamp >= dto1.Timestamp,
            "Later call should have equal or newer timestamp");
    }
}
