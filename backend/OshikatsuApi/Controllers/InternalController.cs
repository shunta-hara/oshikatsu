using Microsoft.AspNetCore.Mvc;
using OshikatsuApi.Authentication;
using OshikatsuApi.DTOs;
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

    public InternalController(
        IInternalCollectorService collectorService,
        ICollectorJobLogService jobLogService)
    {
        _collectorService = collectorService;
        _jobLogService = jobLogService;
    }

    /// <summary>ニュースアイテムを登録する（重複ソースURLは無視）</summary>
    /// <remarks>
    /// 同一の oshiId + sourceUrl の組み合わせが既に存在する場合、
    /// 409 を返さずに既存レコードを返す（冪等）。
    /// </remarks>
    /// <response code="200">登録成功（または既存重複のため無視）</response>
    /// <response code="400">バリデーションエラー</response>
    /// <response code="401">API キーなし・不正</response>
    [HttpPost("news")]
    [ProducesResponseType(typeof(InternalNewsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateNews([FromBody] CreateNewsItemRequestDto dto)
    {
        var result = await _collectorService.CreateNewsItemAsync(dto);
        return Ok(result);
    }

    /// <summary>全推し一覧を取得する（collector が収集対象を把握するために使用）</summary>
    /// <response code="200">推し一覧</response>
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
    /// 同一の oshiId + sourceUrl の組み合わせが既に存在する場合、
    /// 409 を返さずに既存レコードを返す（冪等）。
    /// </remarks>
    /// <response code="200">登録成功（または既存重複のため無視）</response>
    /// <response code="400">バリデーションエラー</response>
    /// <response code="401">API キーなし・不正</response>
    [HttpPost("programs")]
    [ProducesResponseType(typeof(InternalProgramResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateProgram([FromBody] CreateProgramAppearanceRequestDto dto)
    {
        var result = await _collectorService.CreateProgramAppearanceAsync(dto);
        return Ok(result);
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
}
