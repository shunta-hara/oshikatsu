import { apiFetch } from "./client";
import { authResponseSchema, type AuthResponse } from "../types/auth";

/** 新規ユーザー登録 */
export async function register(
  email: string,
  password: string,
): Promise<AuthResponse> {
  const raw = await apiFetch<unknown>("/auth/register", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  return authResponseSchema.parse(raw);
}

/** ログイン */
export async function login(
  email: string,
  password: string,
): Promise<AuthResponse> {
  const raw = await apiFetch<unknown>("/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  return authResponseSchema.parse(raw);
}

/** ログアウト（現在のトークンを無効化） */
export async function logout(): Promise<void> {
  await apiFetch<unknown>("/auth/logout", {
    method: "POST",
  });
}
