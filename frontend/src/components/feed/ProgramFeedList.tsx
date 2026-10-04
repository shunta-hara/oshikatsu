import { useEffect, useState } from "react";
import { getPrograms } from "../../api/feed";
import { ApiError } from "../../api/client";
import type {
  ProgramAppearance,
  ProgramsPaginatedResponse,
} from "../../types/feed";
import { FeedPagination } from "./FeedPagination";
import { DateRangeFilter } from "./DateRangeFilter";

interface ProgramFeedListProps {
  /** 絞り込む推しのID。未指定の場合は全推しの出演情報を表示する */
  oshiId?: number;
  /** 取得完了後に totalCount を通知するコールバック */
  onTotalCountChange?: (count: number) => void;
}

/**
 * ログインユーザーの推し全員分（または特定の推し）の出演情報一覧を表示するコンポーネント。
 * 各アイテムをカード形式で表示し、タイトル・推し名・出典・掲載日を一目で確認できる。
 */
export function ProgramFeedList({
  oshiId,
  onTotalCountChange,
}: ProgramFeedListProps) {
  const [data, setData] = useState<ProgramsPaginatedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  /** 適用済み開始日時（ISO 8601 JST 文字列 or undefined） */
  const [dateFrom, setDateFrom] = useState<string | undefined>(undefined);
  /** 適用済み終了日時（ISO 8601 JST 文字列 or undefined） */
  const [dateTo, setDateTo] = useState<string | undefined>(undefined);

  useEffect(() => {
    const fetchPrograms = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getPrograms(page, 20, oshiId, dateFrom, dateTo);
        setData(result);
        onTotalCountChange?.(result.totalCount);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("出演情報の取得に失敗しました");
        }
      } finally {
        setIsLoading(false);
      }
    };

    void fetchPrograms();
  }, [page, oshiId, dateFrom, dateTo]);

  const handleDateApply = (
    from: string | undefined,
    to: string | undefined,
  ) => {
    setDateFrom(from);
    setDateTo(to);
    setPage(1);
  };

  const isFiltered = !!dateFrom || !!dateTo;

  /** 掲載日として表示する日時を選ぶ（airedAt > collectedAt） */
  const displayDate = (item: ProgramAppearance): string => {
    const dt = item.airedAt ?? item.collectedAt;
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
            ? "該当する出演情報が見つかりません"
            : "まだ出演情報がありません"}
        </p>
      )}

      {!isLoading && !error && data && data.items.length > 0 && (
        <div>
          <ul aria-label="出演情報一覧" className="feed-list">
            {data.items.map((item: ProgramAppearance) => (
              <li key={item.id}>
                <article className="feed-card">
                  <a
                    className="feed-card__title"
                    href={item.sourceUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    {item.programName}
                  </a>
                  <div className="feed-card__meta">
                    <span className="feed-card__oshi-badge">
                      {item.oshiName}
                    </span>
                    {item.broadcaster != null && (
                      <span>{item.broadcaster}</span>
                    )}
                    <time
                      dateTime={item.airedAt ?? item.collectedAt}
                      title={item.sourceUrl}
                    >
                      掲載日: {displayDate(item)}
                    </time>
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
        </div>
      )}
    </div>
  );
}
