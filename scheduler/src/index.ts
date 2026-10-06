export interface Env {
  GITHUB_REPOSITORY: string;
  GITHUB_WORKFLOW_FILE: string;
  GITHUB_REF: string;
  GITHUB_DISPATCH_TOKEN: string;
  /** backend API のベース URL（例: https://oshikatsu-api.onrender.com）*/
  BACKEND_API_URL: string;
  /** backend 内部 API キー（X-Api-Key ヘッダーに設定する）*/
  BACKEND_API_KEY: string;
}

/** GET /api/v1/health のレスポンス */
export interface HealthCheckResponse {
  status: string;
  timestamp: string;
  dbSizeBytes?: number | null;
  dbSizeMb?: number | null;
}

/** DB 容量チェック結果 */
export interface DbCapacityCheckResult {
  dbSizeMb: number | null;
  alertSent: boolean;
}

interface RetryOptions {
  maxRetries?: number;
  sleepFn?: (ms: number) => Promise<void>;
  timeoutMs?: number;
}

/** 期限切れ記事削除 API のレスポンス */
export interface DeleteExpiredNewsResult {
  deletedCount: number;
}

/** Web Push 通知ジョブ API のレスポンス */
export interface PushNotifyResult {
  successCount: number;
  failureCount: number;
  skipCount: number;
  goneRemovedCount: number;
}

const DEFAULT_MAX_RETRIES = 3;
const BASE_DELAY_MS = 1000;

/** DB 容量警告を送信する閾値（MB）*/
const DB_CAPACITY_THRESHOLD_MB = 400;
/** Neon 無料プランの上限（MB）*/
const DB_MAX_SIZE_MB = 500;

function buildUrl(repo: string, workflowFile: string): string {
  return `https://api.github.com/repos/${repo}/actions/workflows/${workflowFile}/dispatches`;
}

/**
 * GitHub の workflow_dispatch API を呼び出してコレクターワークフローを起動する。
 * 一時的なエラー（5xx・429・ネットワーク失敗）は最大 maxRetries 回リトライする。
 * 4xx（429 以外）は再試行しない。
 */
export async function dispatchWorkflow(
  env: Env,
  options: RetryOptions = {},
): Promise<void> {
  const maxRetries = options.maxRetries ?? DEFAULT_MAX_RETRIES;
  const timeoutMs = options.timeoutMs ?? 10_000;
  const sleep =
    options.sleepFn ??
    ((ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)));

  const url = buildUrl(env.GITHUB_REPOSITORY, env.GITHUB_WORKFLOW_FILE);

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    if (attempt > 0) {
      const delayMs = Math.pow(2, attempt - 1) * BASE_DELAY_MS;
      console.log(
        `[scheduler] retry ${attempt}/${maxRetries} (waiting ${delayMs}ms)`,
      );
      await sleep(delayMs);
    }

    let status: number;
    try {
      const res = await fetch(url, {
        signal: AbortSignal.timeout(timeoutMs),
        method: "POST",
        headers: {
          Authorization: `Bearer ${env.GITHUB_DISPATCH_TOKEN}`,
          Accept: "application/vnd.github+json",
          "X-GitHub-Api-Version": "2022-11-28",
          "User-Agent": "oshikatsu-scheduler/1.0",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ ref: env.GITHUB_REF, inputs: {} }),
      });
      status = res.status;
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      if (attempt < maxRetries) {
        console.warn(
          `[scheduler] attempt ${attempt + 1} network error: ${message}`,
        );
        continue;
      }
      console.error(
        `[scheduler] failed after ${maxRetries + 1} attempts (network): ${message}`,
      );
      throw err instanceof Error ? err : new Error(message);
    }

    if (status === 204) {
      console.log(
        `[scheduler] dispatched ${env.GITHUB_WORKFLOW_FILE} successfully`,
      );
      return;
    }

    const isRetryable = status === 429 || status >= 500;
    if (!isRetryable) {
      console.error(`[scheduler] non-retryable HTTP status: ${status}`);
      throw new Error(`GitHub API returned non-retryable status: ${status}`);
    }

    if (attempt < maxRetries) {
      console.warn(
        `[scheduler] attempt ${attempt + 1} received HTTP ${status}, will retry`,
      );
      continue;
    }

    console.error(
      `[scheduler] failed after ${maxRetries + 1} attempts (last status: ${status})`,
    );
    throw new Error(
      `GitHub API returned ${status} after ${maxRetries + 1} attempts`,
    );
  }
}

