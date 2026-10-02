(() => {
  "use strict";
  const root = document.getElementById("battle");
  const status = document.getElementById("battle-status");
  const roster = document.getElementById("battle-roster");
  const template = document.getElementById("battle-slot-template");
  const slots = [];
  const TEXT_DURATION = 200;
  const STATUS_DURATION = 280;
  const nameSegmenter = typeof Intl.Segmenter === "function"
    ? new Intl.Segmenter(undefined, { granularity: "grapheme" }) : null;
  const shortName = (name) => {
    const letters = nameSegmenter
      ? Array.from(nameSegmenter.segment(name), ({ segment }) => segment)
      : Array.from(name);
    return letters.length > 14 ? letters.slice(0, 14).join("") + "." : name;
  };
  let cleanupTimer = 0;
  let signalScope = "";
  const seenAttacks = new Set();
  const createTextTransition = (element, duration = TEXT_DURATION, contentChanged = () => {}) => {
    let target = "";
    let timer = 0;
    element.hidden = true;
    element.dataset.textPhase = "hidden";
    const showTarget = () => {
      timer = 0;
      element.textContent = target;
      element.hidden = !target;
      contentChanged();
      element.dataset.textPhase = "hidden";
      if (!target) return;
      // One text node: the previous message is fully gone before replacement.
      void element.offsetWidth;
      element.dataset.textPhase = "entering";
      timer = window.setTimeout(() => {
        timer = 0;
        element.dataset.textPhase = "shown";
      }, duration);
    };
    const update = (value, animated) => {
      target = value;
      if (!animated) {
        window.clearTimeout(timer);
        timer = 0;
        element.textContent = target;
        element.hidden = !target;
        contentChanged();
        element.dataset.textPhase = target ? "shown" : "hidden";
        return;
      }
      if (element.dataset.textPhase === "exiting") return;
      if (element.textContent === target) return;
      window.clearTimeout(timer);
      if (element.hidden || !element.textContent) { showTarget(); return; }
      element.dataset.textPhase = "exiting";
      // Changes during this interval replace the queued target, never the text on screen.
      timer = window.setTimeout(showTarget, duration);
    };
    return { update, dispose: () => update("", false) };
  };
  const statusTransition = createTextTransition(status, STATUS_DURATION);
  const COPY = {
    es: {
      title: "Batalla Molestosa", rosterAria: "Participantes de Batalla Molestosa",
      recruiting: "Esperando jugadores", ready: "Jugadores listos",
      waitingAttempt: "Esperando intento", waitingLevel: "Esperando nivel",
      active: "Batalla en curso", won: "¡Victoria!", stopping: "Cerrando batalla",
      idle: "Esperando jugadores", participant: "Participante", slot: "Cupo {slot}",
      avatar: "Foto de {name}", entryGift: "Regalo de entrada", previewGift: "Rosa",
    },
    en: {
      title: "Pesky Battle", rosterAria: "Pesky Battle participants",
      recruiting: "Waiting for players", ready: "Players ready",
      waitingAttempt: "Waiting for retry", waitingLevel: "Waiting for level",
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
  const nameMeasure = document.createElement?.("canvas").getContext?.("2d");
  const fitName = (slot) => {
    if (!nameMeasure || !slot.name.textContent || slot.name.hidden || !slot.item.clientWidth) return;
    const preferred = Math.max(9, Math.min(46, slot.item.clientWidth * .21));
    const style = window.getComputedStyle(slot.name);
    nameMeasure.font = `${style.fontWeight} ${preferred}px ${style.fontFamily}`;
    const width = nameMeasure.measureText(slot.name.textContent).width;
    // Fit fourteen wide letters without overlapping adjacent players.
    slot.name.style.setProperty("--name-size-limit", `${Math.min(preferred,
      preferred * Math.max(1, slot.name.clientWidth - 4) / Math.max(1, width))}px`);
  };
  const createSlot = () => {
    const fragment = template.content.cloneNode(true);
    const item = fragment.querySelector(".battle-slot");
    const slot = {
      item, signature: "", hasRendered: false,
      avatar: fragment.querySelector(".battle-slot__avatar"),
      gift: fragment.querySelector(".battle-slot__gift"),
      coin: fragment.querySelector(".battle-slot__coin"),
      initial: fragment.querySelector(".battle-slot__initial"),
      name: fragment.querySelector(".battle-slot__name"),
      attack: fragment.querySelector(".battle-slot__attack"),
      attackImage: fragment.querySelector(".battle-slot__attack-image"),
      attackTimer: 0,
      attackQueue: [],
      challenge: fragment.querySelector(".battle-slot__challenge"),
      challengeImage: fragment.querySelector(".battle-slot__challenge-image"),
      challengeTime: fragment.querySelector(".battle-slot__challenge-time"),
    };
    slot.nameTransition = createTextTransition(slot.name, TEXT_DURATION, () => fitName(slot));
    if (typeof window.ResizeObserver === "function") {
      slot.nameObserver = new window.ResizeObserver(() => fitName(slot));
      slot.nameObserver.observe(item);
    }
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
  const clearAttacks = () => slots.forEach(slot => {
    window.clearTimeout(slot.attackTimer);
    slot.attackTimer = 0;
    slot.attackQueue.length = 0;
    slot.item.dataset.attacking = "false";
    slot.attackImage.removeAttribute("src");
    slot.attack.removeAttribute("aria-label");
  });
  const interaction = (event) => {
    const item = window.CreatorToolsOverlayInteractions?.[event.item];
    return { image: safeImage(item?.imagePath || event.imagePath),
      name: String(item?.names?.[activeLocale] || event.name || "").slice(0, 120) };
  };
  const showNextAttack = (slot) => {
    const attack = slot.attackQueue.shift();
    if (!attack) { slot.attackTimer = 0; return; }
    slot.item.dataset.attacking = "false";
    // Restart only this event's effect; preserve the loaded portrait and roster.
    void slot.attack.offsetWidth;
    slot.attackImage.src = attack.image;
    slot.attack.setAttribute("aria-label", attack.name);
    slot.item.dataset.attacking = "true";
    slot.attackTimer = window.setTimeout(() => {
      slot.item.dataset.attacking = "false";
      slot.attackImage.removeAttribute("src");
      slot.attack.removeAttribute("aria-label");
      showNextAttack(slot);
    }, attack.duration);
  };
  const renderAttack = (attack, clock, initial, legacy = false) => {
    if (!attack || !attack.id || seenAttacks.has(String(attack.id))) return;
    const age = attack.ageMs !== undefined && Number.isFinite(Number(attack.ageMs))
      ? Number(attack.ageMs) : clock - Number(attack.startedAt);
    // Seed the cursor with old history on first connection so the next poll
    // cannot resurrect events that were deliberately skipped.
    if (initial && !legacy && age > 2300) { seenAttacks.add(String(attack.id)); return; }
    const slot = slots[Number(attack.slot) - 1];
    const visual = interaction(attack);
    if (!slot || !Number.isInteger(Number(attack.slot)) || slot.item.dataset.filled !== "true" ||
        !visual.image || !Number.isFinite(age) || age < -500 || age > (legacy ? 1800 : initial ? 2300 : 12000)) return;
    seenAttacks.add(String(attack.id));
    if (seenAttacks.size > 128) seenAttacks.delete(seenAttacks.values().next().value);
    if (slot.attackQueue.length >= 32) return;
    slot.attackQueue.push({ ...visual, duration: legacy ? Math.max(1, 1800 - age) : 1800 });
    if (!slot.attackTimer) showNextAttack(slot);
  };
  const clearChallenges = () => slots.forEach(slot => {
    slot.item.dataset.challenge = "false";
    slot.challenge.setAttribute("aria-hidden", "true");
    slot.challengeImage.removeAttribute("src");
    slot.challengeTime.textContent = "";
  });
  const renderChallenges = (challenges) => {
    slots.forEach((slot, index) => {
      const event = challenges.find(value => Number(value.slot) === index + 1);
      const visual = event ? interaction(event) : null;
      const active = event && visual.image && slot.item.dataset.filled === "true";
      slot.item.dataset.challenge = String(Boolean(active));
      slot.challenge.setAttribute("aria-hidden", String(!active));
      if (!active) return;
      if (slot.challengeImage.src !== visual.image) slot.challengeImage.src = visual.image;
      slot.challenge.dataset.phase = event.phase === "countdown" ? "countdown" : "active";
      const seconds = Math.max(0, Math.min(120, Math.ceil(Number(event.secondsRemaining) || 0)));
      slot.challengeTime.textContent = String(seconds);
      slot.challenge.setAttribute("aria-label", `${visual.name}: ${activeLocale === "en"
        ? event.phase === "countdown" ? "starts in" : "remaining"
        : event.phase === "countdown" ? "empieza en" : "restan"} ${seconds}s`);
    });
  };
  const clearRoster = () => {
    slots.forEach(slot => { slot.nameObserver?.disconnect(); slot.nameTransition.dispose(); });
    clearAttacks();
    clearChallenges();
    roster.replaceChildren();
    slots.length = 0;
  };
  const updateSlot = (slot, slotNumber, participant, giftUrl, giftName, text, showNames, animated) => {
    const displayName = participant
      ? String(participant.displayName || participant.userName || text.participant).trim() || text.participant
      : "";
    slot.nameTransition.update(showNames ? shortName(displayName) : "", animated);
    const avatarUrl = safeImage(participant?.avatarUrl);
    const signature = JSON.stringify([activeLocale, slotNumber, Boolean(participant), displayName, avatarUrl, giftUrl, giftName]);
    if (signature === slot.signature) return;
    slot.signature = signature;
    // Initial roster uses the Tap Farming-style entrance once. A player
    // filling an already visible empty slot keeps the separate join effect.
    if (!participant || slot.item.dataset.filled !== "true") {
      slot.item.dataset.playerEntry = String(Boolean(participant) && slot.hasRendered);
    }
    slot.hasRendered = true;
    slot.item.dataset.filled = String(Boolean(participant));
    slot.item.setAttribute("aria-label", displayName || message(text.slot, "slot", slotNumber));
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
    const visible = ["recruiting", "ready", "waiting_level", "active", "won"].includes(phase);
    const animated = presentation.motion !== false && window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches !== true;
    root.dataset.phase = phase;
    root.dataset.visible = String(visible);
    root.setAttribute("aria-hidden", String(!visible));
    root.dataset.capacity = String(capacity);
    root.dataset.showTitle = String(presentation.showTitle !== false);
    root.dataset.showDetails = String(presentation.showDetails !== false);
    root.dataset.motion = String(presentation.motion !== false);
    for (const [property, value, fallback] of [
      ["--battle-border", presentation.outlineColor, "#c48ae9"],
      ["--battle-player-border", presentation.liquidColor, "#c48ae9"],
      ["--battle-text", presentation.textColor, "#fbf8fc"],
      ["--battle-accent", presentation.collectingColor, "#f4c76f"],
    ]) {
      root.style.setProperty(property, /^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(String(value)) ? value : fallback);
    }
    statusTransition.update(visible && presentation.showTitle !== false ? statusText(state, text) : "", animated);
    if (!visible) {
      slots.forEach(slot => slot.nameTransition.update("", animated));
      clearAttacks();
      clearChallenges();
      signalScope = "";
      seenAttacks.clear();
      if (!animated || slots.length === 0) {
        window.clearTimeout(cleanupTimer);
        cleanupTimer = 0;
        clearRoster();
      } else if (!cleanupTimer) {
        cleanupTimer = window.setTimeout(() => { cleanupTimer = 0; clearRoster(); }, TEXT_DURATION);
      }
      return;
    }
    window.clearTimeout(cleanupTimer);
    cleanupTimer = 0;
    const participants = Array.isArray(state.participants) ? state.participants : [];
    const bySlot = new Map();
    participants.forEach((participant, index) => {
      if (!participant || typeof participant !== "object") return;
      const number = Number(participant.slot ?? index + 1);
      if (Number.isInteger(number) && number >= 1 && number <= capacity && !bySlot.has(number)) {
        bySlot.set(number, participant);
      }
    });
    const giftId = String(state.trigger?.giftId || "");
    // Older mods publish the native Windows path. Use the catalog asset by
    // numeric ID in that case, so a browser never needs file-system access.
    const giftUrl = safeImage(state.trigger?.giftImagePath || state.trigger?.giftImageUrl) ||
      (/^[0-9]{1,160}$/.test(giftId)
        ? safeImage("/assets/creator-tools/gifts/images/" + giftId + ".webp") : "");
    const giftName = String(state.trigger?.giftName || "").trim();
    // Preserve existing nodes and loaded photos when another player joins.
    if (slots.length !== capacity) {
      clearRoster();
      for (let index = 0; index < capacity; index += 1) slots.push(createSlot());
    }
    const showNames = presentation.showDetails !== false;
    slots.forEach((slot, index) => updateSlot(slot, index + 1, bySlot.get(index + 1), giftUrl, giftName, text, showNames, animated));
    if (phase === "active") {
      const scope = JSON.stringify([state.sessionId ?? 0, state.attempt ?? 0, state.eventEpoch ?? 0,
        capacity, participants.map(player => [player?.slot, player?.userId || player?.userName || player?.displayName])]);
      const initial = scope !== signalScope;
      if (initial) { clearAttacks(); clearChallenges(); seenAttacks.clear(); signalScope = scope; }
      const clock = Number(state.serverTime) || Date.now();
      if (Array.isArray(state.attacks)) state.attacks.forEach(attack => renderAttack(attack, clock, initial));
      else if (state.attack) renderAttack(state.attack, Date.now(), initial, true);
      renderChallenges(Array.isArray(state.challenges) ? state.challenges : []);
    } else {
      clearAttacks(); clearChallenges(); signalScope = ""; seenAttacks.clear();
    }
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
