import { createReadStream, existsSync, readFileSync } from "node:fs";
import { createServer } from "node:http";
import { extname, resolve, sep } from "node:path";

const configuredPort = Number(process.argv[2] ?? process.env.CREATOR_TOOLS_PORT);
const port = Number.isInteger(configuredPort) && configuredPort > 0
  ? configuredPort
  : 18081;
const host = process.argv[3] ?? "127.0.0.1";
const assetsRoot = resolve(process.cwd(), "../assets") + sep;
const giftCatalog = JSON.parse(readFileSync(
  resolve(assetsRoot, "creator-tools/gifts/catalog.json"),
  "utf8",
));
const giftsById = new Map(giftCatalog.gifts.map((gift) => [gift.giftId, gift]));
const selection = { boss: 0, weapon1: 0, weapon2: 1, super: 0, charm: 0, modifier: 0 };
let enabled = false;
let interactionRevision = 0;
let interactionFeedback = "ready";
let interactionLastItem = "";
let interactionNextId = 1;
let interactionQueue = [];
let interactionMaxActive = 1;
let interactionShowGiftImage = true;
let interactionSettingsRevision = 0;
let interactionsEnabled = false;
let interactionMasterRevision = 0;
let interactionQueuePaused = false;
let interactionQueueControlRevision = 0;
let phaseTransitionProtectionEnabled = true;
let phaseTransitionProtectionRevision = 0;
let streamRulesRevision = 0;
let streamRulesNextId = 1;
let streamRulesFeedback = "ready";
let streamRulesError = false;
let streamRules = [];
const streamRuleAccumulators = new Map();
const followedViewers = new Set();

function streamRulesState() {
  return {
    ready: true,
    schemaVersion: 2,
    revision: streamRulesRevision,
    engineActive: interactionsEnabled,
    catalogVersion: giftCatalog.catalogVersion,
    feedback: streamRulesFeedback,
    error: streamRulesError,
    maxRules: 100,
    maxEvery: 1000000,
    maxQuantity: 50,
    rules: streamRules.map((rule) => {
      const gift = giftsById.get(rule.giftId);
      return {
        ...rule,
        platform: "tiktok",
        connectionId: "all",
        giftName: rule.eventType === "gift" ? gift?.name ?? rule.giftId : "",
        ...(rule.eventType === "gift"
          ? { coinsPerUnit: gift?.coinsPerUnit ?? 0 }
          : {}),
      };
    }),
  };
}

function resetStreamRuleAccumulators(ruleId) {
  const prefix = `${ruleId}:`;
  for (const key of streamRuleAccumulators.keys()) {
    if (key.startsWith(prefix)) streamRuleAccumulators.delete(key);
  }
}

let peskyEnabled = false;
let peskyRevision = 0;
let peskyFeedback = "ready";
let peskyNames = [];
let peskyDisabledItems = [];
let peskyBattleRevision = 0;
let peskyBattlePhase = "off";
let peskyBattleSessionId = 0;
let peskyBattleAttempt = 0;
let peskyBattleGiftId = "";
let peskyBattleAllowStreamAttacks = true;
let peskyBattleParticipants = [];
let peskyBattleDisabledItems = [];
let peskyBattleFeedback = "ready";
let peskyBattleError = false;
let peskyBattleTargetLevel = "";
let tapFarmingRevision = 1;
let tapFarmingPhase = "off";
let tapFarmingSessionId = 0;
let tapFarmingAttempt = 0;
let tapFarmingTapsPerConversion = 2;
let tapFarmingHealthPointsPerConversion = 1;
let tapFarmingFeedback = "ready";
let tapFarmingError = false;
let overlayComposerRevision = 1;
const overlayComposerPreviews = new Map([
  ["vertical", { profileId: "vertical", revision: 0, runId: 0, sessionId: "", active: false }],
  ["horizontal", { profileId: "horizontal", revision: 0, runId: 0, sessionId: "", active: false }],
]);
const overlayComposerPreviewCancellations = new Map();
let dashboardRevision = 1;
let dashboardNextSequence = 1;
const dashboardSessionId = "mock-session";
const dashboardConnections = [
  { id: "tikfinity-local", platform: "tiktok", connector: "tikfinity", label: "TikTok / TikFinity", status: "connected", account: "", message: "Mock WebSocket connected", lastEventAt: null },
  { id: "twitch", platform: "twitch", connector: "twitch-eventsub", label: "Twitch", status: "pending", account: "", message: "", lastEventAt: null },
  { id: "youtube", platform: "youtube", connector: "youtube-live-chat", label: "YouTube", status: "pending", account: "", message: "", lastEventAt: null },
];
const dashboardCounters = {
  received: 0,
  matched: 0,
  queued: 0,
  ignored: 0,
  gifts: 0,
  valued: 0,
  likes: 0,
  follows: 0,
  subscriptions: 0,
  coins: 0,
  bits: 0,
};
let dashboardEvents = [];
let dashboardNextScheduleId = 1;
const dashboardScheduledTimers = new Map();
const dashboardMaximumScheduled = 1024;

function defaultOverlayProfiles() {
  return [
    {
      id: "vertical",
      canvas: { width: 1080, height: 1920 },
      components: [
        {
          id: "tap_farming",
          x: 360,
          y: 1220,
          width: 360,
          height: 300,
          enabled: true,
          locked: false,
          layer: 20,
          opacity: 100,
          variant: "default",
          showTitle: false,
          showDetails: false,
          motion: true,
          liquidColor: "#ff4f92",
          collectingColor: "#f4c95d",
          textColor: "#ffffff",
          outlineColor: "#f5f5f7",
        },
        {
          id: "pesky_battle",
          x: 60,
          y: 1260,
          width: 960,
          height: 560,
          enabled: true,
          locked: false,
          layer: 10,
          opacity: 100,
          variant: "default",
          showTitle: true,
          showDetails: true,
          motion: true,
          liquidColor: "#ff4f92",
          collectingColor: "#f4c95d",
          textColor: "#ffffff",
          outlineColor: "#f5f5f7",
        },
      ],
    },
    {
      id: "horizontal",
      canvas: { width: 1920, height: 1080 },
      components: [
        {
          id: "tap_farming",
          x: 1395,
          y: 565,
          width: 360,
          height: 300,
          enabled: true,
          locked: false,
          layer: 20,
          opacity: 100,
          variant: "default",
          showTitle: false,
          showDetails: false,
          motion: true,
          liquidColor: "#ff4f92",
          collectingColor: "#f4c95d",
          textColor: "#ffffff",
          outlineColor: "#f5f5f7",
        },
        {
          id: "pesky_battle",
          x: 80,
          y: 720,
          width: 1760,
          height: 300,
          enabled: true,
          locked: false,
          layer: 10,
          opacity: 100,
          variant: "default",
          showTitle: true,
          showDetails: true,
          motion: true,
          liquidColor: "#ff4f92",
          collectingColor: "#f4c95d",
          textColor: "#ffffff",
          outlineColor: "#f5f5f7",
        },
      ],
    },
  ];
}

let overlayComposerProfiles = defaultOverlayProfiles();

function overlayComposerState() {
  return {
    ready: true,
    schemaVersion: 1,
    revision: overlayComposerRevision,
    profiles: overlayComposerProfiles,
    feedback: "ready",
    error: false,
  };
}