/**
 * backend の期限切れ記事削除エンドポイントを呼び出す。
 * 一時的なエラー（5xx・429・ネットワーク失敗）は最大 maxRetries 回リトライする。
 * 4xx（429 以外）は再試行しない。
 * 削除件数を含む結果オブジェクトを返す。
 */
export async function deleteExpiredNews(
  env: Env,
  options: RetryOptions = {},
): Promise<DeleteExpiredNewsResult> {
  const maxRetries = options.maxRetries ?? DEFAULT_MAX_RETRIES;
  const timeoutMs = options.timeoutMs ?? 30_000;
  const sleep =
    options.sleepFn ??
    ((ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)));

  const url = `${env.BACKEND_API_URL.replace(/\/+$/, "")}/api/v1/internal/news/expired`;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    if (attempt > 0) {
      const delayMs = Math.pow(2, attempt - 1) * BASE_DELAY_MS;
      console.log(
        `[scheduler] deleteExpiredNews retry ${attempt}/${maxRetries} (waiting ${delayMs}ms)`,
      );
      await sleep(delayMs);
    }

    let status: number;
    let body: string;
    try {
      const res = await fetch(url, {
        signal: AbortSignal.timeout(timeoutMs),
        method: "DELETE",
        headers: {
          "X-Api-Key": env.BACKEND_API_KEY,
          "Content-Type": "application/json",
        },
      });
      status = res.status;
      body = await res.text();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      if (attempt < maxRetries) {
        console.warn(
          `[scheduler] deleteExpiredNews attempt ${attempt + 1} network error: ${message}`,
        );
        continue;
      }
      console.error(
        `[scheduler] deleteExpiredNews failed after ${maxRetries + 1} attempts (network): ${message}`,
      );
      throw err instanceof Error ? err : new Error(message);
    }

    if (status >= 200 && status < 300) {
      let deletedCount = 0;
      try {
        const json = JSON.parse(body) as { deletedCount?: number };
        deletedCount = json.deletedCount ?? 0;
      } catch {
        // JSON パース失敗は無視してカウント 0 扱い
      }
      console.log(
        `[scheduler] deleteExpiredNews succeeded: deletedCount=${deletedCount}`,
      );
      return { deletedCount };
    }

    const isRetryable = status === 429 || status >= 500;
    if (!isRetryable) {
      console.error(
        `[scheduler] deleteExpiredNews non-retryable HTTP status: ${status}`,
      );
      throw new Error(
        `deleteExpiredNews returned non-retryable status: ${status}`,
      );
    }

    if (attempt < maxRetries) {
      console.warn(
        `[scheduler] deleteExpiredNews attempt ${attempt + 1} received HTTP ${status}, will retry`,
      );
      continue;
    }

    console.error(
      `[scheduler] deleteExpiredNews failed after ${maxRetries + 1} attempts (last status: ${status})`,
    );
    throw new Error(
      `deleteExpiredNews returned ${status} after ${maxRetries + 1} attempts`,
    );
  }

  // unreachable; satisfies TypeScript
  throw new Error("deleteExpiredNews: unexpected loop exit");
}

/**
 * backend の Web Push 通知ジョブエンドポイントを呼び出す。
 * 一時的なエラー（5xx・429・ネットワーク失敗）は最大 maxRetries 回リトライする。
 * 4xx（429 以外）は再試行しない。
 * 送信結果を含む結果オブジェクトを返す。
 */
