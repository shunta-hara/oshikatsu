import { apiFetch } from "./client";
import { vapidPublicKeyResponseSchema } from "../types/push";

/** VAPID 公開鍵を取得する（認証不要） */
export async function getVapidPublicKey(): Promise<string> {
  const raw = await apiFetch<unknown>("/push/vapid-public-key");
  const parsed = vapidPublicKeyResponseSchema.parse(raw);
  return parsed.publicKey;
}

/** Push 購読を backend に登録する（認証必須） */
export async function registerPushSubscription(
  endpoint: string,
  p256dh: string,
  auth: string,
): Promise<void> {
  await apiFetch<void>("/push/subscriptions", {
    method: "POST",
    body: JSON.stringify({ endpoint, p256dh, auth }),
  });
}

/** Push 購読を backend から削除する（認証必須） */
export async function deletePushSubscription(endpoint: string): Promise<void> {
  await apiFetch<void>("/push/subscriptions", {
    method: "DELETE",
    body: JSON.stringify({ endpoint }),
  });
}
