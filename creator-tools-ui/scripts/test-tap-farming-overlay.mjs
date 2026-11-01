import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { runInNewContext } from "node:vm";

const uiRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const overlaySource = readFileSync(join(uiRoot, "../assets/creator-tools/tap-farming-overlay.js"), "utf8");
const liveSource = readFileSync(join(uiRoot, "../assets/creator-tools/live-overlay.js"), "utf8");
const dictionaries = Object.fromEntries(["es", "en"].map(locale => [locale,
  JSON.parse(readFileSync(join(uiRoot, `src/locales/${locale}.json`), "utf8"))]));
const temporary = mkdtempSync(join(tmpdir(), "pichi-tap-panel-"));
let panelSource;
try {
  const compiled = spawnSync(process.execPath, [
    "node_modules/typescript/bin/tsc", "src/features/dashboard/TapFarmingPanel.tsx",
    "--ignoreConfig", "--target", "es2022", "--module", "commonjs", "--jsx", "react-jsx",
    "--outDir", temporary, "--skipLibCheck",
  ], { cwd: uiRoot, encoding: "utf8", windowsHide: true });
  assert.equal(compiled.status, 0, compiled.error?.message || compiled.stdout + compiled.stderr);
  const file = existsSync(join(temporary, "TapFarmingPanel.js")) ? join(temporary, "TapFarmingPanel.js")
    : join(temporary, "features/dashboard/TapFarmingPanel.js");
  panelSource = readFileSync(file, "utf8");
} finally {
  rmSync(temporary, { recursive: true, force: true });
}

class Element {
  dataset = {};
  attributes = {};
  textContent = "";
  offsetWidth = 100;
  style = { setProperty(name, value) { this[name] = value; } };
  setAttribute(name, value) { this.attributes[name] = value; }
  getAttribute(name) { return this.attributes[name]; }
  removeAttribute(name) { delete this.attributes[name]; }
}

function overlay({ motion = false } = {}) {
  const elements = new Map();
  const node = id => {
    if (!elements.has(id)) elements.set(id, new Element());
    return elements.get(id);
  };
  const timers = new Map();
  let timerId = 0, runtime;
  const document = {
    documentElement: {}, hidden: false, getElementById: node,
    querySelector: node, querySelectorAll: () => [], addEventListener() {},
  };
  runInNewContext(overlaySource, { document, URLSearchParams, window: {
    location: { search: "" }, matchMedia: () => ({ matches: !motion, addEventListener() {} }),
    LiveEventOverlayRuntime: { create(config) { runtime = config; } },
    setTimeout(callback, delay) { timers.set(++timerId, { callback, delay }); return timerId; },
    clearTimeout(id) { timers.delete(id); }, requestAnimationFrame() { return 1; }, cancelAnimationFrame() {},
  } });
  return { node, render: runtime.render, initialPreview: runtime.initialPreviewState,
    timers: delay => [...timers.values()].filter(timer => timer.delay === delay).length,
    flush(delay) {
      for (const [id, timer] of [...timers]) if (timer.delay === delay) {
        timers.delete(id); timer.callback();
      }
    },
  };
}

function snapshot() {
  return {
    ready: true, phase: "active", sessionId: 1, levelId: "Robot", bossName: "Robot",
    attempt: 1, phaseIndex: 1, phaseCount: 3,
    conversion: { tapsPerConversion: 2, healthPointsPerConversion: 1 },
    counters: { totalTaps: 1200, bankedTaps: 1000, convertedHealth: 600, reserveHealth: 500, spentHealth: 100 },
    boss: { currentHealth: 1000, totalHealth: 1000, progress: 0 },
    effectiveHealth: { available: true, current: 1500, total: 1000, ratio: 1.5 },
    presentation: { motion: false },
  };
}

function panel(state, locale = "es") {
  const exports = {};
  const jsx = (type, props) => ({ type, props });
  runInNewContext(panelSource, { exports, require(name) {
    if (name === "react/jsx-runtime") return { jsx, jsxs: jsx };
    if (name === "react") return { useEffect() {}, useMemo: compute => compute(), useState: value => [value, () => {}] };
    if (name === "lucide-react") return new Proxy({}, { get: (_, key) => "icon:" + key });
    if (name.endsWith("/ConfigContext")) return { useConfig: () => ({ tapFarming: state, liveEvents: null }) };
    if (name.endsWith("/TapFarmingTestControls")) return { TapFarmingTestControls: () => null };
    if (name.endsWith("/LocalizationContext")) return { useLocalization: () => ({ locale,
      t: key => key.split(".").reduce((value, part) => value?.[part], dictionaries[locale]) ?? key }) };
    throw new Error("Unexpected panel dependency: " + name);
  } });
  const tree = exports.TapFarmingPanel();
  const nodes = [];
  const visit = value => {
    if (Array.isArray(value)) value.forEach(visit);
    else if (value?.props) { nodes.push(value); visit(value.props.children); }
  };
  visit(tree);
  return {
    progress: nodes.find(node => node.props.className === "dashboard-tap-farming__battle-progress"),
    metric: nodes.find(node => node.type === "span" && node.props["data-mode"]),
    labels: nodes.filter(node => node.type === "small").map(node => node.props.children),
  };
}

