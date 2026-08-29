import { z } from "zod";

/** GET /api/v1/oshis の単一レスポンス要素 */
export const oshiResponseSchema = z.object({
  id: z.number().int(),
  name: z.string(),
  createdAt: z.string().datetime({ offset: true }),
});

export type OshiResponse = z.infer<typeof oshiResponseSchema>;

/** GET /api/v1/oshis のレスポンス（配列） */
export const oshiListResponseSchema = z.array(oshiResponseSchema);

export type OshiListResponse = z.infer<typeof oshiListResponseSchema>;
