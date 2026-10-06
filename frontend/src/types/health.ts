import { z } from "zod";

/** GET /api/v1/health のレスポンス（FEAT-053）*/
export const healthResponseSchema = z.object({
  status: z.string(),
  timestamp: z.string(),
  dbSizeBytes: z.number().int().nullable().optional(),
  dbSizeMb: z.number().nullable().optional(),
});

export type HealthResponse = z.infer<typeof healthResponseSchema>;
