import { apiFetch } from "./client";
import { healthResponseSchema, type HealthResponse } from "../types/health";

/** GET /api/v1/health — サービス稼働状態と DB 使用量を取得する（FEAT-053）*/
export async function getHealth(): Promise<HealthResponse> {
  const raw = await apiFetch<unknown>("/health");
  return healthResponseSchema.parse(raw);
}
