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
for (const id of runtimeIds) {
  if (!mockServer.includes(`"${id}"`)) {
    throw new Error(`The mock interaction state is missing ${id}.`);
  }
}

console.log(`Interaction catalog validated (${runtimeIds.length} items).`);
