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
let interactionRandomTestEnabled = false;
let interactionRandomTestRevision = 0;
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
let peskyNames = ["Claudia", "YeiAndPelos", "Yerrisito", "Malono", "Suches", "Elver_hijas"];
let peskyDisabledItems = [];
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

const interactionItems = [
  "hilda_green_zeppelin",
  "hilda_purple_zeppelin",
  "rootpack_homing_carrot",
  "cagney_homing_plant",
  "frogs_firefly",
];

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
  if (!interactionsEnabled || interactionQueuePaused || peskyEnabled) return;
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
    if (command.type === "gift") dashboardCounters.gifts += command.count;
    if (["gift", "currency"].includes(command.type) && command.amount > 0) {
      dashboardCounters.valued += 1;
    }
    if (command.type === "like") dashboardCounters.likes += command.count;
    else if (command.type === "follow") dashboardCounters.follows += command.count;
    else if (command.type === "subscription") dashboardCounters.subscriptions += command.count;
    else if (command.platform === "tiktok" && command.type === "gift") {
      dashboardCounters.coins += amount;
    }
    if (command.platform === "twitch" && ["gift", "currency"].includes(command.type)) {
      dashboardCounters.bits += amount;
    }
    if (!interactionsEnabled) {
      event.status = "ignored";
      event.messageCode = "interactions_disabled";
      dashboardCounters.ignored += 1;
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

function serveAsset(pathname, res) {
  const file = resolve(assetsRoot, pathname.slice("/assets/".length));
  if (!file.startsWith(assetsRoot) || !existsSync(file)) {
    res.writeHead(404).end();
    return;
  }
  const extension = extname(file);
  const type = extension === ".png"
    ? "image/png"
    : extension === ".json"
      ? "application/json; charset=utf-8"
      : "application/octet-stream";
  res.writeHead(200, { "Content-Type": type, "Cache-Control": "no-store" });
  createReadStream(file).pipe(res);
}

createServer((req, res) => {
  const url = new URL(req.url ?? "/", "http://127.0.0.1:" + port);
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
      suspendedByPesky: peskyEnabled,
      randomTestEnabled: interactionRandomTestEnabled,
      randomTestRevision: interactionRandomTestRevision,
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
    const randomTestValue = url.searchParams.get("randomTestEnabled");
    if (randomTestValue !== null) {
      interactionRandomTestEnabled = randomTestValue === "1";
      interactionRandomTestRevision += 1;
      nextFeedback = interactionRandomTestEnabled
        ? "random_test_enabled"
        : "random_test_disabled";
      if (interactionRandomTestEnabled) peskyEnabled = false;
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
  if (url.pathname === "/api/config/pesky") {
    json(res, {
      ready: true,
      available: true,
      enabled: peskyEnabled,
      running: peskyEnabled,
      startingBattle: false,
      waitingForInteractions: false,
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
      pausedInteractionCount: interactionQueue.length,
      pausedInteractionActiveCount: interactionQueue.filter((entry) => entry.status === "active").length,
      maxActive: interactionMaxActive,
      queue: [],
    });
    return;
  }
  if (url.pathname === "/api/config/pesky/set") {
    const enabledValue = url.searchParams.get("enabled");
    const namesValue = url.searchParams.get("names");
    const itemValue = url.searchParams.get("item");
    if (enabledValue !== null) {
      peskyEnabled = enabledValue === "1";
      if (peskyEnabled) {
        interactionRandomTestEnabled = false;
        interactionRandomTestRevision += 1;
      }
      peskyFeedback = peskyEnabled ? "enabled" : "disabled";
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
