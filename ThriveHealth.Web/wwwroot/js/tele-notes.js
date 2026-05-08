// Auto-saves the clinician's structured SOAP notes to /Telemedicine/SaveNotes/{id}.
// Debounces input (saves 1.5s after the last keystroke) and also flushes on tab hide / unload.
(function () {
  "use strict";

  const form = document.getElementById("th-notes-form");
  if (!form) return;
  const sessionId = form.dataset.sessionId;
  if (!sessionId) return;
  const csrf = form.dataset.csrf || "";
  const status = document.getElementById("th-notes-status");

  const fields = ["Subjective", "Objective", "Assessment", "Plan"];
  let lastSerialized = collect();
  let timer = null;
  let inFlight = false;

  function collect() {
    const data = {};
    for (const k of fields) {
      const el = form.querySelector(`textarea[name="${k}"]`);
      data[k] = el ? el.value : "";
    }
    return data;
  }

  function setStatus(text, tone) {
    if (!status) return;
    status.textContent = text;
    status.className = `small ${tone || "text-muted"}`;
  }

  async function save() {
    const data = collect();
    const serialized = JSON.stringify(data);
    if (serialized === JSON.stringify(lastSerialized)) return;
    if (inFlight) return;
    inFlight = true;
    setStatus("Saving…", "text-muted");
    try {
      const res = await fetch(`/Telemedicine/SaveNotes/${sessionId}`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-Token": csrf,
        },
        credentials: "same-origin",
        body: serialized,
      });
      if (res.ok) {
        const body = await res.json();
        const t = body.savedAt ? new Date(body.savedAt) : new Date();
        setStatus(
          `Saved ${t.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`,
          "text-success",
        );
        lastSerialized = data;
      } else {
        setStatus("Save failed — retrying", "text-danger");
      }
    } catch (e) {
      console.error("Notes save failed:", e);
      setStatus("Offline — will retry", "text-warning");
    } finally {
      inFlight = false;
    }
  }

  function schedule() {
    if (timer) clearTimeout(timer);
    setStatus("Editing…", "text-muted");
    timer = setTimeout(save, 1500);
  }

  form.addEventListener("input", schedule);
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden") save();
  });
  window.addEventListener("beforeunload", save);
})();
