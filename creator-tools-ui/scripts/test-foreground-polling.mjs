import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const uiRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const temporary = mkdtempSync(join(tmpdir(), "pichi-foreground-polling-"));
let compiledSource;
try {
  const compiled = spawnSync(process.execPath, [
    "node_modules/typescript/bin/tsc", "src/config/foregroundPolling.ts",
    "--ignoreConfig", "--target", "es2022", "--module", "commonjs",
    "--outDir", temporary, "--skipLibCheck",
  ], { cwd: uiRoot, encoding: "utf8", windowsHide: true });
  assert.equal(compiled.status, 0, compiled.error?.message || compiled.stdout + compiled.stderr);
  compiledSource = readFileSync(join(temporary, "foregroundPolling.js"), "utf8");
} finally {
  rmSync(temporary, { recursive: true, force: true });
}

function events() {
  const listeners = new Map();
  return {
    addEventListener(name, listener) {
      if (!listeners.has(name)) listeners.set(name, new Set());
      listeners.get(name).add(listener);
    },
    removeEventListener(name, listener) { listeners.get(name)?.delete(listener); },
    dispatch(name) { for (const listener of [...listeners.get(name) ?? []]) listener(); },
    listenerCount() { return [...listeners.values()].reduce((sum, set) => sum + set.size, 0); },
  };
}

function fixture({ hidden = false, focused = true } = {}) {
  const document = { ...events(), hidden, hasFocus: () => focused };
  const intervals = new Map();
  let nextTimer = 1;
  const window = {
    ...events(),
    setInterval(callback, milliseconds) {
      const timer = nextTimer++;
      intervals.set(timer, { callback, milliseconds });
      return timer;
    },
    clearInterval(timer) { intervals.delete(timer); },
  };
  const context = vm.createContext({ exports: {}, document, window });
  vm.runInContext(compiledSource, context, { filename: "foregroundPolling.js" });
  return {
    document, window, intervals,
    start: context.exports.startForegroundPolling,
    focus(value, notify = true) {
      focused = value;
      if (notify) window.dispatch(value ? "focus" : "blur");
    },
    hide(value, notify = true) {
      document.hidden = value;
      if (notify) document.dispatch("visibilitychange");
    },
    tick() { for (const { callback } of [...intervals.values()]) callback(); },
  };
}

test("visible focused panels refresh immediately and retain their existing cadence", () => {
  const page = fixture();
  let requests = 0;
  const stop = page.start(() => { requests++; }, 900);
  assert.equal(requests, 1);
  assert.equal(page.intervals.size, 1);
  assert.equal([...page.intervals.values()][0].milliseconds, 900);
  page.tick();
  assert.equal(requests, 2);
  stop();
});

test("an unfocused panel stops automatic requests while explicit command acknowledgements still run", () => {
  const page = fixture();
  let requests = 0;
  const refresh = () => { requests++; };
  const stop = page.start(refresh, 900);
  page.focus(false);
  assert.equal(page.intervals.size, 0);
  for (let i = 0; i < 100; i++) page.tick();
  assert.equal(requests, 1, "leaving the panel open behind Cuphead must not keep polling");
  refresh();
  assert.equal(requests, 2, "explicit refreshes after commands are outside automatic polling");
  page.focus(true);
  assert.equal(requests, 3, "returning to the panel refreshes without waiting for the next tick");
  assert.equal(page.intervals.size, 1);
  page.window.dispatch("focus");
  page.document.dispatch("visibilitychange");
  assert.equal(requests, 3, "duplicate focus/visibility notifications cannot multiply requests");
  page.tick();
  assert.equal(requests, 4);
  stop();
});

test("hidden or unfocused startup waits until both visibility and focus return", () => {
  for (const initial of [{ hidden: true, focused: true },
    { hidden: false, focused: false }, { hidden: true, focused: false }]) {
    const page = fixture(initial);
    let requests = 0;
    const stop = page.start(() => { requests++; }, 1250);
    assert.equal(requests, 0);
    assert.equal(page.intervals.size, 0);
    page.hide(true);
    page.focus(true);
    assert.equal(requests, 0, "focus cannot resume a hidden document");
    page.focus(false);
    page.hide(false);
    assert.equal(requests, 0, "visibility alone cannot resume an unfocused document");
    page.focus(true);
    assert.equal(requests, 1);
    assert.equal(page.intervals.size, 1);
    stop();
  }
});

test("visibility suspends a focused document and resume produces only one refresh", () => {
  const page = fixture();
  let requests = 0;
  const stop = page.start(() => { requests++; }, 1500);
  page.hide(true);
  assert.equal(page.intervals.size, 0);
  page.tick();
  assert.equal(requests, 1);
  page.hide(false);
  assert.equal(requests, 2);
  page.window.dispatch("focus");
  assert.equal(requests, 2);
  stop();
});

test("a missed browser notification still prevents a background request on the next tick", () => {
  for (const background of [page => page.focus(false, false), page => page.hide(true, false)]) {
    const page = fixture();
    let requests = 0;
    const stop = page.start(() => { requests++; }, 900);
    background(page);
    page.tick();
    assert.equal(requests, 1);
    assert.equal(page.intervals.size, 0);
    page.hide(false);
    page.focus(true);
    assert.equal(requests, 2);
    stop();
  }
});

test("unmount removes listeners and timers, including a callback already queued by the browser", () => {
  const page = fixture();
  let requests = 0;
  const stop = page.start(() => { requests++; }, 900);
  const queuedTick = [...page.intervals.values()][0].callback;
  stop();
  stop();
  assert.equal(page.intervals.size, 0);
  assert.equal(page.document.listenerCount() + page.window.listenerCount(), 0);
  queuedTick();
  page.focus(false);
  page.hide(true);
  page.hide(false);
  page.focus(true);
  assert.equal(requests, 1);

  const stopRemounted = page.start(() => { requests++; }, 900);
  assert.equal(requests, 2);
  assert.equal(page.intervals.size, 1, "React StrictMode remount cannot leave a previous timer running");
  stopRemounted();
});
