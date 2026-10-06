import { apiFetch } from "./client";
import {
  collectorStatusResponseSchema,
  collectorOperationsResponseSchema,
  type CollectorStatusResponse,
  type CollectorOperationsResponse,
} from "../types/collector";

/** 最新の収集ジョブ実行状況を取得する */
export async function getCollectorStatus(): Promise<CollectorStatusResponse> {
  const raw = await apiFetch<unknown>("/collector/status");
  return collectorStatusResponseSchema.parse(raw);
}

/** 運用パネル向けデータを取得する（直近24時間のジョブ履歴・失敗中タレント） */
export async function getCollectorOperations(): Promise<CollectorOperationsResponse> {
  const raw = await apiFetch<unknown>("/collector/operations");
  return collectorOperationsResponseSchema.parse(raw);
}
