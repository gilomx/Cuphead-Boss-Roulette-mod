(() => {
  "use strict";
  const root = document.getElementById("battle");
  const status = document.getElementById("battle-status");
  const roster = document.getElementById("battle-roster");
  const template = document.getElementById("battle-slot-template");
  const slots = [];
  const COPY = {
    es: {
      title: "Batalla Molestosa", rosterAria: "Participantes de Batalla Molestosa",
      recruiting: "Esperando jugadores", ready: "Jugadores listos",
      waitingAttempt: "Esperando el siguiente intento", waitingLevel: "Esperando el siguiente nivel",
      active: "Batalla en curso", won: "¡Victoria!", stopping: "Cerrando batalla",
      idle: "Esperando jugadores", participant: "Participante", slot: "Cupo {slot}",
      avatar: "Foto de {name}", entryGift: "Regalo de entrada", previewGift: "Rosa",
    },
    en: {
      title: "Pesky Battle", rosterAria: "Pesky Battle participants",
      recruiting: "Waiting for players", ready: "Players ready",
      waitingAttempt: "Waiting for the next attempt", waitingLevel: "Waiting for the next level",
      active: "Battle in progress", won: "Victory!", stopping: "Closing battle",
      idle: "Waiting for players", participant: "Participant", slot: "Slot {slot}",
      avatar: "Photo of {name}", entryGift: "Entry gift", previewGift: "Rose",
    },
  };
  const normalizeLocale = (value) => {
    const locale = String(value || "").trim().toLowerCase();
    if (locale === "en" || locale.startsWith("en-")) return "en";
    if (locale === "es" || locale.startsWith("es-")) return "es";
    return "";
  };
  const queryLocale = normalizeLocale(new URLSearchParams(window.location.search).get("locale"));
  let activeLocale = queryLocale || "es";
  const message = (text, key, value) => text.replace("{" + key + "}", String(value));
  const statusText = (state, text) => {
    switch (state.phase) {
      case "recruiting": return text.recruiting;
      case "ready": return text.ready;
      case "waiting_level": return state.attempt > 0 ? text.waitingAttempt : text.waitingLevel;
      case "active": return text.active;
      case "won": return text.won;
      case "stopping": return text.stopping;
      default: return text.idle;
    }
  };
  const safeImage = (value) => {
    if (!String(value || "").trim()) return "";
    try {
      const url = new URL(String(value).trim(), window.location.origin);
      return url.protocol === "https:" ||
        (url.protocol === "http:" && url.origin === window.location.origin) ? url.href : "";
    } catch { return ""; }
  };
  const createSlot = () => {
    const fragment = template.content.cloneNode(true);
    const item = fragment.querySelector(".battle-slot");
    const slot = {
      item, signature: "",
      avatar: fragment.querySelector(".battle-slot__avatar"),
      gift: fragment.querySelector(".battle-slot__gift"),
      coin: fragment.querySelector(".battle-slot__coin"),
      initial: fragment.querySelector(".battle-slot__initial"),
      name: fragment.querySelector(".battle-slot__name"),
    };
    slot.avatar.addEventListener("error", () => {
      if (item.dataset.filled !== "true") return;
      slot.avatar.hidden = true;
      slot.initial.hidden = false;
    });
    slot.gift.addEventListener("error", () => {
      if (item.dataset.filled === "true") return;
      slot.gift.hidden = true;
      slot.coin.hidden = false;
    });
    roster.append(fragment);
    return slot;
  };
  const updateSlot = (slot, slotNumber, participant, giftUrl, giftName, text) => {
    const displayName = participant
      ? String(participant.displayName || participant.userName || text.participant).trim() || text.participant
      : "";
    const avatarUrl = safeImage(participant?.avatarUrl);
    const signature = JSON.stringify([activeLocale, slotNumber, Boolean(participant), displayName, avatarUrl, giftUrl, giftName]);
    if (signature === slot.signature) return;
    slot.signature = signature;
    slot.item.dataset.filled = String(Boolean(participant));
    slot.item.setAttribute("aria-label", displayName || message(text.slot, "slot", slotNumber));
    slot.name.textContent = displayName;
    slot.name.hidden = !participant;
    slot.initial.textContent = displayName.split(/\s+/).slice(0, 2).map(word => word.charAt(0)).join("");
    slot.initial.hidden = !participant || Boolean(avatarUrl);
    slot.coin.hidden = Boolean(participant) || Boolean(giftUrl);
    slot.gift.hidden = Boolean(participant) || !giftUrl;
    slot.avatar.hidden = !participant || !avatarUrl;
    if (participant && avatarUrl) {
      slot.avatar.src = avatarUrl;
      slot.avatar.alt = message(text.avatar, "name", displayName);
    } else { slot.avatar.removeAttribute("src"); }
    if (!participant && giftUrl) {
      slot.gift.src = giftUrl;
      slot.gift.alt = giftName || text.entryGift;
    } else { slot.gift.removeAttribute("src"); }
  };
  const render = (state) => {
    if (!state || typeof state !== "object") return;
    activeLocale = normalizeLocale(state.locale) || queryLocale || "es";
    const text = COPY[activeLocale];
    document.documentElement.lang = activeLocale;
    document.title = text.title;
    roster.setAttribute("aria-label", text.rosterAria);
    const phase = typeof state.phase === "string" ? state.phase : "off";
    const requestedCapacity = Number(state.capacity);
    const capacity = Number.isInteger(requestedCapacity) && requestedCapacity >= 2 && requestedCapacity <= 5
      ? requestedCapacity : 5;
    const presentation = state.presentation && typeof state.presentation === "object" ? state.presentation : {};
    root.dataset.phase = phase;
    root.dataset.visible = String(phase !== "off");
    root.dataset.capacity = String(capacity);
    root.dataset.showTitle = String(presentation.showTitle !== false);
    root.dataset.showDetails = String(presentation.showDetails !== false);
    root.dataset.motion = String(presentation.motion !== false);
    status.textContent = statusText(state, text);
    const participants = Array.isArray(state.participants) ? state.participants : [];
    const bySlot = new Map();
    participants.forEach((participant, index) => {
      if (!participant || typeof participant !== "object") return;
      const number = Number(participant.slot ?? index + 1);
      if (Number.isInteger(number) && number >= 1 && number <= capacity && !bySlot.has(number)) {
        bySlot.set(number, participant);
      }
    });
    const giftUrl = safeImage(state.trigger?.giftImagePath || state.trigger?.giftImageUrl);
    const giftName = String(state.trigger?.giftName || "").trim();
    // Preserve existing nodes and loaded photos when another player joins.
    if (slots.length !== capacity) {
      roster.replaceChildren();
      slots.length = 0;
      for (let index = 0; index < capacity; index += 1) slots.push(createSlot());
    }
    slots.forEach((slot, index) => updateSlot(slot, index + 1, bySlot.get(index + 1), giftUrl, giftName, text));
  };
  window.LiveEventOverlayRuntime.create({
    overlay: "pesky-battle", endpoint: "/api/config/pesky-battle", interval: 500, render,
    initialLiveState: { revision: 0, phase: "off", participants: [], capacity: 5 },
    initialPreviewState: {
      revision: 1, phase: "recruiting", locale: activeLocale, attempt: 0, capacity: 5,
      trigger: { giftName: COPY[activeLocale].previewGift, giftImagePath: "/assets/creator-tools/gifts/images/5655.webp" },
      participants: [],
    },
  });
})();
