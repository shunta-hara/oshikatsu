using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace OshikatsuApi.Services;

/// <inheritdoc />
public class GitHubDispatchService : IGitHubDispatchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubDispatchService> _logger;

    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(5);

    public GitHubDispatchService(
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubDispatchService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task TriggerCollectAsync(int oshiId)
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_DISPATCH_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogInformation(
                "GITHUB_DISPATCH_TOKEN が未設定のため workflow_dispatch をスキップします。");
            return;
        }

        var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
                         ?? string.Empty;
        if (string.IsNullOrEmpty(repository))
        {
            _logger.LogWarning(
                "GITHUB_REPOSITORY が未設定のため workflow_dispatch をスキップします。");
            return;
        }

        var workflowFile = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW_FILE")
                           ?? "collect.yml";
        var gitRef = Environment.GetEnvironmentVariable("GITHUB_REF")
                     ?? "master";

        var url =
            $"https://api.github.com/repos/{repository}/actions/workflows/{workflowFile}/dispatches";

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(GitHubDispatchService));
            using var cts = new CancellationTokenSource(DispatchTimeout);

            var payload = new
            {
                @ref = gitRef,
                inputs = new { oshi_id = oshiId.ToString() },
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = JsonContent.Create(payload);
            // GitHub API 必須ヘッダー（トークンはログに出力しない）
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Accept", "application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.Add("User-Agent", "oshikatsu-backend");

            var response = await client.SendAsync(request, cts.Token);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                _logger.LogInformation(
                    "workflow_dispatch を起動しました（oshiId={OshiId}）。", oshiId);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning(
                    "workflow_dispatch の起動に失敗しました（oshiId={OshiId}, status={Status}）。",
                    oshiId, (int)response.StatusCode);
                // body はデバッグ用に Debug レベルで出す（本番ログに流さない）
                _logger.LogDebug("GitHub API レスポンス本文: {Body}", body);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "workflow_dispatch がタイムアウトしました（oshiId={OshiId}）。", oshiId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "workflow_dispatch の起動中に例外が発生しました（oshiId={OshiId}）。", oshiId);
        }
    }
}
