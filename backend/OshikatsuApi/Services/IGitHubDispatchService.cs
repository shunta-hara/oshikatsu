namespace OshikatsuApi.Services;

/// <summary>GitHub Actions の workflow_dispatch を起動するサービス</summary>
public interface IGitHubDispatchService
{
    /// <summary>
    /// <c>collect.yml</c> を workflow_dispatch で起動し、<paramref name="oshiId"/> を inputs に渡す。
    /// <c>GITHUB_DISPATCH_TOKEN</c> が未設定の場合は何もしない。
    /// GitHub API の失敗は例外を握りつぶし警告ログを出力する（呼び出し元を失敗させない）。
    /// </summary>
    Task TriggerCollectAsync(int oshiId);
}
