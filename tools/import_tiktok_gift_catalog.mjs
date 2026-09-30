import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  unlinkSync,
  writeFileSync,
} from "node:fs";
import { spawnSync } from "node:child_process";
import { basename, dirname, extname, resolve } from "node:path";
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
const communityPlaceholderId = "0";
const communityPlaceholderName = "Community Gift";
const imageSize = 96;
const webpQuality = 82;

function booleanValue(value) {
  if (value === true || value === 1 || value === "1" || value === "true") return true;
  if (value === false || value === 0 || value === "0" || value === "false") return false;
  return null;
}

function isCommunityGift(source) {
  for (const key of [
    "ownCommunityGift",
    "isCommunityGift",
    "communityGift",
    "isCustomGift",
    "customGift",
    "personalizedGift",
  ]) {
    if (booleanValue(source?.[key]) === true) return true;
  }
  if (["community_gift", "custom_gift", "personalized_gift"].includes(
    String(source?.giftSubtype ?? "").toLowerCase(),
  )) return true;
  return /saliency_seg_/i.test(String(source?.imageUrl ?? ""));
}

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
const communityGifts = [];
for (const source of sourceRecords) {
  const giftId = String(source.giftId ?? "").trim();
  if (!giftId || excludedGiftIds.has(giftId)) continue;
  if (isCommunityGift(source)) {
    communityGifts.push(source);
    continue;
  }
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

  const imageFileName = `${giftId}.webp`;
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

const placeholderSource = communityGifts.find((source) =>
  String(source.giftName ?? "").trim().toLowerCase() ===
    "regalo de la comunidad",
) ?? communityGifts[0];
if (!placeholderSource) {
  throw new Error("The source catalog has no community gift for the placeholder.");
}
const placeholderImagePath = resolve(
  sourceDirectory,
  String(
    placeholderSource.imagePath ??
      `images/${String(placeholderSource.giftId ?? "").trim()}.png`,
  ),
);
if (!existsSync(placeholderImagePath)) {
  throw new Error("The community gift placeholder image is missing.");
}

const magick = process.env.MAGICK_PATH?.trim() || "magick";
const conversion = spawnSync(magick, [
  "mogrify",
  "-path",
  outputImagesDirectory,
  "-format",
  "webp",
  "-resize",
  `${imageSize}x${imageSize}>`,
  "-strip",
  "-quality",
  String(webpQuality),
  resolve(sourceDirectory, "images", "*.png"),
], {
  encoding: "utf8",
  windowsHide: true,
});
if (conversion.error || conversion.status !== 0) {
  throw new Error(
    "ImageMagick could not generate the optimized WebP catalog. " +
    "Install ImageMagick or set MAGICK_PATH. " +
    String(conversion.error?.message ?? conversion.stderr ?? "").trim(),
  );
}

const convertedPlaceholderPath = resolve(
  outputImagesDirectory,
  `${basename(placeholderImagePath, extname(placeholderImagePath))}.webp`,
);
if (!existsSync(convertedPlaceholderPath)) {
  throw new Error("The converted community gift placeholder is missing.");
}
copyFileSync(
  convertedPlaceholderPath,
  resolve(outputImagesDirectory, `${communityPlaceholderId}.webp`),
);
gifts.push({
  giftId: communityPlaceholderId,
  name: communityPlaceholderName,
  aliases: ["Regalo de la comunidad"],
  coinsPerUnit: 1,
  sourceGiftType: 1,
  imagePath:
    `/assets/creator-tools/gifts/images/${communityPlaceholderId}.webp`,
  sourceImageUrl: "",
  firstSeenAt: String(placeholderSource.firstSeenAt ?? "").trim(),
  kind: "community",
  learned: false,
});

const expectedImageFiles = new Set(
  gifts.map((gift) => `${gift.giftId}.webp`),
);
for (const fileName of readdirSync(outputImagesDirectory)) {
  const lowerName = fileName.toLowerCase();
  if (lowerName.endsWith(".png") ||
      lowerName.endsWith(".webp") && !expectedImageFiles.has(fileName)) {
    unlinkSync(resolve(outputImagesDirectory, fileName));
  }
}
for (const fileName of expectedImageFiles) {
  if (!existsSync(resolve(outputImagesDirectory, fileName))) {
    throw new Error(`Optimized gift image is missing: ${fileName}`);
  }
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
    excludedCommunityGiftCount: communityGifts.length,
    imageFormat: "webp",
    imageMaximumSize: imageSize,
    imageQuality: webpQuality,
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
  `${communityGifts.length} community gifts replaced by one placeholder, ` +
  `${excludedGiftIds.size} explicitly excluded; ` +
  `${imageSize}px WebP quality ${webpQuality}).`,
);
