import { existsSync, readFileSync } from "node:fs";
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
const pngSignature = "89504e470d0a1a0a";
for (const gift of catalog.gifts) {
  if (typeof gift.giftId !== "string" || !/^\d+$/.test(gift.giftId)) {
    throw new Error("Every giftId must be a numeric string.");
  }
  if (seenGiftIds.has(gift.giftId)) {
    throw new Error(`Duplicate giftId: ${gift.giftId}`);
  }
  seenGiftIds.add(gift.giftId);
  if (!gift.name || typeof gift.name !== "string") {
    throw new Error(`Gift ${gift.giftId} has no name.`);
  }
  if (!Number.isInteger(gift.coinsPerUnit) || gift.coinsPerUnit < 1) {
    throw new Error(`Gift ${gift.giftId} has an invalid unit price.`);
  }
  const expectedImagePath =
    `/assets/creator-tools/gifts/images/${gift.giftId}.png`;
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
  const signature = readFileSync(localImagePath).subarray(0, 8).toString("hex");
  if (signature !== pngSignature) {
    throw new Error(`Gift ${gift.giftId} image is not a valid PNG.`);
  }
}

console.log(`Gift catalog validated (${catalog.gifts.length} gifts).`);
