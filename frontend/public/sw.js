/* Service Worker for Web Push notifications */

self.addEventListener("push", (event) => {
  let data = { title: "oshikatsu", body: "" };
  try {
    data = event.data ? event.data.json() : data;
  } catch {
    // JSON パース失敗は無視
  }

  const options = {
    body: data.body || "",
    icon: "/favicon.svg",
    badge: "/favicon.svg",
    data: data,
  };

  event.waitUntil(
    self.registration.showNotification(data.title || "oshikatsu", options),
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();

  event.waitUntil(
    clients
      .matchAll({ type: "window", includeUncontrolled: true })
      .then((clientList) => {
        for (const client of clientList) {
          if ("focus" in client) {
            return client.focus();
          }
        }
        if (clients.openWindow) {
          return clients.openWindow("/");
        }
      }),
  );
});
