import { readFileSync, writeFileSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

// The classic OBS renderer uses the same images and translations as the UI.
const ui = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const source = readFileSync(resolve(ui, "src/features/interactions/interactionCatalog.ts"), "utf8");
const translations = Object.fromEntries(["es", "en"].map(locale => [locale,
  JSON.parse(readFileSync(resolve(ui, `src/locales/${locale}.json`), "utf8"))]));
const catalog = Object.fromEntries([...source.matchAll(/\{\s*id:\s*"([^"]+)"([\s\S]*?)\}/g)].map(([, id, fields]) => {
  const key = fields.match(/titleKey:\s*"([^"]+)"/)[1];
  const visualScale = Number(fields.match(/overlayScale:\s*([0-9.]+)/)?.[1] ?? 1);
  return [id, {
    imagePath: fields.match(/image:\s*"([^"]+)"/)[1],
    visualScale,
    names: Object.fromEntries(Object.entries(translations).map(([locale, messages]) =>
      [locale, key.split(".").reduce((value, part) => value[part], messages)])),
  }];
}));
writeFileSync(resolve(ui, "../assets/creator-tools/overlay-interactions.js"),
  `// Generated from interactionCatalog.ts and locales by npm run build.\nwindow.CreatorToolsOverlayInteractions = ${JSON.stringify(catalog, null, 2)};\n`);
