import { useEffect, useState } from "react";
import { ApiError } from "../../api/client";
import {
  deletePushSubscription,
  getVapidPublicKey,
  registerPushSubscription,
} from "../../api/push";

/**
 * URL-safe base64 文字列を Uint8Array に変換する。
 * VAPID の applicationServerKey 渡し用。
 */
function urlBase64ToUint8Array(base64String: string): Uint8Array<ArrayBuffer> {
  const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
  const rawData = window.atob(base64);
  const outputArray = new Uint8Array(rawData.length) as Uint8Array<ArrayBuffer>;
  for (let i = 0; i < rawData.length; i++) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

/**
 * ArrayBuffer を URL-safe base64 文字列（パディングなし）に変換する。
 * Push Subscription の p256dh / auth キー送信用。
 */
function arrayBufferToBase64Url(buffer: ArrayBuffer): string {
  const uint8Array = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < uint8Array.byteLength; i++) {
    binary += String.fromCharCode(uint8Array[i]);
  }
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=/g, "");
}

/** Push 通知がこのブラウザでサポートされているか確認する */
function isPushSupported(): boolean {
  return (
    "serviceWorker" in navigator &&
    "PushManager" in window &&
    "Notification" in window
  );
}

/**
 * Push 通知の購読・解除ボタン。
 * Push がサポートされていないブラウザでは何も描画しない。
 * 通知がブロックされている場合はブラウザ設定への案内メッセージを表示する。
 */
export function PushNotificationButton() {
  const [isSupported] = useState<boolean>(isPushSupported());
  const [permission, setPermission] = useState<NotificationPermission>(
    "Notification" in window
      ? (Notification.permission as NotificationPermission)
      : "default",
  );
  const [isSubscribed, setIsSubscribed] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // マウント時に既存の Push 購読を確認する
  useEffect(() => {
    if (!isSupported) return;

    void (async () => {
      try {
        const registration = await navigator.serviceWorker.ready;
        const existingSubscription =
          await registration.pushManager.getSubscription();
        setIsSubscribed(existingSubscription !== null);
      } catch {
        // 既存購読の確認に失敗した場合は購読なしとして扱う
      }
    })();
  }, [isSupported]);

  const handleSubscribe = async () => {
    setIsLoading(true);
    setError(null);
    try {
      // Service Worker を登録し準備完了まで待機する
      await navigator.serviceWorker.register("/sw.js");
      const registration = await navigator.serviceWorker.ready;

      // ブラウザの通知許可ダイアログを表示する
      const permissionResult = await Notification.requestPermission();
      setPermission(permissionResult);
      if (permissionResult !== "granted") {
        return;
      }

      // VAPID 公開鍵を取得して PushManager に購読を作成する
      const vapidPublicKey = await getVapidPublicKey();
      const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
      });

      // 購読キーを取得して backend に登録する
      const p256dhBuffer = subscription.getKey("p256dh");
      const authBuffer = subscription.getKey("auth");
      if (p256dhBuffer === null || authBuffer === null) {
        throw new Error("購読キーの取得に失敗しました");
      }
      const p256dh = arrayBufferToBase64Url(p256dhBuffer);
      const auth = arrayBufferToBase64Url(authBuffer);
      await registerPushSubscription(subscription.endpoint, p256dh, auth);
      setIsSubscribed(true);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("通知の購読に失敗しました");
      }
    } finally {
      setIsLoading(false);
    }
  };

  const handleUnsubscribe = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();
      if (subscription) {
        await deletePushSubscription(subscription.endpoint);
        await subscription.unsubscribe();
      }
      setIsSubscribed(false);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("通知の解除に失敗しました");
      }
    } finally {
      setIsLoading(false);
    }
  };

  // Push 通知非対応ブラウザでは何も表示しない
  if (!isSupported) {
    return null;
  }

  // 通知がブロックされている場合はブラウザ設定への案内を表示する
  if (permission === "denied") {
    return (
      <p className="push-notification__blocked" role="status">
        ブラウザ設定から許可してください
      </p>
    );
  }

  return (
    <div className="push-notification">
      {error !== null && (
        <div role="alert" aria-live="assertive" className="error-message">
          {error}
        </div>
      )}
      <button
        type="button"
        className="push-notification__button"
        onClick={() =>
          void (isSubscribed ? handleUnsubscribe() : handleSubscribe())
        }
        disabled={isLoading}
      >
        {isLoading
          ? "処理中..."
          : isSubscribed
            ? "通知を解除する"
            : "通知を受け取る"}
      </button>
    </div>
  );
}
