// Consult-actions UI for the tele-room (clinician side).
// Wires up:
//   - Type-ahead search on inputs that carry a [data-search-url]
//   - Submit handlers for each consult-action modal (Rx / Lab / Imaging / Sick / Referral / Follow-up)
//   - Optimistic list updates after a successful save
(function () {
  "use strict";

  const csrfEl = document.getElementById("th-csrf");
  const sessionEl = document.getElementById("th-session-id");
  if (!csrfEl || !sessionEl) return;
  const csrf = csrfEl.value;
  const sessionId = sessionEl.value;
  const status = document.getElementById("th-actions-status");

  function setStatus(text, tone) {
    if (!status) return;
    status.textContent = text || "—";
    status.className = `small ${tone || "text-muted"}`;
  }

  function fmtDate(d) {
    return d.toLocaleString([], {
      weekday: "short",
      day: "2-digit",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
    });
  }

  async function postJson(path, body) {
    const res = await fetch(path, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-Token": csrf,
      },
      body: JSON.stringify(body),
    });
    let payload = null;
    try {
      payload = await res.json();
    } catch (_e) {
      /* response had no JSON body */
    }
    return { ok: res.ok, status: res.status, payload };
  }

  // ─── Autocomplete ──────────────────────────────────────────────────────
  document
    .querySelectorAll("input[data-search-url]")
    .forEach((input) => attachAutocomplete(input));

  function attachAutocomplete(input) {
    const url = input.dataset.searchUrl;
    const idField = input.parentElement.querySelector('input[type="hidden"]');
    let dropdown = null;
    let timer = null;

    function close() {
      if (dropdown) {
        dropdown.remove();
        dropdown = null;
      }
    }

    async function search() {
      const q = input.value.trim();
      if (q.length < 2) {
        close();
        return;
      }
      const res = await fetch(`${url}?q=${encodeURIComponent(q)}`, {
        credentials: "same-origin",
      });
      if (!res.ok) {
        close();
        return;
      }
      const items = await res.json();
      render(items);
    }

    function render(items) {
      close();
      if (!items.length) return;
      dropdown = document.createElement("div");
      dropdown.className = "list-group position-absolute shadow-sm";
      dropdown.style.zIndex = "1080";
      dropdown.style.width = `${input.offsetWidth}px`;
      dropdown.style.maxHeight = "240px";
      dropdown.style.overflowY = "auto";
      items.forEach((item) => {
        const a = document.createElement("button");
        a.type = "button";
        a.className = "list-group-item list-group-item-action small";
        a.textContent = item.label;
        a.addEventListener("click", () => {
          input.value = item.label;
          if (idField) idField.value = item.id;
          close();
        });
        dropdown.appendChild(a);
      });
      input.parentElement.style.position = "relative";
      input.parentElement.appendChild(dropdown);
    }

    input.addEventListener("input", () => {
      if (idField) idField.value = "";
      if (timer) clearTimeout(timer);
      timer = setTimeout(search, 200);
    });
    input.addEventListener("blur", () => setTimeout(close, 150));
  }

  // ─── Submit wiring ─────────────────────────────────────────────────────
  document
    .querySelectorAll("[data-th-submit]")
    .forEach((btn) =>
      btn.addEventListener("click", () => handleSubmit(btn.dataset.thSubmit)),
    );

  function readForm(formId) {
    const form = document.getElementById(formId);
    const data = {};
    Array.from(form.elements).forEach((el) => {
      if (!el.name) return;
      data[el.name] = el.value === "" ? null : el.value;
    });
    return { form, data };
  }

  function closeModal(modalId) {
    const el = document.getElementById(modalId);
    const m = bootstrap.Modal.getInstance(el) || new bootstrap.Modal(el);
    m.hide();
  }

  function appendListItem(listId, html) {
    const list = document.getElementById(listId);
    if (!list) return;
    const placeholder = list.querySelector("li.text-muted");
    if (placeholder) placeholder.remove();
    const li = document.createElement("li");
    li.className = "border-bottom py-1";
    li.innerHTML = html;
    list.appendChild(li);
  }

  async function handleSubmit(kind) {
    let url, listId, modalId, body, summary;
    switch (kind) {
      case "rx": {
        const { data } = readForm("th-rx-form");
        if (!data.DrugName) return setStatus("Pick a drug first", "text-warning");
        url = `/Telemedicine/Prescribe/${sessionId}`;
        listId = "th-rx-list";
        modalId = "thRxModal";
        body = {
          DrugId: data.DrugId ? parseInt(data.DrugId, 10) : null,
          DrugName: data.DrugName,
          Dose: data.Dose,
          Route: data.Route,
          Frequency: data.Frequency,
          Duration: data.Duration,
          Quantity: data.Quantity ? parseInt(data.Quantity, 10) : null,
          Instructions: data.Instructions,
        };
        summary = `<strong>${escapeHtml(data.DrugName)}</strong> ${escapeHtml(data.Dose || "")} · ${escapeHtml(data.Frequency || "")} · ${escapeHtml(data.Duration || "")}`;
        break;
      }
      case "lab": {
        const { data } = readForm("th-lab-form");
        if (!data.TestName) return setStatus("Pick a test first", "text-warning");
        url = `/Telemedicine/OrderLab/${sessionId}`;
        listId = "th-lab-list";
        modalId = "thLabModal";
        body = {
          LabTestId: data.LabTestId ? parseInt(data.LabTestId, 10) : null,
          TestName: data.TestName,
          ClinicalIndication: data.ClinicalIndication,
          Urgency: parseInt(data.Urgency || "1", 10),
        };
        summary = `<strong>${escapeHtml(data.TestName)}</strong> · <span class="text-muted">${urgencyLabel(body.Urgency)}</span>${data.ClinicalIndication ? " — " + escapeHtml(data.ClinicalIndication) : ""}`;
        break;
      }
      case "img": {
        const { data } = readForm("th-img-form");
        if (!data.StudyDescription) return setStatus("Describe the study", "text-warning");
        url = `/Telemedicine/OrderImaging/${sessionId}`;
        listId = "th-img-list";
        modalId = "thImgModal";
        body = {
          Modality: parseInt(data.Modality || "1", 10),
          StudyDescription: data.StudyDescription,
          ClinicalIndication: data.ClinicalIndication,
          Urgency: parseInt(data.Urgency || "1", 10),
        };
        summary = `<strong>${modalityLabel(body.Modality)} — ${escapeHtml(data.StudyDescription)}</strong> · <span class="text-muted">${urgencyLabel(body.Urgency)}</span>`;
        break;
      }
      case "sick": {
        const { data } = readForm("th-sick-form");
        if (!data.StartDate || !data.EndDate)
          return setStatus("Set the date range", "text-warning");
        url = `/Telemedicine/IssueSickNote/${sessionId}`;
        listId = "th-sick-list";
        modalId = "thSickModal";
        body = {
          StartDate: data.StartDate,
          EndDate: data.EndDate,
          Diagnosis: data.Diagnosis,
          IcdCode: data.IcdCode,
          Recommendations: data.Recommendations,
        };
        const days =
          (new Date(data.EndDate) - new Date(data.StartDate)) /
            (24 * 60 * 60 * 1000) +
          1;
        summary = `<strong>Pending — issuing…</strong> · ${days} day(s) (${data.StartDate} → ${data.EndDate})`;
        break;
      }
      case "ref": {
        const { data } = readForm("th-ref-form");
        if (!data.ReferredToClinicianId && !data.Specialty)
          return setStatus("Pick a clinician or specialty", "text-warning");
        url = `/Telemedicine/Refer/${sessionId}`;
        listId = "th-ref-list";
        modalId = "thReferModal";
        body = {
          ReferredToClinicianId: data.ReferredToClinicianId,
          Specialty: data.Specialty,
          Reason: data.Reason,
          ClinicalSummary: data.ClinicalSummary,
        };
        summary = `<strong>Referral</strong> — ${escapeHtml(data.ReferredToName || data.Specialty || "")}${data.Reason ? " · " + escapeHtml(data.Reason) : ""}`;
        break;
      }
      case "follow": {
        const { data } = readForm("th-follow-form");
        if (!data.ScheduledStartUtc)
          return setStatus("Pick a date/time", "text-warning");
        url = `/Telemedicine/FollowUp/${sessionId}`;
        listId = "th-follow-list";
        modalId = "thFollowModal";
        body = {
          ScheduledStartUtc: data.ScheduledStartUtc,
          Type: parseInt(data.Type || "2", 10),
          DurationMinutes: parseInt(data.DurationMinutes || "15", 10),
          ReasonForVisit: data.ReasonForVisit,
        };
        summary = `<strong>${apptTypeLabel(body.Type)}</strong> · ${fmtDate(new Date(data.ScheduledStartUtc))}${data.ReasonForVisit ? " — " + escapeHtml(data.ReasonForVisit) : ""}`;
        break;
      }
    }

    setStatus("Saving…", "text-muted");
    const result = await postJson(url, body);
    if (!result.ok) {
      setStatus(
        result.payload?.error || `Failed (HTTP ${result.status})`,
        "text-danger",
      );
      return;
    }
    appendListItem(listId, summary);
    closeModal(modalId);
    setStatus("Saved", "text-success");

    if (kind === "sick" && result.payload?.viewUrl) {
      window.open(result.payload.viewUrl, "_blank");
    }
    // Reset the form for next use
    document.getElementById(`th-${kind === "follow" ? "follow" : kind}-form`)?.reset();
  }

  function escapeHtml(s) {
    return (s || "")
      .toString()
      .replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
  }
  function urgencyLabel(u) {
    return u === 3 ? "STAT" : u === 2 ? "Urgent" : "Routine";
  }
  function modalityLabel(m) {
    return ({ 1: "X-Ray", 2: "Ultrasound", 3: "CT", 4: "MRI", 5: "Mammography", 6: "Fluoroscopy", 99: "Other" })[m] || "Imaging";
  }
  function apptTypeLabel(t) {
    return t === 6 ? "Tele-medicine" : t === 2 ? "Follow-up" : "Appointment";
  }
})();
