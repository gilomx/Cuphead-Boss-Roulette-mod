import { createReadStream, existsSync, readFileSync } from "node:fs";
import { createServer } from "node:http";
import { extname, resolve, sep } from "node:path";

const port = 18081;
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
let interactionRandomTestEnabled = false;
let interactionRandomTestRevision = 0;
let phaseTransitionProtectionEnabled = true;
let phaseTransitionProtectionRevision = 0;
let streamRulesRevision = 0;
let streamRulesNextId = 1;
let streamRulesFeedback = "ready";
let streamRulesError = false;
let streamRules = [];
let peskyEnabled = false;
let peskyRevision = 0;
let peskyFeedback = "ready";
let peskyNames = ["Claudia", "YeiAndPelos", "Yerrisito", "Malono", "Suches", "Elver_hijas"];
let peskyDisabledItems = [];
let dashboardRevision = 1;
let dashboardNextSequence = 1;
const dashboardSessionId = "mock-session";
const dashboardConnections = [
  { id: "tikfinity", platform: "tiktok", connector: "tikfinity", label: "TikTok / TikFinity", status: "simulated", account: "", message: "", lastEventAt: null },
  { id: "twitch", platform: "twitch", connector: "twitch-eventsub", label: "Twitch", status: "simulated", account: "", message: "", lastEventAt: null },
  { id: "youtube", platform: "youtube", connector: "youtube-live-chat", label: "YouTube", status: "simulated", account: "", message: "", lastEventAt: null },
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
};
let dashboardEvents = [];

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
  if (peskyEnabled) return;
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
      schemaVersion: 1,
      revision: dashboardRevision,
      engineStatus: "simulated",
      connections: dashboardConnections,
      counters: dashboardCounters,
      events: dashboardEvents,
    });
    return;
  }
  if (url.pathname === "/api/dashboard/simulate") {
    const allowedPlatforms = new Set(["tiktok", "twitch", "youtube"]);
    const allowedTypes = new Set([
      "gift",
      "currency",
      "like",
      "follow",
      "subscription",
      "redemption",
    ]);
    const platform = (url.searchParams.get("platform") ?? "").trim().toLowerCase().slice(0, 24);
    const type = (url.searchParams.get("type") ?? "").trim().toLowerCase().slice(0, 24);
    const connection = dashboardConnections.find((entry) => entry.platform === platform);
    const validPlatform = allowedPlatforms.has(platform) && Boolean(connection);
    const validType = allowedTypes.has(type);
    const valid = validPlatform && validType;
    const sequence = dashboardNextSequence;
    dashboardNextSequence += 1;
    const eventId = `sim-${String(sequence).padStart(10, "0")}`;
    const receivedAt = new Date().toISOString();
    const count = Math.max(1, Math.min(
      1_000_000,
      Math.floor(Number(url.searchParams.get("count"))) || 1,
    ));
    const amount = Math.max(0, Math.min(1_000_000_000, Number(url.searchParams.get("amount")) || 0));
    const defaultUnit = !valid || amount <= 0 || !["gift", "currency"].includes(type)
      ? null
      : platform === "tiktok"
        ? "coin"
        : platform === "twitch"
          ? "bit"
          : platform === "youtube"
            ? "money"
            : null;
    const requestedUnit = (url.searchParams.get("unit") ?? "").trim().toLowerCase().slice(0, 24);
    const requestedCurrency = (url.searchParams.get("currency") ?? "").trim().toUpperCase();
    const event = {
      schemaVersion: 1,
      id: eventId,
      eventId,
      idempotencyKey: `${dashboardSessionId}:${eventId}`,
      sequence,
      connectionId: validPlatform ? connection.id : "simulator",
      streamSessionId: dashboardSessionId,
      platform: validPlatform ? platform : platform || "unknown",
      connector: validPlatform ? connection.connector : "simulator",
      type: validType ? type : type || "unknown",
      user: (url.searchParams.get("user") ?? "").trim().slice(0, 80),
      userId: (url.searchParams.get("userId") ?? "").trim().slice(0, 80) || null,
      amount,
      unit: requestedUnit || defaultUnit,
      currency: /^[A-Z]{3}$/.test(requestedCurrency) ? requestedCurrency : null,
      count,
      itemName: (url.searchParams.get("itemName") ?? "").trim().slice(0, 80),
      status: valid ? "received" : "ignored",
      messageCode: valid
        ? "simulation_received"
        : !validPlatform && !validType
          ? "unsupported_platform_and_type"
          : !validPlatform
            ? "unsupported_platform"
            : "unsupported_event_type",
      receivedAt,
      simulated: true,
    };
    dashboardCounters.received += 1;
    if (valid) {
      connection.lastEventAt = receivedAt;
      if (type === "gift") {
        dashboardCounters.gifts += count;
      }
      if (["gift", "currency"].includes(type) && amount > 0) dashboardCounters.valued += 1;
      if (type === "like") dashboardCounters.likes += count;
      else if (type === "follow") dashboardCounters.follows += count;
      else if (type === "subscription") dashboardCounters.subscriptions += count;
    } else dashboardCounters.ignored += 1;
    dashboardEvents = [event, ...dashboardEvents].slice(0, 500);
    dashboardRevision += 1;
    json(res, { ok: true, sequence }, 202);
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
      available: true,
      suspendedByPesky: peskyEnabled,
      randomTestEnabled: interactionRandomTestEnabled,
      randomTestRevision: interactionRandomTestRevision,
      phaseTransitionProtectionEnabled,
      phaseTransitionProtectionRevision,
      item: "hilda_green_zeppelin",
      items: interactionItems,
      lastItem: interactionLastItem,
      feedback: interactionFeedback,
      error: false,
      revision: interactionRevision,
      queueCount: interactionQueue.length,
      activeCount: interactionQueue.filter((entry) => entry.status === "active").length,
      maxActive: interactionMaxActive,
      maxActiveLimit: 20,
      maxBatch: 50,
      maxDelay: 3600,
      queue: publicInteractionQueue(),
    });
    return;
  }
  if (url.pathname === "/api/config/interactions/rules") {
    json(res, {
      ready: true,
      schemaVersion: 1,
      revision: streamRulesRevision,
      engineActive: false,
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
          eventType: "gift",
          giftName: gift?.name ?? rule.giftId,
          coinsPerUnit: gift?.coinsPerUnit ?? 0,
        };
      }),
    });
    return;
  }
  if (url.pathname === "/api/config/interactions/rules/set") {
    const action = (url.searchParams.get("action") ?? "").toLowerCase();
    const id = Number(url.searchParams.get("id"));
    const index = streamRules.findIndex((rule) => rule.id === id);
    streamRulesError = false;
    if (action === "delete" && index >= 0) {
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
      streamRulesFeedback = streamRules[index].enabled ? "enabled" : "disabled";
    } else if (action === "create" || (action === "update" && index >= 0)) {
      const giftId = url.searchParams.get("giftId") ?? "";
      const interaction = url.searchParams.get("interaction") ?? "";
      const every = Number(url.searchParams.get("every"));
      const quantity = Number(url.searchParams.get("quantity"));
      const name = (url.searchParams.get("name") ?? "").trim().slice(0, 64);
      if (!name || !giftsById.has(giftId) || !interactionItems.includes(interaction) ||
          !Number.isInteger(every) || every < 1 ||
          !Number.isInteger(quantity) || quantity < 1 || quantity > 50) {
        streamRulesFeedback = "invalid_rule";
        streamRulesError = true;
      } else {
        const rule = {
          id: action === "create" ? streamRulesNextId : id,
          name,
          enabled: url.searchParams.get("enabled") !== "0",
          giftId,
          every,
          interaction,
          quantity,
        };
        if (action === "create") {
          streamRules.push(rule);
          streamRulesNextId += 1;
          streamRulesFeedback = "created";
        } else {
          streamRules[index] = rule;
          streamRulesFeedback = "updated";
        }
      }
    } else {
      streamRulesFeedback = index < 0 ? "rule_not_found" : "invalid_action";
      streamRulesError = true;
    }
    streamRulesRevision += 1;
    json(res, { ok: true }, 202);
    return;
  }
  if (url.pathname === "/api/config/interactions/set") {
    const maxActiveValue = url.searchParams.get("maxActive");
    let nextFeedback = "settings_saved";
    if (maxActiveValue !== null) {
      interactionMaxActive = Math.max(
        1,
        Math.min(20, Number(maxActiveValue) || 1),
      );
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
}).listen(port, "127.0.0.1", () => {
  console.log("Creator Tools mock listening on http://127.0.0.1:" + port);
});
