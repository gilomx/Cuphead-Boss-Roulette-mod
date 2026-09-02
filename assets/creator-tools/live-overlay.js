(() => {
  "use strict";

  const DESIGN_MESSAGE = "creator-tools-overlay-composer-design";
  const DESIGN_READY_MESSAGE = "creator-tools-overlay-composer-ready";
  const CHILD_STATE_MESSAGE = "creator-tools-overlay-preview";
  const CHILD_READY_MESSAGE = "creator-tools-overlay-preview-ready";
  const MESSAGE_VERSION = 1;
  const DATA_POLL_INTERVAL = 500;
  const PREVIEW_POLL_INTERVAL = 100;
  const FETCH_TIMEOUT = 3500;

  const root = document.getElementById("live-overlay");
  const query = new URLSearchParams(window.location.search);
  const designer = switchValue(query.get("designer"));
  const designerBackground = normalizeDesignerBackground(query.get("background"));
  const queryLocale = normalizeLocale(query.get("locale"));
  const profileId = resolveProfileId();
  const ownOrigin = window.location.origin;

  const registry = Object.freeze({
    tap_farming: Object.freeze({
      id: "tap_farming",
      overlay: "tap-farming",
      endpoint: "/api/config/tap-farming",
      src: "/tap-farming-overlay",
    }),
    pesky_battle: Object.freeze({
      id: "pesky_battle",
      overlay: "pesky-battle",
      endpoint: "/api/config/pesky-battle",
      src: "/pesky-battle-overlay",
    }),
  });

  const components = new Map();
  let savedProfile = defaultProfile(profileId);
  let activeProfile = savedProfile;
  let realStates = {
    tap_farming: initialTapState(),
    pesky_battle: initialPeskyState(),
  };
  let previewState = { active: false };
  let previewGeneration = 0;
  let activeLocale = queryLocale || "es";
  let dataPollPending = false;
  let previewPollPending = false;
  let disposed = false;
  let dataTimer = 0;
  let previewTimer = 0;

  function switchValue(value) {
    const normalized = String(value || "").trim().toLowerCase();
    return normalized === "1" || normalized === "true" || normalized === "on";
  }

  function finiteNumber(value, fallback = 0) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  function clamp(value, minimum, maximum) {
    return Math.min(maximum, Math.max(minimum, value));
  }

  function normalizeLocale(value) {
    const locale = String(value || "").trim().toLowerCase();
    if (locale === "en" || locale.startsWith("en-")) return "en";
    if (locale === "es" || locale.startsWith("es-")) return "es";
    return "";
  }

  function normalizeDesignerBackground(value) {
    const background = String(value || "").trim().toLowerCase();
    return background === "light" || background === "dark" ? background : "alpha";
  }

  function normalizeComponentId(value) {
    const id = String(value || "").trim().toLowerCase().replaceAll("-", "_");
    if (id === "tap_farming" || id === "pesky_battle") return id;
    return "";
  }

  function normalizeHexColor(value, fallback) {
    const color = String(value || "").trim().toLowerCase();
    return /^#[0-9a-f]{6}$/.test(color) ? color : fallback;
  }

  function resolveProfileId() {
    const requested = String(query.get("profile") || "").trim().toLowerCase();
    if (requested === "vertical" || requested === "horizontal") return requested;
    const segments = window.location.pathname.toLowerCase().split("/").filter(Boolean);
    return segments.includes("vertical") ? "vertical" : "horizontal";
  }

  function defaultProfile(id) {
    const vertical = id === "vertical";
    const placements = vertical
      ? {
          tap_farming: { x: 220, y: 1010, width: 640, height: 720, layer: 20 },
          pesky_battle: { x: 60, y: 1260, width: 960, height: 560, layer: 10 },
        }
      : {
          tap_farming: { x: 1290, y: 430, width: 570, height: 570, layer: 20 },
          pesky_battle: { x: 80, y: 720, width: 1760, height: 300, layer: 10 },
        };
    return {
      id,
      canvas: vertical
        ? { width: 1080, height: 1920 }
        : { width: 1920, height: 1080 },
      components: Object.keys(registry).map((componentId, index) => ({
        id: componentId,
        ...placements[componentId],
        enabled: true,
        locked: false,
        layer: placements[componentId].layer ?? 20 + index,
        opacity: 100,
        variant: "default",
        showTitle: componentId !== "tap_farming",
        showDetails: componentId !== "tap_farming",
        motion: true,
        liquidColor: "#ff4f92",
        collectingColor: "#f4c95d",
        textColor: "#ffffff",
        outlineColor: "#f5f5f7",
      })),
    };
  }

  function profileFrom(value) {
    if (!value || typeof value !== "object") return null;
    if (Array.isArray(value.profiles)) {
      const match = value.profiles.find((candidate) => (
        String(candidate?.id || "").trim().toLowerCase() === profileId
      ));
      return match && typeof match === "object" ? match : null;
    }
    const candidateId = String(value.id || value.profileId || "").trim().toLowerCase();
    if (candidateId && candidateId !== profileId) return null;
    return value;
  }

  function normalizedProfile(value) {
    const fallback = defaultProfile(profileId);
    const source = profileFrom(value);
    if (!source) return fallback;
    const canvasSource = source.canvas && typeof source.canvas === "object"
      ? source.canvas
      : {};
    const canvas = {
      width: Math.max(1, finiteNumber(canvasSource.width, fallback.canvas.width)),
      height: Math.max(1, finiteNumber(canvasSource.height, fallback.canvas.height)),
    };
    const sourceComponents = Array.isArray(source.components) ? source.components : [];
    const byId = new Map(sourceComponents.map((component) => [
      normalizeComponentId(component?.id),
      component,
    ]));
    return {
      id: profileId,
      canvas,
      components: fallback.components.map((defaultComponent) => {
        const component = byId.get(defaultComponent.id);
        return component && typeof component === "object"
          ? { ...defaultComponent, ...component, id: defaultComponent.id }
          : defaultComponent;
      }),
    };
  }

  function coordinatePercent(value, axisLength, fallback) {
    const parsed = finiteNumber(value, fallback);
    return clamp(axisLength > 0 ? parsed / axisLength * 100 : 0, 0, 100);
  }

  function presentationFor(component) {
    return {
      variant: "default",
      showTitle: component.showTitle !== false,
      showDetails: component.showDetails !== false,
      motion: component.motion !== false,
      liquidColor: normalizeHexColor(component.liquidColor, "#ff4f92"),
      collectingColor: normalizeHexColor(component.collectingColor, "#f4c95d"),
      textColor: normalizeHexColor(component.textColor, "#ffffff"),
      outlineColor: normalizeHexColor(component.outlineColor, "#f5f5f7"),
    };
  }

  function componentConfig(id) {
    return activeProfile.components.find((component) => component.id === id) ||
      defaultProfile(profileId).components.find((component) => component.id === id);
  }

  function createComponents() {
    for (const definition of Object.values(registry)) {
      const host = document.createElement("section");
      host.className = "live-overlay__component";
      host.dataset.componentId = definition.id;
      host.dataset.enabled = "true";
      host.dataset.soloHidden = "false";

      const frame = document.createElement("iframe");
      frame.title = definition.id;
      frame.tabIndex = -1;
      frame.setAttribute("aria-hidden", "true");
      frame.setAttribute("scrolling", "no");
      frame.src = embeddedUrl(definition);
      frame.addEventListener("load", () => postComponentState(definition.id));
      host.append(frame);
      root.append(host);
      components.set(definition.id, { definition, host, frame, ready: false });
    }
  }

  function embeddedUrl(definition) {
    const params = new URLSearchParams({ embedded: "1" });
    if (activeLocale === "en") params.set("locale", "en");
    return `${definition.src}?${params}`;
  }

  function applyProfile(profile, selectedId = "") {
    activeProfile = normalizedProfile(profile);
    root.dataset.profile = profileId;
    root.style.setProperty("--canvas-width", String(activeProfile.canvas.width));
    root.style.setProperty("--canvas-height", String(activeProfile.canvas.height));
    const solo = normalizeComponentId(selectedId);

    for (const [id, entry] of components) {
      const config = componentConfig(id);
      const x = coordinatePercent(config.x, activeProfile.canvas.width, 0);
      const y = coordinatePercent(config.y, activeProfile.canvas.height, 0);
      const width = coordinatePercent(
        config.width,
        activeProfile.canvas.width,
        activeProfile.canvas.width,
      );
      const height = coordinatePercent(
        config.height,
        activeProfile.canvas.height,
        activeProfile.canvas.height,
      );
      entry.host.style.setProperty("--component-x", `${x}%`);
      entry.host.style.setProperty("--component-y", `${y}%`);
      entry.host.style.setProperty("--component-width", `${Math.max(0.1, width)}%`);
      entry.host.style.setProperty("--component-height", `${Math.max(0.1, height)}%`);
      entry.host.style.setProperty(
        "--component-opacity",
        String(clamp(finiteNumber(config.opacity, 100), 0, 100) / 100),
      );
      entry.host.style.zIndex = String(Math.round(finiteNumber(config.layer, 1)));
      const forceDesignerVisible = Boolean(designer && solo && solo === id);
      entry.host.dataset.enabled = String(forceDesignerVisible || config.enabled !== false);
      entry.host.dataset.configEnabled = String(config.enabled !== false);
      entry.host.dataset.locked = String(config.locked === true);
      entry.host.dataset.variant = presentationFor(config).variant;
      entry.host.dataset.soloHidden = String(Boolean(solo && solo !== id));
    }
  }

  function phaseItems(count, current, progress, eventPhase) {
    return Array.from({ length: count }, (_, offset) => {
      const index = offset + 1;
      const complete = eventPhase === "completed" || index < current;
      const active = !complete && index === current;
      return {
        index,
        status: complete ? "complete" : active ? "active" : "pending",
        progress: complete ? 1 : active ? progress : 0,
      };
    });
  }

  function previewTapSnapshot(preview) {
    const scenario = String(preview.scenario || preview.phase || "active")
      .trim().toLowerCase();
    const phase = ["off", "collecting", "active", "transition", "completed", "stopping"]
      .includes(scenario) ? scenario : "active";
    const phaseCount = clamp(Math.round(finiteNumber(preview.phaseCount, 4)), 1, 12);
    const phaseIndex = clamp(Math.round(finiteNumber(preview.phaseIndex, 2)), 1, phaseCount);
    const overall = clamp(finiteNumber(preview.overallProgress, 0.43), 0, 1);
    const phaseProgress = clamp(
      finiteNumber(preview.phaseProgress, overall * phaseCount - (phaseIndex - 1)),
      0,
      1,
    );
    const totalHealth = phase === "collecting"
      ? 0
      : Math.max(1, finiteNumber(preview.totalHealth, 3000));
    const currentHealth = clamp(
      finiteNumber(preview.currentHealth, totalHealth * (1 - phaseProgress)),
      0,
      totalHealth,
    );
    const reserveHealth = Math.max(0, finiteNumber(preview.reserveHealth, 1150));
    const spentHealth = Math.max(0, finiteNumber(preview.spentHealth, 5190));
    const totalTaps = Math.max(0, Math.round(finiteNumber(preview.totalTaps, 12680)));
    const tapsPerConversion = Math.max(1, Math.round(finiteNumber(
      preview.tapsPerConversion,
      finiteNumber(preview.tapsPerHealthPoint, 2),
    )));
    const healthPointsPerConversion = Math.max(1, Math.round(finiteNumber(
      preview.healthPointsPerConversion,
      1,
    )));
    const effectiveAvailable = phase !== "collecting" && totalHealth > 0;
    const effectiveTotal = effectiveAvailable
      ? totalHealth + reserveHealth + spentHealth
      : 0;
    const effectiveCurrent = phase === "completed"
      ? 0
      : effectiveAvailable
        ? Math.min(effectiveTotal, currentHealth + reserveHealth)
        : reserveHealth;
    return {
      ready: true,
      schemaVersion: 1,
      revision: Date.now(),
      sessionId: String(
        preview.overlayPreviewKey || preview.sessionId || `preview-${profileId}`,
      ),
      phase,
      bossName: phase === "collecting"
        ? ""
        : String(preview.bossName || (activeLocale === "en"
          ? "Captain Brineybeard"
          : "Capitán Barbasalada")),
      levelId: String(preview.levelId || "preview-level"),
      attempt: Math.max(0, Math.round(finiteNumber(preview.attempt, 2))),
      tapDelta: Math.max(0, Math.round(finiteNumber(preview.tapDelta, 250))),
      damageDelta: Math.max(0, finiteNumber(preview.damageDelta, 0)),
      conversion: {
        tapsPerConversion,
        healthPointsPerConversion,
        tapsPerHealthPoint: tapsPerConversion / healthPointsPerConversion,
      },
      counters: {
        totalTaps,
        bankedTaps: Math.max(0, Math.round(finiteNumber(
          preview.bankedTaps,
          reserveHealth * tapsPerConversion / healthPointsPerConversion,
        ))),
        unconvertedTaps: Math.max(0, finiteNumber(preview.unconvertedTaps, 0)),
        convertedHealth: Math.max(0, finiteNumber(preview.convertedHealth, 6340)),
        reserveHealth,
        spentHealth,
      },
      boss: {
        currentHealth,
        totalHealth,
        progress: phaseProgress,
      },
      effectiveHealth: {
        available: effectiveAvailable,
        current: effectiveCurrent,
        total: effectiveTotal,
        ratio: effectiveAvailable && effectiveTotal > 0
          ? clamp(effectiveCurrent / effectiveTotal, 0, 1)
          : 0,
      },
      phaseIndex,
      phaseCount,
      overallProgress: overall,
      phases: phaseItems(phaseCount, phaseIndex, phaseProgress, phase),
    };
  }

  function previewParticipants(preview, count, capacity) {
    const supplied = Array.isArray(preview.participants) ? preview.participants : [];
    const fallbackNames = activeLocale === "en"
      ? ["La Pichi", "Mugman", "Ms. Chalice", "CupFan", "Dice King"]
      : ["La Pichi", "Mugman", "Srita. Cáliz", "CupFan", "Rey Dado"];
    return Array.from({ length: Math.min(count, capacity) }, (_, index) => {
      const participant = supplied[index] && typeof supplied[index] === "object"
        ? supplied[index]
        : {};
      return {
        slot: index + 1,
        userId: String(participant.userId || `preview-${index + 1}`),
        userName: String(participant.userName || `preview${index + 1}`),
        displayName: String(participant.displayName || fallbackNames[index % fallbackNames.length]),
        avatarUrl: String(participant.avatarUrl || ""),
        joinedAt: String(participant.joinedAt || ""),
      };
    });
  }

  function previewPeskySnapshot(preview) {
    const capacity = clamp(Math.round(finiteNumber(preview.capacity, 5)), 1, 5);
    const requestedCount = Math.round(finiteNumber(preview.participantCount, 3));
    const count = clamp(requestedCount, 0, capacity);
    const scenario = String(preview.scenario || preview.phase || "recruiting")
      .trim().toLowerCase();
    const phase = ["off", "recruiting", "ready", "waiting_level", "active", "won", "stopping"]
      .includes(scenario) ? scenario : "recruiting";
    return {
      ready: true,
      schemaVersion: 1,
      revision: Date.now(),
      phase,
      sessionId: Math.max(1, Math.round(finiteNumber(preview.sessionId, 1))),
      attempt: Math.max(0, Math.round(finiteNumber(preview.attempt, 2))),
      capacity,
      trigger: {
        giftId: String(preview.giftId || "preview-gift"),
        giftName: String(preview.giftName || (activeLocale === "en"
          ? "Entry gift"
          : "Regalo de entrada")),
        giftImagePath: String(preview.giftImagePath || preview.giftImageUrl || ""),
      },
      participants: previewParticipants(preview, count, capacity),
    };
  }

  function initialTapState() {
    return {
      revision: 0,
      sessionId: 0,
      phase: "off",
      phaseIndex: 1,
      phaseCount: 1,
      phases: [],
      counters: {},
      boss: {},
    };
  }

  function initialPeskyState() {
    return { revision: 0, phase: "off", participants: [], capacity: 5 };
  }

  function stateFor(id) {
    if (previewState?.active && previewState.simulationActive === true &&
        normalizeComponentId(previewState.componentId) === id) {
      return id === "tap_farming"
        ? previewTapSnapshot(previewState)
        : previewPeskySnapshot(previewState);
    }
    return realStates[id] || (id === "tap_farming" ? initialTapState() : initialPeskyState());
  }

  function acceptPreviewState(value) {
    const previousRevision = Math.max(
      0,
      Math.round(finiteNumber(previewState.revision, 0)),
    );
    const previousSessionId = String(previewState.sessionId || "");
    const previousActive = previewState.active === true;
    const previousSimulationActive = previewState.simulationActive === true;
    const next = value && typeof value === "object" ? value : { active: false };
    const active = next.active === true;
    const componentId = normalizeComponentId(next.componentId);
    const previousComponentId = normalizeComponentId(previewState.componentId);
    const runId = Math.max(0, Math.round(finiteNumber(next.runId, 0)));
    const previousRunId = Math.max(
      0,
      Math.round(finiteNumber(previewState.runId, 0)),
    );
    if (active && (!previewState.active || componentId !== previousComponentId ||
        (runId > 0 && runId !== previousRunId))) {
      previewGeneration += 1;
    }
    previewState = active
      ? {
          ...next,
          overlayPreviewKey: `composer-${profileId}-${runId || previewGeneration}`,
        }
      : next;
    return previousRevision !== Math.max(
      0,
      Math.round(finiteNumber(previewState.revision, 0)),
    ) || previousSessionId !== String(previewState.sessionId || "") ||
      previousActive !== (previewState.active === true) ||
      previousSimulationActive !== (previewState.simulationActive === true);
  }

  function previewExpired(value) {
    if (!value?.active || !value.expiresAtUtc) return false;
    const expiresAt = Date.parse(value.expiresAtUtc);
    return Number.isFinite(expiresAt) && expiresAt <= Date.now();
  }

  function stateWithPresentation(id, state) {
    const config = componentConfig(id);
    return {
      ...(state && typeof state === "object" ? state : {}),
      locale: activeLocale,
      presentation: presentationFor(config),
    };
  }

  function postComponentState(id) {
    const entry = components.get(id);
    if (!entry?.frame.contentWindow) return;
    entry.frame.contentWindow.postMessage({
      type: CHILD_STATE_MESSAGE,
      version: MESSAGE_VERSION,
      overlay: entry.definition.overlay,
      state: stateWithPresentation(id, stateFor(id)),
    }, ownOrigin === "null" ? "*" : ownOrigin);
  }

  function render(selectedId = "") {
    const selected = normalizeComponentId(selectedId || (
      previewState?.active && previewState.simulationActive === true
        ? previewState.componentId
        : ""
    ));
    applyProfile(activeProfile, selected);
    for (const id of components.keys()) postComponentState(id);
  }

  async function fetchJson(url) {
    const controller = typeof AbortController === "function"
      ? new AbortController()
      : null;
    const timeout = window.setTimeout(() => controller?.abort(), FETCH_TIMEOUT);
    try {
      const response = await fetch(url, {
        cache: "no-store",
        ...(controller ? { signal: controller.signal } : {}),
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const state = await response.json();
      return state && typeof state === "object" ? state : {};
    } finally {
      window.clearTimeout(timeout);
    }
  }

  function syncActiveProfile() {
    activeProfile = normalizedProfile(
      previewState?.active && previewState.layout
        ? previewState.layout
        : savedProfile,
    );
  }

  async function pollData() {
    if (designer || dataPollPending || disposed) return;
    dataPollPending = true;
    try {
      const results = await Promise.allSettled([
        fetchJson("/api/overlay-composer/config"),
        fetchJson(registry.tap_farming.endpoint),
        fetchJson(registry.pesky_battle.endpoint),
      ]);
      if (results[0].status === "fulfilled") {
        savedProfile = normalizedProfile(results[0].value);
      }
      if (results[1].status === "fulfilled") realStates.tap_farming = results[1].value;
      if (results[2].status === "fulfilled") realStates.pesky_battle = results[2].value;
      syncActiveProfile();
      render();
    } finally {
      dataPollPending = false;
    }
  }

  async function pollPreview() {
    if (designer || previewPollPending || disposed) return;
    previewPollPending = true;
    try {
      let changed = false;
      try {
        changed = acceptPreviewState(await fetchJson(
          `/api/overlay-composer/preview?profile=${encodeURIComponent(profileId)}`,
        ));
        activeLocale = queryLocale || normalizeLocale(previewState.locale) || activeLocale;
      } catch {
        if (previewExpired(previewState)) {
          changed = acceptPreviewState({ active: false });
        }
      }
      if (changed) {
        syncActiveProfile();
        render();
      }
    } finally {
      previewPollPending = false;
    }
  }

  function receiveMessage(event) {
    if (event.source !== window.parent &&
        !Array.from(components.values()).some((entry) => event.source === entry.frame.contentWindow)) {
      return;
    }
    if (event.origin !== ownOrigin && !(ownOrigin === "null" && event.origin === "null")) return;
    const message = event.data;
    if (!message || typeof message !== "object") return;

    if (message.type === CHILD_READY_MESSAGE && Number(message.version) === MESSAGE_VERSION) {
      const id = Object.values(registry).find((definition) => (
        definition.overlay === message.overlay
      ))?.id;
      const entry = id ? components.get(id) : null;
      if (entry) {
        entry.ready = true;
        postComponentState(id);
      }
      return;
    }

    if (!designer || event.source !== window.parent ||
        message.type !== DESIGN_MESSAGE ||
        Number(message.version) !== MESSAGE_VERSION) return;
    const incomingProfileId = String(message.profileId || profileId).trim().toLowerCase();
    if (incomingProfileId !== profileId) return;
    activeLocale = normalizeLocale(message.locale) || queryLocale || activeLocale;
    root.dataset.designerBackground = normalizeDesignerBackground(message.background);
    activeProfile = normalizedProfile(message.profile);
    const states = message.states && typeof message.states === "object" ? message.states : {};
    realStates = {
      tap_farming: states.tap_farming && typeof states.tap_farming === "object"
        ? states.tap_farming
        : previewTapSnapshot({ scenario: "active" }),
      pesky_battle: states.pesky_battle && typeof states.pesky_battle === "object"
        ? states.pesky_battle
        : previewPeskySnapshot({ scenario: "recruiting" }),
    };
    previewState = { active: false };
    render(message.selectedComponentId);
  }

  function announceReady() {
    if (!designer || window.parent === window) return;
    window.parent.postMessage({
      type: DESIGN_READY_MESSAGE,
      version: MESSAGE_VERSION,
      profileId,
    }, ownOrigin === "null" ? "*" : ownOrigin);
  }

  function dispose() {
    disposed = true;
    if (dataTimer) window.clearInterval(dataTimer);
    if (previewTimer) window.clearInterval(previewTimer);
    window.removeEventListener("message", receiveMessage);
  }

  document.documentElement.lang = activeLocale;
  root.dataset.designerBackground = designer ? designerBackground : "transparent";
  createComponents();
  applyProfile(activeProfile);
  window.addEventListener("message", receiveMessage);
  window.addEventListener("pagehide", dispose, { once: true });

  if (designer) {
    render("tap_farming");
    announceReady();
  } else {
    void pollData();
    void pollPreview();
    dataTimer = window.setInterval(() => void pollData(), DATA_POLL_INTERVAL);
    previewTimer = window.setInterval(
      () => void pollPreview(),
      PREVIEW_POLL_INTERVAL,
    );
  }
})();
