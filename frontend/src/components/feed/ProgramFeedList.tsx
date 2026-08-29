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
  /** 絞り込む推しのID。未指定の場合は全推しの出演番組を表示する */
  oshiId?: number;
}

/**
 * ログインユーザーの推し全員分（または特定の推し）の出演番組一覧を表示するコンポーネント。
 * ページネーション・エラー・空状態・日付範囲フィルタを処理する。
 */
export function ProgramFeedList({ oshiId }: ProgramFeedListProps) {
  const [data, setData] = useState<ProgramsPaginatedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  /** 適用済み開始日時（ISO 8601 UTC 文字列 or undefined） */
  const [dateFrom, setDateFrom] = useState<string | undefined>(undefined);
  /** 適用済み終了日時（ISO 8601 UTC 文字列 or undefined） */
  const [dateTo, setDateTo] = useState<string | undefined>(undefined);

  useEffect(() => {
    const fetchPrograms = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getPrograms(page, 20, oshiId, dateFrom, dateTo);
        setData(result);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("番組情報の取得に失敗しました");
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

  return (
    <div>
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
            ? "該当する番組が見つかりません"
            : "まだ出演番組情報がありません"}
        </p>
      )}

      {!isLoading && !error && data && data.items.length > 0 && (
        <div>
          <ul aria-label="出演番組一覧">
            {data.items.map((item: ProgramAppearance) => (
              <li key={item.id}>
                <article>
                  <h3>
                    <a
                      href={item.sourceUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      {item.programName}
                    </a>
                  </h3>
                  <p>
                    <span>{item.oshiName}</span>
                    {item.broadcaster != null && ` · ${item.broadcaster}`}
                    {" · "}
                    <time dateTime={item.collectedAt}>
                      {new Date(item.collectedAt).toLocaleString("ja-JP")}
                    </time>
                  </p>
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
