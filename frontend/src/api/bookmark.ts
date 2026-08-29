import { apiFetch } from "./client";
import {
  bookmarksPaginatedSchema,
  type BookmarksPaginatedResponse,
} from "../types/bookmark";

/** 指定したニュース記事をブックマークに登録する */
export async function addBookmark(newsItemId: number): Promise<void> {
  await apiFetch<void>(`/news/${newsItemId}/bookmark`, { method: "POST" });
}

/** 指定したニュース記事のブックマークを解除する */
export async function removeBookmark(newsItemId: number): Promise<void> {
  await apiFetch<void>(`/news/${newsItemId}/bookmark`, { method: "DELETE" });
}

/** ログインユーザーのブックマーク一覧を取得する（登録日時の新しい順） */
export async function getBookmarks(
  page: number = 1,
  pageSize: number = 20,
): Promise<BookmarksPaginatedResponse> {
  const url = `/bookmarks?page=${page}&pageSize=${pageSize}`;
  const raw = await apiFetch<unknown>(url);
  return bookmarksPaginatedSchema.parse(raw);
}
