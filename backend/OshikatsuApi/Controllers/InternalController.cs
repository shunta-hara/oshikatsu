using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.Authentication;
using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;
using OshikatsuApi.Services;

namespace OshikatsuApi.Controllers;

/// <summary>
/// collector 専用の内部 API エンドポイント。
/// X-Api-Key ヘッダーによる API キー認証が必要。
/// </summary>
[ApiController]
[Route("api/v1/internal")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public class InternalController : ControllerBase
{
    private readonly IInternalCollectorService _collectorService;
    private readonly ICollectorJobLogService _jobLogService;
    private readonly INewsCleanupService _cleanupService;

    public InternalController(
        IInternalCollectorService collectorService,
        ICollectorJobLogService jobLogService,
        INewsCleanupService cleanupService)
    {
        _collectorService = collectorService;
        _jobLogService = jobLogService;
        _cleanupService = cleanupService;
    }

    /// <summary>ニュースアイテムを登録する（重複ソースURLは無視）</summary>
    /// <remarks>
    /// 同一の talentId + sourceUrl の組み合わせが既に存在する場合、
    /// 409 を返さずに既存レコードを返す（冪等）。
    /// </remarks>
    /// <response code="200">登録成功（または既存重複のため無視）</response>
    /// <response code="400">バリデーションエラー</response>
    /// <response code="401">API キーなし・不正</response>
    /// <response code="404">指定した talentId が存在しない</response>
    [HttpPost("news")]
    [ProducesResponseType(typeof(InternalNewsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateNews([FromBody] CreateNewsItemRequestDto dto)
    {
        try
        {
            var result = await _collectorService.CreateNewsItemAsync(dto);
            return Ok(result);
        }
        catch (TalentNotFoundException)
        {
            return NotFound(new MessageResponseDto { Message = "指定した talentId が見つかりません。" });
        }
    }

    /// <summary>タレント一覧を取得する（1人以上のユーザーが登録しているタレントのみ）</summary>
    /// <response code="200">タレント一覧</response>
    /// <response code="401">API キーなし・不正</response>
    [HttpGet("oshis")]
    [ProducesResponseType(typeof(IReadOnlyList<InternalOshiDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOshis()
    {
        var result = await _collectorService.GetAllOshisAsync();
        return Ok(result);
    }

    /// <summary>出演番組情報を登録する（重複ソースURLは無視）</summary>
    /// <remarks>
    /// 同一の talentId + sourceUrl の組み合わせが既に存在する場合、
    /// 409 を返さずに既存レコードを返す（冪等）。
    /// </remarks>
    /// <response code="200">登録成功（または既存重複のため無視）</response>
    /// <response code="400">バリデーションエラー</response>
    /// <response code="401">API キーなし・不正</response>
    /// <response code="404">指定した talentId が存在しない</response>
    [HttpPost("programs")]
    [ProducesResponseType(typeof(InternalProgramResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateProgram([FromBody] CreateProgramAppearanceRequestDto dto)
    {
        try
        {
            var result = await _collectorService.CreateProgramAppearanceAsync(dto);
            return Ok(result);
        }
        catch (TalentNotFoundException)
        {
            return NotFound(new MessageResponseDto { Message = "指定した talentId が見つかりません。" });
        }
    }

    /// <summary>ニュースアイテムを一括登録する（重複ソースURL・同タレントのニュース既存URLは無視）</summary>
    /// <remarks>
    /// <para>リクエスト内で重複する sourceUrl は最初の出現のみ保存する。</para>
    /// <para>同一の talentId + sourceUrl が既に存在する場合もスキップされる。</para>
    /// </remarks>
    /// <response code="200">一括登録結果（受信件数・保存件数・スキップ件数）</response>
    /// <response code="400">items が空または200件超、必須項目不足</response>
    /// <response code="401">API キーなし・不正</response>
    /// <response code="404">指定した talentId が存在しない</response>
    [HttpPost("news/batch")]
    [ProducesResponseType(typeof(BatchResultResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BatchCreateNews([FromBody] BatchCreateNewsRequestDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest(new MessageResponseDto { Message = "items は1件以上必要です。" });
        if (dto.Items.Count > 200)
            return BadRequest(new MessageResponseDto { Message = "items は200件以下で送信してください。" });

        try
        {
            var result = await _collectorService.BatchCreateNewsItemsAsync(dto);
            return Ok(result);
        }
        catch (TalentNotFoundException)
        {
            return NotFound(new MessageResponseDto { Message = "指定した talentId が見つかりません。" });
        }
    }

    /// <summary>出演番組情報を一括登録する（重複ソースURL・同タレントのニュース既存URLは無視）</summary>
    /// <remarks>
    /// <para>リクエスト内で重複する sourceUrl は最初の出現のみ保存する。</para>
    /// <para>同一の talentId + sourceUrl が既に番組または同タレントのニュースに存在する場合もスキップされる（FEAT-024）。</para>
    /// </remarks>
    /// <response code="200">一括登録結果（受信件数・保存件数・スキップ件数）</response>
    /// <response code="400">items が空または200件超、必須項目不足</response>
    /// <response code="401">API キーなし・不正</response>
    /// <response code="404">指定した talentId が存在しない</response>
    [HttpPost("programs/batch")]
    [ProducesResponseType(typeof(BatchResultResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BatchCreatePrograms([FromBody] BatchCreateProgramsRequestDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest(new MessageResponseDto { Message = "items は1件以上必要です。" });
        if (dto.Items.Count > 200)
            return BadRequest(new MessageResponseDto { Message = "items は200件以下で送信してください。" });

        try
        {
            var result = await _collectorService.BatchCreateProgramAppearancesAsync(dto);
            return Ok(result);
        }
        catch (TalentNotFoundException)
        {
            return NotFound(new MessageResponseDto { Message = "指定した talentId が見つかりません。" });
        }
    }

    /// <summary>collector バッチの実行ログを記録する</summary>
    /// <response code="200">記録成功</response>
    /// <response code="400">バリデーションエラー</response>
    /// <response code="401">API キーなし・不正</response>
    [HttpPost("job-logs")]
    [ProducesResponseType(typeof(JobLogResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateJobLog([FromBody] CreateJobLogRequestDto dto)
    {
        var result = await _jobLogService.RecordJobLogAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// 保持期間（90日）を超えた未ブックマーク記事を一括削除する。
    /// PublishedAt が null の場合は CollectedAt を基準とする。
    /// ブックマーク済みの記事は削除しない。
    /// 1回の実行で最大 1000 件を削除し、大量レコード蓄積時のロックを抑制する。
    /// </summary>
    /// <response code="200">削除成功。削除件数を JSON で返す。</response>
    /// <response code="401">API キーなし・不正</response>
    [HttpDelete("news/expired")]
    [ProducesResponseType(typeof(DeleteExpiredNewsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteExpiredNews()
    {
        var deletedCount = await _cleanupService.DeleteExpiredNewsAsync();
        return Ok(new DeleteExpiredNewsResponseDto { DeletedCount = deletedCount });
    }
}
