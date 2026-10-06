using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OshikatsuApi.Authentication;
using OshikatsuApi.DTOs;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>
/// Web Push 通知送信ジョブ用の内部 API エンドポイント。
/// X-Api-Key ヘッダーによる API キー認証が必要。
/// scheduler（Cloudflare Workers）から定期呼び出しされる。
/// </summary>
[ApiController]
[Route("api/v1/internal/push")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public class InternalPushController : ControllerBase
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILogger<InternalPushController> _logger;

    public InternalPushController(
        IPushNotificationService pushNotificationService,
        ILogger<InternalPushController> logger)
    {
        _pushNotificationService = pushNotificationService;
        _logger = logger;
    }

    /// <summary>全ユーザーへの Web Push 通知送信ジョブを実行する</summary>
    /// <remarks>
    /// リクエストボディに CustomMessage が設定されている場合は全購読者へブロードキャスト送信する（FEAT-053 DB容量警告等）。
    /// CustomMessage が未設定の場合は直近通知送信以降に新着アイテムがあるユーザーへのみ通知を送信する。
    /// 410 Gone の endpoint は自動削除される。
    /// 送信失敗は最大 3 回リトライ（指数バックオフ）する。
    /// </remarks>
    /// <response code="200">ジョブ実行完了。成功・失敗・スキップ件数を含む結果を返す。</response>
    /// <response code="401">API キーなし・不正</response>
    [HttpPost("notify")]
    [ProducesResponseType(typeof(PushNotifyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Notify(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PushNotifyRequestDto? request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Push 通知ジョブ開始");

        PushNotifyResult result;
        if (!string.IsNullOrWhiteSpace(request?.CustomMessage))
        {
            _logger.LogInformation("カスタムメッセージブロードキャスト: {Message}", request.CustomMessage);
            result = await _pushNotificationService.BroadcastMessageAsync(request.CustomMessage, cancellationToken);
        }
        else
        {
            result = await _pushNotificationService.NotifyAllUsersAsync(cancellationToken);
        }

        return Ok(new PushNotifyResponseDto
        {
            SuccessCount = result.SuccessCount,
            FailureCount = result.FailureCount,
            SkipCount = result.SkipCount,
            GoneRemovedCount = result.GoneRemovedCount,
            WarningSentCount = result.WarningSentCount,
        });
    }
}
