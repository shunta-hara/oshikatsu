import { apiFetch } from "./client";
import {
  dashboardSummarySchema,
  type DashboardSummary,
} from "../types/dashboard";

/**
 * ログインユーザーの推し全員分のダッシュボードカードを取得する。
 * 各カードに推し名・最新ニュース1件・最新出演番組1件が含まれる（データなし時は null）。
 */
export async function getDashboardSummary(): Promise<DashboardSummary> {
  const raw = await apiFetch<unknown>("/dashboard/summary");
  return dashboardSummarySchema.parse(raw);
}