export async function sendPushNotify(
  env: Env,
  options: RetryOptions = {},
): Promise<PushNotifyResult> {
  const maxRetries = options.maxRetries ?? DEFAULT_MAX_RETRIES;
  const timeoutMs = options.timeoutMs ?? 30_000;
  const sleep =
    options.sleepFn ??
    ((ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)));

  const url = `${env.BACKEND_API_URL.replace(/\/+$/, "")}/api/v1/internal/push/notify`;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    if (attempt > 0) {
      const delayMs = Math.pow(2, attempt - 1) * BASE_DELAY_MS;
      console.log(
        `[scheduler] sendPushNotify retry ${attempt}/${maxRetries} (waiting ${delayMs}ms)`,
      );
      await sleep(delayMs);
    }

    let status: number;
    let body: string;
    try {
      const res = await fetch(url, {
        signal: AbortSignal.timeout(timeoutMs),
        method: "POST",
        headers: {
          "X-Api-Key": env.BACKEND_API_KEY,
          "Content-Type": "application/json",
        },
      });
      status = res.status;
      body = await res.text();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      if (attempt < maxRetries) {
        console.warn(
          `[scheduler] sendPushNotify attempt ${attempt + 1} network error: ${message}`,
        );
        continue;
      }
      console.error(
        `[scheduler] sendPushNotify failed after ${maxRetries + 1} attempts (network): ${message}`,
      );
      throw err instanceof Error ? err : new Error(message);
    }

    if (status >= 200 && status < 300) {
      let successCount = 0;
      let failureCount = 0;
      let skipCount = 0;
      let goneRemovedCount = 0;
      try {
        const json = JSON.parse(body) as {
          successCount?: number;
          failureCount?: number;
          skipCount?: number;
          goneRemovedCount?: number;
        };
        successCount = json.successCount ?? 0;
        failureCount = json.failureCount ?? 0;
        skipCount = json.skipCount ?? 0;
        goneRemovedCount = json.goneRemovedCount ?? 0;
      } catch {
        // JSON パース失敗は無視してカウント 0 扱い
      }
      console.log(
        `[scheduler] sendPushNotify succeeded: successCount=${successCount} failureCount=${failureCount} skipCount=${skipCount} goneRemovedCount=${goneRemovedCount}`,
      );
      return { successCount, failureCount, skipCount, goneRemovedCount };
    }

    const isRetryable = status === 429 || status >= 500;
    if (!isRetryable) {
      console.error(
        `[scheduler] sendPushNotify non-retryable HTTP status: ${status}`,
      );
      throw new Error(
        `sendPushNotify returned non-retryable status: ${status}`,
      );
    }

    if (attempt < maxRetries) {
      console.warn(
        `[scheduler] sendPushNotify attempt ${attempt + 1} received HTTP ${status}, will retry`,
      );
      continue;
    }

    console.error(
      `[scheduler] sendPushNotify failed after ${maxRetries + 1} attempts (last status: ${status})`,
    );
    throw new Error(
      `sendPushNotify returned ${status} after ${maxRetries + 1} attempts`,
    );
  }

  // unreachable; satisfies TypeScript
  throw new Error("sendPushNotify: unexpected loop exit");
}

/**
 * backend のヘルスチェックエンドポイントを呼び出して DB 使用量を取得する。
 * 5xx・429・ネットワークエラーは最大 maxRetries 回リトライする。
 */
export async function getHealth(
  env: Env,
  options: RetryOptions = {},
): Promise<HealthCheckResponse> {
  const maxRetries = options.maxRetries ?? DEFAULT_MAX_RETRIES;
  const timeoutMs = options.timeoutMs ?? 10_000;
  const sleep =
    options.sleepFn ??
    ((ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)));

  const url = `${env.BACKEND_API_URL.replace(/\/+$/, "")}/api/v1/health`;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    if (attempt > 0) {
      const delayMs = Math.pow(2, attempt - 1) * BASE_DELAY_MS;
      console.log(
        `[scheduler] getHealth retry ${attempt}/${maxRetries} (waiting ${delayMs}ms)`,
      );
      await sleep(delayMs);
    }

    let status: number;
    let body: string;
    try {
      const res = await fetch(url, {
        signal: AbortSignal.timeout(timeoutMs),
        method: "GET",
        headers: {
          "Content-Type": "application/json",
        },
      });
      status = res.status;
      body = await res.text();
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      if (attempt < maxRetries) {
        console.warn(
          `[scheduler] getHealth attempt ${attempt + 1} network error: ${message}`,
        );
        continue;
      }
      console.error(
        `[scheduler] getHealth failed after ${maxRetries + 1} attempts (network): ${message}`,
      );
      throw err instanceof Error ? err : new Error(message);
    }

    if (status >= 200 && status < 300) {
      try {
        return JSON.parse(body) as HealthCheckResponse;
      } catch {
        return { status: "ok", timestamp: new Date().toISOString() };
      }
    }

    const isRetryable = status === 429 || status >= 500;
    if (!isRetryable) {
      console.error(
        `[scheduler] getHealth non-retryable HTTP status: ${status}`,
      );
      throw new Error(`getHealth returned non-retryable status: ${status}`);
    }

    if (attempt < maxRetries) {
      console.warn(
        `[scheduler] getHealth attempt ${attempt + 1} received HTTP ${status}, will retry`,
      );
      continue;
    }

    console.error(
      `[scheduler] getHealth failed after ${maxRetries + 1} attempts (last status: ${status})`,
    );
    throw new Error(
      `getHealth returned ${status} after ${maxRetries + 1} attempts`,
    );
  }

  // unreachable; satisfies TypeScript
  throw new Error("getHealth: unexpected loop exit");
}

