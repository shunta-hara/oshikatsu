using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <summary>推しの登録・取得・削除を担う</summary>
public interface IOshiService
{
    /// <summary>
    /// 推しを登録してレスポンス DTO を返す。
    /// </summary>
    /// <exception cref="Exceptions.OshiLimitExceededException">登録済み推し数が 20 件に達している場合</exception>
    Task<OshiResponseDto> CreateAsync(int userId, string name);

    /// <summary>
    /// ログインユーザーの推し一覧を登録日時昇順で返す。
    /// </summary>
    Task<IReadOnlyList<OshiResponseDto>> GetAllByUserAsync(int userId);

    /// <summary>
    /// 指定した推しを削除する。
    /// </summary>
    /// <exception cref="Exceptions.OshiNotFoundException">推しが存在しない場合</exception>
    /// <exception cref="Exceptions.OshiAccessDeniedException">推しが別のユーザーに属している場合</exception>
    Task DeleteAsync(int userId, int oshiId);
}