function tapFarmingState() {
  return {
    ready: true,
    schemaVersion: 2,
    revision: tapFarmingRevision,
    phase: tapFarmingPhase,
    sessionId: tapFarmingSessionId,
    attempt: tapFarmingAttempt,
    enabled: tapFarmingPhase !== "off",
    isLiveEventOwner: tapFarmingPhase !== "off",
    blockedByLiveEvent: peskyBattleIsExclusive() ? "pesky_battle" : "",
    gameplayAvailable: true,
    levelId: "",
    bossName: "",
    conversion: {
      tapsPerConversion: tapFarmingTapsPerConversion,
      healthPointsPerConversion: tapFarmingHealthPointsPerConversion,
      tapsPerHealthPoint:
        tapFarmingTapsPerConversion / tapFarmingHealthPointsPerConversion,
    },
    counters: {
      totalTaps: 0,
      bankedTaps: 0,
      unconvertedTaps: 0,
      convertedHealth: 0,
      reserveHealth: 0,
      spentHealth: 0,
    },
    boss: { currentHealth: 0, totalHealth: 0, progress: 0 },
    effectiveHealth: { available: false, current: 0, total: 0, ratio: 0 },
    phaseIndex: 0,
    phaseCount: 0,
    overallProgress: 0,
    phases: [],
    feedback: tapFarmingFeedback,
    error: tapFarmingError,
  };
}

const interactionItems = [
  "hilda_green_zeppelin",
  "hilda_purple_zeppelin",
  "rootpack_homing_carrot",
  "cagney_homing_plant",
  "frogs_firefly",
  "robot_homing_bomb",
];

function peskyBattleIsExclusive() {
  return ["recruiting", "ready", "waiting_level", "active"].includes(
    peskyBattlePhase,
  );
}

function peskyBattleState() {
  const gift = giftsById.get(peskyBattleGiftId);
  return {
    ready: true,
    schemaVersion: 1,
    revision: peskyBattleRevision,
    phase: peskyBattlePhase,
    sessionId: peskyBattleSessionId,
    attempt: peskyBattleAttempt,
    capacity: 5,
    exclusive: peskyBattleIsExclusive(),
    gameplayAvailable: true,
    targetLevel: peskyBattleTargetLevel,
    trigger: {
      giftId: gift?.giftId ?? "",
      giftName: gift?.name ?? "",
      giftImagePath: gift?.imagePath ?? "",
      coinsPerUnit: gift?.coinsPerUnit ?? 0,
    },
    allowStreamAttacks: peskyBattleAllowStreamAttacks,
    participants: peskyBattleParticipants.map(({ identity: _identity, ...participant }) =>
      participant),
    items: interactionItems,
    disabledItems: peskyBattleDisabledItems,
    feedback: peskyBattleFeedback,
    error: peskyBattleError,
  };
}

function closePeskyBattle(feedback = "battle_cancelled") {
  peskyBattlePhase = "off";
  peskyBattleAttempt = 0;
  peskyBattleParticipants = [];
  peskyBattleTargetLevel = "";
  peskyBattleFeedback = feedback;
  peskyBattleError = false;
}

function recruitPeskyBattleParticipant(command) {
  if (
    peskyBattlePhase !== "recruiting" ||
    command.platform !== "tiktok" ||
    command.type !== "gift" ||
    command.itemId !== peskyBattleGiftId
  ) return false;

  const userId = command.userId.trim();
  const userName = command.user.trim();
  const identity = userId
    ? `id:${userId.toLocaleLowerCase()}`
    : userName
      ? `name:${userName.toLocaleLowerCase()}`
      : "";
  if (!identity) {
    peskyBattleFeedback = "participant_identity_missing";
    peskyBattleError = true;
    peskyBattleRevision += 1;
    return false;
  }

  const duplicate = peskyBattleParticipants.some((participant) =>
    participant.identity === identity);
  if (duplicate) {
    peskyBattleFeedback = "participant_already_joined";
    peskyBattleError = false;
    peskyBattleRevision += 1;
    return false;
  }

  const slot = peskyBattleParticipants.length + 1;
  peskyBattleParticipants.push({
    slot,
    userId: userId || `mock-user-${slot}-${peskyBattleSessionId}`,
    userName: userName || userId,
    displayName: userName || userId,
    avatarUrl: "",
    joinedAt: new Date().toISOString(),
    identity,
  });
  if (peskyBattleParticipants.length >= 5) {
    peskyBattlePhase = "ready";
    peskyBattleFeedback = "lobby_ready";
  } else {
    peskyBattleFeedback = "participant_joined";
  }
  peskyBattleError = false;
  peskyBattleRevision += 1;
  return true;
}

const bosses = [
  { id: 0, name: "Hosco y Tosco", plane: false },
  { id: 3, name: "Hilda Berg", plane: true },
  { id: 10, name: "Reynita Abejita", plane: false },
  { id: 20, name: "Esther Espuelas", plane: true },
];
const weapons = [
  { id: 0, name: "Lanzaguisantes", empty: false },
  { id: 1, name: "Expansión", empty: false },
  { id: 4, name: "Carga", empty: false },
  { id: 9, name: "Nada", empty: true },
];
const supers = [
  { id: 0, name: "Súper I" },
  { id: 1, name: "Súper II" },
  { id: 2, name: "Súper III" },
  { id: 3, name: "Nada" },
];
const charms = [
  { id: 0, name: "Corazón" },
  { id: 2, name: "Bomba de humo" },
  { id: 6, name: "Galletita Astral" },
  { id: 10, name: "Nada" },
];
const modifiers = [
  { id: 0, name: "No Dash", none: false, enabled: true, kind: "ground" },
  { id: 1, name: "No mini avión", none: false, enabled: true, kind: "plane" },
  { id: 2, name: "Solo mini avión", none: false, enabled: true, kind: "plane" },
  { id: 3, name: "No bombas", none: false, enabled: true, kind: "plane" },
  { id: 4, name: "No Lanzaguisantes", none: false, enabled: true, kind: "plane" },
  { id: 5, name: "No EX", none: false, enabled: true, kind: "both" },
  { id: 6, name: "Blanco y negro", none: false, enabled: true, kind: "both" },
  { id: 8, name: "Al revés", none: false, enabled: true, kind: "both" },
  { id: 9, name: "HP. 1", none: false, enabled: true, kind: "both" },
  { id: 10, name: "Lluvia de tinta", none: false, enabled: true, kind: "both" },
  { id: 11, name: "50% de daño", none: false, enabled: true, kind: "both" },
  { id: 13, name: "Nada", none: true, enabled: true, kind: "both" },
];

function modifiersForResponse() {
  return modifiers.map((modifier) => ({
    ...modifier,
    canDisable: !modifier.none && modifiers.filter((item) =>
      !item.none && item.kind === modifier.kind && item.enabled).length > 1,
  }));
}

function refreshInteractionQueue() {
  const now = Date.now();
  for (const entry of interactionQueue) {
    if (entry.status === "scheduled" && entry.readyAt <= now) {
      entry.status = "queued";
    }
  }
  if (!interactionsEnabled || interactionQueuePaused) return;
  let active = interactionQueue.filter((entry) => entry.status === "active").length;
  for (const entry of interactionQueue) {
    if (active >= interactionMaxActive) break;
    if (entry.status === "queued") {
      entry.status = "active";
      active += 1;
    }
  }
}

function publicInteractionQueue() {
  return interactionQueue.map(({ readyAt: _readyAt, ...entry }) => entry);
}

