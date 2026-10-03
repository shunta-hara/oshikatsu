import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, apiFetch, getApiBase } from "../api/client";

// ------------------------------------------------------------------
// テスト用ユーティリティ
// ------------------------------------------------------------------
function makeFetchMock(
  status: number,
  body: unknown = null,
  contentType = "application/json",
) {
  const headers = new Headers();
  if (body !== null) {
    headers.set("content-type", contentType);
  }
  return vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    headers,
    json: () => Promise.resolve(body),
  });
}

// ------------------------------------------------------------------
// getApiBase のテスト
// ------------------------------------------------------------------
describe("getApiBase", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it("VITE_API_BASE_URL が未設定の場合は /api/v1 を返す（開発: Vite プロキシ経由）", () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    expect(getApiBase()).toBe("/api/v1");
  });

  it("VITE_API_BASE_URL が設定されている場合は ${baseUrl}/api/v1 を返す", () => {
    vi.stubEnv("VITE_API_BASE_URL", "https://backend.example.com");
    expect(getApiBase()).toBe("https://backend.example.com/api/v1");
  });

  it("VITE_API_BASE_URL に末尾スラッシュなしの URL を設定しても /api/v1 が正しく付く", () => {
    vi.stubEnv("VITE_API_BASE_URL", "https://render-backend.onrender.com");
    expect(getApiBase()).toBe("https://render-backend.onrender.com/api/v1");
  });

  it("VITE_API_BASE_URL の末尾に / が付いていても //api/v1 にならない", () => {
    vi.stubEnv("VITE_API_BASE_URL", "https://backend.example.com/");
    expect(getApiBase()).toBe("https://backend.example.com/api/v1");
  });

  it("VITE_API_BASE_URL の末尾に複数の / が付いていても //api/v1 にならない", () => {
    vi.stubEnv("VITE_API_BASE_URL", "https://backend.example.com///");
    expect(getApiBase()).toBe("https://backend.example.com/api/v1");
  });
});

// ------------------------------------------------------------------
// apiFetch のテスト
// ------------------------------------------------------------------
describe("apiFetch", () => {
  beforeEach(() => {
    vi.unstubAllEnvs();
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
  });

  // --- 正常系: VITE_API_BASE_URL 未設定（開発環境） ---
  it("VITE_API_BASE_URL 未設定時は /api/v1 プレフィックスで fetch を呼ぶ", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    const mockFetch = makeFetchMock(200, { id: 1 });
    vi.stubGlobal("fetch", mockFetch);

    await apiFetch("/oshis");

    expect(mockFetch).toHaveBeenCalledWith(
      "/api/v1/oshis",
      expect.objectContaining({ headers: expect.anything() }),
    );
  });

  // --- 正常系: VITE_API_BASE_URL 設定済み（本番環境） ---
  it("VITE_API_BASE_URL 設定時はそのオリジン + /api/v1 で fetch を呼ぶ", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "https://api.production.example.com");
    const mockFetch = makeFetchMock(200, { id: 1 });
    vi.stubGlobal("fetch", mockFetch);

    await apiFetch("/oshis");

    expect(mockFetch).toHaveBeenCalledWith(
      "https://api.production.example.com/api/v1/oshis",
      expect.objectContaining({ headers: expect.anything() }),
    );
  });

  // --- 正常系: JSON レスポンス ---
  it("200 JSON レスポンスを正しくパースして返す", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    vi.stubGlobal("fetch", makeFetchMock(200, { name: "推し太郎" }));

    const result = await apiFetch<{ name: string }>("/oshis/1");
    expect(result).toEqual({ name: "推し太郎" });
  });

  // --- 正常系: 204 No Content ---
  it("204 No Content のとき undefined を返す（ApiError をスローしない）", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 204,
        headers: new Headers(),
        json: () => Promise.resolve(null),
      }),
    );

    const result = await apiFetch<void>("/oshis/1");
    expect(result).toBeUndefined();
  });

  // --- 正常系: Authorization ヘッダーが付く ---
  it("localStorage にトークンがある場合 Authorization ヘッダーが付く", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    localStorage.setItem("auth_token", "my-secret-token");
    const mockFetch = makeFetchMock(200, {});
    vi.stubGlobal("fetch", mockFetch);

    await apiFetch("/oshis");

    expect(mockFetch).toHaveBeenCalledWith(
      "/api/v1/oshis",
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: "Bearer my-secret-token",
        }),
      }),
    );
  });

  it("localStorage にトークンがない場合 Authorization ヘッダーは付かない", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    const mockFetch = makeFetchMock(200, {});
    vi.stubGlobal("fetch", mockFetch);

    await apiFetch("/oshis");

    const calledHeaders = mockFetch.mock.calls[0][1].headers as Record<
      string,
      string
    >;
    expect(calledHeaders["Authorization"]).toBeUndefined();
  });

  // --- 異常系: 4xx ---
  it("401 レスポンスのとき ApiError(401) をスローする", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    vi.stubGlobal("fetch", makeFetchMock(401, { message: "Unauthorized" }));

    await expect(apiFetch("/protected")).rejects.toThrow(ApiError);
    await expect(apiFetch("/protected")).rejects.toMatchObject({
      status: 401,
      message: "Unauthorized",
    });
  });

  it("404 レスポンスのとき ApiError(404) をスローする", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    vi.stubGlobal("fetch", makeFetchMock(404, { message: "Not Found" }));

    await expect(apiFetch("/oshis/9999")).rejects.toThrow(ApiError);
    await expect(apiFetch("/oshis/9999")).rejects.toMatchObject({
      status: 404,
    });
  });

  // --- 異常系: 5xx ---
  it("500 レスポンスのとき ApiError(500) をスローする", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    vi.stubGlobal("fetch", makeFetchMock(500, null, "text/plain"));

    await expect(apiFetch("/oshis")).rejects.toMatchObject({ status: 500 });
  });

  // --- 境界値: エラーボディが JSON でない場合 ---
  it("エラーレスポンスの body が JSON でない場合でも ApiError をスローする", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "");
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
        headers: new Headers({ "content-type": "text/html" }),
        json: () => Promise.reject(new Error("not json")),
      }),
    );

    await expect(apiFetch("/oshis")).rejects.toMatchObject({
      status: 503,
      message: "HTTP 503",
    });
  });
});
