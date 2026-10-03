/** API 呼び出し基盤: JSON fetch ラッパー */

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ApiError";
  }
}

/**
 * API のベース URL を返す。
 * - `VITE_API_BASE_URL` が設定されている場合: `${VITE_API_BASE_URL}/api/v1`
 *   （本番: Cloudflare Pages から Render backend への cross-origin リクエスト）
 * - 未設定の場合: `/api/v1`
 *   （開発: Vite の `/api` プロキシが localhost:5000 に転送する）
 */
export function getApiBase(): string {
  const baseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
  return baseUrl ? `${baseUrl}/api/v1` : "/api/v1";
}

/**
 * JSON レスポンスを返す fetch ラッパー。
 * 4xx/5xx は ApiError をスローする。
 */
export async function apiFetch<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  };

  const token = localStorage.getItem("auth_token");
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(`${getApiBase()}${path}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    // エラーレスポンスからメッセージを取得する
    let errorMessage = `HTTP ${response.status}`;
    try {
      const body = (await response.json()) as { message?: string };
      if (body.message) {
        errorMessage = body.message;
      }
    } catch {
      // JSON パース失敗時はデフォルトメッセージを使う
    }
    throw new ApiError(response.status, errorMessage);
  }

  // 204 No Content など body が空の場合を考慮
  const contentType = response.headers.get("content-type");
  if (contentType && contentType.includes("application/json")) {
    return response.json() as Promise<T>;
  }
  return undefined as T;
}
