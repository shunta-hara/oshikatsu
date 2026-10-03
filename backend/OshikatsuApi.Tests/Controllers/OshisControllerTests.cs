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

public class OshisControllerTests
{
    private readonly IOshiService _oshiService;
    private readonly IGitHubDispatchService _githubDispatchService;
    private readonly OshisController _sut;

    public OshisControllerTests()
    {
        _oshiService = Substitute.For<IOshiService>();
        _githubDispatchService = Substitute.For<IGitHubDispatchService>();
        _githubDispatchService.TriggerCollectAsync(Arg.Any<int>())
            .Returns(Task.CompletedTask);
        _sut = new OshisController(_oshiService, _githubDispatchService);
        SetupAuthenticatedUser(userId: 1);
    }

    // ── POST /api/v1/oshis: 正常系 ─────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201()
    {
        var dto = new CreateOshiRequestDto { Name = "鈴木一郎" };
        _oshiService.CreateAsync(1, dto.Name)
            .Returns(new OshiResponseDto { Id = 1, Name = dto.Name, CreatedAt = DateTime.UtcNow });

        var result = await _sut.Create(dto);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, status.StatusCode);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOshiResponseDto()
    {
        var dto = new CreateOshiRequestDto { Name = "鈴木一郎" };
        var expected = new OshiResponseDto { Id = 1, Name = "鈴木一郎", CreatedAt = DateTime.UtcNow };
        _oshiService.CreateAsync(1, dto.Name).Returns(expected);

        var result = await _sut.Create(dto);

        var status = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsType<OshiResponseDto>(status.Value);
        Assert.Equal("鈴木一郎", body.Name);
        Assert.Equal(1, body.Id);
    }

    [Fact]
    public async Task Create_ValidRequest_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 42);
        var dto = new CreateOshiRequestDto { Name = "テスト" };
        _oshiService.CreateAsync(42, dto.Name)
            .Returns(new OshiResponseDto { Id = 1, Name = dto.Name, CreatedAt = DateTime.UtcNow });

        await _sut.Create(dto);

        await _oshiService.Received(1).CreateAsync(42, dto.Name);
    }

    // ── POST /api/v1/oshis: 異常系 ─────────────────────────────────────────

    [Fact]
    public async Task Create_LimitExceeded_Returns422()
    {
        var dto = new CreateOshiRequestDto { Name = "21件目" };
        _oshiService.CreateAsync(1, dto.Name)
            .ThrowsAsync(new OshiLimitExceededException());

        var result = await _sut.Create(dto);

        var status = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, status.StatusCode);
    }

    [Fact]
    public async Task Create_LimitExceeded_ReturnsErrorMessage()
    {
        var dto = new CreateOshiRequestDto { Name = "21件目" };
        _oshiService.CreateAsync(1, dto.Name)
            .ThrowsAsync(new OshiLimitExceededException());

        var result = await _sut.Create(dto);

        var status = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(status.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    // ── GET /api/v1/oshis: 正常系 ──────────────────────────────────────────

    [Fact]
    public async Task GetAll_AuthenticatedUser_Returns200()
    {
        _oshiService.GetAllByUserAsync(1)
            .Returns(Array.Empty<OshiResponseDto>());

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithOshis_ReturnsListOfOshiResponseDto()
    {
        var expected = new List<OshiResponseDto>
        {
            new() { Id = 1, Name = "推しA", CreatedAt = DateTime.UtcNow },
            new() { Id = 2, Name = "推しB", CreatedAt = DateTime.UtcNow },
        };
        _oshiService.GetAllByUserAsync(1).Returns(expected);

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<OshiResponseDto>>(ok.Value);
        Assert.Equal(2, body.Count);
    }

    // ── GET /api/v1/oshis: 境界値 ──────────────────────────────────────────

    [Fact]
    public async Task GetAll_NoOshis_ReturnsEmptyList()
    {
        _oshiService.GetAllByUserAsync(1)
            .Returns(Array.Empty<OshiResponseDto>());

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<OshiResponseDto>>(ok.Value);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetAll_CallsServiceWithCurrentUserId()
    {
        SetupAuthenticatedUser(userId: 99);
        _oshiService.GetAllByUserAsync(99)
            .Returns(Array.Empty<OshiResponseDto>());

        await _sut.GetAll();

        await _oshiService.Received(1).GetAllByUserAsync(99);
    }

    // ── DELETE /api/v1/oshis/{id}: 正常系 ──────────────────────────────────

    [Fact]
    public async Task Delete_OwnOshi_Returns204()
    {
        _oshiService.DeleteAsync(1, 5).Returns(Task.CompletedTask);

        var result = await _sut.Delete(5);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_CallsServiceWithCorrectIds()
    {
        SetupAuthenticatedUser(userId: 7);
        _oshiService.DeleteAsync(7, 3).Returns(Task.CompletedTask);

        await _sut.Delete(3);

        await _oshiService.Received(1).DeleteAsync(7, 3);
    }

    // ── DELETE /api/v1/oshis/{id}: 異常系 ──────────────────────────────────

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        _oshiService.DeleteAsync(1, 9999)
            .ThrowsAsync(new OshiNotFoundException());

        var result = await _sut.Delete(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsErrorMessage()
    {
        _oshiService.DeleteAsync(1, 9999)
            .ThrowsAsync(new OshiNotFoundException());

        var result = await _sut.Delete(9999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(notFound.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    [Fact]
    public async Task Delete_OtherUsersOshi_Returns403()
    {
        _oshiService.DeleteAsync(1, 2)
            .ThrowsAsync(new OshiAccessDeniedException());

        var result = await _sut.Delete(2);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherUsersOshi_ReturnsErrorMessage()
    {
        _oshiService.DeleteAsync(1, 2)
            .ThrowsAsync(new OshiAccessDeniedException());

        var result = await _sut.Delete(2);

        var forbidden = Assert.IsType<ObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(forbidden.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    // ── ヘルパー ────────────────────────────────────────────────────────────

    private void SetupAuthenticatedUser(int userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
    }
}
