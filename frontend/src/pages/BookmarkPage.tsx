import { useEffect, useState } from "react";
import { getBookmarks } from "../api/bookmark";
import { getOshis } from "../api/oshi";
import { ApiError } from "../api/client";
import type {
  BookmarkResponse,
  BookmarksPaginatedResponse,
} from "../types/bookmark";
import type { OshiResponse } from "../types/oshi";
import { NavBar } from "../components/NavBar";
import { FeedPagination } from "../components/feed/FeedPagination";
import { BookmarkButton } from "../components/feed/BookmarkButton";
import { OshiFilter } from "../components/feed/OshiFilter";
import { NewsSearchBox } from "../components/feed/NewsSearchBox";
import { DateRangeFilter } from "../components/feed/DateRangeFilter";

/**
 * ブックマーク一覧ページ。
 * ログインユーザーがブックマークしたニュース記事を登録日時の新しい順で表示する。
 * 推しの選択・キーワード検索・期間の絞り込みが可能。
 * 各アイテムをカード形式で表示し、タイトル・推し名・出典・掲載日・ブックマーク解除を一目で確認できる。
 */
export function BookmarkPage() {
  const [data, setData] = useState<BookmarksPaginatedResponse | null>(null);
  const [page, setPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // 推し一覧（OshiFilter 用）
  const [oshis, setOshis] = useState<OshiResponse[]>([]);
  const [selectedOshiId, setSelectedOshiId] = useState<number | null>(null);

  // キーワード検索（コミット済み）
  const [searchQuery, setSearchQuery] = useState("");

  // 期間フィルタ
  const [dateFrom, setDateFrom] = useState<string | undefined>(undefined);
  const [dateTo, setDateTo] = useState<string | undefined>(undefined);

  const isFiltered =
    selectedOshiId !== null || !!searchQuery || !!dateFrom || !!dateTo;

  // マウント時に推し一覧を取得する
  useEffect(() => {
    const fetchOshis = async () => {
      try {
        const result = await getOshis();
        setOshis(result);
      } catch {
        // 推し一覧の取得失敗はサイレントに処理（フィルタUIが使えなくなるだけ）
      }
    };
    void fetchOshis();
  }, []);

  useEffect(() => {
    const fetchBookmarks = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getBookmarks(
          page,
          20,
          selectedOshiId ?? undefined,
          searchQuery || undefined,
          dateFrom,
          dateTo,
        );
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
  }, [page, selectedOshiId, searchQuery, dateFrom, dateTo]);

  const handleOshiFilterChange = (oshiId: number | null) => {
    setSelectedOshiId(oshiId);
    setPage(1);
  };

  const handleSearch = (query: string) => {
    setSearchQuery(query);
    setPage(1);
  };

  const handleClearSearch = () => {
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

  /** 掲載日として表示する日時を選ぶ（publishedAt > collectedAt） */
  const displayDate = (item: BookmarkResponse): string => {
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
    <>
      <NavBar />
      <main className="page-content">
        <h2 className="page-heading">ブックマーク一覧</h2>

        {/* 絞り込みUI（ダッシュボードのフィードと統一） */}
        <div className="section-card">
          <OshiFilter
            oshis={oshis}
            selectedOshiId={selectedOshiId}
            onChange={handleOshiFilterChange}
          />
          <NewsSearchBox
            value={searchQuery}
            onSearch={handleSearch}
            onClear={handleClearSearch}
          />
          <DateRangeFilter onApply={handleDateApply} />
        </div>

        {isLoading && <p className="loading-text">読み込み中...</p>}

        {!isLoading && error && (
          <div role="alert" aria-live="assertive" className="error-message">
            {error}
          </div>
        )}

        {!isLoading && !error && data && data.items.length === 0 && (
          <p className="empty-state">
            {isFiltered
              ? "条件に一致するブックマークがありません"
              : "ブックマークした記事はありません"}
          </p>
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
                      <time dateTime={item.publishedAt ?? item.collectedAt}>
                        掲載日: {displayDate(item)}
                      </time>
                    </div>
                    {item.sourceName && (
                      <span className="feed-card__source-name">
                        {item.sourceName}
                      </span>
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
