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
    Task<PaginatedResponseDto<BookmarkResponseDto>> GetBookmarksAsync(int userId, int page, int pageSize);
}
