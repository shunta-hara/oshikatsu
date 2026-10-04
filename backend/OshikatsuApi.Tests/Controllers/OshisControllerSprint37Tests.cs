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

/// <summary>
/// Sprint 37 (FEAT-039) 追加テスト — OshisController の 409 重複登録エラーと
/// GitHubDispatchService の talent_id dispatch を確認する。
/// </summary>
public class OshisControllerSprint37Tests
{
    private readonly IOshiService _oshiService;
    private readonly IGitHubDispatchService _githubDispatchService;
    private readonly OshisController _sut;

    public OshisControllerSprint37Tests()
    {
        _oshiService = Substitute.For<IOshiService>();
        _githubDispatchService = Substitute.For<IGitHubDispatchService>();
        _githubDispatchService.TriggerCollectAsync(Arg.Any<int>())
            .Returns(Task.CompletedTask);
        _sut = new OshisController(_oshiService, _githubDispatchService);
        SetupAuthenticatedUser(userId: 1);
    }

    // ── POST /api/v1/oshis: 409 重複登録 ────────────────────────────────────

    [Fact]
    public async Task Create_DuplicateTalent_Returns409()
    {
        // FEAT-039: 同一ユーザーが同名タレントを再登録すると 409
        var dto = new CreateOshiRequestDto { Name = "重複テスト" };
        _oshiService.CreateAsync(1, dto.Name)
            .ThrowsAsync(new OshiDuplicateException());

        var result = await _sut.Create(dto);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateTalent_ReturnsErrorMessage()
    {
        var dto = new CreateOshiRequestDto { Name = "重複テスト" };
        _oshiService.CreateAsync(1, dto.Name)
            .ThrowsAsync(new OshiDuplicateException());

        var result = await _sut.Create(dto);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<MessageResponseDto>(conflict.Value);
        Assert.False(string.IsNullOrEmpty(body.Message));
    }

    // ── POST /api/v1/oshis: GitHub Actions dispatch（talent_id） ────────────

    [Fact]
    public async Task Create_Success_DispatchesWithTalentId()
    {
        // FEAT-039: dispatch inputs は talent_id を使う
        var dto = new CreateOshiRequestDto { Name = "テスト" };
        var talentId = 42;
        _oshiService.CreateAsync(1, dto.Name)
            .Returns(new OshiResponseDto { Id = 10, Name = dto.Name, CreatedAt = DateTime.UtcNow, TalentId = talentId });

        await _sut.Create(dto);

        // dispatch は talent_id（42）で呼ばれるべき
        await _githubDispatchService.Received(1).TriggerCollectAsync(talentId);
    }

    [Fact]
    public async Task Create_Success_NotDispatchedWithUserOshiId()
    {
        // dispatch は user_oshi_id（Id=10）ではなく talent_id（42）で呼ばれること
        var dto = new CreateOshiRequestDto { Name = "テスト" };
        var userOshiId = 10;
        var talentId = 42;
        _oshiService.CreateAsync(1, dto.Name)
            .Returns(new OshiResponseDto { Id = userOshiId, Name = dto.Name, CreatedAt = DateTime.UtcNow, TalentId = talentId });

        await _sut.Create(dto);

        // user_oshi_id（10）では dispatch されないこと
        await _githubDispatchService.DidNotReceive().TriggerCollectAsync(userOshiId);
    }

    // ── GET /api/v1/oshis: id は user_oshi_id ───────────────────────────────

    [Fact]
    public async Task GetAll_ResponseIds_AreUserOshiIds()
    {
        // FEAT-039: id は user_oshis.id, name は talents.display_name
        var expected = new List<OshiResponseDto>
        {
            new() { Id = 101, Name = "タレントA", CreatedAt = DateTime.UtcNow },
            new() { Id = 202, Name = "タレントB", CreatedAt = DateTime.UtcNow },
        };
        _oshiService.GetAllByUserAsync(1).Returns(expected);

        var result = await _sut.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsAssignableFrom<IReadOnlyList<OshiResponseDto>>(ok.Value);
        Assert.Equal(2, body.Count);
        Assert.Equal(101, body[0].Id);
        Assert.Equal(202, body[1].Id);
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
