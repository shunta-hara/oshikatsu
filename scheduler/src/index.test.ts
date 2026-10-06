import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  checkDbCapacity,
  deleteExpiredNews,
  dispatchWorkflow,
  getHealth,
  sendPushBroadcast,
  sendPushNotify,
  type Env,
  type HealthCheckResponse,
} from "./index";

const mockEnv: Env = {
  GITHUB_REPOSITORY: "owner/repo",
  GITHUB_WORKFLOW_FILE: "collect.yml",
  GITHUB_REF: "master",
  GITHUB_DISPATCH_TOKEN: "test-token-secret",
  BACKEND_API_URL: "https://api.example.com",
  BACKEND_API_KEY: "test-api-key",
};

const noopSleep = (): Promise<void> => Promise.resolve();

function mockResponse(status: number, body = ""): Response {
  return {
    status,
    text: () => Promise.resolve(body),
  } as unknown as Response;
}

describe("dispatchWorkflow", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  // 正常系

  it("204 が返ったとき正常終了する", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep }),
    ).resolves.toBeUndefined();
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("正しい URL・ヘッダー・本文で fetch を呼ぶ", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    await dispatchWorkflow(mockEnv, { sleepFn: noopSleep });

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const [url, init] = mockFetch.mock.calls[0] as [string, RequestInit];

    // 正しい URL
    expect(url).toBe(
      "https://api.github.com/repos/owner/repo/actions/workflows/collect.yml/dispatches",
    );
    // HTTP メソッド
    expect(init.method).toBe("POST");

    // ヘッダー
    const headers = init.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer test-token-secret");
    expect(headers["Accept"]).toBe("application/vnd.github+json");
    expect(headers["X-GitHub-Api-Version"]).toBe("2022-11-28");
    expect(headers["User-Agent"]).toBeTruthy();

    // ボディ
    expect(JSON.parse(init.body as string)).toEqual({
      ref: "master",
      inputs: {},
    });
  });

  it("5xx の後に 204 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep }),
    ).resolves.toBeUndefined();
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("429 の後に 204 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(429))
      .mockResolvedValueOnce(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep }),
    ).resolves.toBeUndefined();
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("ネットワークエラーの後に 204 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockRejectedValueOnce(new Error("Network failure"))
      .mockResolvedValueOnce(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep }),
    ).resolves.toBeUndefined();
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  // 異常系

  it("401 では再試行せずにエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(401));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep }),
    ).rejects.toThrow("non-retryable status: 401");
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("422 では再試行せずにエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(422));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep }),
    ).rejects.toThrow("non-retryable status: 422");
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  // 境界値: ちょうど 3 回リトライ（合計 4 回呼ばれる）

  it("5xx が続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(503));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("after 4 attempts");
    // 初回 + リトライ3回 = 合計4回
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  it("429 が続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(429));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("after 4 attempts");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  it("ネットワークエラーが続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi
      .fn()
      .mockRejectedValue(new Error("Connection refused"));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("Connection refused");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  // リトライ時の sleep

  it("1回目のリトライで sleepFn に 1000ms を渡す", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    const sleepFn = vi.fn().mockResolvedValue(undefined);
    await dispatchWorkflow(mockEnv, { sleepFn });

    expect(sleepFn).toHaveBeenCalledTimes(1);
    expect(sleepFn).toHaveBeenCalledWith(1000);
  });

  it("2回目のリトライで sleepFn に 2000ms を渡す（指数バックオフ）", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    const sleepFn = vi.fn().mockResolvedValue(undefined);
    await dispatchWorkflow(mockEnv, { sleepFn });

    expect(sleepFn).toHaveBeenCalledTimes(2);
    expect(sleepFn).toHaveBeenNthCalledWith(1, 1000);
    expect(sleepFn).toHaveBeenNthCalledWith(2, 2000);
  });

  // タイムアウト（AbortError）はリトライ対象

  it("タイムアウト（AbortError）後に 204 が返ったときリトライして成功する", async () => {
    const abortError = new DOMException(
      "The operation was aborted.",
      "AbortError",
    );
    const mockFetch = vi
      .fn()
      .mockRejectedValueOnce(abortError)
      .mockResolvedValueOnce(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep }),
    ).resolves.toBeUndefined();
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("タイムアウトが続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const abortError = new DOMException(
      "The operation was aborted.",
      "AbortError",
    );
    const mockFetch = vi.fn().mockRejectedValue(abortError);
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      dispatchWorkflow(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("aborted");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  it("timeoutMs オプションが fetch の signal に反映される", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(204));
    vi.stubGlobal("fetch", mockFetch);

    await dispatchWorkflow(mockEnv, { sleepFn: noopSleep, timeoutMs: 5000 });

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(init.signal).toBeInstanceOf(AbortSignal);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// deleteExpiredNews
// ─────────────────────────────────────────────────────────────────────────────

describe("deleteExpiredNews", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  // 正常系

  it("200 が返ったとき削除件数を返す", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(200, JSON.stringify({ deletedCount: 42 })),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await deleteExpiredNews(mockEnv, { sleepFn: noopSleep });

    expect(result.deletedCount).toBe(42);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("正しい URL・メソッド・ヘッダーで fetch を呼ぶ", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(200, JSON.stringify({ deletedCount: 0 })),
      );
    vi.stubGlobal("fetch", mockFetch);

    await deleteExpiredNews(mockEnv, { sleepFn: noopSleep });

    const [url, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example.com/api/v1/internal/news/expired");
    expect(init.method).toBe("DELETE");
    const headers = init.headers as Record<string, string>;
    expect(headers["X-Api-Key"]).toBe("test-api-key");
  });

  it("削除件数 0 でも正常に返す", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(200, JSON.stringify({ deletedCount: 0 })),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await deleteExpiredNews(mockEnv, { sleepFn: noopSleep });

    expect(result.deletedCount).toBe(0);
  });

  it("BACKEND_API_URL の末尾スラッシュを除去してパスを組み立てる", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(200, JSON.stringify({ deletedCount: 1 })),
      );
    vi.stubGlobal("fetch", mockFetch);

    const envWithSlash: Env = {
      ...mockEnv,
      BACKEND_API_URL: "https://api.example.com/",
    };
    await deleteExpiredNews(envWithSlash, { sleepFn: noopSleep });

    const [url] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example.com/api/v1/internal/news/expired");
  });

  // リトライ: 5xx

  it("5xx の後に 200 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(
        mockResponse(200, JSON.stringify({ deletedCount: 3 })),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await deleteExpiredNews(mockEnv, { sleepFn: noopSleep });

    expect(result.deletedCount).toBe(3);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("429 の後に 200 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(429))
      .mockResolvedValueOnce(
        mockResponse(200, JSON.stringify({ deletedCount: 1 })),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await deleteExpiredNews(mockEnv, { sleepFn: noopSleep });

    expect(result.deletedCount).toBe(1);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("ネットワークエラーの後に 200 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockRejectedValueOnce(new Error("Network failure"))
      .mockResolvedValueOnce(
        mockResponse(200, JSON.stringify({ deletedCount: 0 })),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await deleteExpiredNews(mockEnv, { sleepFn: noopSleep });

    expect(result.deletedCount).toBe(0);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  // 異常系: 非リトライ

  it("401 では再試行せずエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(401));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      deleteExpiredNews(mockEnv, { sleepFn: noopSleep }),
    ).rejects.toThrow("non-retryable status: 401");
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("403 では再試行せずエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(403));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      deleteExpiredNews(mockEnv, { sleepFn: noopSleep }),
    ).rejects.toThrow("non-retryable status: 403");
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  // 境界値: 最大リトライ後も失敗

  it("5xx が続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(503));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      deleteExpiredNews(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("after 4 attempts");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  it("ネットワークエラーが続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi
      .fn()
      .mockRejectedValue(new Error("Connection refused"));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      deleteExpiredNews(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("Connection refused");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// sendPushNotify
// ─────────────────────────────────────────────────────────────────────────────

describe("sendPushNotify", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  // 正常系

  it("200 が返ったとき送信結果を返す", async () => {
    const mockFetch = vi.fn().mockResolvedValue(
      mockResponse(
        200,
        JSON.stringify({
          successCount: 5,
          failureCount: 1,
          skipCount: 2,
          goneRemovedCount: 0,
        }),
      ),
    );
    vi.stubGlobal("fetch", mockFetch);

    const result = await sendPushNotify(mockEnv, { sleepFn: noopSleep });

    expect(result.successCount).toBe(5);
    expect(result.failureCount).toBe(1);
    expect(result.skipCount).toBe(2);
    expect(result.goneRemovedCount).toBe(0);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("正しい URL・メソッド・ヘッダーで fetch を呼ぶ", async () => {
    const mockFetch = vi.fn().mockResolvedValue(
      mockResponse(
        200,
        JSON.stringify({
          successCount: 0,
          failureCount: 0,
          skipCount: 0,
          goneRemovedCount: 0,
        }),
      ),
    );
    vi.stubGlobal("fetch", mockFetch);

    await sendPushNotify(mockEnv, { sleepFn: noopSleep });

    const [url, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example.com/api/v1/internal/push/notify");
    expect(init.method).toBe("POST");
    const headers = init.headers as Record<string, string>;
    expect(headers["X-Api-Key"]).toBe("test-api-key");
  });

  it("全カウント 0 でも正常に返す", async () => {
    const mockFetch = vi.fn().mockResolvedValue(
      mockResponse(
        200,
        JSON.stringify({
          successCount: 0,
          failureCount: 0,
          skipCount: 0,
          goneRemovedCount: 0,
        }),
      ),
    );
    vi.stubGlobal("fetch", mockFetch);

    const result = await sendPushNotify(mockEnv, { sleepFn: noopSleep });

    expect(result.successCount).toBe(0);
    expect(result.failureCount).toBe(0);
    expect(result.skipCount).toBe(0);
    expect(result.goneRemovedCount).toBe(0);
  });

  it("BACKEND_API_URL の末尾スラッシュを除去してパスを組み立てる", async () => {
    const mockFetch = vi.fn().mockResolvedValue(
      mockResponse(
        200,
        JSON.stringify({
          successCount: 1,
          failureCount: 0,
          skipCount: 0,
          goneRemovedCount: 0,
        }),
      ),
    );
    vi.stubGlobal("fetch", mockFetch);

    const envWithSlash: Env = {
      ...mockEnv,
      BACKEND_API_URL: "https://api.example.com/",
    };
    await sendPushNotify(envWithSlash, { sleepFn: noopSleep });

    const [url] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example.com/api/v1/internal/push/notify");
  });

  // リトライ: 5xx

  it("5xx の後に 200 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 3,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 0,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await sendPushNotify(mockEnv, { sleepFn: noopSleep });

    expect(result.successCount).toBe(3);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("429 の後に 200 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(429))
      .mockResolvedValueOnce(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 1,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 0,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await sendPushNotify(mockEnv, { sleepFn: noopSleep });

    expect(result.successCount).toBe(1);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("ネットワークエラーの後に 200 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockRejectedValueOnce(new Error("Network failure"))
      .mockResolvedValueOnce(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 0,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 0,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await sendPushNotify(mockEnv, { sleepFn: noopSleep });

    expect(result.successCount).toBe(0);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  // 異常系: 非リトライ

  it("401 では再試行せずエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(401));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      sendPushNotify(mockEnv, { sleepFn: noopSleep }),
    ).rejects.toThrow("non-retryable status: 401");
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("403 では再試行せずエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(403));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      sendPushNotify(mockEnv, { sleepFn: noopSleep }),
    ).rejects.toThrow("non-retryable status: 403");
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  // 境界値: 最大リトライ後も失敗

  it("5xx が続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(503));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      sendPushNotify(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("after 4 attempts");
    // 初回 + リトライ3回 = 合計4回
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  it("ネットワークエラーが続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi
      .fn()
      .mockRejectedValue(new Error("Connection refused"));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      sendPushNotify(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("Connection refused");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  // リトライ時の sleep（指数バックオフ）

  it("1回目のリトライで sleepFn に 1000ms を渡す", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 0,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 0,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const sleepFn = vi.fn().mockResolvedValue(undefined);
    await sendPushNotify(mockEnv, { sleepFn });

    expect(sleepFn).toHaveBeenCalledTimes(1);
    expect(sleepFn).toHaveBeenCalledWith(1000);
  });

  it("2回目のリトライで sleepFn に 2000ms を渡す（指数バックオフ）", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 0,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 0,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const sleepFn = vi.fn().mockResolvedValue(undefined);
    await sendPushNotify(mockEnv, { sleepFn });

    expect(sleepFn).toHaveBeenCalledTimes(2);
    expect(sleepFn).toHaveBeenNthCalledWith(1, 1000);
    expect(sleepFn).toHaveBeenNthCalledWith(2, 2000);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// getHealth（FEAT-053）
// ─────────────────────────────────────────────────────────────────────────────

describe("getHealth", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function makeHealthResponse(
    overrides: Partial<HealthCheckResponse> = {},
  ): HealthCheckResponse {
    return {
      status: "ok",
      timestamp: "2026-10-06T00:00:00.000Z",
      dbSizeBytes: 209_715_200,
      dbSizeMb: 200.0,
      ...overrides,
    };
  }

  // 正常系

  it("200 が返ったとき HealthCheckResponse を返す", async () => {
    const expected = makeHealthResponse({ dbSizeMb: 200.0 });
    const mockFetch = vi
      .fn()
      .mockResolvedValue(mockResponse(200, JSON.stringify(expected)));
    vi.stubGlobal("fetch", mockFetch);

    const result = await getHealth(mockEnv, { sleepFn: noopSleep });

    expect(result.status).toBe("ok");
    expect(result.dbSizeMb).toBe(200.0);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("正しい URL・メソッドで fetch を呼ぶ", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(200, JSON.stringify(makeHealthResponse())),
      );
    vi.stubGlobal("fetch", mockFetch);

    await getHealth(mockEnv, { sleepFn: noopSleep });

    const [url, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example.com/api/v1/health");
    expect(init.method).toBe("GET");
  });

  it("dbSizeMb が null の場合も正常に返す", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(
          200,
          JSON.stringify(
            makeHealthResponse({ dbSizeMb: null, dbSizeBytes: null }),
          ),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await getHealth(mockEnv, { sleepFn: noopSleep });

    expect(result.dbSizeMb).toBeNull();
  });

  it("BACKEND_API_URL の末尾スラッシュを除去してパスを組み立てる", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(200, JSON.stringify(makeHealthResponse())),
      );
    vi.stubGlobal("fetch", mockFetch);

    const envWithSlash: Env = {
      ...mockEnv,
      BACKEND_API_URL: "https://api.example.com/",
    };
    await getHealth(envWithSlash, { sleepFn: noopSleep });

    const [url] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example.com/api/v1/health");
  });

  // リトライ

  it("5xx の後に 200 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(
        mockResponse(200, JSON.stringify(makeHealthResponse())),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await getHealth(mockEnv, { sleepFn: noopSleep });

    expect(result.status).toBe("ok");
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  // 異常系

  it("401 では再試行せずエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(401));
    vi.stubGlobal("fetch", mockFetch);

    await expect(getHealth(mockEnv, { sleepFn: noopSleep })).rejects.toThrow(
      "non-retryable status: 401",
    );
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("5xx が続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(503));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      getHealth(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("after 4 attempts");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });

  it("ネットワークエラーが続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi
      .fn()
      .mockRejectedValue(new Error("Connection refused"));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      getHealth(mockEnv, { sleepFn: noopSleep, maxRetries: 3 }),
    ).rejects.toThrow("Connection refused");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// sendPushBroadcast（FEAT-053）
// ─────────────────────────────────────────────────────────────────────────────

describe("sendPushBroadcast", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  const testMessage =
    "DB 使用量が 450 MB に達しています（上限 500 MB）。ご確認ください。";

  // 正常系

  it("200 が返ったとき送信結果を返す", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 3,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 1,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await sendPushBroadcast(mockEnv, testMessage, {
      sleepFn: noopSleep,
    });

    expect(result.successCount).toBe(3);
    expect(result.goneRemovedCount).toBe(1);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("正しい URL・メソッド・ヘッダー・ボディで fetch を呼ぶ", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 0,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 0,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    await sendPushBroadcast(mockEnv, testMessage, { sleepFn: noopSleep });

    const [url, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example.com/api/v1/internal/push/notify");
    expect(init.method).toBe("POST");
    const headers = init.headers as Record<string, string>;
    expect(headers["X-Api-Key"]).toBe("test-api-key");
    expect(JSON.parse(init.body as string)).toEqual({
      customMessage: testMessage,
    });
  });

  it("BACKEND_API_URL の末尾スラッシュを除去してパスを組み立てる", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 1,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 0,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const envWithSlash: Env = {
      ...mockEnv,
      BACKEND_API_URL: "https://api.example.com/",
    };
    await sendPushBroadcast(envWithSlash, testMessage, { sleepFn: noopSleep });

    const [url] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("https://api.example.com/api/v1/internal/push/notify");
  });

  // リトライ

  it("5xx の後に 200 が返ったときリトライして成功する", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(503))
      .mockResolvedValueOnce(
        mockResponse(
          200,
          JSON.stringify({
            successCount: 2,
            failureCount: 0,
            skipCount: 0,
            goneRemovedCount: 0,
          }),
        ),
      );
    vi.stubGlobal("fetch", mockFetch);

    const result = await sendPushBroadcast(mockEnv, testMessage, {
      sleepFn: noopSleep,
    });

    expect(result.successCount).toBe(2);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  // 異常系

  it("401 では再試行せずエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(401));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      sendPushBroadcast(mockEnv, testMessage, { sleepFn: noopSleep }),
    ).rejects.toThrow("non-retryable status: 401");
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("5xx が続いて最大3回リトライ後も失敗するとエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(503));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      sendPushBroadcast(mockEnv, testMessage, {
        sleepFn: noopSleep,
        maxRetries: 3,
      }),
    ).rejects.toThrow("after 4 attempts");
    expect(mockFetch).toHaveBeenCalledTimes(4);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// checkDbCapacity（FEAT-053）
// ─────────────────────────────────────────────────────────────────────────────

describe("checkDbCapacity", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function makeHealthBody(dbSizeMb: number | null): string {
    return JSON.stringify({
      status: "ok",
      timestamp: "2026-10-06T00:00:00.000Z",
      dbSizeBytes: dbSizeMb !== null ? Math.round(dbSizeMb * 1_048_576) : null,
      dbSizeMb,
    });
  }

  function makePushBody(): string {
    return JSON.stringify({
      successCount: 5,
      failureCount: 0,
      skipCount: 0,
      goneRemovedCount: 0,
    });
  }

  // 正常系: 閾値以下

  it("dbSizeMb が 400 MB 以下のとき alertSent=false を返す", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(mockResponse(200, makeHealthBody(200.0)));
    vi.stubGlobal("fetch", mockFetch);

    const result = await checkDbCapacity(mockEnv, { sleepFn: noopSleep });

    expect(result.dbSizeMb).toBe(200.0);
    expect(result.alertSent).toBe(false);
    // Push は送信されない
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it("dbSizeMb が ちょうど 400 MB のとき alertSent=false を返す（境界値：閾値と等しい）", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(mockResponse(200, makeHealthBody(400.0)));
    vi.stubGlobal("fetch", mockFetch);

    const result = await checkDbCapacity(mockEnv, { sleepFn: noopSleep });

    expect(result.alertSent).toBe(false);
  });

  // 正常系: 閾値超過 → Push 送信

  it("dbSizeMb が 400 MB 超過のとき alertSent=true でPushが送信される", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(200, makeHealthBody(450.5)))
      .mockResolvedValueOnce(mockResponse(200, makePushBody()));
    vi.stubGlobal("fetch", mockFetch);

    const result = await checkDbCapacity(mockEnv, { sleepFn: noopSleep });

    expect(result.dbSizeMb).toBe(450.5);
    expect(result.alertSent).toBe(true);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it("Push 送信時のボディに使用量と上限が含まれる", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(200, makeHealthBody(460.0)))
      .mockResolvedValueOnce(mockResponse(200, makePushBody()));
    vi.stubGlobal("fetch", mockFetch);

    await checkDbCapacity(mockEnv, { sleepFn: noopSleep });

    const [, pushInit] = mockFetch.mock.calls[1] as [string, RequestInit];
    const body = JSON.parse(pushInit.body as string) as {
      customMessage?: string;
    };
    expect(body.customMessage).toContain("460");
    expect(body.customMessage).toContain("500");
  });

  // 境界値: dbSizeMb が null

  it("dbSizeMb が null のとき alertSent=false でPushを送信しない", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValue(mockResponse(200, makeHealthBody(null)));
    vi.stubGlobal("fetch", mockFetch);

    const result = await checkDbCapacity(mockEnv, { sleepFn: noopSleep });

    expect(result.dbSizeMb).toBeNull();
    expect(result.alertSent).toBe(false);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  // 異常系: health チェック失敗

  it("getHealth が失敗したときエラーをスローする", async () => {
    const mockFetch = vi.fn().mockResolvedValue(mockResponse(503));
    vi.stubGlobal("fetch", mockFetch);

    await expect(
      checkDbCapacity(mockEnv, { sleepFn: noopSleep, maxRetries: 0 }),
    ).rejects.toThrow();
  });
});
