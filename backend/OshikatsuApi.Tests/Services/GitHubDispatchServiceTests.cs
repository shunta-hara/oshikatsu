using System.Net;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OshikatsuApi.Services;
using Xunit;

namespace OshikatsuApi.Tests.Services;

public class GitHubDispatchServiceTests : IDisposable
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<GitHubDispatchService> _logger;
    private FakeHttpMessageHandler? _fakeHandler;

    // 環境変数を汚染しないように元の値を保存する
    private readonly string? _originalToken;
    private readonly string? _originalRepo;
    private readonly string? _originalWorkflow;
    private readonly string? _originalRef;

    public GitHubDispatchServiceTests()
    {
        _factory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<GitHubDispatchService>>();

        _originalToken = Environment.GetEnvironmentVariable("GITHUB_DISPATCH_TOKEN");
        _originalRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        _originalWorkflow = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW_FILE");
        _originalRef = Environment.GetEnvironmentVariable("GITHUB_REF");
    }

    public void Dispose()
    {
        // テスト後に環境変数を元に戻す
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", _originalToken);
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", _originalRepo);
        Environment.SetEnvironmentVariable("GITHUB_WORKFLOW_FILE", _originalWorkflow);
        Environment.SetEnvironmentVariable("GITHUB_REF", _originalRef);

        _fakeHandler?.Dispose();
    }

    private GitHubDispatchService CreateSut() => new(_factory, _logger);

    private void SetupHttpClient(HttpStatusCode statusCode = HttpStatusCode.NoContent,
        bool throwException = false)
    {
        _fakeHandler = new FakeHttpMessageHandler(statusCode, throwException);
        var client = new HttpClient(_fakeHandler);
        _factory.CreateClient(nameof(GitHubDispatchService)).Returns(client);
    }

    // ── 正常系 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TriggerCollectAsync_NoToken_DoesNotMakeHttpCall()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", null);
        var sut = CreateSut();

        await sut.TriggerCollectAsync(1);

        _factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task TriggerCollectAsync_EmptyToken_DoesNotMakeHttpCall()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "");
        var sut = CreateSut();

        await sut.TriggerCollectAsync(1);

        _factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task TriggerCollectAsync_WithToken_MakesPostToGitHubApi()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "owner/repo");
        Environment.SetEnvironmentVariable("GITHUB_WORKFLOW_FILE", "collect.yml");
        Environment.SetEnvironmentVariable("GITHUB_REF", "master");
        SetupHttpClient(HttpStatusCode.NoContent);
        var sut = CreateSut();

        await sut.TriggerCollectAsync(42);

        Assert.Equal(1, _fakeHandler!.RequestCount);
        Assert.Equal(HttpMethod.Post, _fakeHandler.LastRequest!.Method);
        Assert.Contains("owner/repo", _fakeHandler.LastRequest.RequestUri!.ToString());
        Assert.Contains("collect.yml", _fakeHandler.LastRequest.RequestUri.ToString());
    }

    [Fact]
    public async Task TriggerCollectAsync_WithToken_SendsTalentIdInBody()
    {
        // FEAT-039: oshi_id → talent_id に変更
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "owner/repo");
        SetupHttpClient(HttpStatusCode.NoContent);
        var sut = CreateSut();

        await sut.TriggerCollectAsync(99);

        // FakeHandler がリクエスト送信時に本文を読み取って保存している
        Assert.Contains("99", _fakeHandler!.LastRequestBody);
        Assert.Contains("talent_id", _fakeHandler.LastRequestBody);
        Assert.DoesNotContain("oshi_id", _fakeHandler.LastRequestBody);
    }

    [Fact]
    public async Task TriggerCollectAsync_DefaultWorkflowFile_UsesCollectYml()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "owner/repo");
        Environment.SetEnvironmentVariable("GITHUB_WORKFLOW_FILE", null);
        SetupHttpClient(HttpStatusCode.NoContent);
        var sut = CreateSut();

        await sut.TriggerCollectAsync(1);

        Assert.Contains("collect.yml", _fakeHandler!.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task TriggerCollectAsync_WithToken_SetsRequiredGitHubHeaders()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "owner/repo");
        SetupHttpClient(HttpStatusCode.NoContent);
        var sut = CreateSut();

        await sut.TriggerCollectAsync(1);

        var req = _fakeHandler!.LastRequest!;
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.True(req.Headers.Contains("Accept"));
        Assert.True(req.Headers.Contains("X-GitHub-Api-Version"));
        Assert.True(req.Headers.Contains("User-Agent"));
    }

    // ── 異常系 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TriggerCollectAsync_GitHubReturnsError_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "owner/repo");
        SetupHttpClient(HttpStatusCode.Unauthorized);
        var sut = CreateSut();

        var exception = await Record.ExceptionAsync(() => sut.TriggerCollectAsync(1));

        Assert.Null(exception);
    }

    [Fact]
    public async Task TriggerCollectAsync_NetworkError_DoesNotThrow()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "owner/repo");
        SetupHttpClient(throwException: true);
        var sut = CreateSut();

        var exception = await Record.ExceptionAsync(() => sut.TriggerCollectAsync(1));

        Assert.Null(exception);
    }

    // ── 境界値 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task TriggerCollectAsync_NoRepository_DoesNotMakeHttpCall()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", null);
        var sut = CreateSut();

        await sut.TriggerCollectAsync(1);

        _factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task TriggerCollectAsync_EmptyRepository_DoesNotMakeHttpCall()
    {
        Environment.SetEnvironmentVariable("GITHUB_DISPATCH_TOKEN", "test-token");
        Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "");
        var sut = CreateSut();

        await sut.TriggerCollectAsync(1);

        _factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }
}

/// <summary>テスト用のフェイク HTTP ハンドラー</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly bool _throwException;

    public int RequestCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }
    /// <summary>リクエスト本文を SendAsync 内で読み取った文字列（Content が Dispose される前に保存）</summary>
    public string LastRequestBody { get; private set; } = string.Empty;

    public FakeHttpMessageHandler(
        HttpStatusCode statusCode = HttpStatusCode.NoContent,
        bool throwException = false)
    {
        _statusCode = statusCode;
        _throwException = throwException;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_throwException)
        {
            throw new HttpRequestException("Simulated network error");
        }

        RequestCount++;
        LastRequest = request;
        // Content は Dispose される前にここで読み取る
        if (request.Content != null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(_statusCode);
    }
}
