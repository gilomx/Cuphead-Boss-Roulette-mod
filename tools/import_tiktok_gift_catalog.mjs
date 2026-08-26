import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  writeFileSync,
} from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "..");
const sourceDirectory = process.argv[2]
  ? resolve(process.argv[2])
  : null;
const catalogVersionArgument = process.argv.find((argument) =>
  argument.startsWith("--catalog-version="),
);
const catalogVersion = catalogVersionArgument
  ? catalogVersionArgument.slice("--catalog-version=".length).trim()
  : "";
const excludedGiftIds = new Set(
  process.argv
    .filter((argument) => argument.startsWith("--exclude="))
    .flatMap((argument) => argument.slice("--exclude=".length).split(","))
    .map((giftId) => giftId.trim())
    .filter(Boolean),
);

if (!sourceDirectory || !catalogVersion) {
  throw new Error(
    "Usage: node tools/import_tiktok_gift_catalog.mjs " +
    "<extracted-directory> --catalog-version=<version> " +
    "[--exclude=<giftId,...>]",
  );
}

const sourceCatalogPath = resolve(sourceDirectory, "catalogo.json");
if (!existsSync(sourceCatalogPath)) {
  throw new Error(`Source catalog not found: ${sourceCatalogPath}`);
}

const sourceRecords = JSON.parse(readFileSync(sourceCatalogPath, "utf8"));
if (!Array.isArray(sourceRecords)) {
  throw new Error("catalogo.json must contain an array.");
}

const outputDirectory = resolve(
  repositoryRoot,
  "assets",
  "creator-tools",
  "gifts",
);
const outputImagesDirectory = resolve(outputDirectory, "images");
mkdirSync(outputImagesDirectory, { recursive: true });

const seenGiftIds = new Set();
const gifts = [];
for (const source of sourceRecords) {
  const giftId = String(source.giftId ?? "").trim();
  if (!giftId || excludedGiftIds.has(giftId)) continue;
  if (!/^\d+$/.test(giftId)) {
    throw new Error(`Invalid giftId: ${giftId || "<empty>"}`);
  }
  if (seenGiftIds.has(giftId)) {
    throw new Error(`Duplicate giftId: ${giftId}`);
  }
  seenGiftIds.add(giftId);

  const name = String(source.giftName ?? "").trim();
  const coinsPerUnit = Number(source.diamondCount);
  const sourceGiftType = Number(source.giftType);
  const sourceImagePath = resolve(
    sourceDirectory,
    String(source.imagePath ?? `images/${giftId}.png`),
  );
  if (!name) throw new Error(`Gift ${giftId} has no name.`);
  if (!Number.isInteger(coinsPerUnit) || coinsPerUnit < 1) {
    throw new Error(`Gift ${giftId} has an invalid unit price.`);
  }
  if (!Number.isInteger(sourceGiftType) || sourceGiftType < 1) {
    throw new Error(`Gift ${giftId} has an invalid source gift type.`);
  }
  if (!existsSync(sourceImagePath)) {
    throw new Error(`Gift ${giftId} is missing its image.`);
  }

  const imageFileName = `${giftId}.png`;
  copyFileSync(
    sourceImagePath,
    resolve(outputImagesDirectory, imageFileName),
  );
  gifts.push({
    giftId,
    name,
    aliases: [],
    coinsPerUnit,
    sourceGiftType,
    imagePath: `/assets/creator-tools/gifts/images/${imageFileName}`,
    sourceImageUrl: String(source.imageUrl ?? "").trim(),
    firstSeenAt: String(source.firstSeenAt ?? "").trim(),
  });
}

const firstSeenTimes = gifts
  .map((gift) => Date.parse(gift.firstSeenAt))
  .filter(Number.isFinite);
const snapshotAt = firstSeenTimes.length > 0
  ? new Date(Math.max(...firstSeenTimes)).toISOString()
  : null;
const catalog = {
  schemaVersion: 1,
  catalogVersion,
  platform: "tiktok",
  locale: "es",
  snapshotAt,
  source: {
    kind: "tikfinity-gift-farmer-export",
    unitPriceField: "diamondCount",
  },
  giftCount: gifts.length,
  gifts,
};

writeFileSync(
  resolve(outputDirectory, "catalog.json"),
  `${JSON.stringify(catalog, null, 2)}\n`,
  "utf8",
);
console.log(
  `TikTok gift catalog imported (${gifts.length} kept, ` +
  `${sourceRecords.length - gifts.length} excluded).`,
);
