import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const uiRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(uiRoot, "..");

const contracts = readFileSync(
  resolve(repositoryRoot, "Interactions", "CreatorToolsInteractionContracts.cs"),
  "utf8",
);
const view = readFileSync(
  resolve(uiRoot, "src", "features", "interactions", "interactionCatalog.ts"),
  "utf8",
);
const mockServer = readFileSync(resolve(uiRoot, "scripts", "mock-server.mjs"), "utf8");
const locales = ["es", "en"].map((locale) => ({
  locale,
  messages: JSON.parse(readFileSync(resolve(uiRoot, "src", "locales", `${locale}.json`), "utf8")),
}));

const constants = new Map(
  [...contracts.matchAll(/internal const string\s+(\w+)\s*=\s*"([^"]+)"/g)]
    .map((match) => [match[1], match[2]]),
);
const allBlock = contracts.match(
  /internal static readonly string\[\]\s+All\s*=\s*\{([\s\S]*?)\};/,
);
if (!allBlock) throw new Error("CreatorToolsInteractionIds.All was not found.");

const runtimeIds = [...allBlock[1].matchAll(/\b([A-Z]\w*)\b/g)]
  .map((match) => constants.get(match[1]))
  .filter(Boolean);
const panelIds = [...view.matchAll(/\bid:\s*"([^"]+)"/g)]
  .map((match) => match[1]);

function compareExact(expected, actual, label) {
  const expectedSet = new Set(expected);
  const actualSet = new Set(actual);
  const missing = expected.filter((id) => !actualSet.has(id));
  const extra = actual.filter((id) => !expectedSet.has(id));
  if (missing.length || extra.length || actual.length !== actualSet.size) {
    throw new Error(
      `${label} does not match CreatorToolsInteractionIds.All. ` +
      `Missing: ${missing.join(", ") || "none"}. ` +
      `Extra/duplicate: ${extra.join(", ") || "none"}.`,
    );
  }
}

compareExact(runtimeIds, panelIds, "interactionItems");
const mockItems = mockServer.match(/const interactionItems\s*=\s*\[([\s\S]*?)\];/);
if (!mockItems) throw new Error("The mock interactionItems array was not found.");
compareExact(runtimeIds, [...mockItems[1].matchAll(/"([^"]+)"/g)]
  .map((match) => match[1]), "Mock interactionItems");

function validateTranslation(key) {
  for (const { locale, messages } of locales) {
    const translated = key.split(".").reduce((value, part) => value?.[part], messages);
    if (typeof translated !== "string" || !translated.trim()) {
      throw new Error(`Missing ${locale} interaction translation: ${key}.`);
    }
  }
}

for (const match of view.matchAll(/\{\s*id:\s*"([^"]+)"([\s\S]*?)\}/g)) {
  const [, id, fields] = match;
  const category = fields.match(/\bcategory:\s*"([^"]+)"/)?.[1];
  if (category !== "attack" && category !== "mini_boss") {
    throw new Error(`Unknown or missing category for ${id}: ${category}.`);
  }
  validateTranslation(`interactions.categories.${category}`);
  for (const field of ["titleKey", "imageAltKey", "typeKey"]) {
    const key = fields.match(new RegExp(`\\b${field}:\\s*"([^"]+)"`))?.[1];
    if (!key) throw new Error(`Missing ${field} for ${id}.`);
    validateTranslation(key);
  }
  const image = fields.match(/\bimage:\s*"([^"]+)"/)?.[1];
  if (!image || !/^\/assets\/creator-tools\/interactions\/[a-z0-9-]+\.png$/.test(image)) {
    throw new Error(`Invalid local interaction preview for ${id}: ${image}.`);
  }
  const png = readFileSync(resolve(repositoryRoot, image.slice(1)));
  if (!png.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))) {
    throw new Error(`The interaction preview is not a PNG: ${image}.`);
  }
}
for (const key of [
  "interactions.categories.all",
  "interactions.catalog.filterLabel",
  "interactions.miniBoss.description",
  "interactions.miniBoss.compatibility",
  "interactions.feedback.requires_ground_level",
]) validateTranslation(key);

console.log(`Interaction catalog validated (${runtimeIds.length} items).`);
