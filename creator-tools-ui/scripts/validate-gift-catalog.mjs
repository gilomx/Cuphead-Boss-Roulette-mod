import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const uiRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(uiRoot, "..");
const catalogPath = resolve(
  repositoryRoot,
  "assets",
  "creator-tools",
  "gifts",
  "catalog.json",
);
const catalog = JSON.parse(readFileSync(catalogPath, "utf8"));

if (catalog.schemaVersion !== 1) {
  throw new Error("The gift catalog schemaVersion must be 1.");
}
if (!catalog.catalogVersion || typeof catalog.catalogVersion !== "string") {
  throw new Error("The gift catalog must have a catalogVersion.");
}
if (!Array.isArray(catalog.gifts) || catalog.gifts.length === 0) {
  throw new Error("The gift catalog must contain gifts.");
}
if (catalog.giftCount !== catalog.gifts.length) {
  throw new Error("giftCount does not match the number of gifts.");
}

const seenGiftIds = new Set();
let communityGiftCount = 0;
const imagesDirectory = resolve(
  repositoryRoot,
  "assets",
  "creator-tools",
  "gifts",
  "images",
);

function webpDimensions(buffer) {
  if (buffer.length < 30 || buffer.toString("ascii", 0, 4) !== "RIFF" ||
      buffer.toString("ascii", 8, 12) !== "WEBP") return null;
  const chunk = buffer.toString("ascii", 12, 16);
  if (chunk === "VP8X") {
    return {
      width: 1 + buffer.readUIntLE(24, 3),
      height: 1 + buffer.readUIntLE(27, 3),
    };
  }
  if (chunk === "VP8L" && buffer[20] === 0x2f) {
    const bits = buffer.readUInt32LE(21);
    return {
      width: 1 + (bits & 0x3fff),
      height: 1 + ((bits >>> 14) & 0x3fff),
    };
  }
  if (chunk === "VP8 " && buffer.toString("hex", 23, 26) === "9d012a") {
    return {
      width: buffer.readUInt16LE(26) & 0x3fff,
      height: buffer.readUInt16LE(28) & 0x3fff,
    };
  }
  return null;
}

for (const gift of catalog.gifts) {
  if (typeof gift.giftId !== "string" || !/^\d+$/.test(gift.giftId)) {
    throw new Error("Every giftId must be a numeric string.");
  }
  if (seenGiftIds.has(gift.giftId)) {
    throw new Error(`Duplicate giftId: ${gift.giftId}`);
  }
  seenGiftIds.add(gift.giftId);
  if (gift.kind === "community") {
    communityGiftCount += 1;
    if (gift.giftId !== "0" || gift.learned !== false) {
      throw new Error("The community placeholder must use unlearned giftId 0.");
    }
  }
  if (!gift.name || typeof gift.name !== "string") {
    throw new Error(`Gift ${gift.giftId} has no name.`);
  }
  if (!Number.isInteger(gift.coinsPerUnit) || gift.coinsPerUnit < 1) {
    throw new Error(`Gift ${gift.giftId} has an invalid unit price.`);
  }
  const expectedImagePath =
    `/assets/creator-tools/gifts/images/${gift.giftId}.webp`;
  if (gift.imagePath !== expectedImagePath) {
    throw new Error(`Gift ${gift.giftId} has an invalid imagePath.`);
  }
  const localImagePath = resolve(
    repositoryRoot,
    gift.imagePath.replace(/^\/assets\//, "assets/"),
  );
  if (!existsSync(localImagePath)) {
    throw new Error(`Gift ${gift.giftId} image is missing.`);
  }
  const dimensions = webpDimensions(readFileSync(localImagePath));
  if (!dimensions) {
    throw new Error(`Gift ${gift.giftId} image is not a valid WebP.`);
  }
  if (dimensions.width < 1 || dimensions.height < 1 ||
      dimensions.width > 96 || dimensions.height > 96) {
    throw new Error(
      `Gift ${gift.giftId} exceeds the 96px image limit ` +
      `(${dimensions.width}x${dimensions.height}).`,
    );
  }
}

if (communityGiftCount !== 1) {
  throw new Error("The gift catalog must contain exactly one community placeholder.");
}


const expectedFiles = new Set(
  catalog.gifts.map((gift) => `${gift.giftId}.webp`),
);
const packagedImages = readdirSync(imagesDirectory).filter((fileName) =>
  /\.(?:png|webp)$/i.test(fileName));
if (packagedImages.length !== expectedFiles.size ||
    packagedImages.some((fileName) => !expectedFiles.has(fileName))) {
  throw new Error("The gift image directory contains stale or missing files.");
}

console.log(`Gift catalog validated (${catalog.gifts.length} gifts).`);
