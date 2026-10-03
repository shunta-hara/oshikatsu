import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { getOshis } from "../api/oshi";
import { getUnreadCount, markAsRead } from "../api/feed";
import { getDashboardSummary } from "../api/dashboard";
import { ApiError } from "../api/client";
import { NavBar } from "../components/NavBar";
import { OshiForm } from "../components/oshi/OshiForm";
import { OshiList } from "../components/oshi/OshiList";
import { NewsFeedList } from "../components/feed/NewsFeedList";
import { ProgramFeedList } from "../components/feed/ProgramFeedList";
import { OshiFilter } from "../components/feed/OshiFilter";
import { NotificationBadge } from "../components/feed/NotificationBadge";
import { CollectorStatus } from "../components/collector/CollectorStatus";
import { OshiCardList } from "../components/dashboard/OshiCardList";
import type { OshiResponse } from "../types/oshi";
import type { OshiDashboardCard } from "../types/dashboard";

/**
 * ダッシュボードページ（認証済みユーザー専用）。
 * 推し管理セクション・マルチ推しダッシュボード・フィードフィルタUI・収集ジョブ状況を含む。
 */
export function DashboardPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const [oshis, setOshis] = useState<OshiResponse[]>([]);
  const [isFetchingOshis, setIsFetchingOshis] = useState(false);
  const [fetchOshisError, setFetchOshisError] = useState<string | null>(null);

  // マルチ推しダッシュボードカード
  const [dashboardCards, setDashboardCards] = useState<OshiDashboardCard[]>([]);
  const [isFetchingDashboard, setIsFetchingDashboard] = useState(false);
  const [fetchDashboardError, setFetchDashboardError] = useState<string | null>(
    null,
  );

  // 未読件数（ページ開くと取得→表示後にリセット）
  const [unreadCount, setUnreadCount] = useState(0);

  // URLクエリパラメータから選択中の推しIDを読み取る
  const selectedOshiId = (() => {
    const val = searchParams.get("oshiId");
    if (val !== null && /^\d+$/.test(val)) return Number(val);
    return null;
  })();

  const fetchDashboard = async () => {
    setIsFetchingDashboard(true);
    setFetchDashboardError(null);
    try {
      const data = await getDashboardSummary();
      setDashboardCards(data);
    } catch (err) {
      if (err instanceof ApiError) {
        setFetchDashboardError(err.message);
      } else {
        setFetchDashboardError("ダッシュボードの取得に失敗しました");
      }
    } finally {
      setIsFetchingDashboard(false);
    }
  };

  // マウント時に推し一覧・ダッシュボード・未読件数を取得し、既読にリセットする
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

    const fetchAndResetUnread = async () => {
      try {
        // 未読件数を取得してバッジに表示（前回訪問以降の新着数をユーザーへ通知）
        const count = await getUnreadCount();
        setUnreadCount(count);
        // フィード一覧を開いたのでサーバー側カウントをリセット（次回訪問で 0 になる）
        await markAsRead();
      } catch {
        // 未読件数の取得・リセット失敗はサイレントに処理（メイン機能ではない）
      }
    };

    void fetchOshis();
    void fetchDashboard();
    void fetchAndResetUnread();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleOshiAdded = (oshi: OshiResponse) => {
    setOshis((prev) => [...prev, oshi]);
    // 推し追加後にダッシュボードカードを再取得する
    void fetchDashboard();
  };

  const handleOshiDeleted = (id: number) => {
    setOshis((prev) => prev.filter((o) => o.id !== id));
    // 削除した推しが選択中の場合はフィルタをクリア
    if (selectedOshiId === id) {
      setSearchParams({});
    }
    // 推し削除後にダッシュボードカードを再取得する
    void fetchDashboard();
  };

  const handleOshiFilterChange = (oshiId: number | null) => {
    if (oshiId === null) {
      setSearchParams({});
    } else {
      setSearchParams({ oshiId: String(oshiId) });
    }
  };

  return (
    <>
      <NavBar />
      <main className="page-content">
        <h1>
          oshikatsu ダッシュボード
          <NotificationBadge count={unreadCount} />
        </h1>

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

        <section aria-labelledby="multi-oshi-dashboard-title">
          <h2 id="multi-oshi-dashboard-title">マルチ推しダッシュボード</h2>

          {fetchDashboardError && (
            <div role="alert" aria-live="assertive">
              {fetchDashboardError}
            </div>
          )}

          {isFetchingDashboard ? (
            <p>読み込み中...</p>
          ) : (
            <OshiCardList cards={dashboardCards} />
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
    </>
  );
}
