(() => {
  "use strict";

  const root = document.getElementById("battle");
  const status = document.getElementById("battle-status");
  const trigger = document.getElementById("battle-trigger");
  const giftImage = document.getElementById("battle-gift-image");
  const giftName = document.getElementById("battle-gift-name");
  const progress = document.getElementById("battle-progress");
  const roster = document.getElementById("battle-roster");
  const template = document.getElementById("battle-slot-template");
  const eyebrow = document.getElementById("battle-eyebrow");
  let lastRosterSignature = "";

  const COPY = {
    es: {
      title: "Batalla Molestosa",
      eyebrow: "BATALLA MOLESTOSA",
      rosterAria: "Participantes de Batalla Molestosa",
      recruiting: "Reclutando contrincantes",
      ready: "Equipo completo",
      waitingAttempt: "Esperando el siguiente intento",
      waitingLevel: "Esperando el siguiente nivel",
      active: "Batalla en curso · Intento {attempt}",
      won: "¡Victoria!",
      idle: "Esperando reclutamiento",
      participant: "Participante",
      slot: "Cupo {slot}",
      avatar: "Foto de {name}",
      entryGift: "Regalo de entrada",
      previewGift: "Rebanada de pastel",
    },
    en: {
      title: "Pesky Battle",
      eyebrow: "PESKY BATTLE",
      rosterAria: "Pesky Battle participants",
      recruiting: "Recruiting opponents",
      ready: "Roster ready",
      waitingAttempt: "Waiting for the next attempt",
      waitingLevel: "Waiting for the next level",
      active: "Battle in progress · Attempt {attempt}",
      won: "Victory!",
      idle: "Waiting for recruitment",
      participant: "Participant",
      slot: "Slot {slot}",
      avatar: "Photo of {name}",
      entryGift: "Entry gift",
      previewGift: "Slice of cake",
    },
  };

  const normalizeLocale = (value) => {
    const locale = String(value || "").trim().toLowerCase();
    if (locale === "en" || locale.startsWith("en-")) return "en";
    if (locale === "es" || locale.startsWith("es-")) return "es";
    return "";
  };
  const queryLocale = normalizeLocale(
    new URLSearchParams(window.location.search).get("locale"),
  );
  let activeLocale = queryLocale || "es";

  const message = (template, values = {}) => Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template,
  );

  const applyLocale = (locale) => {
    activeLocale = locale || "es";
    const text = COPY[activeLocale];
    document.documentElement.lang = activeLocale;
    document.title = text.title;
    eyebrow.textContent = text.eyebrow;
    roster.setAttribute("aria-label", text.rosterAria);
  };

  const statusText = (state) => {
    const text = COPY[activeLocale];
    switch (state.phase) {
      case "recruiting": return text.recruiting;
      case "ready": return text.ready;
      case "waiting_level": return state.attempt > 0
        ? text.waitingAttempt
        : text.waitingLevel;
      case "active": return message(text.active, {
        attempt: Math.max(1, state.attempt || 1),
      });
      case "won": return text.won;
      default: return text.idle;
    }
  };

  const participantName = (participant) => (
    participant?.displayName || participant?.userName || COPY[activeLocale].participant
  ).trim();

  const safeAvatar = (participant) => {
    const value = String(participant?.avatarUrl || "").trim();
    if (!value) return "";
    try {
      const url = new URL(value, window.location.origin);
      return url.protocol === "https:" || url.origin === window.location.origin
        ? url.href
        : "";
    } catch {
      return "";
    }
  };

  const createSlot = (slotNumber, participant) => {
    const fragment = template.content.cloneNode(true);
    const item = fragment.querySelector(".battle-slot");
    const avatar = fragment.querySelector(".battle-slot__avatar");
    const initial = fragment.querySelector(".battle-slot__initial");
    const number = fragment.querySelector(".battle-slot__number");
    const name = fragment.querySelector(".battle-slot__name");

    number.textContent = String(slotNumber);
    if (!participant) {
      item.dataset.filled = "false";
      initial.textContent = "?";
      name.textContent = message(COPY[activeLocale].slot, { slot: slotNumber });
      return fragment;
    }

    const displayName = participantName(participant);
    const avatarUrl = safeAvatar(participant);
    item.dataset.filled = "true";
    initial.textContent = displayName.charAt(0) || "?";
    name.textContent = displayName;
    item.title = displayName;
    if (avatarUrl) {
      avatar.src = avatarUrl;
      avatar.alt = message(COPY[activeLocale].avatar, { name: displayName });
      avatar.hidden = false;
      avatar.addEventListener("error", () => {
        avatar.hidden = true;
        avatar.removeAttribute("src");
      }, { once: true });
    }
    return fragment;
  };

  const render = (state) => {
    if (!state || typeof state !== "object") return;
    applyLocale(normalizeLocale(state.locale) || queryLocale || "es");
    const text = COPY[activeLocale];
    const phase = typeof state.phase === "string" ? state.phase : "off";
    const participants = Array.isArray(state.participants) ? state.participants : [];
    const capacity = Math.max(1, Math.min(5, Number(state.capacity) || 5));
    const bySlot = new Map(participants.map((participant, index) => [
      Math.max(1, Number(participant?.slot) || index + 1),
      participant,
    ]));

    root.dataset.phase = phase;
    root.dataset.visible = String(phase !== "off");
    status.textContent = statusText(state);
    progress.textContent = `${Math.min(participants.length, capacity)}/${capacity}`;

    const battleTrigger = state.trigger && typeof state.trigger === "object"
      ? state.trigger
      : {};
    const currentGiftName = String(battleTrigger.giftName || "").trim();
    const currentGiftImage = String(
      battleTrigger.giftImagePath || battleTrigger.giftImageUrl || "",
    ).trim();
    trigger.hidden = phase === "off";
    giftName.textContent = currentGiftName || text.entryGift;
    if (currentGiftImage) {
      giftImage.src = currentGiftImage;
      giftImage.hidden = false;
      giftImage.onerror = () => {
        giftImage.hidden = true;
        giftImage.removeAttribute("src");
      };
    } else {
      giftImage.hidden = true;
      giftImage.removeAttribute("src");
    }

    const rosterSignature = JSON.stringify({
      locale: activeLocale,
      capacity,
      participants: participants.map((participant) => ({
        slot: participant?.slot,
        userId: participant?.userId,
        userName: participant?.userName,
        displayName: participant?.displayName,
        avatarUrl: participant?.avatarUrl,
      })),
    });
    if (rosterSignature !== lastRosterSignature) {
      lastRosterSignature = rosterSignature;
      roster.replaceChildren();
      for (let slot = 1; slot <= capacity; slot += 1) {
        roster.append(createSlot(slot, bySlot.get(slot)));
      }
    }
  };

  window.LiveEventOverlayRuntime.create({
    overlay: "pesky-battle",
    endpoint: "/api/config/pesky-battle",
    interval: 500,
    render,
    initialLiveState: {
      revision: 0,
      phase: "off",
      participants: [],
      capacity: 5,
    },
    initialPreviewState: {
      revision: 1,
      phase: "recruiting",
      locale: activeLocale,
      attempt: 0,
      capacity: 5,
      trigger: {
        giftName: COPY[activeLocale].previewGift,
        giftImagePath: "/assets/creator-tools/gifts/images/6784.png",
      },
      participants: [
        { slot: 1, userId: "preview-1", displayName: "La Pichi" },
        { slot: 2, userId: "preview-2", displayName: "Don Taza" },
        { slot: 3, userId: "preview-3", displayName: "Señorita Cáliz" },
      ],
    },
  });
})();
