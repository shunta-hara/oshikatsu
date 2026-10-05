import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  deleteExpiredNews,
  dispatchWorkflow,
  sendPushNotify,
  type Env,
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
