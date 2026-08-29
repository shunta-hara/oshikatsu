import { z } from "zod";

/** POST /api/v1/auth/register および login の成功レスポンス */
export const authResponseSchema = z.object({
  token: z.string().min(1),
  expiresAt: z.string().datetime({ offset: true }),
});

export type AuthResponse = z.infer<typeof authResponseSchema>;

/** エラーレスポンス（backend の MessageResponseDto） */
export const messageResponseSchema = z.object({
  message: z.string(),
});

export type MessageResponse = z.infer<typeof messageResponseSchema>;

/** ローカルストレージに保存する認証情報 */
export interface StoredAuth {
  token: string;
  expiresAt: string;
}