function parseDashboardSimulation(searchParams) {
  const allowedPlatforms = new Set(["tiktok", "twitch", "youtube"]);
  const allowedTypes = new Set([
    "gift",
    "currency",
    "like",
    "follow",
    "subscription",
    "redemption",
  ]);
  const platform = (searchParams.get("platform") ?? "").trim().toLowerCase().slice(0, 24);
  const type = (searchParams.get("type") ?? "").trim().toLowerCase().slice(0, 24);
  const connection = dashboardConnections.find((entry) => entry.platform === platform);
  const validPlatform = allowedPlatforms.has(platform) && Boolean(connection);
  const validType = allowedTypes.has(type);
  const rawCount = (searchParams.get("count") ?? "").trim();
  const parsedCount = /^[+-]?\d+$/.test(rawCount) ? Number(rawCount) : 1;
  const count = Math.max(1, Math.min(1_000, parsedCount || 1));
  const giftId = (
    (searchParams.get("giftId") ?? "").trim() ||
    (searchParams.get("itemId") ?? "").trim()
  ).slice(0, 160);
  const gift = platform === "tiktok" && type === "gift"
    ? giftsById.get(giftId)
    : null;
  if (platform === "tiktok" && type === "gift" && !gift) {
    return { error: "unknown_gift" };
  }

  const requestedAmount = Math.max(
    0,
    Math.min(1_000_000_000, Number(searchParams.get("amount")) || 0),
  );
  const amount = gift
    ? Math.min(1_000_000_000, gift.coinsPerUnit * count)
    : requestedAmount;
  const defaultUnit = amount <= 0 || !["gift", "currency"].includes(type)
    ? null
    : platform === "tiktok"
      ? "coin"
      : platform === "twitch"
        ? "bit"
        : platform === "youtube"
          ? "money"
          : null;
  const requestedUnit = (searchParams.get("unit") ?? "").trim().toLowerCase().slice(0, 24);
  const requestedCurrency = (searchParams.get("currency") ?? "").trim().toUpperCase();
  const rawDelay = Number(searchParams.get("delaySeconds"));
  const delaySeconds = Number.isFinite(rawDelay)
    ? Math.max(0, Math.min(3600, rawDelay))
    : 0;

  return {
    error: "",
    delaySeconds,
    command: {
      platform,
      type,
      connection,
      validPlatform,
      validType,
      count,
      amount,
      user: (searchParams.get("user") ?? "").trim().slice(0, 80),
      userId: (searchParams.get("userId") ?? "").trim().slice(0, 80),
      itemId: gift?.giftId ?? null,
      itemName: gift?.name ?? "",
      itemImageUrl: gift?.imagePath ?? null,
      unitValue: gift ? gift.coinsPerUnit : count > 0 ? amount / count : amount,
      unit: gift ? "coin" : requestedUnit || defaultUnit,
      currency: gift
        ? null
        : /^[A-Z]{3}$/.test(requestedCurrency)
          ? requestedCurrency
          : null,
    },
  };
}

function executeDashboardSimulation(command) {
  const sequence = dashboardNextSequence;
  dashboardNextSequence += 1;
  const eventId = `sim-${String(sequence).padStart(10, "0")}`;
  const receivedAt = new Date().toISOString();
  const valid = command.validPlatform && command.validType;
  const event = {
    schemaVersion: 2,
    id: eventId,
    eventId,
    idempotencyKey: `${dashboardSessionId}:${eventId}`,
    sequence,
    connectionId: command.validPlatform ? `simulator-${command.platform}` : "simulator",
    streamSessionId: dashboardSessionId,
    platform: command.validPlatform ? command.platform : command.platform || "unknown",
    connector: "simulator",
    type: command.validType ? command.type : command.type || "unknown",
    user: command.user,
    userId: command.userId || null,
    amount: command.amount,
    unitValue: command.unitValue,
    totalValue: command.amount,
    unit: command.unit,
    currency: command.currency,
    count: command.count,
    itemId: command.itemId,
    itemName: command.itemName,
    itemImageUrl: command.itemImageUrl,
    streakId: null,
    streakState: "none",
    rawEventType: "dashboard_simulation",
    status: valid ? "received" : "ignored",
    messageCode: valid
      ? "simulation_received"
      : !command.validPlatform && !command.validType
        ? "unsupported_platform_and_type"
        : !command.validPlatform
          ? "unsupported_platform"
          : "unsupported_event_type",
    receivedAt,
    simulated: true,
  };

  dashboardCounters.received += 1;
  if (valid) {
    const recruitedForBattle = recruitPeskyBattleParticipant(command);
    if (command.type === "gift") dashboardCounters.gifts += command.count;
    if (["gift", "currency"].includes(command.type) && command.amount > 0) {
      dashboardCounters.valued += 1;
    }
    if (command.type === "like") dashboardCounters.likes += command.count;
    else if (command.type === "follow") dashboardCounters.follows += command.count;
    else if (command.type === "subscription") dashboardCounters.subscriptions += command.count;
    else if (command.platform === "tiktok" && command.type === "gift") {
      dashboardCounters.coins += command.amount;
    }
    if (command.platform === "twitch" && ["gift", "currency"].includes(command.type)) {
      dashboardCounters.bits += command.amount;
    }
    if (!interactionsEnabled) {
      if (!recruitedForBattle) {
        event.status = "ignored";
        event.messageCode = "interactions_disabled";
        dashboardCounters.ignored += 1;
      }
    } else if (peskyBattleIsExclusive() && !peskyBattleAllowStreamAttacks) {
      if (!recruitedForBattle) {
        event.status = "ignored";
        event.messageCode = "pesky_battle_stream_attacks_blocked";
        dashboardCounters.ignored += 1;
      }
    } else if (command.platform === "tiktok" && ["gift", "like", "follow"].includes(command.type)) {
      const matchedRules = [];
      const queuedActions = [];
      let interactionQueueChanged = false;
      const viewerKey = command.userId
        ? `id:${command.userId}`
        : command.user ? `name:${command.user.toLowerCase()}` : "";
      const followKey = `simulator-tiktok\n${viewerKey}`;
      const repeatedFollow = command.type === "follow" && followedViewers.has(followKey);
      if (command.type === "follow" && viewerKey && !repeatedFollow) {
        followedViewers.add(followKey);
      }
      for (const rule of streamRules.filter((candidate) =>
        !repeatedFollow && candidate.enabled && candidate.eventType === command.type &&
        (candidate.eventType !== "gift" || candidate.giftId === command.itemId))) {
        let triggers = 1;
        if (rule.eventType !== "follow") {
          const accumulatorKey = `${rule.id}:simulator-tiktok\n${
            rule.eventType === "like" ? viewerKey : ""
          }`;
          const total = (streamRuleAccumulators.get(accumulatorKey) ?? 0) + command.count;
          triggers = Math.floor(total / rule.every);
          streamRuleAccumulators.set(accumulatorKey, total % rule.every);
        }
        if (triggers <= 0) continue;
        matchedRules.push(rule.name);
        queuedActions.push(rule.interaction);
        const requested = triggers * rule.quantity;
        const accepted = Math.max(0, Math.min(
          requested,
          50,
          200 - interactionQueue.length,
        ));
        for (let index = 0; index < accepted; index += 1) {
          interactionQueue.push({
            id: interactionNextId,
            item: rule.interaction,
            donor: command.user,
            delaySeconds: 0,
            readyAt: Date.now(),
            status: "queued",
          });
          interactionNextId += 1;
        }
        dashboardCounters.queued += accepted;
        interactionQueueChanged ||= accepted > 0;
      }
      if (interactionQueueChanged) {
        refreshInteractionQueue();
        interactionFeedback = "queued";
        interactionRevision += 1;
      }
      if (matchedRules.length > 0) {
        dashboardCounters.matched += 1;
        event.status = "queued";
        event.messageCode = "rules_queued";
        event.rule = matchedRules.join(", ");
        event.action = queuedActions.join(", ");
      } else if (repeatedFollow) {
        event.messageCode = "follow_already_seen";
      } else if (streamRules.some((candidate) =>
        candidate.enabled && candidate.eventType === command.type &&
        (candidate.eventType !== "gift" || candidate.giftId === command.itemId))) {
        event.messageCode = "threshold_pending";
      }
    }
    if (recruitedForBattle) {
      event.messageCode = peskyBattleFeedback;
    }
  } else {
    dashboardCounters.ignored += 1;
  }
  dashboardEvents = [event, ...dashboardEvents].slice(0, 500);
  dashboardRevision += 1;
}