test("OBS displays raw growth above 100% and all effective HP while only liquid fill is capped", () => {
  const view = overlay();
  const state = snapshot();
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "150%");
  assert.equal(view.node("tap-health-remaining").textContent, "1,500 PV");
  assert.equal(view.node("tap-liquid-level").style.transform, "translateY(13px)");
  assert.match(view.node("tap-heart-label").textContent, /150%.*1,500.*1,000/);
  state.effectiveHealth = { available: true, current: 2500, total: 1000, ratio: 2.5 };
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "250%", "2.5 is a ratio, not a legacy 2.5 percent value");
  assert.equal(view.node("tap-health-remaining").textContent, "2,500 PV");
  assert.equal(view.node("tap-liquid-level").style.transform, "translateY(13px)");
});

test("retry collection displays only unspent taps/HP and victory reads its closed round summary", () => {
  const view = overlay();
  const state = snapshot();
  state.phase = "collecting";
  view.render(state);
  assert.equal(view.node("tap-collecting-total").textContent, "1,000");
  assert.equal(view.node("tap-collecting-health").textContent, "500");
  assert.equal(view.node("tap-collecting-health-caption").textContent, "PV DISPONIBLES");
  delete state.counters.bankedTaps;
  state.counters.reserveHealth = 100;
  state.counters.unconvertedTaps = 1;
  view.render(state);
  assert.equal(view.node("tap-collecting-total").textContent, "201", "legacy snapshots derive only the remaining balance");
  assert.equal(view.node("tap-collecting-health").textContent, "100");
  state.phase = "completed";
  state.lastRound = { totalTaps: 1200, spentHealth: 600, attempts: 3 };
  state.counters = { totalTaps: 0, bankedTaps: 0, reserveHealth: 0, convertedHealth: 0, spentHealth: 0 };
  view.render(state);
  assert.equal(view.node("tap-victory-taps").textContent, "1,200");
  assert.equal(view.node("tap-victory-absorbed").textContent, "600 PV");
  assert.equal(view.node("tap-victory-attempts").textContent, "3");
  state.counters = { totalTaps: 20, bankedTaps: 20, reserveHealth: 10, convertedHealth: 10, spentHealth: 0 };
  view.render(state);
  assert.equal(view.node("tap-victory-taps").textContent, "1,200", "new likes cannot overwrite the victory result");
  state.phase = "collecting";
  view.render(state);
  assert.equal(view.node("tap-collecting-total").textContent, "20");
  assert.equal(view.node("tap-collecting-health").textContent, "10");
});

test("heart entry starts empty, fills once, reveals content, and survives live updates and native phase changes", () => {
  const view = overlay({ motion: true });
  const state = snapshot();
  state.presentation.motion = true;
  state.phase = "collecting";
  view.render(state);
  state.phase = "active";
  view.render(state);
  assert.equal(view.node("tap-farming").dataset.heartEntering, "true");
  assert.equal(view.node("tap-liquid-level").style.transform, "translateY(258px)");
  assert.equal(view.timers(340), 1);
  assert.equal(view.timers(620), 1, "boss artwork waits for the heart's entrance");
  state.effectiveHealth = { available: true, current: 400, total: 1000, ratio: .4 };
  view.render(state);
  assert.equal(view.timers(340), 1, "polls cannot restart the entrance timer");
  view.flush(340);
  assert.equal(view.node("tap-liquid-level").style.transform, "translateY(160px)", "fill uses the newest health sample");
  state.phase = "transition";
  view.render(state);
  state.phase = "active";
  view.render(state);
  assert.equal(view.timers(340), 0, "native phase handoffs do not replay the whole heart entry");
  view.flush(1350);
  assert.equal(view.node("tap-farming").dataset.heartEntering, "false");
  state.phase = "collecting"; view.render(state);
  state.phase = "active"; state.attempt++; view.render(state);
  assert.equal(view.node("tap-liquid-level").style.transform, "translateY(258px)", "retry re-arms an empty heart");
  state.phase = "off"; view.render(state);
  assert.equal(view.timers(340), 0, "leaving the heart phase cancels delayed fills");
  assert.equal(view.timers(1350), 0);
});

test("panel retains raw percentage and accessible value above 100% while its bar stays within bounds", () => {
  for (const [ratio, percent, width] of [[1.5, 150, "100%"], [2.5, 250, "100%"], [0.4, 40, "40%"]]) {
    const state = snapshot();
    state.effectiveHealth = { available: true, current: ratio * 1000, total: 1000, ratio };
    const view = panel(state);
    assert.equal(view.metric.props.children, percent + "%");
    assert.equal(view.progress.props["aria-valuenow"], percent);
    assert.equal(view.progress.props["aria-valuemax"], Math.max(100, percent));
    assert.equal(view.progress.props["aria-valuetext"], percent + "%");
    assert.equal(view.progress.props.children.props.style.width, width);
  }
});

