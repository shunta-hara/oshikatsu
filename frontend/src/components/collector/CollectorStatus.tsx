import { useEffect, useState } from "react";
import { getCollectorStatus } from "../../api/collector";
import { ApiError } from "../../api/client";
import type { CollectorStatusResponse } from "../../types/collector";

/**
 * 収集ジョブの最新実行状況を表示するコンポーネント。
 */
export function CollectorStatus() {
  const [status, setStatus] = useState<CollectorStatusResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchStatus = async () => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await getCollectorStatus();
        setStatus(result);
      } catch (err) {
        if (err instanceof ApiError) {
          setError(err.message);
        } else {
          setError("収集ジョブ状況の取得に失敗しました");
        }
      } finally {
        setIsLoading(false);
      }
    };

    void fetchStatus();
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

  if (!status) {
    return null;
  }

  return (
    <dl className="collector-status-dl" aria-label="収集ジョブ実行状況">
      <div className="collector-status-row">
        <dt className="collector-status-dt">ニュース収集 最終実行</dt>
        <dd className="collector-status-dd">
          {status.latestNewsJob ? (
            <span>
              {new Date(status.latestNewsJob.executedAt).toLocaleString(
                "ja-JP",
              )}
              {" / 成功: "}
              {status.latestNewsJob.successCount}件{" / 失敗: "}
              {status.latestNewsJob.failureCount}件
            </span>
          ) : (
            <span>未実行</span>
          )}
        </dd>
      </div>
      <div className="collector-status-row">
        <dt className="collector-status-dt">番組収集 最終実行</dt>
        <dd className="collector-status-dd">
          {status.latestProgramJob ? (
            <span>
              {new Date(status.latestProgramJob.executedAt).toLocaleString(
                "ja-JP",
              )}
              {" / 成功: "}
              {status.latestProgramJob.successCount}件{" / 失敗: "}
              {status.latestProgramJob.failureCount}件
            </span>
          ) : (
            <span>未実行</span>
          )}
        </dd>
      </div>
    </dl>
  );
}
