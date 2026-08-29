import { apiFetch } from "./client";
import {
  newsPaginatedSchema,
  programsPaginatedSchema,
  type NewsPaginatedResponse,
  type ProgramsPaginatedResponse,
} from "../types/feed";

/** ログインユーザーの推し全員分（または特定の推し）のニュース一覧を取得する（収集日時の新着順） */
export async function getNews(
  page: number = 1,
  pageSize: number = 20,
  oshiId?: number,
): Promise<NewsPaginatedResponse> {
  let url = `/news?page=${page}&pageSize=${pageSize}`;
  if (oshiId !== undefined) {
    url += `&oshiId=${oshiId}`;
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
