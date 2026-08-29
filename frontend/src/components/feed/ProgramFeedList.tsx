import { useEffect, useState } from "react";
import { getPrograms } from "../../api/feed";
import { ApiError } from "../../api/client";
import type {
  ProgramAppearance,
  ProgramsPaginatedResponse,
} from "../../types/feed";
import { FeedPagination } from "./FeedPagination";

interface ProgramFeedListProps {
  /** 絞り込む推しのID。未指定の場合は全推しの出演番組を表示する */
  oshiId?: number;
}

/**
 * ログインユーザーの推し全員分（または特定の推し）の出演番組一覧を表示するコンポーネント。
 * ページネーション・エラー・空状態を処理する。
 */
export function ProgramFeedList({ oshiId }: ProgramFeedListProps) {
  const [data, setData] = useState<ProgramsPaginatedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchPrograms = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getPrograms(page, 20, oshiId);
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
    return <p>まだ出演番組情報がありません</p>;
  }

  return (
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
  );
}
