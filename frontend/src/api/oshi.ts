import { apiFetch } from "./client";
import {
  oshiListResponseSchema,
  oshiResponseSchema,
  type OshiListResponse,
  type OshiResponse,
} from "../types/oshi";

/** ログインユーザーの推し一覧を取得する */
export async function getOshis(): Promise<OshiListResponse> {
  const raw = await apiFetch<unknown>("/oshis");
  return oshiListResponseSchema.parse(raw);
}

/** 推しを新規登録する */
export async function createOshi(name: string): Promise<OshiResponse> {
  const raw = await apiFetch<unknown>("/oshis", {
    method: "POST",
    body: JSON.stringify({ name }),
  });
  return oshiResponseSchema.parse(raw);
}

/** 指定した推しを削除する */
export async function deleteOshi(id: number): Promise<void> {
  await apiFetch<void>(`/oshis/${id}`, {
    method: "DELETE",
  });
}
