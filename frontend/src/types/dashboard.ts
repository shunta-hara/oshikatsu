import { z } from "zod";

/** ダッシュボードカード内の軽量ニュースサマリー */
export const newsItemSummarySchema = z.object({
  id: z.number().int(),
  title: z.string(),
  sourceUrl: z.string().url(),
  collectedAt: z.string().datetime({ offset: true }),
});
export type NewsItemSummary = z.infer<typeof newsItemSummarySchema>;

/** ダッシュボードカード内の軽量出演番組サマリー */
export const programAppearanceSummarySchema = z.object({
  id: z.number().int(),
  programName: z.string(),
  broadcaster: z.string().nullable(),
  sourceUrl: z.string().url(),
  collectedAt: z.string().datetime({ offset: true }),
});
export type ProgramAppearanceSummary = z.infer<
  typeof programAppearanceSummarySchema
>;

/** 推し1人分のダッシュボードカード（推し名・最新ニュース・最新番組） */
export const oshiDashboardCardSchema = z.object({
  oshiId: z.number().int(),
  oshiName: z.string(),
  latestNews: newsItemSummarySchema.nullable(),
  latestProgram: programAppearanceSummarySchema.nullable(),
});
export type OshiDashboardCard = z.infer<typeof oshiDashboardCardSchema>;

/** GET /api/v1/dashboard/summary のレスポンス */
export const dashboardSummarySchema = z.array(oshiDashboardCardSchema);
export type DashboardSummary = z.infer<typeof dashboardSummarySchema>;
