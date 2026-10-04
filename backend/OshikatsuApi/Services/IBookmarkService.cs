using OshikatsuApi.DTOs;
using OshikatsuApi.Exceptions;

namespace OshikatsuApi.Services;

/// <summary>ニュース記事のブックマーク管理を担う</summary>
public interface IBookmarkService
{
    /// <summary>
    /// 指定したニュースアイテムをブックマークに追加する。
    /// </summary>
    /// <exception cref="NewsItemNotFoundException">ニュースアイテムが存在しない場合</exception>
    /// <exception cref="BookmarkAlreadyExistsException">すでにブックマーク済みの場合</exception>
    Task AddBookmarkAsync(int userId, int newsItemId);

    /// <summary>
    /// 指定したニュースアイテムのブックマークを解除する。
    /// </summary>
    /// <exception cref="BookmarkNotFoundException">ブックマークが存在しない場合</exception>
    Task RemoveBookmarkAsync(int userId, int newsItemId);

    /// <summary>
    /// ユーザーのブックマーク一覧をブックマーク登録日時の新しい順でページネーション取得する。
    /// </summary>
    /// <param name="userId">対象ユーザーID</param>
    /// <param name="page">ページ番号（1起算）</param>
    /// <param name="pageSize">1ページあたりの件数</param>
    /// <param name="oshiId">推しIDで絞り込む（任意）。自分の推し以外を指定しても空を返す</param>
    /// <param name="query">タイトル・要約のキーワード部分一致（英字の大小文字を区別しない）</param>
    /// <param name="from">掲載日の下限（任意・オフセット付き日時を UTC に正規化して使用）</param>
    /// <param name="to">掲載日の上限（任意・オフセット付き日時を UTC に正規化して使用）</param>
    Task<PaginatedResponseDto<BookmarkResponseDto>> GetBookmarksAsync(
        int userId, int page, int pageSize,
        int? oshiId = null, string? query = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null);
}
