using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <summary>
/// collector から呼ばれる内部APIのビジネスロジック。
/// 同一ソースURLの重複登録は無視する（冪等）。
/// </summary>
public interface IInternalCollectorService
{
    /// <summary>ニュースアイテムを登録する（重複は無視）</summary>
    Task<InternalNewsResponseDto> CreateNewsItemAsync(CreateNewsItemRequestDto dto);

    /// <summary>出演番組情報を登録する（重複は無視）</summary>
    Task<InternalProgramResponseDto> CreateProgramAppearanceAsync(CreateProgramAppearanceRequestDto dto);

    /// <summary>全ユーザーの推し一覧を返す（collector が収集対象を把握するために使用）</summary>
    Task<IReadOnlyList<InternalOshiDto>> GetAllOshisAsync();

    /// <summary>
    /// ニュースアイテムを一括登録する。
    /// 重複する sourceUrl（DB既存・リクエスト内）はスキップする。
    /// 存在しない oshiId は <see cref="OshikatsuApi.Exceptions.OshiNotFoundException"/> をスロー。
    /// </summary>
    Task<BatchResultResponseDto> BatchCreateNewsItemsAsync(BatchCreateNewsRequestDto dto);

    /// <summary>
    /// 出演番組アイテムを一括登録する。
    /// 重複する sourceUrl（DB既存・リクエスト内・同推しのニュースに既存）はスキップする。
    /// 存在しない oshiId は <see cref="OshikatsuApi.Exceptions.OshiNotFoundException"/> をスロー。
    /// </summary>
    Task<BatchResultResponseDto> BatchCreateProgramAppearancesAsync(BatchCreateProgramsRequestDto dto);
}
