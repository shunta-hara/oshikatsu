using OshikatsuApi.DTOs;

namespace OshikatsuApi.Services;

/// <summary>推しの登録・取得・削除を担う</summary>
public interface IOshiService
{
    /// <summary>
    /// 推しを登録してレスポンス DTO を返す。
    /// タレントモデル対応: 同名タレントが既存の場合は既存 Talent を再利用し、
    /// 新しい UserOshi レコードを作成する。
    /// </summary>
    /// <exception cref="Exceptions.OshiLimitExceededException">登録済み推し数が 20 件に達している場合</exception>
    /// <exception cref="Exceptions.OshiDuplicateException">同一ユーザーが既に同じタレントを登録済みの場合</exception>
    Task<OshiResponseDto> CreateAsync(int userId, string name);

    /// <summary>
    /// ログインユーザーの推し一覧を登録日時昇順で返す。
    /// id は user_oshis.id、name は talents.display_name を返す。
    /// </summary>
    Task<IReadOnlyList<OshiResponseDto>> GetAllByUserAsync(int userId);

    /// <summary>
    /// 指定した推し（UserOshi）を削除する。
    /// Talent・NewsItem・ProgramAppearance は削除しない。
    /// </summary>
    /// <exception cref="Exceptions.OshiNotFoundException">推しが存在しない場合</exception>
    /// <exception cref="Exceptions.OshiAccessDeniedException">推しが別のユーザーに属している場合</exception>
    Task DeleteAsync(int userId, int userOshiId);
}
