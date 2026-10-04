using OshikatsuApi.Entities;

namespace OshikatsuApi.Services;

/// <summary>タレントの検索・作成を担うサービス</summary>
public interface ITalentService
{
    /// <summary>
    /// 正規化済み名前が一致するタレントを返す。存在しない場合は新規作成して返す。
    /// display_name は最初に登録したユーザーの入力値を保持し、以降は変更しない。
    /// </summary>
    /// <param name="name">入力された名前（最初の登録時に display_name として保存される）</param>
    /// <returns>既存または新規作成したタレント</returns>
    Task<Talent> FindOrCreateTalentAsync(string name);
}
