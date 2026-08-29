import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { logout } from "../api/auth";
import { getOshis } from "../api/oshi";
import { ApiError } from "../api/client";
import { useAuth } from "../context/AuthContext";
import { OshiForm } from "../components/oshi/OshiForm";
import { OshiList } from "../components/oshi/OshiList";
import { NewsFeedList } from "../components/feed/NewsFeedList";
import { ProgramFeedList } from "../components/feed/ProgramFeedList";
import { OshiFilter } from "../components/feed/OshiFilter";
import { CollectorStatus } from "../components/collector/CollectorStatus";
import type { OshiResponse } from "../types/oshi";

/**
 * ダッシュボードページ（認証済みユーザー専用）。
 * 推し管理セクション・フィードフィルタUI・収集ジョブ状況を含む。
 */
export function DashboardPage() {
  const { clearAuth } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [logoutError, setLogoutError] = useState<string | null>(null);

  const [oshis, setOshis] = useState<OshiResponse[]>([]);
  const [isFetchingOshis, setIsFetchingOshis] = useState(false);
  const [fetchOshisError, setFetchOshisError] = useState<string | null>(null);

  // URLクエリパラメータから選択中の推しIDを読み取る
  const selectedOshiId = (() => {
    const val = searchParams.get("oshiId");
    if (val !== null && /^\d+$/.test(val)) return Number(val);
    return null;
  })();

  // マウント時に推し一覧を取得する
  useEffect(() => {
    const fetchOshis = async () => {
      setIsFetchingOshis(true);
      setFetchOshisError(null);
      try {
        const data = await getOshis();
        setOshis(data);
      } catch (err) {
        if (err instanceof ApiError) {
          setFetchOshisError(err.message);
        } else {
          setFetchOshisError("推し一覧の取得に失敗しました");
        }
      } finally {
        setIsFetchingOshis(false);
      }
    };

    void fetchOshis();
  }, []);

  const handleOshiAdded = (oshi: OshiResponse) => {
    setOshis((prev) => [...prev, oshi]);
  };

  const handleOshiDeleted = (id: number) => {
    setOshis((prev) => prev.filter((o) => o.id !== id));
    // 削除した推しが選択中の場合はフィルタをクリア
    if (selectedOshiId === id) {
      setSearchParams({});
    }
  };

  const handleOshiFilterChange = (oshiId: number | null) => {
    if (oshiId === null) {
      setSearchParams({});
    } else {
      setSearchParams({ oshiId: String(oshiId) });
    }
  };

  const handleLogout = async () => {
    setIsLoggingOut(true);
    setLogoutError(null);
    try {
      await logout();
    } catch (err) {
      // ネットワークエラーなどでもクライアント側はトークンを削除する
      if (!(err instanceof ApiError)) {
        setLogoutError(
          "ログアウト中にエラーが発生しましたが、セッションを終了します",
        );
      }
    } finally {
      clearAuth();
      navigate("/auth", { replace: true });
    }
  };

  return (
    <main>
      <header>
        <h1>oshikatsu ダッシュボード</h1>
        <button onClick={() => void handleLogout()} disabled={isLoggingOut}>
          {isLoggingOut ? "ログアウト中..." : "ログアウト"}
        </button>
      </header>

      {logoutError && (
        <div role="alert" aria-live="assertive">
          {logoutError}
        </div>
      )}

      <section aria-labelledby="oshi-section-title">
        <h2 id="oshi-section-title">推し管理</h2>
        <OshiForm onOshiAdded={handleOshiAdded} />

        {fetchOshisError && (
          <div role="alert" aria-live="assertive">
            {fetchOshisError}
          </div>
        )}

        {isFetchingOshis ? (
          <p>読み込み中...</p>
        ) : (
          <OshiList oshis={oshis} onOshiDeleted={handleOshiDeleted} />
        )}
      </section>

      <section aria-labelledby="feed-section-title">
        <h2 id="feed-section-title">フィード</h2>
        <OshiFilter
          oshis={oshis}
          selectedOshiId={selectedOshiId}
          onChange={handleOshiFilterChange}
        />

        <section aria-labelledby="news-section-title">
          <h3 id="news-section-title">ニュース</h3>
          <NewsFeedList
            key={String(selectedOshiId)}
            oshiId={selectedOshiId ?? undefined}
          />
        </section>

        <section aria-labelledby="programs-section-title">
          <h3 id="programs-section-title">出演番組</h3>
          <ProgramFeedList
            key={String(selectedOshiId)}
            oshiId={selectedOshiId ?? undefined}
          />
        </section>
      </section>

      <section aria-labelledby="collector-section-title">
        <h2 id="collector-section-title">収集ジョブ状況</h2>
        <CollectorStatus />
      </section>
    </main>
  );
}
