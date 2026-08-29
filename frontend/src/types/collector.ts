import { z } from "zod";

/** GET /api/v1/collector/status の単一ジョブログエントリ */
export const jobLogResponseSchema = z.object({
  id: z.number().int(),
  jobType: z.string(),
  executedAt: z.string().datetime({ offset: true }),
  successCount: z.number().int(),
  failureCount: z.number().int(),
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
