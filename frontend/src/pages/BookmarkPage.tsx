import { useEffect, useState } from "react";
import { getBookmarks } from "../api/bookmark";
import { ApiError } from "../api/client";
import type {
  BookmarkResponse,
  BookmarksPaginatedResponse,
} from "../types/bookmark";
import { NavBar } from "../components/NavBar";
import { FeedPagination } from "../components/feed/FeedPagination";
import { BookmarkButton } from "../components/feed/BookmarkButton";

/**
 * ブックマーク一覧ページ。
 * ログインユーザーがブックマークしたニュース記事を登録日時の新しい順で表示する。
 * 各アイテムをカード形式で表示し、タイトル・推し名・出典・掲載日・ブックマーク解除を一目で確認できる。
 */
export function BookmarkPage() {
  const [data, setData] = useState<BookmarksPaginatedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchBookmarks = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getBookmarks(page, 20);
        setData(result);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("ブックマークの取得に失敗しました");
        }
      } finally {
        setIsLoading(false);
      }
    };

    void fetchBookmarks();
  }, [page]);

  return (
    <>
      <NavBar />
      <main className="page-content">
        <h2 className="page-heading">ブックマーク一覧</h2>

        {isLoading && <p className="loading-text">読み込み中...</p>}

        {!isLoading && error && (
          <div role="alert" aria-live="assertive" className="error-message">
            {error}
          </div>
        )}

        {!isLoading && !error && data && data.items.length === 0 && (
          <p className="empty-state">ブックマークした記事はありません</p>
        )}

        {!isLoading && !error && data && data.items.length > 0 && (
          <>
            <ul aria-label="ブックマーク一覧" className="feed-list">
              {data.items.map((item: BookmarkResponse) => (
                <li key={item.id}>
                  <article className="feed-card">
                    <a
                      className="feed-card__title"
                      href={item.sourceUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      {item.title}
                    </a>
                    <div className="feed-card__meta">
                      <span className="feed-card__oshi-badge">
                        {item.oshiName}
                      </span>
                      <time dateTime={item.collectedAt}>
                        掲載日:{" "}
                        {new Date(item.collectedAt).toLocaleString("ja-JP", {
                          year: "numeric",
                          month: "numeric",
                          day: "numeric",
                          hour: "2-digit",
                          minute: "2-digit",
                        })}
                      </time>
                    </div>
                    {item.summary && (
                      <p className="feed-card__summary">{item.summary}</p>
                    )}
                    <div className="feed-card__actions">
                      <BookmarkButton
                        newsItemId={item.newsItemId}
                        initialIsBookmarked={true}
                      />
                    </div>
                  </article>
                </li>
              ))}
            </ul>
            <FeedPagination
              currentPage={data.page}
              totalPages={data.totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </main>
    </>
  );
}
