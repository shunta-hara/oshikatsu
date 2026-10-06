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
import { TvGuideLinks } from "../components/feed/TvGuideLinks";
import { OshiFilter } from "../components/feed/OshiFilter";
import { NotificationBadge } from "../components/feed/NotificationBadge";
import { CollectorStatus } from "../components/collector/CollectorStatus";
import { CollectorOperationsPanel } from "../components/collector/CollectorOperationsPanel";
import { OshiCardList } from "../components/dashboard/OshiCardList";
import { PushNotificationButton } from "../components/push/PushNotificationButton";
import type { OshiResponse } from "../types/oshi";
import type { OshiDashboardCard } from "../types/dashboard";

/** アクティブなフィードタブ（狭い画面でのタブ切り替え用） */
type FeedTab = "news" | "programs";

/**
 * ダッシュボードページ（認証済みユーザー専用）。
 * 960px 以上ではサイドバー（推し管理・収集ジョブ）とメイン（フィード）の2カラムレイアウト。
 * 1024px 以上ではフィード内のニュースと出演情報が2列横並び。
 * 375px〜1023px ではフィード内をタブで切り替える。
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

  // フィードの件数（タブ・見出しに表示する）
  const [newsCount, setNewsCount] = useState(0);
  const [programsCount, setProgramsCount] = useState(0);

  // 狭い画面でのアクティブタブ
  const [activeTab, setActiveTab] = useState<FeedTab>("news");

  // URLクエリパラメータから選択中の推しIDを読み取る
  const selectedOshiId = (() => {
    const val = searchParams.get("oshiId");
    if (val !== null && /^\d+$/.test(val)) return Number(val);
    return null;
  })();

  // 選択中の推しのオブジェクト（番組表リンクに使う名前の取得用）
  const selectedOshi =
    selectedOshiId !== null
      ? (oshis.find((o) => o.id === selectedOshiId) ?? null)
      : null;

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
    // フィルタ変更時はカウントをリセット（再取得後に更新される）
    setNewsCount(0);
    setProgramsCount(0);
    if (oshiId === null) {
      setSearchParams({});
    } else {
      setSearchParams({ oshiId: String(oshiId) });
    }
  };

  return (
    <>
      <NavBar />
      <main className="page-content page-content--wide">
        <h1 className="dashboard-heading">
          oshikatsu ダッシュボード
          <NotificationBadge count={unreadCount} />
        </h1>

        <div className="dashboard-layout">
          {/* ── サイドバー：推し管理 & 収集ジョブ ── */}
          <aside>
            <section
              className="section-card"
              aria-labelledby="oshi-section-title"
            >
              <h2 id="oshi-section-title" className="section-card__title">
                推し管理
              </h2>
              <OshiForm onOshiAdded={handleOshiAdded} />

              {fetchOshisError && (
                <div
                  role="alert"
                  aria-live="assertive"
                  className="error-message"
                >
                  {fetchOshisError}
                </div>
              )}

              {isFetchingOshis ? (
                <p className="loading-text">読み込み中...</p>
              ) : (
                <OshiList oshis={oshis} onOshiDeleted={handleOshiDeleted} />
              )}
            </section>

            <section
              className="section-card"
              aria-labelledby="push-notification-section-title"
            >
              <h2
                id="push-notification-section-title"
                className="section-card__title"
              >
                プッシュ通知
              </h2>
              <PushNotificationButton />
            </section>

            <section
              className="section-card"
              aria-labelledby="collector-section-title"
            >
              <h2 id="collector-section-title" className="section-card__title">
                収集ジョブ状況
              </h2>
              <CollectorStatus />
            </section>

            <section
              className="section-card"
              aria-labelledby="operations-section-title"
            >
              <h2 id="operations-section-title" className="section-card__title">
                運用パネル
              </h2>
              <CollectorOperationsPanel />
            </section>
          </aside>

          {/* ── メイン：マルチ推しダッシュボード & フィード ── */}
          <div>
            <section
              className="section-card"
              aria-labelledby="multi-oshi-dashboard-title"
            >
              <h2
                id="multi-oshi-dashboard-title"
                className="section-card__title"
              >
                マルチ推しダッシュボード
              </h2>

              {fetchDashboardError && (
                <div
                  role="alert"
                  aria-live="assertive"
                  className="error-message"
                >
                  {fetchDashboardError}
                </div>
              )}

              {isFetchingDashboard ? (
                <p className="loading-text">読み込み中...</p>
              ) : (
                <OshiCardList cards={dashboardCards} />
              )}
            </section>

            <section
              className="section-card"
              aria-labelledby="feed-section-title"
            >
              <h2 id="feed-section-title" className="section-card__title">
                フィード
              </h2>
              <OshiFilter
                oshis={oshis}
                selectedOshiId={selectedOshiId}
                onChange={handleOshiFilterChange}
              />

              {/* タブナビ（狭い画面のみ表示、CSS で切り替え） */}
              <div
                role="tablist"
                aria-label="フィード切り替え"
                className="feed-tabs"
              >
                <button
                  role="tab"
                  id="feed-tab-news"
                  aria-selected={activeTab === "news"}
                  aria-controls="feed-tabpanel-news"
                  className={`feed-tab${activeTab === "news" ? " feed-tab--active" : ""}`}
                  onClick={() => setActiveTab("news")}
                  type="button"
                >
                  ニュース（{newsCount}件）
                </button>
                <button
                  role="tab"
                  id="feed-tab-programs"
                  aria-selected={activeTab === "programs"}
                  aria-controls="feed-tabpanel-programs"
                  className={`feed-tab${activeTab === "programs" ? " feed-tab--active" : ""}`}
                  onClick={() => setActiveTab("programs")}
                  type="button"
                >
                  出演情報（{programsCount}件）
                </button>
              </div>

              {/* 2カラム表示 ─ 1024px以上では横並び、未満では縦積み＋タブ連動 */}
              <div className="feed-two-col">
                <div
                  id="feed-tabpanel-news"
                  role="tabpanel"
                  aria-labelledby="feed-tab-news"
                  className={`feed-col${activeTab !== "news" ? " feed-col--tab-hidden" : ""}`}
                >
                  <h3 className="feed-col__heading">
                    ニュース（{newsCount}件）
                  </h3>
                  <NewsFeedList
                    key={String(selectedOshiId)}
                    oshiId={selectedOshiId ?? undefined}
                    onTotalCountChange={setNewsCount}
                  />
                </div>

                <div
                  id="feed-tabpanel-programs"
                  role="tabpanel"
                  aria-labelledby="feed-tab-programs"
                  className={`feed-col${activeTab !== "programs" ? " feed-col--tab-hidden" : ""}`}
                >
                  <h3 className="feed-col__heading">
                    出演情報（{programsCount}件）
                  </h3>
                  {selectedOshi !== null && (
                    <TvGuideLinks oshiName={selectedOshi.name} />
                  )}
                  <ProgramFeedList
                    key={String(selectedOshiId)}
                    oshiId={selectedOshiId ?? undefined}
                    onTotalCountChange={setProgramsCount}
                  />
                </div>
              </div>
            </section>
          </div>
        </div>
      </main>
    </>
  );
}
