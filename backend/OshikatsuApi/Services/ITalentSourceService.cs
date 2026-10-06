using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <summary>
/// タレントソース（talent_sources テーブル）管理サービスのインターフェース。
/// </summary>
public interface ITalentSourceService
{
    /// <summary>タレントのソース一覧を取得する。talentId が null のときは全件取得。</summary>
    Task<IReadOnlyList<TalentSourceResponseDto>> GetSourcesAsync(int? talentId = null);

    /// <summary>タレントにソースを登録する。同一 (talentId, sourceType, sourceId) の重複は上書きしない（409 を返す）。</summary>
    /// <returns>登録されたソース DTO</returns>
    /// <exception cref="OshikatsuApi.Exceptions.TalentNotFoundException">指定した talentId が存在しない</exception>
    /// <exception cref="OshikatsuApi.Exceptions.DuplicateTalentSourceException">既に同一ソースが登録済み</exception>
    Task<TalentSourceResponseDto> CreateSourceAsync(CreateTalentSourceRequestDto dto);

    /// <summary>ソースを削除する。存在しない場合は false を返す。</summary>
    Task<bool> DeleteSourceAsync(int id);
}
