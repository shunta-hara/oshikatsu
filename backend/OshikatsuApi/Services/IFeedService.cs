using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <summary>ユーザー向けニュース・出演番組フィードの取得を担う</summary>
public interface IFeedService
{
    /// <summary>
    /// ユーザーの推し全員分（または特定の推し）のニュースを収集日時の新しい順でページネーション取得する。
    /// oshiId が指定された場合はその推しのニュースのみ返す。指定した oshiId がユーザーの推しでない場合は空を返す。
    /// </summary>
    Task<PaginatedResponseDto<NewsItemResponseDto>> GetNewsAsync(int userId, int page, int pageSize, int? oshiId = null);

    /// <summary>
    /// ユーザーの推し全員分（または特定の推し）の出演番組を収集日時の新しい順でページネーション取得する。
    /// oshiId が指定された場合はその推しの番組のみ返す。指定した oshiId がユーザーの推しでない場合は空を返す。
    /// </summary>
    Task<PaginatedResponseDto<ProgramAppearanceResponseDto>> GetProgramsAsync(int userId, int page, int pageSize, int? oshiId = null);
}
