// Tele-room — LiveKit-based video consult.
// Connects with a server-minted JWT, publishes camera + mic, renders the remote
// participant in the main stage, and wires up the toolbar (mute/cam/screen/hangup).
(function () {
  "use strict";

  const stage = document.getElementById("th-tele-stage");
  if (!stage) return;
  if (
    typeof window.LivekitClient === "undefined" &&
    typeof window.LiveKitClient === "undefined"
  ) {
    console.error("LiveKit client SDK not loaded.");
    return;
  }
  const LK = window.LivekitClient || window.LiveKitClient;

  const url = stage.dataset.livekitUrl;
  const token = stage.dataset.livekitToken;
  const afterCallUrl = stage.dataset.afterCallUrl || "/";
  const mode = stage.dataset.mode || "video"; // "video" | "audio" | "chat"
  const confirmUrl = stage.dataset.confirmUrl || null;
  const confirmCsrf = stage.dataset.csrf || "";
  const displayName = stage.dataset.displayName || "Participant";

  const remoteContainer = document.getElementById("th-tele-remote");
  const remotePlaceholder = document.getElementById(
    "th-tele-remote-placeholder",
  );
  const selfWrap = document.getElementById("th-tele-self");
  const selfVideo = document.getElementById("th-tele-self-video");
  const status = document.getElementById("th-tele-status");
  const micBtn = document.getElementById("th-tele-mic");
  const camBtn = document.getElementById("th-tele-cam");
  const screenBtn = document.getElementById("th-tele-screen");
  const hangupBtn = document.getElementById("th-tele-hangup");

  let micOn = true;
  let camOn = true;
  let screenOn = false;

  function setStatus(text) {
    if (status) status.textContent = text;
  }

  // Map<trackSid, HTMLMediaElement> — lets us remove only the unsubscribed track's element
  // and keeps the camera tile alive when a screen share is published alongside it.
  const trackElements = new Map();

  function showPlaceholder(visible) {
    if (!remotePlaceholder) return;
    remotePlaceholder.style.display = visible ? "" : "none";
  }

  function attachRemoteTrack(track) {
    const el = track.attach();
    el.style.width = "100%";
    el.style.height = "100%";
    el.style.objectFit = "contain";
    if (track.kind === "video") {
      el.classList.add("th-tele-remote-video");
      el.dataset.source = track.source || "unknown";
    }
    trackElements.set(track.sid, el);
    remoteContainer.appendChild(el);
    if (track.kind === "video") showPlaceholder(false);
  }

  function detachRemoteTrack(track) {
    const el = trackElements.get(track.sid);
    track.detach().forEach((node) => node.remove());
    if (el) {
      el.remove();
      trackElements.delete(track.sid);
    }
    if (!remoteContainer.querySelector("video")) showPlaceholder(true);
  }

  if (!url || !token) {
    setStatus("Missing token");
    return;
  }

  const room = new LK.Room({
    adaptiveStream: true,
    dynacast: true,
    videoCaptureDefaults: { resolution: LK.VideoPresets.h540.resolution },
  });

  function refreshStatus() {
    setStatus(
      room.remoteParticipants.size > 0 ? "In call" : "Waiting for other side…",
    );
  }

  const qualityWidget = document.getElementById("th-tele-quality");
  function applyQuality(q) {
    if (!qualityWidget) return;
    qualityWidget.classList.remove("d-none", "q-excellent", "q-good", "q-poor");
    const map = {
      [LK.ConnectionQuality.Excellent]: {
        cls: "q-excellent",
        label: "Excellent",
      },
      [LK.ConnectionQuality.Good]: { cls: "q-good", label: "Good" },
      [LK.ConnectionQuality.Poor]: { cls: "q-poor", label: "Poor" },
    };
    const meta = map[q] || { cls: "q-good", label: "Connected" };
    qualityWidget.classList.add(meta.cls);
    qualityWidget.querySelector(".label").textContent = meta.label;
  }

  room
    .on(LK.RoomEvent.TrackSubscribed, (track, _publication, _participant) => {
      attachRemoteTrack(track);
      refreshStatus();
    })
    .on(LK.RoomEvent.TrackUnsubscribed, (track) => detachRemoteTrack(track))
    .on(LK.RoomEvent.ParticipantConnected, () => refreshStatus())
    .on(LK.RoomEvent.ParticipantDisconnected, () => {
      refreshStatus();
      if (room.remoteParticipants.size === 0) showPlaceholder(true);
    })
    .on(LK.RoomEvent.ConnectionQualityChanged, (quality, participant) => {
      // Only surface the local participant's quality — that's what the user can act on.
      if (
        participant &&
        participant.identity === room.localParticipant?.identity
      )
        applyQuality(quality);
    })
    .on(LK.RoomEvent.Disconnected, () => setStatus("Disconnected"))
    .on(LK.RoomEvent.Reconnecting, () => setStatus("Reconnecting…"))
    .on(LK.RoomEvent.Reconnected, () => setStatus("Reconnected"));

  async function confirmJoinedOnServer() {
    if (!confirmUrl) return;
    try {
      await fetch(confirmUrl, {
        method: "POST",
        credentials: "same-origin",
        headers: { "X-CSRF-Token": confirmCsrf },
      });
    } catch (e) {
      console.error("ConfirmJoin failed:", e);
    }
  }

  async function start() {
    setStatus("Connecting to " + url + "…");
    try {
      await room.connect(url, token);
    } catch (err) {
      console.error("LiveKit connect failed:", err);
      const msg =
        err && (err.message || err.reason)
          ? err.message || err.reason
          : String(err);
      setStatus("Failed: " + msg);
      return;
    }
    // Only once we're really in the room do we tell the server "joined" — earlier we marked
    // the session InCall as soon as the clinician clicked Join, even if they cancelled the
    // consent or never connected.
    await confirmJoinedOnServer();
    refreshStatus();

    if (mode === "chat") {
      // Chat mode publishes nothing — neither camera nor mic. Just data channels.
      initChat();
      return;
    }

    try {
      if (mode === "audio") {
        await room.localParticipant.setMicrophoneEnabled(true);
        await room.localParticipant.setCameraEnabled(false);
      } else {
        await room.localParticipant.enableCameraAndMicrophone();
      }
    } catch (err) {
      console.error("Could not access camera/mic:", err);
      setStatus("Camera/mic blocked");
    }

    const camPub = room.localParticipant.getTrackPublication(
      LK.Track.Source.Camera,
    );
    if (mode !== "audio" && camPub && camPub.track) {
      const el = camPub.track.attach();
      selfVideo.replaceWith(el);
      el.id = "th-tele-self-video";
      el.autoplay = true;
      el.playsInline = true;
      el.muted = true;
      selfWrap.classList.remove("d-none");
    }

    // If a remote participant was already in the room, attach their tracks
    room.remoteParticipants.forEach((p) => {
      p.trackPublications.forEach((pub) => {
        if (pub.track) attachRemoteTrack(pub.track);
      });
    });
  }

  // ─── Chat mode (data channels) ─────────────────────────────────────────
  function initChat() {
    const list = document.getElementById("th-tele-chat-list");
    const form = document.getElementById("th-tele-chat-form");
    const input = document.getElementById("th-tele-chat-input");
    if (!list || !form || !input) return;

    const decoder = new TextDecoder();
    const encoder = new TextEncoder();

    function escapeHtml(s) {
      return (s || "")
        .toString()
        .replace(
          /[&<>"']/g,
          (c) =>
            ({
              "&": "&amp;",
              "<": "&lt;",
              ">": "&gt;",
              '"': "&quot;",
              "'": "&#39;",
            })[c],
        );
    }
    function appendMsg({ self, who, text, system }) {
      const el = document.createElement("div");
      el.className = system ? "system" : self ? "msg me" : "msg";
      el.innerHTML = system
        ? escapeHtml(text)
        : `<div class="who">${escapeHtml(who)}</div>${escapeHtml(text)}`;
      list.appendChild(el);
      list.scrollTop = list.scrollHeight;
    }

    appendMsg({
      system: true,
      text: "Chat started — messages are end-to-end encrypted and not stored on the server.",
    });

    room.on(LK.RoomEvent.DataReceived, (payload, participant) => {
      try {
        const txt = decoder.decode(payload);
        appendMsg({
          self: false,
          who: participant?.identity || "Other",
          text: txt,
        });
      } catch (_e) {
        /* malformed payload — ignore */
      }
    });

    form.addEventListener("submit", async (e) => {
      e.preventDefault();
      const txt = input.value.trim();
      if (!txt) return;
      try {
        await room.localParticipant.publishData(encoder.encode(txt), {
          reliable: true,
        });
        appendMsg({ self: true, who: displayName, text: txt });
        input.value = "";
      } catch (err) {
        console.error("Chat send failed:", err);
      }
    });

    // Pop the placeholder out of the way for chat mode
    if (remotePlaceholder) remotePlaceholder.style.display = "none";
  }

  micBtn?.addEventListener("click", async () => {
    micOn = !micOn;
    await room.localParticipant.setMicrophoneEnabled(micOn);
    micBtn.querySelector("i").className = micOn
      ? "bi bi-mic-fill"
      : "bi bi-mic-mute-fill";
    micBtn.classList.toggle("btn-light", micOn);
    micBtn.classList.toggle("btn-warning", !micOn);
  });

  camBtn?.addEventListener("click", async () => {
    camOn = !camOn;
    await room.localParticipant.setCameraEnabled(camOn);
    camBtn.querySelector("i").className = camOn
      ? "bi bi-camera-video-fill"
      : "bi bi-camera-video-off-fill";
    camBtn.classList.toggle("btn-light", camOn);
    camBtn.classList.toggle("btn-warning", !camOn);
    selfWrap.classList.toggle("d-none", !camOn);
  });

  screenBtn?.addEventListener("click", async () => {
    try {
      screenOn = !screenOn;
      await room.localParticipant.setScreenShareEnabled(screenOn);
      screenBtn.classList.toggle("btn-light", !screenOn);
      screenBtn.classList.toggle("btn-success", screenOn);
    } catch (err) {
      console.error("Screen share failed:", err);
      screenOn = false;
    }
  });

  hangupBtn?.addEventListener("click", async () => {
    try {
      await room.disconnect();
    } catch {
      /* ignore */
    }
    window.location.href = afterCallUrl;
  });

  window.addEventListener("beforeunload", () => {
    try {
      room.disconnect();
    } catch {
      /* ignore */
    }
  });

  // Block start() until the user has acknowledged the consent overlay. SessionStorage remembers the
  // ack within the tab so a page refresh during a call doesn't prompt again.
  const consent = document.getElementById("th-consent-overlay");
  const consentCheck = document.getElementById("th-consent-check");
  const consentAccept = document.getElementById("th-consent-accept");
  const consentKey = `th-tele-consent-${stage.dataset.livekitRoom || "x"}`;

  function dismissOverlay() {
    if (consent) consent.style.display = "none";
  }

  if (!consent || sessionStorage.getItem(consentKey) === "yes") {
    dismissOverlay();
    start();
  } else {
    if (consentCheck && consentAccept) {
      consentCheck.addEventListener("change", () => {
        consentAccept.disabled = !consentCheck.checked;
      });
      consentAccept.addEventListener("click", () => {
        sessionStorage.setItem(consentKey, "yes");
        dismissOverlay();
        start();
      });
    } else {
      // Defensive: if the overlay is malformed, fall through and start so the call still works.
      dismissOverlay();
      start();
    }
  }
})();
