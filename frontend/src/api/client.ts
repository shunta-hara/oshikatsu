/** API 呼び出し基盤: JSON fetch ラッパー */

const API_BASE = "/api/v1";

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ApiError";
  }
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

  const response = await fetch(`${API_BASE}${path}`, {
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
