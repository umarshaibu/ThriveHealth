// Browser-side Web Push registration. Reads the server's VAPID public key from the role-specific
// PushKey endpoint, requests Notification permission lazily (after a real user gesture would be
// nicer, but we deliberately do it on first chat-page load while the user is engaged), and
// subscribes the browser via the PushManager.
//
// Pages opt in by including this script and exposing a meta tag:
//   <meta name="th-push-key-url" content="/Portal/PushKey" />
//   <meta name="th-push-subscribe-url" content="/Portal/PushSubscribe" />
//   <meta name="th-csrf" content="@csrf" />
(function () {
  "use strict";

  if (!("serviceWorker" in navigator) || !("PushManager" in window)) return;

  const keyMeta = document.querySelector('meta[name="th-push-key-url"]');
  const subMeta = document.querySelector('meta[name="th-push-subscribe-url"]');
  const csrfMeta = document.querySelector('meta[name="th-csrf"]');
  if (!keyMeta || !subMeta) return;

  const keyUrl = keyMeta.content;
  const subscribeUrl = subMeta.content;
  const csrf = csrfMeta?.content || "";

  function urlBase64ToUint8Array(s) {
    const padding = "=".repeat((4 - (s.length % 4)) % 4);
    const b64 = (s + padding).replace(/-/g, "+").replace(/_/g, "/");
    const raw = atob(b64);
    const out = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
    return out;
  }

  async function arrayBufferB64Url(buf) {
    const bytes = new Uint8Array(buf);
    let bin = "";
    for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
    return btoa(bin).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
  }

  async function start() {
    try {
      const reg = await navigator.serviceWorker.register("/sw.js");
      const keyResp = await fetch(keyUrl, { credentials: "same-origin" });
      if (!keyResp.ok) return;
      const { publicKey, configured } = await keyResp.json();
      if (!configured || !publicKey) return;

      // Already subscribed? Re-send the descriptor so the server knows the latest endpoint
      // (PushManager URLs can rotate). Otherwise prompt for permission and subscribe.
      let sub = await reg.pushManager.getSubscription();
      if (!sub) {
        if (Notification.permission === "denied") return;
        if (Notification.permission === "default") {
          const perm = await Notification.requestPermission();
          if (perm !== "granted") return;
        }
        sub = await reg.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: urlBase64ToUint8Array(publicKey),
        });
      }
      const json = sub.toJSON();
      const body = {
        Endpoint: json.endpoint,
        P256dh: json.keys?.p256dh || (await arrayBufferB64Url(sub.getKey("p256dh"))),
        Auth: json.keys?.auth || (await arrayBufferB64Url(sub.getKey("auth"))),
      };
      await fetch(subscribeUrl, {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-CSRF-Token": csrf },
        body: JSON.stringify(body),
      });
    } catch (e) {
      console.warn("Web Push setup failed:", e);
    }
  }

  // Defer slightly so it doesn't compete with first-paint resources.
  setTimeout(start, 1500);
})();
