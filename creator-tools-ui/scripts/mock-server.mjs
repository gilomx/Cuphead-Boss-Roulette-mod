import { createReadStream, existsSync } from "node:fs";
import { createServer } from "node:http";
import { extname, resolve, sep } from "node:path";

const port = 18081;
const assetsRoot = resolve(process.cwd(), "../assets") + sep;
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
  const type = extname(file) === ".png" ? "image/png" : "application/octet-stream";
  res.writeHead(200, { "Content-Type": type, "Cache-Control": "no-store" });
  createReadStream(file).pipe(res);
}

createServer((req, res) => {
  const url = new URL(req.url ?? "/", "http://127.0.0.1:" + port);
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
      randomTestEnabled: interactionRandomTestEnabled,
      randomTestRevision: interactionRandomTestRevision,
      item: "hilda_green_zeppelin",
      items: ["hilda_green_zeppelin", "hilda_purple_zeppelin"],
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
    }
    refreshInteractionQueue();
    interactionFeedback = nextFeedback;
    interactionRevision += 1;
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
