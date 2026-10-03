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
        <h2>ブックマーク一覧</h2>

        {isLoading && <p>読み込み中...</p>}

        {!isLoading && error && (
          <div role="alert" aria-live="assertive">
            {error}
          </div>
        )}

        {!isLoading && !error && data && data.items.length === 0 && (
          <p>ブックマークした記事はありません</p>
        )}

        {!isLoading && !error && data && data.items.length > 0 && (
          <>
            <ul aria-label="ブックマーク一覧">
              {data.items.map((item: BookmarkResponse) => (
                <li key={item.id}>
                  <article>
                    <h3>
                      <a
                        href={item.sourceUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        {item.title}
                      </a>
                    </h3>
                    <p>
                      <span>{item.oshiName}</span>
                      {" · "}
                      <time dateTime={item.collectedAt}>
                        {new Date(item.collectedAt).toLocaleString("ja-JP")}
                      </time>
                    </p>
                    {item.summary && <p>{item.summary}</p>}
                    <BookmarkButton
                      newsItemId={item.newsItemId}
                      initialIsBookmarked={true}
                    />
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
