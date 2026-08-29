import { useEffect, useState } from "react";
import { getNews } from "../../api/feed";
import { ApiError } from "../../api/client";
import type { NewsItem, NewsPaginatedResponse } from "../../types/feed";
import { FeedPagination } from "./FeedPagination";

interface NewsFeedListProps {
  /** 絞り込む推しのID。未指定の場合は全推しのニュースを表示する */
  oshiId?: number;
}

/**
 * ログインユーザーの推し全員分（または特定の推し）のニュース一覧を表示するコンポーネント。
 * ページネーション・エラー・空状態を処理する。
 */
export function NewsFeedList({ oshiId }: NewsFeedListProps) {
  const [data, setData] = useState<NewsPaginatedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchNews = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getNews(page, 20, oshiId);
        setData(result);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("ニュースの取得に失敗しました");
        }
      } finally {
        setIsLoading(false);
      }
    };

    void fetchNews();
  }, [page, oshiId]);

  if (isLoading) {
    return <p>読み込み中...</p>;
  }

  if (error) {
    return (
      <div role="alert" aria-live="assertive">
        {error}
      </div>
    );
  }

  if (!data || data.items.length === 0) {
    return <p>まだニュース情報がありません</p>;
  }

  return (
    <div>
      <ul aria-label="ニュース一覧">
        {data.items.map((item: NewsItem) => (
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
            </article>
          </li>
        ))}
      </ul>
      <FeedPagination
        currentPage={data.page}
        totalPages={data.totalPages}
        onPageChange={setPage}
      />
    </div>
  );
}
