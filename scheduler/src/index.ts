export interface Env {
  GITHUB_REPOSITORY: string;
  GITHUB_WORKFLOW_FILE: string;
  GITHUB_REF: string;
  GITHUB_DISPATCH_TOKEN: string;
}

interface DispatchOptions {
  maxRetries?: number;
  sleepFn?: (ms: number) => Promise<void>;
  timeoutMs?: number;
}

const DEFAULT_MAX_RETRIES = 3;
const BASE_DELAY_MS = 1000;

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
  options: DispatchOptions = {},
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

export default {
  async scheduled(
    _event: ScheduledController,
    env: Env,
    _ctx: ExecutionContext,
  ): Promise<void> {
    console.log(`[scheduler] cron triggered at ${new Date().toISOString()}`);
    try {
      await dispatchWorkflow(env);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      console.error(`[scheduler] cron handler failed: ${message}`);
    }
  },
} satisfies ExportedHandler<Env>;
