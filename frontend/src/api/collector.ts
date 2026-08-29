import { apiFetch } from "./client";
import {
  collectorStatusResponseSchema,
  type CollectorStatusResponse,
} from "../types/collector";

/** 最新の収集ジョブ実行状況を取得する */
export async function getCollectorStatus(): Promise<CollectorStatusResponse> {
  const raw = await apiFetch<unknown>("/collector/status");
  return collectorStatusResponseSchema.parse(raw);
}