function json(res, body, status = 200) {
  const value = Buffer.from(JSON.stringify(body));
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": value.length,
    "Cache-Control": "no-store",
    "Access-Control-Allow-Origin": "*",
  });
  res.end(value);
}

function readJsonBody(req, res, callback) {
  const chunks = [];
  let length = 0;
  req.on("data", (chunk) => {
    length += chunk.length;
    if (length > 65536) {
      json(res, { ok: false, error: "payload_too_large" }, 413);
      req.destroy();
      return;
    }
    chunks.push(chunk);
  });
  req.on("end", () => {
    if (res.writableEnded) return;
    try {
      const parsed = JSON.parse(Buffer.concat(chunks).toString("utf8") || "{}");
      callback(parsed && typeof parsed === "object" ? parsed : {});
    } catch {
      json(res, { ok: false, error: "invalid_json" }, 400);
    }
  });
}

function normalizeOverlayComponent(component, canvas) {
  const minimumWidth = component.id === "pesky_battle" ? 320 : 220;
  const minimumHeight = component.id === "pesky_battle" ? 180 : 220;
  component.width = Math.max(
    minimumWidth,
    Math.min(canvas.width, Math.round(Number(component.width) || minimumWidth)),
  );
  component.height = Math.max(
    minimumHeight,
    Math.min(canvas.height, Math.round(Number(component.height) || minimumHeight)),
  );
  component.x = Math.max(
    0,
    Math.min(canvas.width - component.width, Math.round(Number(component.x) || 0)),
  );
  component.y = Math.max(
    0,
    Math.min(canvas.height - component.height, Math.round(Number(component.y) || 0)),
  );
  component.layer = Math.max(0, Math.min(100, Math.round(Number(component.layer) || 0)));
  const opacity = Number(component.opacity ?? 100);
  component.opacity = Number.isFinite(opacity)
    ? Math.max(0, Math.min(100, Math.round(opacity)))
    : 100;
  component.enabled = component.enabled !== false;
  component.locked = component.locked === true;
  component.showTitle = component.id === "tap_farming"
    ? false
    : component.showTitle !== false;
  component.showDetails = component.showDetails !== false;
  component.motion = component.motion !== false;
  component.variant = "default";
  component.liquidColor = /^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/.test(String(component.liquidColor ?? ""))
    ? String(component.liquidColor).toLowerCase()
    : "#ff4f92";
  component.collectingColor = /^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/.test(String(component.collectingColor ?? ""))
    ? String(component.collectingColor).toLowerCase()
    : "#f4c95d";
  component.textColor = /^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/.test(String(component.textColor ?? ""))
    ? String(component.textColor).toLowerCase()
    : "#ffffff";
  component.outlineColor = /^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/.test(String(component.outlineColor ?? ""))
    ? String(component.outlineColor).toLowerCase()
    : "#f5f5f7";
}

function applyOverlayComposerCommand(command, res) {
  if (Number(command.schemaVersion ?? 1) !== 1) {
    json(res, { ok: false, error: "unsupported_schema" }, 400);
    return;
  }
  if (Number(command.expectedRevision) !== overlayComposerRevision) {
    json(res, overlayComposerState(), 409);
    return;
  }
  const operation = String(command.operation ?? "update");
  const profile = overlayComposerProfiles.find((entry) => entry.id === command.profileId);
  if (!profile) {
    json(res, { ok: false, error: "unknown_profile" }, 400);
    return;
  }

  if (operation === "reset") {
    const defaults = defaultOverlayProfiles().find((entry) => entry.id === profile.id);
    overlayComposerProfiles = overlayComposerProfiles.map((entry) =>
      entry.id === profile.id ? defaults : entry);
  } else if (operation === "copy") {
    const source = overlayComposerProfiles.find((entry) => entry.id === command.sourceProfileId);
    if (!source) {
      json(res, { ok: false, error: "unknown_source_profile" }, 400);
      return;
    }
    const copied = profile.components.map((target) => {
      const sourceComponent = source.components.find((entry) => entry.id === target.id);
      if (!sourceComponent) return target;
      const next = {
        ...sourceComponent,
        x: Math.round(sourceComponent.x * profile.canvas.width / source.canvas.width),
        y: Math.round(sourceComponent.y * profile.canvas.height / source.canvas.height),
        width: Math.round(sourceComponent.width * profile.canvas.width / source.canvas.width),
        height: Math.round(sourceComponent.height * profile.canvas.height / source.canvas.height),
      };
      normalizeOverlayComponent(next, profile.canvas);
      return next;
    });
    profile.components = copied;
  } else if (operation === "update") {
    const component = profile.components.find((entry) => entry.id === command.componentId);
    if (!component) {
      json(res, { ok: false, error: "unknown_component" }, 400);
      return;
    }
    for (const key of ["liquidColor", "collectingColor", "textColor", "outlineColor"]) {
      if (Object.prototype.hasOwnProperty.call(command, key) &&
          !/^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/.test(String(command[key] ?? ""))) {
        json(res, { ok: false, error: "invalid_color" }, 400);
        return;
      }
    }
    for (const key of [
      "x", "y", "width", "height", "enabled", "locked", "layer",
      "opacity",
      "variant", "showTitle", "showDetails", "motion",
      "liquidColor", "collectingColor", "textColor", "outlineColor",
    ]) {
      if (Object.prototype.hasOwnProperty.call(command, key)) component[key] = command[key];
    }
    normalizeOverlayComponent(component, profile.canvas);
  } else {
    json(res, { ok: false, error: "invalid_operation" }, 400);
    return;
  }
  overlayComposerRevision += 1;
  json(res, overlayComposerState());
}

function serveAsset(pathname, res) {
  const file = resolve(assetsRoot, pathname.slice("/assets/".length));
  if (!file.startsWith(assetsRoot) || !existsSync(file)) {
    res.writeHead(404).end();
    return;
  }
  const extension = extname(file);
  const type = extension === ".png"
    ? "image/png"
    : extension === ".webp"
      ? "image/webp"
      : extension === ".svg"
        ? "image/svg+xml"
        : extension === ".css"
          ? "text/css; charset=utf-8"
          : extension === ".js"
            ? "text/javascript; charset=utf-8"
            : extension === ".html"
              ? "text/html; charset=utf-8"
    : extension === ".json"
      ? "application/json; charset=utf-8"
      : "application/octet-stream";
  res.writeHead(200, { "Content-Type": type, "Cache-Control": "no-store" });
  createReadStream(file).pipe(res);
}

function serveCreatorToolFile(fileName, contentType, res) {
  const file = resolve(assetsRoot, "creator-tools", fileName);
  if (!file.startsWith(assetsRoot) || !existsSync(file)) {
    res.writeHead(404).end();
    return;
  }
  res.writeHead(200, {
    "Content-Type": contentType,
    "Cache-Control": "no-store",
  });
  createReadStream(file).pipe(res);
}

