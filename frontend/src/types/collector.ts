import { z } from "zod";

/** GET /api/v1/collector/status の単一ジョブログエントリ */
export const jobLogResponseSchema = z.object({
  id: z.number().int(),
  jobType: z.string(),
  executedAt: z.string().datetime({ offset: true }),
  successCount: z.number().int(),
  failureCount: z.number().int(),
  failureReason: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  talentId: z.number().int().nullable().optional(),
  createdAt: z.string().datetime({ offset: true }),
});

export type JobLogResponse = z.infer<typeof jobLogResponseSchema>;

/** GET /api/v1/collector/status のレスポンス */
export const collectorStatusResponseSchema = z.object({
  latestNewsJob: jobLogResponseSchema.nullable(),
  latestProgramJob: jobLogResponseSchema.nullable(),
});

export type CollectorStatusResponse = z.infer<
  typeof collectorStatusResponseSchema
>;

/** 連続失敗中のタレント情報（GET /api/v1/collector/operations） */
export const failingTalentStatusSchema = z.object({
  talentId: z.number().int(),
  talentName: z.string(),
  consecutiveFailureCount: z.number().int(),
});

export type FailingTalentStatus = z.infer<typeof failingTalentStatusSchema>;

/** GET /api/v1/collector/operations のレスポンス */
export const collectorOperationsResponseSchema = z.object({
  recentJobs: z.array(jobLogResponseSchema),
  isOverallJobFailing: z.boolean(),
  failingTalents: z.array(failingTalentStatusSchema),
});

export type CollectorOperationsResponse = z.infer<
  typeof collectorOperationsResponseSchema
>;