test("missing ratios derive from original total; negative/invalid inputs and native fallbacks stay bounded", () => {
  const view = overlay();
  const state = snapshot();
  delete state.effectiveHealth.ratio;
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "150%");
  state.effectiveHealth.ratio = NaN;
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "150%");
  delete state.effectiveHealth;
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "150%", "legacy native HP fallback includes the remaining reserve");
  assert.equal(panel(state).metric.props.children, "150%");
  state.effectiveHealth = { available: true, current: -100, total: 1000, ratio: -0.1 };
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "0%");
  assert.equal(view.node("tap-health-remaining").textContent, "0 PV");
  assert.equal(view.node("tap-liquid-level").style.transform, "translateY(258px)");
});

test("collecting, unavailable boss health, completed and retry preserve their display modes", () => {
  const view = overlay();
  const state = snapshot();
  state.effectiveHealth.available = false;
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "100%");
  assert.equal(view.node("tap-health-remaining").textContent, "");
  assert.equal(view.node("tap-farming").dataset.healthPending, "true");
  state.phase = "collecting";
  view.render(state);
  assert.equal(view.node("tap-farming").dataset.phase, "collecting");
  assert.equal(view.node("tap-collecting-total").textContent, "1,000");
  assert.equal(view.node("tap-collecting-health").textContent, "500");
  state.phase = "completed";
  state.effectiveHealth = { available: true, current: 0, total: 1000, ratio: 0 };
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "0%");
  assert.equal(view.node("tap-victory-absorbed").textContent, "100 PV");
  state.phase = "active"; state.attempt = 2;
  state.effectiveHealth = { available: true, current: 1500, total: 1000, ratio: 1.5 };
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "150%");
  assert.equal(view.node("tap-health-remaining").textContent, "1,500 PV");
});

test("standalone preview and unified OBS preview use original HP as their fixed denominator", () => {
  const view = overlay();
  const standalone = view.initialPreview;
  assert.equal(standalone.effectiveHealth.total, standalone.boss.totalHealth);
  assert.ok(standalone.effectiveHealth.ratio > 1);
  view.render(standalone);
  assert.equal(view.node("tap-percent").textContent, "153%");

  const helpers = liveSource.slice(liveSource.indexOf("  function finiteNumber("), liveSource.indexOf("  function normalizeLocale("));
  const preview = liveSource.slice(liveSource.indexOf("  function phaseItems("), liveSource.indexOf("  function previewParticipants("));
  const project = runInNewContext(`const activeLocale = "es", profileId = "vertical";\n${helpers}\n${preview}\npreviewTapSnapshot`);
  const input = { phase: "active", totalHealth: 3000, currentHealth: 1900, reserveHealth: 5000, spentHealth: 4401 };
  const first = project(input);
  assert.equal(first.effectiveHealth.total, 3000);
  assert.equal(first.effectiveHealth.current, 6900);
  assert.equal(first.effectiveHealth.ratio, 2.3);
  assert.equal(project({ ...input, spentHealth: 100000 }).effectiveHealth.total, 3000,
    "spent reserve cannot inflate the original denominator");
  assert.equal(project({ ...input, phase: "completed" }).effectiveHealth.current, 0);
  assert.equal(project({ ...input, levelId: "DicePalaceMain" }).effectiveHealth.scope, "arena");
  assert.equal(project({ ...input, levelId: "Robot" }).effectiveHealth.scope, "boss");
  view.render(first);
  assert.equal(view.node("tap-percent").textContent, "230%");
  assert.equal(view.node("tap-health-remaining").textContent, "6,900 PV");
});

test("arena scope labels only the current arena and never substitutes the global reserve into its percentage", () => {
  const state = snapshot();
  state.levelId = "DicePalaceMain";
  state.counters.reserveHealth = 5000;
  state.boss.currentHealth = 250;
  state.effectiveHealth = { available: true, current: 375, total: 1000, ratio: 0.375, scope: "arena" };
  const view = overlay();
  view.render(state);
  assert.equal(view.node("tap-percent").textContent, "38%");
  assert.equal(view.node("tap-health-remaining").textContent, "375 PV");
  assert.match(view.node("tap-heart-label").textContent, /^Vida de la arena actual\./);
  for (const [locale, label] of [["es", "Vida de la arena actual"], ["en", "Current arena health"]]) {
    const result = panel(state, locale);
    assert.equal(result.metric.props.children, "38%");
    assert.ok(result.labels.includes(label));
    assert.equal(result.progress.props["aria-label"], label);
    assert.equal(result.progress.props["aria-valuetext"], "38% · " + label);
  }
  for (const scope of ["boss", undefined]) {
    state.effectiveHealth.scope = scope;
    view.render(state);
    assert.ok(!view.node("tap-heart-label").textContent.includes("arena actual"));
    assert.ok(!panel(state).labels.includes("Vida de la arena actual"));
  }
});
