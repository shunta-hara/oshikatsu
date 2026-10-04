import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { dispatchWorkflow, type Env } from "./index";

const mockEnv: Env = {
  GITHUB_REPOSITORY: "owner/repo",
  GITHUB_WORKFLOW_FILE: "collect.yml",
  GITHUB_REF: "master",
  GITHUB_DISPATCH_TOKEN: "test-token-secret",
};

const noopSleep = (): Promise<void> => Promise.resolve();

function mockResponse(status: number): Response {
  return { status } as unknown as Response;
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
