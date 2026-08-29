import { useEffect, useState } from "react";
import { getNews } from "../../api/feed";
import { ApiError } from "../../api/client";
import type { NewsItem, NewsPaginatedResponse } from "../../types/feed";
import { FeedPagination } from "./FeedPagination";
import { NewsSearchBox } from "./NewsSearchBox";
import { DateRangeFilter } from "./DateRangeFilter";

interface NewsFeedListProps {
  /** 絞り込む推しのID。未指定の場合は全推しのニュースを表示する */
  oshiId?: number;
}

/**
 * ログインユーザーの推し全員分（または特定の推し）のニュース一覧を表示するコンポーネント。
 * ページネーション・エラー・空状態・キーワード検索・日付範囲フィルタを処理する。
 */
export function NewsFeedList({ oshiId }: NewsFeedListProps) {
  const [data, setData] = useState<NewsPaginatedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  /** コミット済み検索キーワード（「検索」ボタンを押したときに確定） */
  const [searchQuery, setSearchQuery] = useState("");
  /** 適用済み開始日時（ISO 8601 UTC 文字列 or undefined） */
  const [dateFrom, setDateFrom] = useState<string | undefined>(undefined);
  /** 適用済み終了日時（ISO 8601 UTC 文字列 or undefined） */
  const [dateTo, setDateTo] = useState<string | undefined>(undefined);

  useEffect(() => {
    const fetchNews = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getNews(
          page,
          20,
          oshiId,
          searchQuery || undefined,
          dateFrom,
          dateTo,
        );
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
  }, [page, oshiId, searchQuery, dateFrom, dateTo]);

  const handleSearch = (query: string) => {
    setSearchQuery(query);
    setPage(1);
  };

  const handleClear = () => {
    setSearchQuery("");
    setPage(1);
  };

  const handleDateApply = (
    from: string | undefined,
    to: string | undefined,
  ) => {
    setDateFrom(from);
    setDateTo(to);
    setPage(1);
  };

  const isFiltered = !!searchQuery || !!dateFrom || !!dateTo;

  return (
    <div>
      <NewsSearchBox
        value={searchQuery}
        onSearch={handleSearch}
        onClear={handleClear}
      />
      <DateRangeFilter onApply={handleDateApply} />

      {isLoading && <p>読み込み中...</p>}

      {!isLoading && error && (
        <div role="alert" aria-live="assertive">
          {error}
        </div>
      )}

      {!isLoading && !error && data && data.items.length === 0 && (
        <p>
          {isFiltered
            ? "該当するニュースが見つかりません"
            : "まだニュース情報がありません"}
        </p>
      )}

      {!isLoading && !error && data && data.items.length > 0 && (
        <>
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
        </>
      )}
    </div>
  );
}
