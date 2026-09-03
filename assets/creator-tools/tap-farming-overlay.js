(() => {
  "use strict";

  const root = document.getElementById("tap-farming");
  const liquidLevel = document.getElementById("tap-liquid-level");
  const bodyWave = document.getElementById("tap-body-wave");
  const middleWave = document.getElementById("tap-middle-wave");
  const frontWave = document.getElementById("tap-front-wave");
  const bubbles = Array.from(document.querySelectorAll(".tap-heart__bubble"));
  const bossImage = document.getElementById("tap-boss-image");
  const heart = document.querySelector(".tap-heart");
  const percent = document.getElementById("tap-percent");
  const healthRemaining = document.getElementById("tap-health-remaining");
  const delta = document.getElementById("tap-delta");
  const heartLabel = document.getElementById("tap-heart-label");
  const collecting = document.getElementById("tap-collecting");
  const collectingConversion = document.getElementById("tap-collecting-conversion");
  const collectingTotal = document.getElementById("tap-collecting-total");
  const collectingCaption = document.getElementById("tap-collecting-caption");
  const collectingHealth = document.getElementById("tap-collecting-health");
  const collectingHealthCaption = document.getElementById("tap-collecting-health-caption");
  const victoryTitle = document.getElementById("tap-victory-title");
  const victoryTapsLabel = document.getElementById("tap-victory-taps-label");
  const victoryTaps = document.getElementById("tap-victory-taps");
  const victoryAttemptsLabel = document.getElementById("tap-victory-attempts-label");
  const victoryAttempts = document.getElementById("tap-victory-attempts");
  const victoryAbsorbedLabel = document.getElementById("tap-victory-absorbed-label");
  const victoryAbsorbed = document.getElementById("tap-victory-absorbed");
  const mainTop = document.getElementById("tap-main-top");
  const mainBottom = document.getElementById("tap-main-bottom");
  const middleTop = document.getElementById("tap-middle-top");
  const middleBottom = document.getElementById("tap-middle-bottom");
  const frontTop = document.getElementById("tap-front-top");
  const frontMiddle = document.getElementById("tap-front-middle");
  const frontBottom = document.getElementById("tap-front-bottom");

  const DEFAULT_COLORS = Object.freeze({
    liquidColor: "#ff4f92",
    collectingColor: "#f4c95d",
    textColor: "#ffffff",
    outlineColor: "#f5f5f7",
  });

  const BOSS_IMAGES = Object.freeze({
    Frogs: "hoscoytosco.png",
    Veggies: "pandillaraiz.png",
    Slime: "goopylegrande.png",
    FlyingBlimp: "hilda.png",
    Flower: "claveldecagney.png",
    Baroness: "baronesa.png",
    FlyingGenie: "djimmi.png",
    Clown: "beppi.png",
    FlyingBird: "titi.png",
    Dragon: "fosforo.png",
    Bee: "abejita.png",
    Mouse: "werner.png",
    Pirate: "capitan.png",
    FlyingMermaid: "calamaria.png",
    SallyStagePlay: "sally.png",
    Robot: "robot.png",
    Train: "expreso.png",
    DicePalace: "dado.png",
    DicePalaceMain: "dado.png",
    Devil: "diablo.png",
    RumRunners: "alimanas.png",
    FlyingCowboy: "vaca.png",
    Airplane: "perritos.png",
    Graveyard: "angelydemonio.png",
    SnowCult: "genovevo.png",
    OldMan: "granito.png",
    Saltbaker: "salero.png",
  });

  const COPY = {
    es: {
      title: "Farmeando taps",
      conversion: "{taps} {tapUnit} = {health} {healthUnit}",
      tapSingular: "TAP",
      tapPlural: "TAPS",
      collectingTaps: "TAPS",
      convertedHealth: "PV CONVERTIDOS",
      roundTaps: "TAPS TOTALES",
      victory: "¡VICTORIA!",
      attempts: "Intentos",
      absorbed: "Vida extra quitada",
      heartAria: "{percent}% de vida; {current} de {total}; {reserve} de reserva",
      heartPendingAria: "100% de vida; esperando datos del jefe; {reserve} de reserva",
      healthPointsShort: "PV",
    },
    en: {
      title: "Tap Farming",
      conversion: "{taps} {tapUnit} = {health} {healthUnit}",
      tapSingular: "TAP",
      tapPlural: "TAPS",
      collectingTaps: "TAPS",
      convertedHealth: "HP CONVERTED",
      roundTaps: "TOTAL TAPS",
      victory: "VICTORY!",
      attempts: "Attempts",
      absorbed: "Extra health removed",
      heartAria: "{percent}% health; {current} of {total}; {reserve} reserve",
      heartPendingAria: "100% health; waiting for boss data; {reserve} reserve",
      healthPointsShort: "HP",
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
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  let activeLocale = queryLocale || "es";
  let numberFormat = new Intl.NumberFormat("es-MX", { maximumFractionDigits: 1 });
  let lastTapSample = null;
  let deltaTimer = 0;
  let bossImageTimer = 0;
  let heartPulseTimer = 0;
  let activeBossImageKey = "";
  let motionEnabled = true;
  let animationFrame = 0;
  let staticMotionApplied = false;

  const message = (template, values = {}) => Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template,
  );

  const objectValue = (value) => value && typeof value === "object" ? value : {};

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

  const normalizeColor = (value, fallback) => {
    const color = String(value || "").trim().toLowerCase();
    return /^#[0-9a-f]{6}([0-9a-f]{2})?$/.test(color) ? color : fallback;
  };

  const colorParts = (value, fallback) => {
    const color = normalizeColor(value, fallback);
    return {
      hex: color.slice(0, 7),
      alpha: color.length === 9
        ? Number.parseInt(color.slice(7, 9), 16) / 255
        : 1,
    };
  };

  const colorCss = (color) => {
    const channels = rgb(color.hex);
    return `rgba(${channels.join(", ")}, ${color.alpha.toFixed(3)})`;
  };

  const rgb = (hex) => [
    Number.parseInt(hex.slice(1, 3), 16),
    Number.parseInt(hex.slice(3, 5), 16),
    Number.parseInt(hex.slice(5, 7), 16),
  ];

  const hex = (channels) => `#${channels.map((channel) => (
    Math.round(clamp(channel, 0, 255)).toString(16).padStart(2, "0")
  )).join("")}`;

  const mix = (base, target, amount) => {
    const from = rgb(base);
    const to = rgb(target);
    return hex(from.map((channel, index) => (
      channel + (to[index] - channel) * amount
    )));
  };

  const applyColors = (presentation) => {
    const liquidColor = colorParts(
      presentation.liquidColor,
      DEFAULT_COLORS.liquidColor,
    );
    const collectingColor = colorParts(
      presentation.collectingColor,
      DEFAULT_COLORS.collectingColor,
    );
    const textColor = colorParts(
      presentation.textColor,
      DEFAULT_COLORS.textColor,
    );
    const outlineColor = colorParts(
      presentation.outlineColor,
      DEFAULT_COLORS.outlineColor,
    );
    root.style.setProperty("--liquid-color", colorCss(liquidColor));
    root.style.setProperty("--collecting-color", colorCss(collectingColor));
    root.style.setProperty("--collecting-fill-color", colorCss({
      hex: collectingColor.hex,
      alpha: collectingColor.alpha * 0.2,
    }));
    root.style.setProperty(
      "--metric-color",
      colorCss(textColor),
    );
    root.style.setProperty("--text-color", colorCss(textColor));
    root.style.setProperty("--outline-color", colorCss(outlineColor));

    const light = mix(liquidColor.hex, "#ffffff", 0.28);
    const dark = mix(liquidColor.hex, "#000000", 0.18);
    const pale = mix(liquidColor.hex, "#ffffff", 0.45);
    const front = mix(liquidColor.hex, "#000000", 0.02);
    mainTop.setAttribute("stop-color", light);
    mainTop.setAttribute("stop-opacity", String(liquidColor.alpha));
    mainBottom.setAttribute("stop-color", dark);
    mainBottom.setAttribute("stop-opacity", String(liquidColor.alpha));
    middleTop.setAttribute("stop-color", pale);
    middleTop.setAttribute("stop-opacity", String(liquidColor.alpha * 0.22));
    middleBottom.setAttribute("stop-color", pale);
    middleBottom.setAttribute("stop-opacity", String(liquidColor.alpha * 0.025));
    frontTop.setAttribute("stop-color", front);
    frontTop.setAttribute("stop-opacity", String(liquidColor.alpha * 0.48));
    frontMiddle.setAttribute("stop-color", front);
    frontMiddle.setAttribute("stop-opacity", String(liquidColor.alpha * 0.18));
    frontBottom.setAttribute("stop-color", front);
    frontBottom.setAttribute("stop-opacity", "0");
  };

  const normalizedPhase = (value) => {
    const phase = String(value || "off").trim().toLowerCase();
    if (["waiting_level", "waiting", "armed"].includes(phase)) return "collecting";
    if (phase === "transitioning") return "transition";
    if (["won", "complete"].includes(phase)) return "completed";
    return ["off", "collecting", "active", "transition", "completed", "stopping"]
      .includes(phase) ? phase : "off";
  };

  const normalizedPresentation = (value) => {
    const presentation = objectValue(value);
    return {
      variant: "default",
      motion: presentation.motion !== false,
      liquidColor: normalizeColor(
        presentation.liquidColor,
        DEFAULT_COLORS.liquidColor,
      ),
      collectingColor: normalizeColor(
        presentation.collectingColor,
        DEFAULT_COLORS.collectingColor,
      ),
      textColor: normalizeColor(
        presentation.textColor,
        DEFAULT_COLORS.textColor,
      ),
      outlineColor: normalizeColor(
        presentation.outlineColor,
        DEFAULT_COLORS.outlineColor,
      ),
    };
  };

  const normalizedSnapshot = (state) => {
    const counters = objectValue(state.counters);
    const boss = objectValue(state.boss);
    const effectiveHealth = objectValue(state.effectiveHealth);
    const conversion = objectValue(state.conversion);
    const rawPhases = Array.isArray(state.phases) ? state.phases : [];
    const phase = normalizedPhase(state.phase);
    const phaseCount = clamp(Math.round(firstNumber(
      state.phaseCount,
      rawPhases.length,
      1,
    )) || 1, 1, 12);
    const phaseIndex = clamp(
      Math.round(firstNumber(state.phaseIndex, 1)) || 1,
      1,
      phaseCount,
    );
    const nativeTotalHealth = nonnegative(boss.totalHealth, state.totalHealth);
    const explicitCurrent = [boss.currentHealth, state.currentHealth]
      .find((value) => value !== null && value !== undefined && value !== "");
    const bossProgress = progressValue(
      boss.progress,
      state.bossProgress,
      state.phaseProgress,
    );
    const nativeCurrentHealth = nativeTotalHealth > 0
      ? clamp(
        explicitCurrent === undefined
          ? nativeTotalHealth * (1 - bossProgress)
          : nonnegative(explicitCurrent),
        0,
        nativeTotalHealth,
      )
      : nonnegative(explicitCurrent);
    const hasEffectiveAvailability = typeof effectiveHealth.available === "boolean";
    const effectiveAvailable = hasEffectiveAvailability
      ? effectiveHealth.available
      : nativeTotalHealth > 0;
    const totalHealth = effectiveAvailable
      ? nonnegative(effectiveHealth.total, nativeTotalHealth)
      : 0;
    const currentHealth = effectiveAvailable
      ? clamp(nonnegative(effectiveHealth.current, nativeCurrentHealth), 0, totalHealth)
      : nonnegative(counters.reserveHealth, state.reserveHealth);
    const effectiveRatio = effectiveAvailable && totalHealth > 0
      ? progressValue(effectiveHealth.ratio, currentHealth / totalHealth)
      : 0;
    const hasOverall = state.overallProgress !== null &&
      state.overallProgress !== undefined &&
      state.overallProgress !== "" &&
      Number.isFinite(Number(state.overallProgress));
    const derivedOverall = phaseCount > 0
      ? clamp((phaseIndex - 1 + bossProgress) / phaseCount, 0, 1)
      : 0;

    return {
      phase,
      phaseCount,
      phaseIndex,
      totalHealth,
      currentHealth,
      effectiveAvailable,
      effectiveRatio,
      reserveHealth: nonnegative(counters.reserveHealth, state.reserveHealth),
      convertedHealth: nonnegative(
        counters.convertedHealth,
        state.convertedHealth,
        nonnegative(counters.reserveHealth, state.reserveHealth) +
          nonnegative(counters.spentHealth, state.spentHealth),
      ),
      spentHealth: nonnegative(counters.spentHealth, state.spentHealth),
      totalTaps: nonnegative(counters.totalTaps, state.totalTaps),
      tapsPerConversion: Math.max(1, Math.round(firstNumber(
        conversion.tapsPerConversion,
        conversion.tapsPerHealthPoint,
        state.tapsPerConversion,
        state.tapsPerHealthPoint,
        1,
      ))),
      healthPointsPerConversion: Math.max(1, Math.round(firstNumber(
        conversion.healthPointsPerConversion,
        state.healthPointsPerConversion,
        1,
      ))),
      overallProgress: hasOverall
        ? progressValue(state.overallProgress)
        : derivedOverall,
      bossName: String(state.bossName || boss.name || "").trim(),
      levelId: String(state.levelId || boss.levelId || "").trim(),
      attempt: Math.max(0, Math.round(firstNumber(state.attempt))),
      sessionKey: String(
        state.overlayPreviewKey ||
        state.sessionId ||
        state.eventSessionId ||
        `${state.levelId || boss.levelId || ""}|${state.attempt || 0}`,
      ),
      presentation: normalizedPresentation(state.presentation),
    };
  };

  const applyLocale = (locale) => {
    activeLocale = locale || "es";
    const text = COPY[activeLocale];
    numberFormat = new Intl.NumberFormat(activeLocale === "en" ? "en-US" : "es-MX", {
      maximumFractionDigits: 1,
    });
    document.documentElement.lang = activeLocale;
    document.title = text.title;
    collectingCaption.textContent = text.collectingTaps;
    collectingHealthCaption.textContent = text.convertedHealth;
    victoryTitle.textContent = text.victory;
    victoryTapsLabel.textContent = text.roundTaps;
    victoryAttemptsLabel.textContent = text.attempts;
    victoryAbsorbedLabel.textContent = text.absorbed;
  };

  const showTapDelta = (value) => {
    if (value <= 0) return;
    if (deltaTimer) window.clearTimeout(deltaTimer);
    delta.textContent = `+${format(Math.round(value))} taps`;
    delta.dataset.active = "false";
    void delta.offsetWidth;
    delta.dataset.active = "true";
    deltaTimer = window.setTimeout(() => {
      delta.dataset.active = "false";
      delta.textContent = "";
    }, 1300);
  };

  const pulseActivity = (phase) => {
    if (!motionEnabled || reducedMotion.matches) return;
    const target = phase === "collecting" ? collecting : heart;
    if (!target) return;
    if (heartPulseTimer) window.clearTimeout(heartPulseTimer);
    target.dataset.pulse = "false";
    void target.offsetWidth;
    target.dataset.pulse = "true";
    heartPulseTimer = window.setTimeout(() => {
      target.dataset.pulse = "false";
      heartPulseTimer = 0;
    }, 500);
  };

  const updateTapDelta = (snapshot) => {
    const nextSample = {
      phase: snapshot.phase,
      sessionKey: snapshot.sessionKey,
      total: snapshot.totalTaps,
    };
    const reset = !lastTapSample ||
      lastTapSample.sessionKey !== nextSample.sessionKey ||
      lastTapSample.total > nextSample.total ||
      (lastTapSample.phase === "off" && nextSample.phase !== "off");
    if (!reset && nextSample.total > lastTapSample.total) {
      showTapDelta(nextSample.total - lastTapSample.total);
      pulseActivity(snapshot.phase);
    }
    lastTapSample = nextSample;
  };

  const bossImagePath = (snapshot) => {
    const rawLevel = snapshot.levelId || snapshot.bossName;
    const normalizedLevel = String(rawLevel || "")
      .replace(/^level_/i, "")
      .replace(/^level/i, "");
    const level = Object.keys(BOSS_IMAGES).find(
      (key) => key.toLowerCase() === normalizedLevel.toLowerCase(),
    );
    if (level) return `/assets/bosses/${BOSS_IMAGES[level]}`;
    if (/^dicepalace/i.test(normalizedLevel)) return "/assets/bosses/dado.png";
    return "";
  };

  const updateBossImage = (snapshot) => {
    const eligiblePhase = ["active", "transition"]
      .includes(snapshot.phase);
    const path = eligiblePhase ? bossImagePath(snapshot) : "";
    const key = path
      ? `${snapshot.levelId}|${snapshot.attempt}|${path}`
      : "";
    if (!path) {
      if (bossImageTimer) window.clearTimeout(bossImageTimer);
      bossImageTimer = 0;
      activeBossImageKey = "";
      bossImage.dataset.visible = "false";
      bossImage.removeAttribute("href");
      return false;
    }
    if (key === activeBossImageKey) return true;

    if (bossImageTimer) window.clearTimeout(bossImageTimer);
    activeBossImageKey = key;
    bossImage.dataset.visible = "false";
    bossImage.setAttribute("href", path);
    bossImageTimer = window.setTimeout(() => {
      bossImageTimer = 0;
      if (activeBossImageKey !== key) return;
      bossImage.dataset.visible = "true";
    }, 300);
    return true;
  };

  const render = (state, context = {}) => {
    if (!state || typeof state !== "object") return;
    applyLocale(normalizeLocale(state.locale) || queryLocale || "es");
    const text = COPY[activeLocale];
    const snapshot = normalizedSnapshot(state);
    const presentation = snapshot.presentation;
    const visible = snapshot.phase !== "off";
    const heartPhase = ["active", "transition"].includes(snapshot.phase);
    const pendingBossHealth = heartPhase && !snapshot.effectiveAvailable;
    const healthRatio = pendingBossHealth ? 1 : snapshot.effectiveRatio;
    const healthPercent = Math.round(healthRatio * 100);
    const collectedPoints = format(snapshot.reserveHealth);
    const metricValue = `${healthPercent}%`;

    root.dataset.phase = snapshot.phase;
    root.dataset.valueMode = "percent";
    root.dataset.healthPending = String(pendingBossHealth);
    root.dataset.visible = String(visible);
    root.dataset.variant = presentation.variant;
    root.dataset.motion = String(presentation.motion);
    root.setAttribute("aria-hidden", String(!visible));
    motionEnabled = presentation.motion;
    applyColors(presentation);
    root.style.setProperty("--metric-font-size", "34px");

    liquidLevel.style.transform = `translateY(${258 - healthRatio * 245}px)`;
    const hasBossImage = updateBossImage(snapshot);
    percent.setAttribute("y", hasBossImage ? "178" : "148");
    percent.textContent = metricValue;
    healthRemaining.setAttribute("y", hasBossImage ? "206" : "176");
    healthRemaining.textContent = pendingBossHealth
      ? ""
      : `${format(snapshot.currentHealth)} ${text.healthPointsShort}`;
    collectingTotal.textContent = format(snapshot.totalTaps);
    collectingHealth.textContent = format(snapshot.convertedHealth);
    collectingConversion.textContent = message(text.conversion, {
      taps: format(snapshot.tapsPerConversion),
      tapUnit: snapshot.tapsPerConversion === 1 ? text.tapSingular : text.tapPlural,
      health: format(snapshot.healthPointsPerConversion),
      healthUnit: text.healthPointsShort,
    });
    victoryTaps.textContent = format(snapshot.totalTaps);
    victoryAttempts.textContent = format(Math.max(1, snapshot.attempt));
    victoryAbsorbed.textContent = `${format(snapshot.spentHealth)} ${text.healthPointsShort}`;
    heartLabel.textContent = pendingBossHealth
      ? message(text.heartPendingAria, { reserve: collectedPoints })
      : message(text.heartAria, {
        percent: healthPercent,
        current: format(snapshot.currentHealth),
        total: format(snapshot.totalHealth),
        reserve: collectedPoints,
      });
    updateTapDelta(snapshot);
    ensureAnimation();
  };

  const bubbleSeeds = bubbles.map((bubble, index) => ({
    bubble,
    cycle: Math.max(16, Number(bubble.getAttribute("cy")) - 22),
    offset: index * 19.7,
    speed: 0.018 + index % 5 * 0.004,
    sway: 1.4 + index % 3,
  }));

  const applyStaticMotion = () => {
    if (staticMotionApplied) return;
    bodyWave.style.transform = "";
    middleWave.style.transform = "";
    frontWave.style.transform = "";
    for (const seed of bubbleSeeds) {
      seed.bubble.style.transform = "";
      seed.bubble.style.opacity = "0.55";
    }
    staticMotionApplied = true;
  };

  const shouldAnimate = () => motionEnabled &&
    !reducedMotion.matches &&
    root.dataset.visible === "true" &&
    !document.hidden;

  const animate = (now) => {
    animationFrame = 0;
    if (!shouldAnimate()) {
      applyStaticMotion();
      return;
    }
    staticMotionApplied = false;
    bodyWave.style.transform = `translate3d(${Math.sin(now / 1120) * 28}px, ${Math.sin(now / 610) * 1.4}px, 0)`;
    middleWave.style.transform = `translate3d(${Math.sin(now / 840 + 1.7) * 34}px, ${Math.cos(now / 530) * 1.8}px, 0)`;
    frontWave.style.transform = `translate3d(${Math.sin(now / 640 + 3.2) * 42}px, ${Math.sin(now / 450) * 2.1}px, 0)`;
    for (const seed of bubbleSeeds) {
      const travel = (now * seed.speed + seed.offset) % seed.cycle;
      const life = travel / seed.cycle;
      const x = Math.sin(now / 410 + seed.offset) * seed.sway;
      seed.bubble.style.transform = `translate3d(${x}px, ${-travel}px, 0)`;
      seed.bubble.style.opacity = String(Math.sin(life * Math.PI) * 0.92);
    }
    animationFrame = window.requestAnimationFrame(animate);
  };

  const ensureAnimation = () => {
    if (shouldAnimate()) {
      if (!animationFrame)
        animationFrame = window.requestAnimationFrame(animate);
      return;
    }
    if (animationFrame) window.cancelAnimationFrame(animationFrame);
    animationFrame = 0;
    applyStaticMotion();
  };

  applyLocale(activeLocale);
  applyColors(DEFAULT_COLORS);
  document.addEventListener("visibilitychange", ensureAnimation);
  if (typeof reducedMotion.addEventListener === "function")
    reducedMotion.addEventListener("change", ensureAnimation);
  else if (typeof reducedMotion.addListener === "function")
    reducedMotion.addListener(ensureAnimation);
  ensureAnimation();

  window.LiveEventOverlayRuntime.create({
    overlay: "tap-farming",
    endpoint: "/api/config/tap-farming",
    interval: 500,
    render,
    initialLiveState: {
      revision: 0,
      sessionId: 0,
      phase: "off",
      phaseIndex: 1,
      phaseCount: 1,
      phases: [],
      counters: {},
      boss: {},
    },
    initialPreviewState: {
      revision: 1,
      sessionId: 1,
      phase: "active",
      locale: activeLocale,
      bossName: activeLocale === "en" ? "Captain Brineybeard" : "Capitán Barbasalada",
      levelId: "Pirate",
      attempt: 2,
      counters: {
        totalTaps: 12486,
        reserveHealth: 3842,
        spentHealth: 900,
      },
      boss: {
        currentHealth: 752,
        totalHealth: 3000,
        progress: 0.68,
      },
      effectiveHealth: {
        available: true,
        current: 4594,
        total: 7742,
        ratio: 4594 / 7742,
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