/**
 * backend の Web Push ブロードキャストエンドポイントを呼び出す。
 * customMessage を request body に含めて POST /api/v1/internal/push/notify を呼び出す。
 */
export async function sendPushBroadcast(
  env: Env,
  message: string,
  options: RetryOptions = {},
): Promise<PushNotifyResult> {
  const maxRetries = options.maxRetries ?? DEFAULT_MAX_RETRIES;
  const timeoutMs = options.timeoutMs ?? 30_000;
  const sleep =
    options.sleepFn ??
    ((ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)));

  const url = `${env.BACKEND_API_URL.replace(/\/+$/, "")}/api/v1/internal/push/notify`;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    if (attempt > 0) {
      const delayMs = Math.pow(2, attempt - 1) * BASE_DELAY_MS;
      console.log(
        `[scheduler] sendPushBroadcast retry ${attempt}/${maxRetries} (waiting ${delayMs}ms)`,
      );
      await sleep(delayMs);
    }

    let status: number;
    let body: string;
    try {
      const res = await fetch(url, {
        signal: AbortSignal.timeout(timeoutMs),
        method: "POST",
        headers: {
          "X-Api-Key": env.BACKEND_API_KEY,
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ customMessage: message }),
      });
      status = res.status;
      body = await res.text();
    } catch (err) {
      const message_ = err instanceof Error ? err.message : String(err);
      if (attempt < maxRetries) {
        console.warn(
          `[scheduler] sendPushBroadcast attempt ${attempt + 1} network error: ${message_}`,
        );
        continue;
      }
      console.error(
        `[scheduler] sendPushBroadcast failed after ${maxRetries + 1} attempts (network): ${message_}`,
      );
      throw err instanceof Error ? err : new Error(message_);
    }

    if (status >= 200 && status < 300) {
      let successCount = 0;
      let failureCount = 0;
      let skipCount = 0;
      let goneRemovedCount = 0;
      try {
        const json = JSON.parse(body) as {
          successCount?: number;
          failureCount?: number;
          skipCount?: number;
          goneRemovedCount?: number;
        };
        successCount = json.successCount ?? 0;
        failureCount = json.failureCount ?? 0;
        skipCount = json.skipCount ?? 0;
        goneRemovedCount = json.goneRemovedCount ?? 0;
      } catch {
        // JSON パース失敗は無視してカウント 0 扱い
      }
      console.log(
        `[scheduler] sendPushBroadcast succeeded: successCount=${successCount} failureCount=${failureCount} goneRemovedCount=${goneRemovedCount}`,
      );
      return { successCount, failureCount, skipCount, goneRemovedCount };
    }

    const isRetryable = status === 429 || status >= 500;
    if (!isRetryable) {
      console.error(
        `[scheduler] sendPushBroadcast non-retryable HTTP status: ${status}`,
      );
      throw new Error(
        `sendPushBroadcast returned non-retryable status: ${status}`,
      );
    }

    if (attempt < maxRetries) {
      console.warn(
        `[scheduler] sendPushBroadcast attempt ${attempt + 1} received HTTP ${status}, will retry`,
      );
      continue;
    }

    console.error(
      `[scheduler] sendPushBroadcast failed after ${maxRetries + 1} attempts (last status: ${status})`,
    );
    throw new Error(
      `sendPushBroadcast returned ${status} after ${maxRetries + 1} attempts`,
    );
  }

  // unreachable; satisfies TypeScript
  throw new Error("sendPushBroadcast: unexpected loop exit");
}

