import { useEffect, useState } from "react";
import { getNews } from "../../api/feed";
import { ApiError } from "../../api/client";
import type { NewsItem, NewsPaginatedResponse } from "../../types/feed";
import { FeedPagination } from "./FeedPagination";
import { NewsSearchBox } from "./NewsSearchBox";
import { DateRangeFilter } from "./DateRangeFilter";
import { BookmarkButton } from "./BookmarkButton";

interface NewsFeedListProps {
  /** 絞り込む推しのID。未指定の場合は全推しのニュースを表示する */
  oshiId?: number;
  /** 取得完了後に totalCount を通知するコールバック */
  onTotalCountChange?: (count: number) => void;
}

/**
 * ログインユーザーの推し全員分（または特定の推し）のニュース一覧を表示するコンポーネント。
 * 各アイテムをカード形式で表示し、タイトル・推し名・出典・掲載日・ブックマークを一目で確認できる。
 */
export function NewsFeedList({
  oshiId,
  onTotalCountChange,
}: NewsFeedListProps) {
  const [data, setData] = useState<NewsPaginatedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  /** コミット済み検索キーワード（「検索」ボタンを押したときに確定） */
  const [searchQuery, setSearchQuery] = useState("");
  /** 適用済み開始日時（ISO 8601 JST 文字列 or undefined） */
  const [dateFrom, setDateFrom] = useState<string | undefined>(undefined);
  /** 適用済み終了日時（ISO 8601 JST 文字列 or undefined） */
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
        onTotalCountChange?.(result.totalCount);
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

  /** 掲載日として表示する日時を選ぶ（publishedAt > collectedAt） */
  const displayDate = (item: NewsItem): string => {
    const dt = item.publishedAt ?? item.collectedAt;
    return new Date(dt).toLocaleString("ja-JP", {
      year: "numeric",
      month: "numeric",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <div>
      <NewsSearchBox
        value={searchQuery}
        onSearch={handleSearch}
        onClear={handleClear}
      />
      <DateRangeFilter onApply={handleDateApply} />

      {isLoading && <p className="loading-text">読み込み中...</p>}

      {!isLoading && error && (
        <div role="alert" aria-live="assertive" className="error-message">
          {error}
        </div>
      )}

      {!isLoading && !error && data && data.items.length === 0 && (
        <p className="empty-state">
          {isFiltered
            ? "該当するニュースが見つかりません"
            : "まだニュース情報がありません"}
        </p>
      )}

      {!isLoading && !error && data && data.items.length > 0 && (
        <>
          <ul aria-label="ニュース一覧" className="feed-list">
            {data.items.map((item: NewsItem) => (
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
                    <time
                      dateTime={item.publishedAt ?? item.collectedAt}
                      title={item.sourceUrl}
                    >
                      掲載日: {displayDate(item)}
                    </time>
                  </div>
                  {item.summary && (
                    <p className="feed-card__summary">{item.summary}</p>
                  )}
                  <div className="feed-card__actions">
                    <BookmarkButton
                      newsItemId={item.id}
                      initialIsBookmarked={item.isBookmarked ?? false}
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
    </div>
  );
}
