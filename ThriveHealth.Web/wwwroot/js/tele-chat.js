// Persistent chat-thread client.
// Real-time delivery via SignalR (/hubs/telechat) with HTTP polling as a fallback if the socket
// drops. Drives both the patient Portal/Chat view and the clinician Telemedicine/Chat view.
(function () {
  "use strict";

  const root = document.getElementById("th-chat-shell");
  if (!root) return;

  const messagesUrl = root.dataset.messagesUrl;
  const sendUrl = root.dataset.sendUrl;
  const uploadUrl = root.dataset.uploadUrl;
  const csrf = root.dataset.csrf || "";
  const selfRole = root.dataset.selfRole || "Patient"; // "Patient" or "Clinician"
  const patientId = parseInt(root.dataset.patientId || "0", 10);
  const list = document.getElementById("th-chat-list");
  const form = document.getElementById("th-chat-form");
  const input = document.getElementById("th-chat-input");
  const fileInput = document.getElementById("th-chat-file");
  const status = document.getElementById("th-chat-status");
  const baseTitle = document.title;

  let lastId = parseInt(root.dataset.initialLastId || "0", 10);
  let polling = false;
  let unread = 0;

  function escapeHtml(s) {
    return (s || "").toString().replace(
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
  function fmtTime(d) {
    return new Date(d).toLocaleTimeString([], {
      hour: "2-digit",
      minute: "2-digit",
    });
  }
  function setStatus(text, tone) {
    if (!status) return;
    status.textContent = text;
    status.className = `small ${tone || "text-muted"}`;
  }

  function flashTitle() {
    if (document.visibilityState === "visible") return;
    unread += 1;
    document.title = `(${unread}) ${baseTitle}`;
  }
  function clearUnread() {
    unread = 0;
    document.title = baseTitle;
  }
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") clearUnread();
  });

  function notifyDesktop(m) {
    if (document.visibilityState === "visible") return;
    if (
      typeof Notification === "undefined" ||
      Notification.permission !== "granted"
    )
      return;
    const title = m.who || "New message";
    const body = (m.body || "").slice(0, 120);
    const n = new Notification(title, {
      body,
      tag: `tele-${m.id}`,
      renotify: false,
    });
    n.onclick = () => {
      window.focus();
      n.close();
    };
    setTimeout(() => n.close(), 6000);
  }
  if (
    typeof Notification !== "undefined" &&
    Notification.permission === "default"
  ) {
    setTimeout(() => Notification.requestPermission().catch(() => {}), 1500);
  }

  function attachmentHtml(att) {
    const isImage = (att.contentType || "").startsWith("image/");
    if (isImage) {
      return `<a href="${escapeHtml(att.url)}" target="_blank"><img src="${escapeHtml(att.url)}" alt="${escapeHtml(att.fileName)}" style="max-width:240px;max-height:240px;border-radius:8px;display:block;margin-top:6px;" /></a>`;
    }
    const sizeKb = Math.max(1, Math.round((att.sizeBytes || 0) / 1024));
    return `<a class="th-chat-att" href="${escapeHtml(att.url)}" target="_blank" style="display:inline-flex;gap:6px;align-items:center;margin-top:6px;background:rgba(0,0,0,.08);padding:6px 10px;border-radius:8px;color:inherit;text-decoration:none;"><i class="bi bi-file-earmark-pdf"></i>${escapeHtml(att.fileName)} <span style="opacity:.6;">(${sizeKb} KB)</span></a>`;
  }

  function quotedHtml(reply) {
    if (!reply) return "";
    return `<div class="th-chat-quote" data-jump="${reply.id}"><div class="quote-who">${escapeHtml(reply.who || "Someone")}</div><div class="quote-snippet">${escapeHtml(reply.snippet || "")}</div></div>`;
  }

  function appendMessage(m) {
    if (m.id <= lastId) return;
    lastId = m.id;
    const isMine = m.role === selfRole;
    const isSystem = m.role === "System";
    const el = document.createElement("div");
    el.className = `th-chat-msg ${isSystem ? "system" : isMine ? "me" : "them"}`;
    el.dataset.id = m.id;
    const atts = (m.attachments || []).map(attachmentHtml).join("");
    const quote = quotedHtml(m.replyTo);
    // Read-receipt: only meaningful on outgoing messages. Single tick = sent, double tick = read.
    const tick =
      isMine && !isSystem
        ? m.readByOther
          ? '<span class="read-tick read" title="Read">✓✓</span>'
          : '<span class="read-tick" title="Sent">✓</span>'
        : "";
    const replyBtn = isSystem
      ? ""
      : `<button type="button" class="th-chat-reply-btn" data-reply-id="${m.id}" data-reply-who="${escapeHtml(m.who || "")}" data-reply-snippet="${escapeHtml(m.body || "")}" title="Reply"><i class="bi bi-reply"></i></button>`;
    el.innerHTML = isSystem
      ? `<div class="body">${escapeHtml(m.body)}</div>`
      : `${quote}<div class="who">${escapeHtml(m.who)}</div><div class="body">${escapeHtml(m.body)}</div>${atts}<div class="time">${fmtTime(m.sentAt)} ${tick}</div>${replyBtn}`;

    const empty = list.querySelector(".th-chat-empty");
    if (empty) empty.remove();
    list.appendChild(el);
    if (!isMine && !isSystem) {
      flashTitle();
      notifyDesktop(m);
    }
  }

  function scrollToBottom() {
    list.scrollTop = list.scrollHeight;
  }

  async function poll() {
    if (polling) return;
    polling = true;
    try {
      const url = lastId > 0 ? `${messagesUrl}?sinceId=${lastId}` : messagesUrl;
      const res = await fetch(url, { credentials: "same-origin" });
      if (!res.ok) {
        setStatus("Connection issue — retrying", "text-warning");
        return;
      }
      const body = await res.json();
      const msgs = body.messages || [];
      let hadNew = false;
      for (const m of msgs) {
        if (m.id <= lastId) continue;
        appendMessage(m);
        hadNew = true;
      }
      if (hadNew) scrollToBottom();
      setStatus(
        connection?.state === "Connected" ? "Live" : "Connected",
        "text-success",
      );
    } catch (e) {
      console.error("Chat poll failed:", e);
      setStatus("Offline", "text-danger");
    } finally {
      polling = false;
    }
  }

  async function send(text) {
    if (!text.trim()) return false;
    setStatus("Sending…", "text-muted");
    const body = { Body: text };
    if (replyState.id) body.RepliesToMessageId = replyState.id;
    try {
      const res = await fetch(sendUrl, {
        method: "POST",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json", "X-CSRF-Token": csrf },
        body: JSON.stringify(body),
      });
      if (!res.ok) {
        let err = "Send failed";
        try {
          const b = await res.json();
          err = b.error || err;
        } catch (_e) {
          /* ignore */
        }
        setStatus(err, "text-danger");
        return false;
      }
      setStatus("Sent", "text-success");
      return true;
    } catch (e) {
      console.error("Chat send failed:", e);
      setStatus("Offline — message not sent", "text-danger");
      return false;
    }
  }

  async function uploadFile(file) {
    if (!uploadUrl) return false;
    if (!file) return false;
    if (file.size > 5 * 1024 * 1024) {
      setStatus("File is larger than 5 MB", "text-danger");
      return false;
    }
    setStatus(`Uploading ${file.name}…`, "text-muted");
    const fd = new FormData();
    fd.append("file", file);
    fd.append("__RequestVerificationToken", csrf);
    try {
      const res = await fetch(uploadUrl, {
        method: "POST",
        credentials: "same-origin",
        headers: { "X-CSRF-Token": csrf },
        body: fd,
      });
      if (!res.ok) {
        let err = "Upload failed";
        try {
          const b = await res.json();
          err = b.error || err;
        } catch (_e) {
          /* ignore */
        }
        setStatus(err, "text-danger");
        return false;
      }
      setStatus("Uploaded", "text-success");
      return true;
    } catch (e) {
      console.error("Upload failed:", e);
      setStatus("Upload failed", "text-danger");
      return false;
    }
  }

  form?.addEventListener("submit", async (e) => {
    e.preventDefault();
    const text = input?.value || "";
    const ok = await send(text);
    if (ok) {
      input.value = "";
      input.focus();
      clearReply();
      setTimeout(poll, 100);
    }
  });

  fileInput?.addEventListener("change", async (e) => {
    const f = e.target.files?.[0];
    if (!f) return;
    await uploadFile(f);
    e.target.value = ""; // reset so the same file can be picked again
    setTimeout(poll, 100);
  });

  // Plain Enter sends; Shift+Enter inserts a newline (matches WhatsApp / Slack default).
  input?.addEventListener("keydown", (e) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      form.requestSubmit();
    }
  });

  // ─── Reply-to wiring ───────────────────────────────────────────────────
  const replyState = { id: null };
  const replyPreview = document.getElementById("th-chat-reply-preview");
  const replyPreviewWho = document.getElementById("th-chat-reply-preview-who");
  const replyPreviewSnippet = document.getElementById(
    "th-chat-reply-preview-snippet",
  );
  const replyClearBtn = document.getElementById("th-chat-reply-clear");

  function setReply(id, who, snippet) {
    replyState.id = parseInt(id, 10);
    if (replyPreview) {
      if (replyPreviewWho) replyPreviewWho.textContent = who || "Replying…";
      if (replyPreviewSnippet) replyPreviewSnippet.textContent = snippet || "";
      replyPreview.classList.remove("d-none");
    }
    input?.focus();
  }
  function clearReply() {
    replyState.id = null;
    replyPreview?.classList.add("d-none");
  }
  replyClearBtn?.addEventListener("click", clearReply);

  list.addEventListener("click", (e) => {
    const replyBtn = e.target.closest(".th-chat-reply-btn");
    if (replyBtn) {
      setReply(
        replyBtn.dataset.replyId,
        replyBtn.dataset.replyWho,
        replyBtn.dataset.replySnippet,
      );
      return;
    }
    // Click a quoted block to jump to the original
    const quote = e.target.closest(".th-chat-quote");
    if (quote && quote.dataset.jump) {
      const target = list.querySelector(
        `.th-chat-msg[data-id="${quote.dataset.jump}"]`,
      );
      if (target) {
        target.scrollIntoView({ behavior: "smooth", block: "center" });
        target.classList.add("flash");
        setTimeout(() => target.classList.remove("flash"), 1200);
      }
    }
  });

  // ─── SignalR live channel ──────────────────────────────────────────────
  let connection = null;
  if (window.signalR && patientId) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/telechat")
      .withAutomaticReconnect()
      .build();
    connection.on("messageReceived", (m) => {
      if (m.patientId !== patientId) return;
      appendMessage(m);
      scrollToBottom();
    });
    connection.on("messagesRead", (payload) => {
      if (payload.patientId !== patientId) return;
      // The OTHER party just read our messages — flip our outbound bubbles' tick to ✓✓.
      const reader = payload.reader; // "Patient" or "Clinician"
      // Ours = messages we sent. If reader === Clinician, that's the patient seeing read receipts.
      // If reader === Patient, that's the clinician seeing read receipts.
      const ourRoleSent = reader === "Clinician" ? "Patient" : "Clinician";
      if (ourRoleSent !== selfRole) return;
      for (const id of payload.messageIds || []) {
        const el = list.querySelector(
          `.th-chat-msg[data-id="${id}"] .read-tick`,
        );
        if (el) {
          el.textContent = "✓✓";
          el.classList.add("read");
          el.title = "Read";
        }
      }
    });
    connection.onreconnecting(() => setStatus("Reconnecting…", "text-warning"));
    connection.onreconnected(() => setStatus("Live", "text-success"));
    connection.onclose(() =>
      setStatus("Offline — falling back to polling", "text-warning"),
    );
    connection
      .start()
      .then(() => connection.invoke("SubscribePatient", patientId))
      .then(() => setStatus("Live", "text-success"))
      .catch((err) =>
        console.warn("SignalR start failed; using polling only:", err),
      );
  }

  scrollToBottom();
  poll();
  // Polling kept as a fallback at a relaxed interval. SignalR delivers in-tab; polling catches any
  // missed events while the socket was reconnecting.
  setInterval(poll, 8000);
})();
