import { z } from "zod";

/** GET /api/v1/push/vapid-public-key のレスポンス */
export const vapidPublicKeyResponseSchema = z.object({
  publicKey: z.string().min(1),
});
export type VapidPublicKeyResponse = z.infer<
  typeof vapidPublicKeyResponseSchema
>;

/** POST /api/v1/push/subscriptions のリクエストボディ */
export const pushSubscriptionRequestSchema = z.object({
  endpoint: z.string().url(),
  p256dh: z.string().min(1),
  auth: z.string().min(1),
});
export type PushSubscriptionRequest = z.infer<
  typeof pushSubscriptionRequestSchema
>;

/** DELETE /api/v1/push/subscriptions のリクエストボディ */
export const deletePushSubscriptionRequestSchema = z.object({
  endpoint: z.string().url(),
});
export type DeletePushSubscriptionRequest = z.infer<
  typeof deletePushSubscriptionRequestSchema
>;
