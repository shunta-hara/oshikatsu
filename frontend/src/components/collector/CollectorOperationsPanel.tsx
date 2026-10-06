import { useEffect, useState } from "react";
import { getCollectorOperations } from "../../api/collector";
import { getHealth } from "../../api/health";
import { ApiError } from "../../api/client";
import type { CollectorOperationsResponse } from "../../types/collector";
import type { HealthResponse } from "../../types/health";

/** Neon 無料プランの上限（MB）*/
const DB_MAX_SIZE_MB = 500;

/**
 * 収集ジョブの運用パネル。
 * 直近24時間のジョブ実行履歴、連続失敗中のタレント一覧、および DB 使用量を表示する。
 */
export function CollectorOperationsPanel() {
  const [data, setData] = useState<CollectorOperationsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [isLoadingHealth, setIsLoadingHealth] = useState(false);

  useEffect(() => {
    const fetchData = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getCollectorOperations();
        setData(result);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("運用パネルの取得に失敗しました");
        }
      } finally {
        setIsLoading(false);
      }
    };

    void fetchData();
  }, []);

  useEffect(() => {
    const fetchHealth = async () => {
      setIsLoadingHealth(true);
      try {
        const result = await getHealth();
        setHealth(result);
      } catch {
        // ヘルスチェック失敗はサイレントに処理（DB サイズ非表示で継続）
      } finally {
        setIsLoadingHealth(false);
      }
    };

    void fetchHealth();
  }, []);

  if (isLoading) {
    return <p className="loading-text">読み込み中...</p>;
  }

  if (error) {
    return (
      <div role="alert" aria-live="assertive" className="error-message">
        {error}
      </div>
    );
  }

  if (!data) {
    return null;
  }

  const hasFailures =
    data.isOverallJobFailing || data.failingTalents.length > 0;

  const dbSizeMb = health?.dbSizeMb ?? null;
  const dbUsagePercent =
    dbSizeMb !== null
      ? Math.min(Math.round((dbSizeMb / DB_MAX_SIZE_MB) * 100), 100)
      : null;

  return (
    <div className="collector-ops-panel" aria-label="収集ジョブ運用パネル">
      {/* DB 使用量（FEAT-053）*/}
      <div aria-label="DB 使用量" className="collector-ops-db-usage">
        <p className="collector-ops-section-label">Neon DB 使用量</p>
        {isLoadingHealth ? (
          <p className="loading-text">読み込み中...</p>
        ) : dbSizeMb !== null && dbUsagePercent !== null ? (
          <div className="collector-ops-db-usage-detail">
            <p>
              {dbSizeMb} MB / {DB_MAX_SIZE_MB} MB（{dbUsagePercent}%）
            </p>
            <div
              role="progressbar"
              aria-label="DB 使用率"
              aria-valuenow={dbUsagePercent}
              aria-valuemin={0}
              aria-valuemax={100}
              className="collector-ops-db-progress"
            >
              <div
                className="collector-ops-db-progress__bar"
                style={{ width: `${dbUsagePercent}%` }}
              />
            </div>
          </div>
        ) : (
          <p className="collector-ops-empty">DB 使用量を取得できませんでした</p>
        )}
      </div>

      {/* 失敗アラートエリア */}
      {hasFailures && (
        <div
          role="alert"
          aria-live="polite"
          className="collector-ops-alert"
          aria-label="収集失敗アラート"
        >
          {data.isOverallJobFailing && (
            <p className="collector-ops-alert__item">
              全体収集ジョブが現在失敗中です
            </p>
          )}
          {data.failingTalents.length > 0 && (
            <ul
              className="collector-ops-failing-list"
              aria-label="連続失敗中のタレント一覧"
            >
              {data.failingTalents.map((t) => (
                <li key={t.talentId} className="collector-ops-failing-item">
                  <span className="collector-ops-failing-name">
                    {t.talentName}
                  </span>
                  <span className="collector-ops-failing-count">
                    {t.consecutiveFailureCount}回連続失敗中
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* 直近24時間のジョブ履歴 */}
      <div aria-label="直近24時間のジョブ履歴">
        <p className="collector-ops-section-label">直近24時間のジョブ実行</p>
        {data.recentJobs.length === 0 ? (
          <p className="collector-ops-empty">直近24時間の実行記録なし</p>
        ) : (
          <ul className="collector-ops-job-list">
            {data.recentJobs.map((job) => (
              <li
                key={job.id}
                className={`collector-ops-job-item${job.failureCount > 0 ? " collector-ops-job-item--failure" : ""}`}
              >
                <span className="collector-ops-job-type">{job.jobType}</span>
                <span className="collector-ops-job-time">
                  {new Date(job.executedAt).toLocaleString("ja-JP")}
                </span>
                <span className="collector-ops-job-result">
                  成功: {job.successCount}件 / 失敗: {job.failureCount}件
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
