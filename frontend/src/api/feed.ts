import { apiFetch } from "./client";
import {
  newsPaginatedSchema,
  programsPaginatedSchema,
  unreadCountSchema,
  type NewsPaginatedResponse,
  type ProgramsPaginatedResponse,
} from "../types/feed";

/** ログインユーザーの推し全員分（または特定の推し）のニュース一覧を取得する（収集日時の新着順） */
export async function getNews(
  page: number = 1,
  pageSize: number = 20,
  oshiId?: number,
  query?: string,
): Promise<NewsPaginatedResponse> {
  let url = `/news?page=${page}&pageSize=${pageSize}`;
  if (oshiId !== undefined) {
    url += `&oshiId=${oshiId}`;
  }
  if (query !== undefined && query !== "") {
    url += `&q=${encodeURIComponent(query)}`;
  }
  const raw = await apiFetch<unknown>(url);
  return newsPaginatedSchema.parse(raw);
}

/** ログインユーザーの推し全員分（または特定の推し）の出演番組一覧を取得する（収集日時の新着順） */
export async function getPrograms(
  page: number = 1,
  pageSize: number = 20,
  oshiId?: number,
): Promise<ProgramsPaginatedResponse> {
  let url = `/programs?page=${page}&pageSize=${pageSize}`;
  if (oshiId !== undefined) {
    url += `&oshiId=${oshiId}`;
  }
  const raw = await apiFetch<unknown>(url);
  return programsPaginatedSchema.parse(raw);
}

/** 前回閲覧以降に追加された未読件数（ニュース＋出演番組）を取得する */
export async function getUnreadCount(): Promise<number> {
  const raw = await apiFetch<unknown>("/feed/unread-count");
  const parsed = unreadCountSchema.parse(raw);
  return parsed.unreadCount;
}

/** フィード閲覧日時を現在日時に更新し、未読件数をリセットする */
export async function markAsRead(): Promise<void> {
  await apiFetch<void>("/feed/mark-as-read", { method: "POST" });
}
