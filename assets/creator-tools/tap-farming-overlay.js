(() => {
  "use strict";

  const root = document.getElementById("tap-farming");
  const bossName = document.getElementById("tap-boss-name");
  const status = document.getElementById("tap-status");
  const previewBadge = document.getElementById("tap-preview-badge");
  const phaseLabel = document.getElementById("tap-phase-label");
  const overallProgress = document.getElementById("tap-overall-progress");
  const phaseTrack = document.getElementById("tap-phase-track");
  const phaseTemplate = document.getElementById("tap-phase-template");
  const reserveCard = document.getElementById("tap-reserve-card");
  const reserveHealth = document.getElementById("tap-reserve-health");
  const reserveFill = document.getElementById("tap-reserve-fill");
  const bankedTaps = document.getElementById("tap-banked-taps");
  const reserveEquivalence = document.getElementById("tap-reserve-equivalence");
  const totalTaps = document.getElementById("tap-total");
  const convertedHealth = document.getElementById("tap-converted-health");
  const spentHealth = document.getElementById("tap-spent-health");
  const conversion = document.getElementById("tap-conversion");
  const conversionProgress = document.getElementById("tap-conversion-progress");
  const conversionRemainder = document.getElementById("tap-conversion-remainder");
  const eyebrow = document.getElementById("tap-eyebrow");
  const journeyLabel = document.getElementById("tap-journey-label");
  const reserveLabel = document.getElementById("tap-reserve-label");
  const bankedLabel = document.getElementById("tap-banked-label");
  const totalLabel = document.getElementById("tap-total-label");
  const generatedLabel = document.getElementById("tap-generated-label");
  const absorbedLabel = document.getElementById("tap-absorbed-label");
  const conversionLabel = document.getElementById("tap-conversion-label");
  const conversionEquation = document.getElementById("tap-conversion-equation");

  const COPY = {
    es: {
      title: "Farmeando taps",
      eyebrow: "FARMEANDO TAPS",
      waitingBoss: "Esperando jefe",
      nextBoss: "Próximo jefe",
      currentBoss: "Jefe actual",
      preview: "VISTA PREVIA",
      journey: "AVANCE DEL COMBATE",
      preparing: "Preparando el combate",
      phase: "Fase {index}",
      phaseOf: "Fase {current} de {count}",
      phaseTrack: "Progreso por fases",
      overallAria: "Avance del combate: {percent}%. Fase {current} de {count}.",
      reserve: "RESERVA DE TAPS",
      point: "punto",
      points: "puntos",
      tapWaiting: "tap esperando",
      tapsWaiting: "taps esperando",
      noReserve: "Sin reserva activa",
      pointReady: "{value} punto listo",
      pointsReady: "{value} puntos listos",
      timesBase: "{value} veces la vida base",
      percentBase: "{value} de la vida base",
      total: "TAPS COMUNITARIOS",
      generated: "PUNTOS GENERADOS",
      absorbed: "DAÑO ABSORBIDO",
      conversion: "CONVERSIÓN",
      conversionOne: "tap = 1 punto",
      conversionMany: "taps = 1 punto",
      remainder: "{current} de {total} para el siguiente",
      statusCollecting: "Recolectando taps para el combate",
      statusActive: "Fase {current}/{count} · Intento {attempt}",
      statusTransition: "Transición · guardando taps",
      statusCompleted: "¡Jefe derrotado!",
      statusStopping: "Cerrando evento",
      statusOff: "Evento desactivado",
      phaseComplete: "SUPERADA",
      phaseActive: "EN CURSO",
      phaseTransition: "EN TRANSICIÓN",
      phasePending: "PENDIENTE",
    },
    en: {
      title: "Tap Farming",
      eyebrow: "TAP FARMING",
      waitingBoss: "Waiting for a boss",
      nextBoss: "Next boss",
      currentBoss: "Current boss",
      preview: "PREVIEW",
      journey: "BATTLE PROGRESS",
      preparing: "Preparing the battle",
      phase: "Phase {index}",
      phaseOf: "Phase {current} of {count}",
      phaseTrack: "Progress by phase",
      overallAria: "Battle progress: {percent}%. Phase {current} of {count}.",
      reserve: "TAP RESERVE",
      point: "point",
      points: "points",
      tapWaiting: "tap waiting",
      tapsWaiting: "taps waiting",
      noReserve: "No active reserve",
      pointReady: "{value} point ready",
      pointsReady: "{value} points ready",
      timesBase: "{value} times base health",
      percentBase: "{value} of base health",
      total: "COMMUNITY TAPS",
      generated: "POINTS GENERATED",
      absorbed: "DAMAGE ABSORBED",
      conversion: "CONVERSION",
      conversionOne: "tap = 1 point",
      conversionMany: "taps = 1 point",
      remainder: "{current} of {total} to the next point",
      statusCollecting: "Collecting taps for the battle",
      statusActive: "Phase {current}/{count} · Attempt {attempt}",
      statusTransition: "Transition · banking taps",
      statusCompleted: "Boss defeated!",
      statusStopping: "Closing event",
      statusOff: "Event disabled",
      phaseComplete: "CLEARED",
      phaseActive: "IN PROGRESS",
      phaseTransition: "TRANSITIONING",
      phasePending: "PENDING",
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
  let numberFormat = new Intl.NumberFormat(activeLocale === "en" ? "en-US" : "es-MX", {
    maximumFractionDigits: 1,
  });
  let percentFormat = new Intl.NumberFormat(activeLocale === "en" ? "en-US" : "es-MX", {
    style: "percent",
    maximumSignificantDigits: 3,
  });

  const message = (template, values = {}) => Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template,
  );

  const applyLocale = (locale) => {
    activeLocale = locale || "es";
    numberFormat = new Intl.NumberFormat(activeLocale === "en" ? "en-US" : "es-MX", {
      maximumFractionDigits: 1,
    });
    percentFormat = new Intl.NumberFormat(activeLocale === "en" ? "en-US" : "es-MX", {
      style: "percent",
      maximumSignificantDigits: 3,
    });
    const text = COPY[activeLocale];
    document.documentElement.lang = activeLocale;
    document.title = text.title;
    eyebrow.textContent = text.eyebrow;
    journeyLabel.textContent = text.journey;
    previewBadge.textContent = text.preview;
    reserveLabel.textContent = text.reserve;
    totalLabel.textContent = text.total;
    generatedLabel.textContent = text.generated;
    absorbedLabel.textContent = text.absorbed;
    conversionLabel.textContent = text.conversion;
    phaseTrack.setAttribute("aria-label", text.phaseTrack);
  };

  const valueObject = (value) => value && typeof value === "object" ? value : {};

  const firstNumber = (...values) => {
    for (const value of values) {
      if (value === null || value === undefined || value === "") continue;
      const parsed = Number(value);
      if (Number.isFinite(parsed)) return parsed;
    }
    return 0;
  };

  const nonnegative = (...values) => Math.max(0, firstNumber(...values));
  const clamp = (value, minimum, maximum) => Math.min(maximum, Math.max(minimum, value));
  const format = (value) => numberFormat.format(Math.max(0, Number(value) || 0));

  const progressValue = (...values) => {
    const value = firstNumber(...values);
    return clamp(value > 1 ? value / 100 : value, 0, 1);
  };

  const normalizedPhase = (value) => {
    const phase = String(value || "off").trim().toLowerCase();
    if (phase === "waiting_level" || phase === "waiting" || phase === "armed") {
      return "collecting";
    }
    if (phase === "transitioning") return "transition";
    if (phase === "won" || phase === "complete") return "completed";
    if (["off", "collecting", "active", "transition", "completed", "stopping"]
      .includes(phase)) return phase;
    return "off";
  };

  const normalizedPhaseStatus = (value, index, activeIndex, eventPhase) => {
    const phaseStatus = String(value || "").trim().toLowerCase();
    if (["complete", "completed", "won", "done"].includes(phaseStatus)) {
      return "complete";
    }
    if (["active", "current", "transition"].includes(phaseStatus)) {
      return "active";
    }
    if (["pending", "waiting", "locked", "queued"].includes(phaseStatus)) {
      return "pending";
    }
    if (eventPhase === "completed" || index < activeIndex) return "complete";
    if (index === activeIndex) return "active";
    return "pending";
  };

  const phaseStatusText = (phaseStatus, eventPhase) => {
    const text = COPY[activeLocale];
    if (phaseStatus === "complete") return text.phaseComplete;
    if (phaseStatus === "active") {
      return eventPhase === "transition" ? text.phaseTransition : text.phaseActive;
    }
    return text.phasePending;
  };

  const statusText = (phase, currentPhase, phaseCount, attempt) => {
    const text = COPY[activeLocale];
    switch (phase) {
      case "collecting": return text.statusCollecting;
      case "active": return message(text.statusActive, {
        current: currentPhase,
        count: phaseCount,
        attempt: Math.max(1, attempt),
      });
      case "transition": return text.statusTransition;
      case "completed": return text.statusCompleted;
      case "stopping": return text.statusStopping;
      default: return text.statusOff;
    }
  };

  const ensurePhaseSlots = (count) => {
    while (phaseTrack.children.length < count) {
      phaseTrack.append(phaseTemplate.content.cloneNode(true));
    }
    while (phaseTrack.children.length > count) {
      phaseTrack.lastElementChild?.remove();
    }
  };

  const normalizedSnapshot = (state) => {
    const counters = valueObject(state.counters);
    const boss = valueObject(state.boss);
    const conversionState = valueObject(state.conversion);
    const rawPhases = Array.isArray(state.phases) ? state.phases : [];
    const phase = normalizedPhase(state.phase);
    const count = clamp(Math.round(firstNumber(
      state.phaseCount,
      rawPhases.length,
      1,
    )) || 1, 1, 12);
    const current = clamp(Math.round(firstNumber(state.phaseIndex, 1)) || 1, 1, count);
    const reserve = nonnegative(counters.reserveHealth, state.reserveHealth);
    const generated = nonnegative(counters.convertedHealth, state.convertedHealth);
    const spent = nonnegative(counters.spentHealth, state.spentHealth);
    const taps = nonnegative(counters.totalTaps, state.totalTaps);
    const banked = nonnegative(counters.bankedTaps, state.bankedTaps);
    const unconverted = nonnegative(counters.unconvertedTaps, state.unconvertedTaps,
      state.remainderTaps);
    const tapsPerPoint = Math.max(1, firstNumber(
      conversionState.tapsPerHealthPoint,
      state.tapsPerHealthPoint,
      2,
    ));
    const totalBossHealth = nonnegative(boss.totalHealth, state.totalHealth);
    const hasCurrentBossHealth = (
      boss.currentHealth !== null && boss.currentHealth !== undefined &&
      boss.currentHealth !== ""
    ) || (
      state.currentHealth !== null && state.currentHealth !== undefined &&
      state.currentHealth !== ""
    );
    const bossProgress = progressValue(boss.progress, state.bossProgress,
      totalBossHealth > 0 && hasCurrentBossHealth
        ? 1 - (nonnegative(boss.currentHealth, state.currentHealth) / totalBossHealth)
        : 0);

    const phases = Array.from({ length: count }, (_, offset) => {
      const index = offset + 1;
      const source = valueObject(rawPhases[offset]);
      const sourceStatus = normalizedPhaseStatus(source.status, index, current, phase);
      const fallbackProgress = sourceStatus === "complete"
        ? 1
        : sourceStatus === "active" ? bossProgress : 0;
      return {
        index,
        label: String(source.label || message(COPY[activeLocale].phase, { index })),
        status: sourceStatus,
        progress: sourceStatus === "complete"
          ? 1
          : sourceStatus === "pending"
            ? 0
            : progressValue(source.progress, fallbackProgress),
      };
    });

    const calculatedOverall = phases.reduce((sum, item) => sum + item.progress, 0) / count;
    const hasOverall = state.overallProgress !== null &&
      state.overallProgress !== undefined && state.overallProgress !== "" &&
      Number.isFinite(Number(state.overallProgress));

    return {
      phase,
      bossName: String(state.bossName || boss.name || "").trim(),
      attempt: Math.max(0, Math.round(firstNumber(state.attempt, 0))),
      phaseIndex: current,
      phaseCount: count,
      overallProgress: hasOverall
        ? progressValue(state.overallProgress)
        : calculatedOverall,
      phases,
      totalTaps: taps,
      bankedTaps: banked,
      unconvertedTaps: unconverted,
      convertedHealth: generated,
      reserveHealth: reserve,
      spentHealth: spent,
      tapsPerHealthPoint: tapsPerPoint,
      totalBossHealth,
    };
  };

  const render = (state, context = {}) => {
    if (!state || typeof state !== "object") return;
    applyLocale(normalizeLocale(state.locale) || queryLocale || "es");
    const text = COPY[activeLocale];
    const snapshot = normalizedSnapshot(state);
    const visible = snapshot.phase !== "off";
    const phasePercent = Math.round(snapshot.overallProgress * 100);
    const phaseHealth = snapshot.totalBossHealth > 0
      ? snapshot.totalBossHealth / snapshot.phaseCount
      : Math.max(1, snapshot.convertedHealth || snapshot.reserveHealth);
    const reserveRatio = snapshot.reserveHealth / Math.max(1, phaseHealth);
    const reserveWidth = clamp(reserveRatio, 0, 1);
    const baseLifeRatio = snapshot.totalBossHealth > 0
      ? snapshot.reserveHealth / snapshot.totalBossHealth
      : 0;
    const remainder = snapshot.tapsPerHealthPoint > 0
      ? snapshot.unconvertedTaps % snapshot.tapsPerHealthPoint
      : 0;
    const remainderRatio = remainder / snapshot.tapsPerHealthPoint;

    root.dataset.phase = snapshot.phase;
    root.dataset.visible = String(visible);
    root.style.setProperty("--phase-count", String(snapshot.phaseCount));
    bossName.textContent = snapshot.bossName ||
      (snapshot.phase === "collecting" ? text.nextBoss : text.currentBoss);
    status.textContent = statusText(
      snapshot.phase,
      snapshot.phaseIndex,
      snapshot.phaseCount,
      snapshot.attempt,
    );
    previewBadge.hidden = !context.preview;
    phaseLabel.textContent = snapshot.phase === "collecting" && !snapshot.bossName
      ? text.preparing
      : message(text.phaseOf, {
        current: snapshot.phaseIndex,
        count: snapshot.phaseCount,
      });
    overallProgress.textContent = `${phasePercent}%`;
    phaseTrack.setAttribute(
      "aria-label",
      message(text.overallAria, {
        percent: phasePercent,
        current: snapshot.phaseIndex,
        count: snapshot.phaseCount,
      }),
    );

    ensurePhaseSlots(snapshot.phaseCount);
    snapshot.phases.forEach((item, offset) => {
      const slot = phaseTrack.children[offset];
      const itemProgress = clamp(item.progress, 0, 1);
      slot.dataset.status = item.status;
      slot.style.setProperty("--phase-progress", `${itemProgress * 100}%`);
      slot.style.setProperty(
        "--phase-reserve",
        item.status === "active" ? `${reserveWidth * 100}%` : "0%",
      );
      slot.querySelector(".tap-phase__number").textContent = String(item.index);
      slot.querySelector(".tap-phase__status").textContent =
        phaseStatusText(item.status, snapshot.phase);
      slot.querySelector(".tap-phase__percent").textContent = `${Math.round(itemProgress * 100)}%`;
      slot.title = `${item.label}: ${Math.round(itemProgress * 100)}%`;
    });

    reserveCard.dataset.active = String(snapshot.reserveHealth > 0 || snapshot.bankedTaps > 0);
    reserveCard.dataset.overflow = String(reserveRatio > 1);
    reserveCard.style.setProperty("--reserve-progress", `${reserveWidth * 100}%`);
    reserveFill.style.setProperty("--reserve-progress", `${reserveWidth * 100}%`);
    reserveHealth.textContent = `+${format(snapshot.reserveHealth)} ${
      snapshot.reserveHealth === 1 ? text.point : text.points
    }`;
    bankedTaps.textContent = format(snapshot.bankedTaps);
    bankedLabel.textContent = snapshot.bankedTaps === 1
      ? text.tapWaiting
      : text.tapsWaiting;
    reserveEquivalence.textContent = snapshot.reserveHealth <= 0
      ? text.noReserve
      : snapshot.totalBossHealth <= 0
        ? message(snapshot.reserveHealth === 1 ? text.pointReady : text.pointsReady, {
          value: format(snapshot.reserveHealth),
        })
        : baseLifeRatio >= 1
          ? message(text.timesBase, { value: numberFormat.format(baseLifeRatio) })
          : message(text.percentBase, { value: percentFormat.format(baseLifeRatio) });

    totalTaps.textContent = format(snapshot.totalTaps);
    convertedHealth.textContent = format(snapshot.convertedHealth);
    spentHealth.textContent = format(snapshot.spentHealth);
    conversion.textContent = format(snapshot.tapsPerHealthPoint);
    conversionEquation.textContent = snapshot.tapsPerHealthPoint === 1
      ? text.conversionOne
      : text.conversionMany;
    conversionProgress.style.setProperty("--conversion-progress", `${remainderRatio * 100}%`);
    conversionRemainder.textContent = message(text.remainder, {
      current: format(remainder),
      total: format(snapshot.tapsPerHealthPoint),
    });
  };

  window.LiveEventOverlayRuntime.create({
    overlay: "tap-farming",
    endpoint: "/api/config/tap-farming",
    interval: 500,
    render,
    initialLiveState: {
      revision: 0,
      phase: "off",
      phaseIndex: 1,
      phaseCount: 1,
      phases: [],
      conversion: { tapsPerHealthPoint: 2 },
      counters: {},
      boss: {},
    },
    initialPreviewState: {
      revision: 1,
      phase: "active",
      locale: activeLocale,
      bossName: activeLocale === "en" ? "Captain Brineybeard" : "Capitán Barbasalada",
      levelId: "level_pirate",
      attempt: 2,
      conversion: { tapsPerHealthPoint: 2 },
      counters: {
        totalTaps: 12486,
        bankedTaps: 328,
        unconvertedTaps: 1,
        convertedHealth: 6243,
        reserveHealth: 1842,
        spentHealth: 4401,
      },
      boss: {
        currentHealth: 752,
        totalHealth: 3000,
        progress: 0.68,
      },
      phaseIndex: 2,
      phaseCount: 3,
      overallProgress: 0.56,
      phases: [
        { index: 1, status: "complete", progress: 1 },
        { index: 2, status: "active", progress: 0.68 },
        { index: 3, status: "pending", progress: 0 },
      ],
    },
  });
})();