/**
 * Neon DB 容量チェックジョブ（FEAT-053）。
 * GET /api/v1/health を呼び出し、dbSizeMb が閾値（400 MB）を超えていた場合に
 * Web Push 警告通知を全購読者へ送信する。
 * 送信メッセージには現在の使用量と上限（500 MB）を含む。
 */
export async function checkDbCapacity(
  env: Env,
  options: RetryOptions = {},
): Promise<DbCapacityCheckResult> {
  const health = await getHealth(env, options);
  const dbSizeMb = health.dbSizeMb ?? null;

  if (dbSizeMb === null) {
    console.log(
      "[scheduler] DB 使用量を取得できませんでした（dbSizeMb=null）。チェックをスキップします。",
    );
    return { dbSizeMb: null, alertSent: false };
  }

  console.log(`[scheduler] DB 使用量: ${dbSizeMb} MB / ${DB_MAX_SIZE_MB} MB`);

  if (dbSizeMb <= DB_CAPACITY_THRESHOLD_MB) {
    console.log(
      `[scheduler] DB 使用量は閾値以下（${DB_CAPACITY_THRESHOLD_MB} MB）。通知は送信しません。`,
    );
    return { dbSizeMb, alertSent: false };
  }

  const message = `DB 使用量が ${dbSizeMb} MB に達しています（上限 ${DB_MAX_SIZE_MB} MB）。不要なデータの削除をご検討ください。`;
  console.warn(`[scheduler] DB 容量警告: ${message}`);

  await sendPushBroadcast(env, message, options);

  return { dbSizeMb, alertSent: true };
}

/** 毎週月曜 0:00 UTC の削除 Cron の cron 式 */
const CLEANUP_CRON = "0 0 * * 1";

/** 2時間ごとの Web Push 通知 Cron の cron 式（コレクター起動の30分後）*/
const NOTIFY_CRON = "47 */2 * * *";

/** 毎週月曜 1:00 UTC の Neon DB 容量チェック Cron の cron 式（FEAT-053）*/
const DB_CAPACITY_CHECK_CRON = "0 1 * * 1";

export default {
  async scheduled(
    event: ScheduledController,
    env: Env,
    _ctx: ExecutionContext,
  ): Promise<void> {
    console.log(
      `[scheduler] cron triggered at ${new Date().toISOString()} (cron="${event.cron}")`,
    );

    if (event.cron === CLEANUP_CRON) {
      // 週次: 期限切れ記事の削除
      try {
        const result = await deleteExpiredNews(env);
        console.log(
          `[scheduler] cleanup cron completed: deletedCount=${result.deletedCount}`,
        );
      } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        console.error(`[scheduler] cleanup cron failed: ${message}`);
      }
    } else if (event.cron === NOTIFY_CRON) {
      // 2時間ごと（収集30分後）: Web Push 通知ジョブの実行
      try {
        const result = await sendPushNotify(env);
        console.log(
          `[scheduler] notify cron completed: successCount=${result.successCount} failureCount=${result.failureCount} skipCount=${result.skipCount} goneRemovedCount=${result.goneRemovedCount}`,
        );
      } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        console.error(`[scheduler] notify cron failed: ${message}`);
      }
    } else if (event.cron === DB_CAPACITY_CHECK_CRON) {
      // 週次: Neon DB 容量チェック（FEAT-053）
      try {
        const result = await checkDbCapacity(env);
        if (result.alertSent) {
          console.log(
            `[scheduler] db-capacity cron completed: dbSizeMb=${result.dbSizeMb} → alert sent`,
          );
        } else {
          console.log(
            `[scheduler] db-capacity cron completed: dbSizeMb=${result.dbSizeMb} → no alert`,
          );
        }
      } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        console.error(`[scheduler] db-capacity cron failed: ${message}`);
      }
    } else {
      // 2時間ごと: コレクターワークフローのディスパッチ
      try {
        await dispatchWorkflow(env);
      } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        console.error(`[scheduler] cron handler failed: ${message}`);
      }
    }
  },
} satisfies ExportedHandler<Env>;
