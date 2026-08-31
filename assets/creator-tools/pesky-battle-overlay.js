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
  let lastRevision = -1;
  let lastRosterSignature = "";
  let requestPending = false;

  const statusText = (state) => {
    switch (state.phase) {
      case "recruiting": return "Reclutando contrincantes";
      case "ready": return "Equipo completo";
      case "waiting_level": return state.attempt > 0
        ? "Esperando el siguiente intento"
        : "Esperando el siguiente nivel";
      case "active": return `Batalla en curso · Intento ${Math.max(1, state.attempt || 1)}`;
      case "won": return "¡Victoria!";
      default: return "Esperando reclutamiento";
    }
  };

  const participantName = (participant) => (
    participant?.displayName || participant?.userName || "Participante"
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
      name.textContent = `Cupo ${slotNumber}`;
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
      avatar.alt = `Foto de ${displayName}`;
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
    giftName.textContent = currentGiftName || "Regalo de entrada";
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

  const refresh = async () => {
    if (requestPending) return;
    requestPending = true;
    try {
      const response = await fetch("/api/config/pesky-battle", { cache: "no-store" });
      if (!response.ok) return;
      const state = await response.json();
      const revision = Number(state?.revision);
      if (!Number.isFinite(revision) || revision !== lastRevision) {
        lastRevision = Number.isFinite(revision) ? revision : lastRevision;
        render(state);
      }
    } catch {
      // Keep the last complete snapshot visible through a short reconnect.
    } finally {
      requestPending = false;
    }
  };

  render({ phase: "off", participants: [], capacity: 5 });
  void refresh();
  window.setInterval(refresh, 500);
})();
