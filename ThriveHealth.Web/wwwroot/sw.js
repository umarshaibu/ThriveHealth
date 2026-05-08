// Service worker for ThriveHealth Web Push.
// Receives push events while the tab is closed and shows the OS notification. Click → focus or open.

self.addEventListener("install", (e) => {
  self.skipWaiting();
});

self.addEventListener("activate", (e) => {
  e.waitUntil(self.clients.claim());
});

self.addEventListener("push", (event) => {
  let payload = { title: "ThriveHealth", body: "New activity", url: "/", tag: "thrivehealth" };
  if (event.data) {
    try {
      payload = Object.assign(payload, event.data.json());
    } catch {
      payload.body = event.data.text();
    }
  }
  const options = {
    body: payload.body,
    tag: payload.tag,
    icon: "/icon-192.png",
    badge: "/icon-192.png",
    data: { url: payload.url || "/" },
    renotify: true,
  };
  event.waitUntil(self.registration.showNotification(payload.title, options));
});

self.addEventListener("notificationclick", (event) => {
  const url = event.notification.data?.url || "/";
  event.notification.close();
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then((clientsArr) => {
      for (const c of clientsArr) {
        // Re-use an existing tab if we already have one open.
        if ("focus" in c) {
          c.navigate(url);
          return c.focus();
        }
      }
      if (self.clients.openWindow) return self.clients.openWindow(url);
    }),
  );
});