createServer((req, res) => {
  const url = new URL(req.url ?? "/", "http://127.0.0.1:" + port);
  if (url.pathname === "/config" || url.pathname.startsWith("/config/")) {
    serveCreatorToolFile(
      "config.html",
      "text/html; charset=utf-8",
      res,
    );
    return;
  }
  if (url.pathname === "/config.js" || url.pathname === "/config.css") {
    serveCreatorToolFile(
      url.pathname.slice(1),
      url.pathname.endsWith(".js")
        ? "text/javascript; charset=utf-8"
        : "text/css; charset=utf-8",
      res,
    );
    return;
  }
  if ([
    "/overlay/vertical",
    "/overlay/horizontal",
    "/live-overlay",
    "/live-overlay/",
  ].includes(url.pathname)) {
    serveCreatorToolFile(
      "live-overlay.html",
      "text/html; charset=utf-8",
      res,
    );
    return;
  }
  if ([
    "/tap-farming-overlay",
    "/tap-farming-overlay/",
    "/tap-farming-overlay.html",
  ].includes(url.pathname)) {
    serveCreatorToolFile(
      "tap-farming-overlay.html",
      "text/html; charset=utf-8",
      res,
    );
    return;
  }
  if (url.pathname === "/tap-farming-overlay.css") {
    serveCreatorToolFile(
      "tap-farming-overlay.css",
      "text/css; charset=utf-8",
      res,
    );
    return;
  }
  if (url.pathname === "/tap-farming-overlay.js") {
    serveCreatorToolFile(
      "tap-farming-overlay.js",
      "text/javascript; charset=utf-8",
      res,
    );
    return;
  }
  if ([
    "/pesky-battle-overlay",
    "/pesky-battle-overlay/",
    "/pesky-battle-overlay.html",
  ].includes(url.pathname)) {
    serveCreatorToolFile(
      "pesky-battle-overlay.html",
      "text/html; charset=utf-8",
      res,
    );
    return;
  }
  if (url.pathname === "/pesky-battle-overlay.css") {
    serveCreatorToolFile(
      "pesky-battle-overlay.css",
      "text/css; charset=utf-8",
      res,
    );
    return;
  }
  if (url.pathname === "/pesky-battle-overlay.js") {
    serveCreatorToolFile(
      "pesky-battle-overlay.js",
      "text/javascript; charset=utf-8",
      res,
    );
    return;
  }
  if (url.pathname === "/api/overlay-composer/config") {
    json(res, overlayComposerState());
    return;
  }
  if (url.pathname === "/api/overlay-composer/config/set") {
    if (req.method !== "POST") {
      json(res, { ok: false, error: "method_not_allowed" }, 405);
      return;
    }
    readJsonBody(req, res, (command) => applyOverlayComposerCommand(command, res));
    return;
  }
  if (url.pathname === "/api/overlay-composer/preview") {
    const profileId = url.searchParams.get("profile") ?? "horizontal";
    let preview = overlayComposerPreviews.get(profileId);
    if (!preview) {
      preview = { profileId, revision: 0, active: false };
      overlayComposerPreviews.set(profileId, preview);
    }
    if (preview.active && preview.expiresAt <= Date.now()) {
      preview = {
        ...preview,
        revision: preview.revision + 1,
        active: false,
        expiresAt: 0,
        feedback: "expired",
      };
      overlayComposerPreviews.set(profileId, preview);
    }
    if (!preview.active) {
      json(res, {
        ready: true,
        schemaVersion: 1,
        revision: preview.revision,
        runId: preview.runId ?? 0,
        active: false,
        simulationActive: false,
        layout: null,
        profileId,
        sessionId: preview.sessionId ?? "",
        componentId: "",
        scenario: "",
        expiresAtUtc: null,
        feedback: preview.feedback ?? "ready",
        error: false,
      });
      return;
    }
    json(res, {
      ready: true,
      schemaVersion: 1,
      revision: preview.revision,
      runId: preview.runId ?? 0,
      active: true,
      simulationActive: preview.simulationActive === true,
      layout: preview.layout ?? null,
      profileId,
      sessionId: preview.sessionId ?? "",
      componentId: preview.componentId,
      scenario: preview.scenario,
      bossName: String(preview.bossName ?? ""),
      levelId: String(preview.levelId ?? ""),
      expiresAtUtc: new Date(preview.expiresAt).toISOString(),
      totalTaps: Number(preview.totalTaps) || 0,
      tapDelta: Number(preview.tapDelta) || 0,
      damageDelta: Number(preview.damageDelta) || 0,
      reserveHealth: Number(preview.reserveHealth) || 0,
      spentHealth: Number(preview.spentHealth) || 0,
      currentHealth: Number(preview.currentHealth) || 0,
      totalHealth: Number(preview.totalHealth) || 0,
      overallProgress: Number(preview.overallProgress) || 0,
      phaseIndex: Number(preview.phaseIndex) || 0,
      phaseCount: Number(preview.phaseCount) || 0,
      attempt: Number(preview.attempt) || 0,
      participantCount: Number(preview.participantCount) || 0,
      capacity: Number(preview.capacity) || 5,
      feedback: "preview_active",
      error: false,
    });
    return;
  }
  if (url.pathname === "/api/overlay-composer/preview/set") {
    if (req.method !== "POST") {
      json(res, { ok: false, error: "method_not_allowed" }, 405);
      return;
    }
    readJsonBody(req, res, (command) => {
      const profileId = String(command.profileId ?? "");
      if (!["vertical", "horizontal"].includes(profileId)) {
        json(res, { ok: false, error: "unknown_profile" }, 400);
        return;
      }
      const current = overlayComposerPreviews.get(profileId) ?? {
        profileId,
        revision: 0,
        runId: 0,
        sessionId: "",
        active: false,
      };
      const sessionId = String(command.sessionId ?? "").trim().toLowerCase();
      if (!/^[a-z0-9_-]{1,96}$/.test(sessionId)) {
        json(res, { ok: false, error: "invalid_preview_session" }, 400);
        return;
      }
      const cancellationKey = `${profileId}\n${sessionId}`;
      for (const [key, expiresAt] of overlayComposerPreviewCancellations) {
        if (expiresAt <= Date.now()) overlayComposerPreviewCancellations.delete(key);
      }
      if (command.operation === "start" &&
          overlayComposerPreviewCancellations.has(cancellationKey)) {
        json(res, {
          ready: true,
          active: current.active,
          revision: current.revision,
          sessionId: current.sessionId ?? "",
          componentId: current.componentId ?? "",
          feedback: "preview_session_cancelled",
          error: true,
        }, 409);
        return;
      }
      if (command.operation !== "start" && sessionId !== current.sessionId) {
        overlayComposerPreviewCancellations.set(
          cancellationKey,
          Date.now() + 2 * 60 * 1000,
        );
        json(res, {
          ready: true,
          active: current.active,
          revision: current.revision,
          sessionId: current.sessionId ?? "",
          componentId: current.componentId ?? "",
          feedback: "preview_session_conflict",
          error: true,
        }, 409);
        return;
      }
      if (command.expectedRevision !== undefined &&
          Number(command.expectedRevision) !== current.revision) {
        json(res, {
          ready: true,
          active: current.active,
          revision: current.revision,
          sessionId: current.sessionId ?? "",
          componentId: current.componentId ?? "",
          feedback: "revision_conflict",
          error: true,
        }, 409);
        return;
      }
      if (command.operation === "stop") {
        overlayComposerPreviewCancellations.set(
          cancellationKey,
          Date.now() + 2 * 60 * 1000,
        );
        const stopped = {
          ...current,
          active: false,
          revision: current.revision + 1,
          expiresAt: 0,
          feedback: "stopped",
        };
        overlayComposerPreviews.set(profileId, stopped);
        json(res, {
          ok: true,
          active: false,
          revision: stopped.revision,
          sessionId: stopped.sessionId,
        });
        return;
      }
      if (!["tap_farming", "pesky_battle"].includes(command.componentId)) {
        json(res, { ok: false, error: "unknown_component" }, 400);
        return;
      }
      let layout;
      try {
        layout = JSON.parse(String(command.layoutJson ?? ""));
      } catch {
        layout = null;
      }
      if (!layout || layout.id !== profileId ||
          !Array.isArray(layout.components) || layout.components.length !== 2) {
        json(res, { ok: false, error: "invalid_preview_layout" }, 400);
        return;
      }
      if (command.operation === "update" && !current.active) {
        json(res, {
          ready: true,
          active: false,
          revision: current.revision,
          sessionId: current.sessionId ?? "",
          componentId: current.componentId ?? "",
          feedback: "preview_not_active",
          error: true,
        }, 409);
        return;
      }
      const next = {
        ...current,
        ...command,
        simulationActive: command.simulationActive === true,
        layout,
        sessionId,
        active: true,
        revision: current.revision + 1,
        runId: command.operation === "start"
          ? (current.runId ?? 0) + 1
          : current.runId ?? 0,
        expiresAt: Date.now() + 2 * 60 * 1000,
      };
      overlayComposerPreviews.set(profileId, next);
      json(res, {
        ok: true,
        active: true,
        revision: next.revision,
        runId: next.runId,
        sessionId: next.sessionId,
        expiresAtUtc: new Date(next.expiresAt).toISOString(),
      });
    });
    return;
  }
  if (url.pathname === "/api/dashboard") {
    json(res, {
      ready: true,
      schemaVersion: 2,
      revision: dashboardRevision,
      engineStatus: "running",
      streamSessionId: dashboardSessionId,
      connections: dashboardConnections,
      counters: dashboardCounters,
      events: dashboardEvents,
    });
    return;
  }
  if (url.pathname === "/api/dashboard/simulate") {
    const parsed = parseDashboardSimulation(url.searchParams);
    if (parsed.error) {
      json(res, { ok: false, error: parsed.error }, 400);
      return;
    }
    if (dashboardScheduledTimers.size >= dashboardMaximumScheduled) {
      json(res, { ok: false, error: "simulation_queue_full" }, 429);
      return;
    }
    if (parsed.delaySeconds <= 0) {
      executeDashboardSimulation(parsed.command);
      json(res, { ok: true, queued: true }, 202);
      return;
    }

    const scheduleId = dashboardNextScheduleId;
    dashboardNextScheduleId += 1;
    const timer = setTimeout(() => {
      dashboardScheduledTimers.delete(scheduleId);
      executeDashboardSimulation(parsed.command);
    }, parsed.delaySeconds * 1000);
    dashboardScheduledTimers.set(scheduleId, timer);
    json(res, { ok: true, queued: true, scheduleId }, 202);
    return;
  }
  if (url.pathname === "/api/config") {
    json(res, { ready: true, enabled, selection, bosses, weapons, supers, charms, modifiers: modifiersForResponse() });
    return;
  }
  if (url.pathname === "/api/config/set") {
    const challenge = Number(url.searchParams.get("challenge"));
    if (Number.isInteger(challenge)) {
      const modifier = modifiers.find((item) => item.id === challenge && !item.none);
      if (modifier) {
        const nextEnabled = url.searchParams.get("challengeEnabled") === "1";
        const enabledInKind = modifiers.filter((item) =>
          !item.none && item.kind === modifier.kind && item.enabled).length;
        if (nextEnabled || enabledInKind > 1) {
          modifier.enabled = nextEnabled;
        }
      }
    }
    const enabledParam = url.searchParams.get("enabled");
    if (enabledParam !== null) {
      enabled = enabledParam === "1";
    }
    for (const key of Object.keys(selection)) {
      const rawValue = url.searchParams.get(key);
      if (rawValue !== null) {
        const value = Number(rawValue);
        if (Number.isInteger(value)) selection[key] = value;
      }
    }
    json(res, { ok: true }, 202);
    return;
  }
  if (url.pathname === "/api/config/interactions") {
    refreshInteractionQueue();
    json(res, {
      ready: true,
      available: interactionsEnabled,
      interactionsEnabled,
      masterRevision: interactionMasterRevision,
      queuePaused: interactionQueuePaused,
      queueControlRevision: interactionQueueControlRevision,
      phaseTransitionProtectionEnabled,
      phaseTransitionProtectionRevision,
      showGiftImage: interactionShowGiftImage,
      settingsRevision: interactionSettingsRevision,
      item: "hilda_green_zeppelin",
      items: interactionItems,
      lastItem: interactionLastItem,
      feedback: interactionFeedback,
      error: false,
      revision: interactionRevision,
      queueCount: interactionQueue.length,
      activeCount: interactionQueue.filter((entry) => entry.status === "active").length,
      pendingCount: interactionQueue.filter((entry) => entry.status !== "active").length,
      backlogCount: 0,
      maxActive: interactionMaxActive,
      maxActiveLimit: 20,
      maxBatch: 50,
      maxDelay: 3600,
      queue: publicInteractionQueue(),
    });
    return;
  }
  if (url.pathname === "/api/config/interactions/rules") {
    json(res, streamRulesState());
    return;
  }
  if (url.pathname === "/api/config/interactions/rules/set") {
    const action = (url.searchParams.get("action") ?? "").toLowerCase();
    const id = Number(url.searchParams.get("id"));
    const index = streamRules.findIndex((rule) => rule.id === id);
    streamRulesError = false;
    if (action === "delete" && index >= 0) {
      resetStreamRuleAccumulators(id);
      streamRules.splice(index, 1);
      streamRulesFeedback = "deleted";
    } else if (action === "duplicate" && index >= 0) {
      streamRules.splice(index + 1, 0, {
        ...streamRules[index],
        id: streamRulesNextId,
        name: (streamRules[index].name + " (copia)").slice(0, 64),
      });
      streamRulesNextId += 1;
      streamRulesFeedback = "duplicated";
    } else if (action === "toggle" && index >= 0) {
      streamRules[index].enabled = url.searchParams.get("enabled") === "1";
      if (!streamRules[index].enabled) resetStreamRuleAccumulators(id);
      streamRulesFeedback = streamRules[index].enabled ? "enabled" : "disabled";
    } else if (action === "create" || (action === "update" && index >= 0)) {
      const eventType = url.searchParams.get("eventType") ?? "gift";
      const giftId = url.searchParams.get("giftId") ?? "";
      const interaction = url.searchParams.get("interaction") ?? "";
      const every = Number(url.searchParams.get("every"));
      const quantity = Number(url.searchParams.get("quantity"));
      const name = (url.searchParams.get("name") ?? "").trim().slice(0, 64);
      if (!name || !["gift", "like", "follow"].includes(eventType) ||
          (eventType === "gift" && !giftsById.has(giftId)) ||
          !interactionItems.includes(interaction) ||
          !Number.isInteger(every) || every < 1 ||
          !Number.isInteger(quantity) || quantity < 1 || quantity > 50) {
        streamRulesFeedback = "invalid_rule";
        streamRulesError = true;
      } else {
        const rule = {
          id: action === "create" ? streamRulesNextId : id,
          name,
          enabled: url.searchParams.get("enabled") !== "0",
          eventType,
          giftId: eventType === "gift" ? giftId : "",
          every: eventType === "follow" ? 1 : every,
          interaction,
          quantity,
        };
        if (action === "create") {
          streamRules.push(rule);
          streamRulesNextId += 1;
          streamRulesFeedback = "created";
        } else {
          resetStreamRuleAccumulators(id);
          streamRules[index] = rule;
          streamRulesFeedback = "updated";
        }
      }
    } else {
      streamRulesFeedback = index < 0 ? "rule_not_found" : "invalid_action";
      streamRulesError = true;
    }
    streamRulesRevision += 1;
    json(res, streamRulesState());
    return;
  }
  if (url.pathname === "/api/config/interactions/set") {
    const interactionsEnabledValue = url.searchParams.get("interactionsEnabled");
    const queuePausedValue = url.searchParams.get("queuePaused");
    const clearPendingValue = url.searchParams.get("clearPending");
    const maxActiveValue = url.searchParams.get("maxActive");
    const showGiftImageValue = url.searchParams.get("showGiftImage");
    let nextFeedback = "settings_saved";
    if (interactionsEnabledValue !== null) {
      interactionsEnabled = interactionsEnabledValue === "1";
      interactionMasterRevision += 1;
      if (!interactionsEnabled) {
        interactionQueue = interactionQueue.filter((entry) => entry.status === "active");
        interactionQueuePaused = false;
        interactionQueueControlRevision += 1;
        streamRuleAccumulators.clear();
        followedViewers.clear();
      }
      nextFeedback = interactionsEnabled
        ? "interactions_enabled"
        : "interactions_disabled";
    } else if (queuePausedValue !== null && interactionsEnabled) {
      interactionQueuePaused = queuePausedValue === "1";
      interactionQueueControlRevision += 1;
      nextFeedback = interactionQueuePaused ? "queue_paused" : "queue_resumed";
    } else if (clearPendingValue !== null) {
      const previousLength = interactionQueue.length;
      interactionQueue = interactionQueue.filter((entry) => entry.status === "active");
      interactionQueueControlRevision += 1;
      nextFeedback = interactionQueue.length < previousLength
        ? "pending_cleared"
        : "pending_empty";
    }
    if (maxActiveValue !== null) {
      interactionMaxActive = Math.max(
        1,
        Math.min(20, Number(maxActiveValue) || 1),
      );
    }
    if (showGiftImageValue !== null) {
      interactionShowGiftImage = showGiftImageValue === "1";
    }
    if (maxActiveValue !== null || showGiftImageValue !== null) {
      interactionSettingsRevision += 1;
    }
    const phaseTransitionProtectionValue = url.searchParams.get(
      "phaseTransitionProtectionEnabled",
    );
    if (phaseTransitionProtectionValue !== null) {
      phaseTransitionProtectionEnabled =
        phaseTransitionProtectionValue === "1";
      phaseTransitionProtectionRevision += 1;
      nextFeedback = phaseTransitionProtectionEnabled
        ? "phase_transition_protection_enabled"
        : "phase_transition_protection_disabled";
    }
    refreshInteractionQueue();
    interactionFeedback = nextFeedback;
    interactionRevision += 1;
    json(res, { ok: true }, 202);
    return;
  }
  if (url.pathname === "/api/config/live-events") {
    const activeEvent = tapFarmingPhase !== "off"
      ? "tap_farming"
      : peskyBattleIsExclusive()
        ? "pesky_battle"
        : "";
    json(res, {
      ready: true,
      schemaVersion: 1,
      revision: tapFarmingRevision + peskyBattleRevision,
      activeEvent,
      status: activeEvent ? "active" : "idle",
      stoppingEvent: "",
      feedback: "ready",
      error: false,
    });
    return;
  }
  if (url.pathname === "/api/config/tap-farming") {
    json(res, tapFarmingState());
    return;
  }
  if (url.pathname === "/api/config/tap-farming/set") {
    const action = (url.searchParams.get("operation") ??
      url.searchParams.get("action") ?? "").trim().toLowerCase();
    const hasCanonicalTaps = url.searchParams.has("tapsPerConversion");
    const hasCanonicalHealth = url.searchParams.has("healthPointsPerConversion");
    const hasLegacyRate = url.searchParams.has("tapsPerHealthPoint");
    const tapsPerConversion = Number(hasCanonicalTaps
      ? url.searchParams.get("tapsPerConversion")
      : hasLegacyRate
        ? url.searchParams.get("tapsPerHealthPoint")
        : tapFarmingTapsPerConversion);
    const healthPointsPerConversion = Number(hasCanonicalHealth
      ? url.searchParams.get("healthPointsPerConversion")
      : hasLegacyRate && !hasCanonicalTaps && !hasCanonicalHealth
        ? 1
        : tapFarmingHealthPointsPerConversion);
    const validConversion = Number.isInteger(tapsPerConversion) &&
      tapsPerConversion >= 1 && tapsPerConversion <= 100000 &&
      Number.isInteger(healthPointsPerConversion) &&
      healthPointsPerConversion >= 1 && healthPointsPerConversion <= 100000;
    tapFarmingError = false;
    if (!validConversion) {
      tapFarmingFeedback = "invalid_taps_per_health_point";
      tapFarmingError = true;
      tapFarmingRevision += 1;
      json(res, tapFarmingState(), 202);
      return;
    }
    if (tapFarmingPhase === "off") {
      tapFarmingTapsPerConversion = tapsPerConversion;
      tapFarmingHealthPointsPerConversion = healthPointsPerConversion;
      tapFarmingFeedback = "settings_saved";
    }
    if (["activate", "arm", "start"].includes(action)) {
      if (peskyBattleIsExclusive()) {
        tapFarmingFeedback = "blocked_by_live_event";
        tapFarmingError = true;
      } else {
        tapFarmingSessionId += 1;
        tapFarmingAttempt = 0;
        tapFarmingPhase = "collecting";
        tapFarmingFeedback = "tap_farming_activated";
      }
    } else if (["deactivate", "disable", "off", "cancel", "finish", "reset"].includes(action)) {
      tapFarmingPhase = "off";
      tapFarmingAttempt = 0;
      tapFarmingFeedback = "tap_farming_deactivated";
    }
    tapFarmingRevision += 1;
    json(res, tapFarmingState(), 202);
    return;
  }
  if (url.pathname === "/api/config/pesky-battle") {
    json(res, peskyBattleState());
    return;
  }
  if (url.pathname === "/api/config/pesky-battle/set") {
    const requestedAction = (url.searchParams.get("action") ?? "")
      .trim()
      .toLowerCase();
    const enabledValue = url.searchParams.get("enabled");
    const action = requestedAction || (enabledValue === "1"
      ? "arm"
      : enabledValue === "0"
        ? "off"
        : "");
    const giftIdValue = url.searchParams.get("giftId");
    const streamAttacksValue = url.searchParams.get("allowStreamAttacks");
    const itemValue = url.searchParams.get("item");
    const itemEnabledValue = url.searchParams.get("itemEnabled");
    const hasSetting = giftIdValue !== null || streamAttacksValue !== null ||
      itemValue !== null;

    peskyBattleError = false;
    if (enabledValue !== null && !["0", "1"].includes(enabledValue)) {
      peskyBattleFeedback = "invalid_setting";
      peskyBattleError = true;
    }

    if (!peskyBattleError && giftIdValue !== null) {
      if (peskyBattlePhase !== "off") {
        peskyBattleFeedback = "battle_active_setting_locked";
        peskyBattleError = true;
      } else if (!giftsById.has(giftIdValue)) {
        peskyBattleFeedback = "unknown_gift";
        peskyBattleError = true;
      } else {
        peskyBattleGiftId = giftIdValue;
        peskyBattleFeedback = "gift_saved";
      }
    }
    if (!peskyBattleError && itemValue !== null) {
      if (peskyBattlePhase !== "off") {
        peskyBattleFeedback = "battle_active_setting_locked";
        peskyBattleError = true;
      } else if (!interactionItems.includes(itemValue) ||
          !["0", "1"].includes(itemEnabledValue ?? "")) {
        peskyBattleFeedback = interactionItems.includes(itemValue)
          ? "invalid_setting"
          : "unknown_item";
        peskyBattleError = true;
      } else if (itemEnabledValue === "1") {
        peskyBattleDisabledItems = peskyBattleDisabledItems.filter(
          (item) => item !== itemValue,
        );
        peskyBattleFeedback = "items_saved";
      } else if (
        !peskyBattleDisabledItems.includes(itemValue) &&
        peskyBattleDisabledItems.length < interactionItems.length - 1
      ) {
        peskyBattleDisabledItems.push(itemValue);
        peskyBattleFeedback = "items_saved";
      } else if (peskyBattleDisabledItems.includes(itemValue)) {
        peskyBattleFeedback = "items_saved";
      } else {
        peskyBattleFeedback = "items_required";
        peskyBattleError = true;
      }
    }

    if (!peskyBattleError && streamAttacksValue !== null) {
      if (!["0", "1"].includes(streamAttacksValue)) {
        peskyBattleFeedback = "invalid_setting";
        peskyBattleError = true;
      } else {
        peskyBattleAllowStreamAttacks = streamAttacksValue === "1";
        peskyBattleFeedback = peskyBattleAllowStreamAttacks
          ? "stream_attacks_allowed"
          : "stream_attacks_blocked";
      }
    }

    if (!peskyBattleError && action === "arm") {
      if (peskyBattlePhase !== "off") {
        peskyBattleFeedback = "invalid_action";
        peskyBattleError = true;
      } else if (!giftsById.has(peskyBattleGiftId)) {
        peskyBattleFeedback = "gift_required";
        peskyBattleError = true;
      } else if (peskyBattleDisabledItems.length >= interactionItems.length) {
        peskyBattleFeedback = "items_required";
        peskyBattleError = true;
      } else {
        peskyBattleSessionId += 1;
        peskyBattlePhase = "recruiting";
        peskyBattleAttempt = 0;
        peskyBattleParticipants = [];
        peskyBattleTargetLevel = "";
        peskyBattleFeedback = "battle_armed";
        if (peskyEnabled) {
          peskyEnabled = false;
          peskyFeedback = "disabled_by_pesky_battle";
          peskyRevision += 1;
        }
      }
    } else if (!peskyBattleError && action === "start") {
      if (peskyBattlePhase !== "ready" || peskyBattleParticipants.length < 5) {
        peskyBattleFeedback = peskyBattlePhase === "ready" ||
          peskyBattlePhase === "recruiting"
          ? "lobby_not_ready"
          : "invalid_action";
        peskyBattleError = true;
      } else {
        peskyBattlePhase = "waiting_level";
        peskyBattleFeedback = "waiting_for_level";
      }
    } else if (!peskyBattleError && ["cancel", "off", "disable"].includes(action)) {
      closePeskyBattle();
    } else if (!peskyBattleError && action === "reset") {
      closePeskyBattle();
    } else if (!peskyBattleError && action && action !== "arm") {
      peskyBattleFeedback = "invalid_action";
      peskyBattleError = true;
    } else if (!peskyBattleError && !action && !hasSetting) {
      peskyBattleFeedback = "invalid_setting";
      peskyBattleError = true;
    }

    peskyBattleRevision += 1;
    json(res, peskyBattleState(), 202);
    return;
  }
  if (url.pathname === "/api/config/pesky") {
    json(res, {
      ready: true,
      available: true,
      enabled: peskyEnabled,
      running: peskyEnabled,
      startingBattle: false,
      revision: peskyRevision,
      feedback: peskyFeedback,
      error: false,
      minimumInterval: 1.25,
      maximumInterval: 3.25,
      names: peskyNames,
      items: interactionItems,
      disabledItems: peskyDisabledItems,
      queueCount: 0,
      activeCount: 0,
      maxActive: interactionMaxActive,
      queue: [],
      blockedByPeskyBattle: peskyBattleIsExclusive(),
    });
    return;
  }
  if (url.pathname === "/api/config/pesky/set") {
    const enabledValue = url.searchParams.get("enabled");
    const namesValue = url.searchParams.get("names");
    const itemValue = url.searchParams.get("item");
    if (enabledValue !== null) {
      if (enabledValue === "1" && peskyBattleIsExclusive()) {
        peskyEnabled = false;
        peskyFeedback = "blocked_by_pesky_battle";
      } else {
        peskyEnabled = enabledValue === "1";
        peskyFeedback = peskyEnabled ? "enabled" : "disabled";
      }
    } else if (namesValue !== null) {
      peskyNames = [...new Set(namesValue.split(/\r?\n|\r/).map((name) => name.trim()).filter(Boolean))];
      peskyFeedback = "names_saved";
    } else if (itemValue !== null && interactionItems.includes(itemValue)) {
      if (url.searchParams.get("itemEnabled") === "1") {
        peskyDisabledItems = peskyDisabledItems.filter((item) => item !== itemValue);
      } else if (!peskyDisabledItems.includes(itemValue)) {
        peskyDisabledItems.push(itemValue);
      }
      peskyFeedback = "items_saved";
    }
    peskyRevision += 1;
    json(res, { ok: true }, 202);
    return;
  }
  if (url.pathname === "/api/config/interactions/test") {
    if (!interactionsEnabled) {
      interactionFeedback = "interactions_disabled";
      interactionRevision += 1;
      json(res, { ok: true }, 202);
      return;
    }
    interactionLastItem = url.searchParams.get("item") ?? "";
    const donor = (url.searchParams.get("donor") ?? "DONOR").slice(0, 32);
    const quantity = Math.max(1, Math.min(50, Number(url.searchParams.get("quantity")) || 1));
    const delaySeconds = Math.max(0, Math.min(3600, Number(url.searchParams.get("delay")) || 0));
    const readyAt = Date.now() + delaySeconds * 1000;
    for (let index = 0; index < quantity; index += 1) {
      interactionQueue.push({
        id: interactionNextId,
        item: interactionLastItem,
        donor,
        delaySeconds,
        readyAt,
        status: delaySeconds > 0 ? "scheduled" : "queued",
      });
      interactionNextId += 1;
    }
    refreshInteractionQueue();
    interactionFeedback = "queued";
    interactionRevision += 1;
    json(res, { ok: true }, 202);
    return;
  }
  if (url.pathname.startsWith("/assets/")) {
    serveAsset(url.pathname, res);
    return;
  }
  res.writeHead(404).end();
}).listen(port, host, () => {
  const displayHost = host.includes(":") ? `[${host}]` : host;
  console.log(`Creator Tools mock listening on http://${displayHost}:${port}`);
});
