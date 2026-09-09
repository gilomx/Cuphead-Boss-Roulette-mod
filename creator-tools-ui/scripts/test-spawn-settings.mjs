import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { createServer } from "node:net";
import { dirname, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const uiRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const groupDefaults = {
  miniBossMinimumInterval: 30, miniBossMaximumInterval: 30,
  lightMinimumBatch: 1, lightMaximumBatch: 1,
  strongMinimumBatch: 1, strongMaximumBatch: 1,
};
const numericKeys = [
  "minimumInterval", "maximumInterval", "miniBossCooldownSeconds",
  "miniBossIntervalMultiplier", "maximumCompanionsDuringMiniBoss",
  ...Object.keys(groupDefaults),
];
const select = (value, keys) => Object.fromEntries(keys.map((key) => [key, value[key]]));
const numericSettings = (state) => select(state, numericKeys);
const mandatoryPacing = {
  enabled: true, minimumInterval: 2, maximumInterval: 4,
  miniBossIntervalMultiplier: 3, maximumCompanionsDuringMiniBoss: 2,
};

async function unusedPort() {
  const reservation = createServer();
  reservation.listen(0, "127.0.0.1");
  await once(reservation, "listening");
  const port = reservation.address().port;
  await new Promise((resolve, reject) => reservation.close((error) => error ? reject(error) : resolve()));
  return port;
}

test("HTTP spawn settings contract (mock only; no gameplay scheduling)", async (t) => {
  const port = await unusedPort();
  const child = spawn(process.execPath, ["scripts/mock-server.mjs", String(port), "127.0.0.1"], {
    cwd: uiRoot,
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });
  let output = "";
  child.stdout.on("data", (chunk) => { output += chunk; });
  child.stderr.on("data", (chunk) => { output += chunk; });
  t.after(async () => {
    if (child.exitCode !== null || child.signalCode !== null) return;
    const exited = once(child, "exit");
    child.kill();
    const force = setTimeout(() => child.kill("SIGKILL"), 3000);
    try { await exited; } finally { clearTimeout(force); }
  });
  await new Promise((resolve, reject) => {
    const timeout = setTimeout(() => finish(new Error(`Mock startup timed out. ${output}`)), 5000);
    const onData = () => {
      if (output.includes("Creator Tools mock listening")) finish();
    };
    const onExit = (code) => finish(new Error(`Mock exited during startup (${code}). ${output}`));
    const onError = (error) => finish(error);
    function finish(error) {
      clearTimeout(timeout);
      child.stdout.off("data", onData);
      child.off("exit", onExit);
      child.off("error", onError);
      error ? reject(error) : resolve();
    }
    child.stdout.on("data", onData);
    child.once("exit", onExit);
    child.once("error", onError);
  });

  const request = async (path, values) => {
    const query = values ? `?${new URLSearchParams(values)}` : "";
    const response = await fetch(`http://127.0.0.1:${port}${path}${query}`, {
      signal: AbortSignal.timeout(3000),
    });
    return { status: response.status, body: await response.json() };
  };
  const getPesky = async () => (await request("/api/config/pesky")).body;
  const getInteractions = async () => (await request("/api/config/interactions")).body;
  const setPesky = (values) => request("/api/config/pesky/set", values);
  const setInteractions = (values, other = {}) => request("/api/config/interactions/set", {
    ...other,
    ...Object.fromEntries(Object.entries(values).map(([key, value]) => ["pacing." + key, value])),
  });
  const accepted = (response) => {
    assert.equal(response.status, 202);
    assert.equal(response.body.ok, true);
  };
  const initialPesky = await getPesky();
  assert.equal(initialPesky.allowConcurrentStrongInteractions, false);
  assert.equal(initialPesky.defaultAllowConcurrentStrongInteractions, false);
  const initialInteractions = await getInteractions();
  const peskyDefaults = Object.fromEntries(numericKeys.map((key) => [
    key, initialPesky["default" + key[0].toUpperCase() + key.slice(1)],
  ]));

  await t.test("snapshots expose all six current values and restore defaults", () => {
    assert.deepEqual(select(initialPesky, Object.keys(groupDefaults)), groupDefaults);
    assert.deepEqual(select(peskyDefaults, Object.keys(groupDefaults)), groupDefaults);
    assert.deepEqual(select(initialInteractions.pacing, Object.keys(groupDefaults)), groupDefaults);
    assert.deepEqual(select(initialInteractions.defaultPacing, Object.keys(groupDefaults)), groupDefaults);
    assert.equal(initialInteractions.defaultPacing.enabled, false);
    assert.equal(initialInteractions.maxMiniBosses, 1);
    assert.deepEqual(numericSettings(initialPesky), peskyDefaults);
  });

  await t.test("intense concurrency is atomic, defaults off and belongs only to Pesky", async () => {
    accepted(await setPesky({ ...peskyDefaults, allowConcurrentStrongInteractions: true }));
    assert.equal((await getPesky()).allowConcurrentStrongInteractions, true);
    accepted(await setPesky(peskyDefaults));
    assert.equal((await getPesky()).allowConcurrentStrongInteractions, true);
    for (const invalid of ["yes", "2", "", "null", "on"]) {
      const rejected = await setPesky({ ...peskyDefaults, minimumInterval: 7, maximumInterval: 9, allowConcurrentStrongInteractions: invalid });
      assert.equal(rejected.body.ok, false);
      const state = await getPesky();
      assert.equal(state.allowConcurrentStrongInteractions, true);
      assert.deepEqual(numericSettings(state), peskyDefaults);
    }
    assert.deepEqual((await getInteractions()).pacing, initialInteractions.pacing);
    accepted(await setPesky({ ...peskyDefaults, allowConcurrentStrongInteractions: false }));
    assert.equal((await getPesky()).allowConcurrentStrongInteractions, false);
  });

  await t.test("each mode saves independent ranges and group sizes", async () => {
    const peskyValues = {
      ...peskyDefaults, minimumInterval: 1, maximumInterval: 2,
      miniBossMinimumInterval: 5, miniBossMaximumInterval: 9,
      lightMinimumBatch: 2, lightMaximumBatch: 5,
      strongMinimumBatch: 1, strongMaximumBatch: 3,
    };
    accepted(await setPesky(peskyValues));
    assert.deepEqual((await getInteractions()).pacing, initialInteractions.pacing);
    assert.deepEqual(numericSettings(await getPesky()), { ...peskyValues, miniBossCooldownSeconds: 5 });
    accepted(await setInteractions({
      ...mandatoryPacing, miniBossMinimumInterval: 12, miniBossMaximumInterval: 18,
      lightMinimumBatch: 3, lightMaximumBatch: 6,
      strongMinimumBatch: 2, strongMaximumBatch: 4,
    }, { maxActive: 8, showGiftImage: 0 }));
    const saved = await getInteractions();
    assert.equal(saved.pacing.miniBossCooldownSeconds, 12);
    assert.equal(saved.pacing.lightMaximumBatch, 6);
    assert.equal(saved.maxActive, 8);
    assert.equal(saved.showGiftImage, false);
    assert.deepEqual(numericSettings(await getPesky()), { ...peskyValues, miniBossCooldownSeconds: 5 });
  });

  await t.test("omitted new pairs survive ordinary edits and legacy requests", async () => {
    const beforePesky = numericSettings(await getPesky());
    accepted(await setPesky({ minimumInterval: 2.5, maximumInterval: 5 }));
    assert.deepEqual(numericSettings(await getPesky()), {
      ...beforePesky, minimumInterval: 2.5, maximumInterval: 5,
    });
    const beforeInteractions = (await getInteractions()).pacing;
    accepted(await setInteractions(mandatoryPacing));
    assert.deepEqual((await getInteractions()).pacing, { ...beforeInteractions, ...mandatoryPacing });
    accepted(await setPesky({ minimumInterval: 2.5, maximumInterval: 5, miniBossCooldownSeconds: 7.5 }));
    assert.equal((await getPesky()).miniBossMinimumInterval, 7.5);
    assert.equal((await getPesky()).miniBossMaximumInterval, 7.5);
    accepted(await setInteractions({ ...mandatoryPacing, miniBossCooldownSeconds: 11 }));
    assert.equal((await getInteractions()).pacing.miniBossMinimumInterval, 11);
    assert.equal((await getInteractions()).pacing.miniBossMaximumInterval, 11);
    for (const [send, read] of [
      [(value) => setPesky(value), async () => getPesky()],
      [(value) => setInteractions({ ...mandatoryPacing, ...value }), async () => (await getInteractions()).pacing],
    ]) {
      accepted(await send({
        minimumInterval: 2, maximumInterval: 4, miniBossCooldownSeconds: 99,
        miniBossMinimumInterval: 6, miniBossMaximumInterval: 10,
      }));
      const saved = await read();
      assert.equal(saved.miniBossCooldownSeconds, 6);
      assert.equal(saved.miniBossMinimumInterval, 6);
      assert.equal(saved.miniBossMaximumInterval, 10);
    }
  });

  await t.test("invalid or incomplete requests do not partially change either mode", async () => {
    const invalidRequests = [
      { minimumInterval: "" }, { minimumInterval: "NaN" }, { maximumInterval: "Infinity" },
      { minimumInterval: 5, maximumInterval: 4 }, { minimumInterval: 0.34 },
      { miniBossMinimumInterval: 5 }, { miniBossMaximumInterval: 10 },
      { lightMinimumBatch: 2 }, { lightMaximumBatch: 3 },
      { strongMinimumBatch: 2 }, { strongMaximumBatch: 3 },
      { miniBossMinimumInterval: -1, miniBossMaximumInterval: 10 },
      { miniBossMinimumInterval: 1, miniBossMaximumInterval: 301 },
      { miniBossMinimumInterval: 11, miniBossMaximumInterval: 10 },
      { miniBossMinimumInterval: " ", miniBossMaximumInterval: 10 },
      { miniBossMinimumInterval: 1, miniBossMaximumInterval: "Infinity" },
      { lightMinimumBatch: 0, lightMaximumBatch: 1 },
      { lightMinimumBatch: 1, lightMaximumBatch: 21 },
      { lightMinimumBatch: 3, lightMaximumBatch: 2 },
      { lightMinimumBatch: 1.5, lightMaximumBatch: 2 },
      { lightMinimumBatch: 1, lightMaximumBatch: "" },
      { strongMinimumBatch: 0, strongMaximumBatch: 1 },
      { strongMinimumBatch: 1, strongMaximumBatch: 21 },
      { strongMinimumBatch: 3, strongMaximumBatch: 2 },
      { strongMinimumBatch: 1, strongMaximumBatch: 2.5 },
      { strongMinimumBatch: "NaN", strongMaximumBatch: 2 },
      { strongMinimumBatch: 1, strongMaximumBatch: "0x10" },
      { miniBossCooldownSeconds: "" }, { miniBossCooldownSeconds: -1 },
      { miniBossCooldownSeconds: "bad", miniBossMinimumInterval: 1, miniBossMaximumInterval: 2 },
      { miniBossIntervalMultiplier: 0 }, { miniBossIntervalMultiplier: 11 },
      { maximumCompanionsDuringMiniBoss: 1.5 }, { maximumCompanionsDuringMiniBoss: 21 },
    ];
    for (const invalid of invalidRequests) {
      const beforePesky = await getPesky();
      const beforeInteractions = await getInteractions();
      const peskyResponse = await setPesky({ minimumInterval: 3, maximumInterval: 4, ...invalid });
      assert.equal(peskyResponse.body.ok, false, JSON.stringify(invalid));
      assert.equal(peskyResponse.body.feedback, "invalid_interval");
      assert.deepEqual(numericSettings(await getPesky()), numericSettings(beforePesky));
      assert.deepEqual((await getInteractions()).pacing, beforeInteractions.pacing);
      const interactionResponse = await setInteractions({ ...mandatoryPacing, minimumInterval: 3, ...invalid }, {
        maxActive: 18, showGiftImage: 1,
      });
      assert.equal(interactionResponse.status, 400, JSON.stringify(invalid));
      assert.equal(interactionResponse.body.feedback, "invalid_setting");
      const afterInteractions = await getInteractions();
      assert.deepEqual(select(afterInteractions, ["pacing", "maxActive", "showGiftImage", "settingsRevision"]),
        select(beforeInteractions, ["pacing", "maxActive", "showGiftImage", "settingsRevision"]));
      assert.deepEqual(numericSettings(await getPesky()), numericSettings(beforePesky));
    }
    const before = await getInteractions();
    for (const other of [{ maxActive: "invalid" }, { maxActive: 1.5 }, { maxMiniBosses: "bad" }, { showGiftImage: "bad" }]) {
      assert.equal((await setInteractions({ ...mandatoryPacing, minimumInterval: 3 }, other)).status, 400);
      assert.deepEqual((await getInteractions()).pacing, before.pacing);
    }
    assert.equal((await setInteractions({ miniBossMinimumInterval: 1, miniBossMaximumInterval: 2 })).status, 400);
    assert.equal((await setInteractions({ ...mandatoryPacing, enabled: "bad" })).status, 400);
    for (const missing of Object.keys(mandatoryPacing)) {
      const incomplete = { ...mandatoryPacing };
      delete incomplete[missing];
      assert.equal((await setInteractions(incomplete)).status, 400, `missing ${missing}`);
    }
    assert.equal((await setPesky({ minimumInterval: 2 })).body.ok, false);
    const groupOnly = await setPesky({ miniBossMinimumInterval: 1, miniBossMaximumInterval: 2 });
    assert.equal(groupOnly.body.ok, false);
    assert.equal(groupOnly.body.feedback, "invalid_setting");
  });

  await t.test("valid boundaries allow zero rest, fractional seconds and batches of twenty", async () => {
    const values = {
      minimumInterval: 0.35, maximumInterval: 300,
      miniBossMinimumInterval: 0, miniBossMaximumInterval: 300,
      lightMinimumBatch: 1, lightMaximumBatch: 20,
      strongMinimumBatch: 20, strongMaximumBatch: 20,
      miniBossIntervalMultiplier: 1.5, maximumCompanionsDuringMiniBoss: 0,
    };
    accepted(await setPesky(values));
    accepted(await setInteractions({ enabled: true, ...values }));
    assert.deepEqual(numericSettings(await getPesky()), numericSettings((await getInteractions()).pacing));
    accepted(await setPesky({ minimumInterval: "3.5e-1", maximumInterval: "3e2" }));
    assert.equal((await getPesky()).minimumInterval, 0.35);
  });

  await t.test("copy and restore requests preserve toggles, queues and unrelated settings", async () => {
    accepted(await setPesky({ enabled: 1 }));
    accepted(await setPesky({ names: "Preserved viewer" }));
    accepted(await setPesky({ item: "baroness_waffle", itemEnabled: 0 }));
    accepted(await request("/api/config/interactions/set", { interactionsEnabled: 1 }));
    accepted(await request("/api/config/interactions/set", { queuePaused: 1 }));
    accepted(await request("/api/config/interactions/test", {
      item: "hilda_green_zeppelin", donor: "Preserved gift", quantity: 3, delay: 3600,
    }));
    accepted(await setInteractions({ ...mandatoryPacing, enabled: false }));
    const beforePesky = await getPesky();
    const beforeInteractions = await getInteractions();
    const copied = {
      ...peskyDefaults, minimumInterval: 1.2, maximumInterval: 2.2,
      miniBossMinimumInterval: 8, miniBossMaximumInterval: 14,
      lightMinimumBatch: 2, lightMaximumBatch: 5,
      strongMinimumBatch: 1, strongMaximumBatch: 2,
    };
    // The UI copies with one request per owner, preserving the saved switches.
    accepted(await setPesky(copied));
    accepted(await setInteractions({ ...copied, enabled: beforeInteractions.pacing.enabled }));
    assert.deepEqual(numericSettings(await getPesky()), { ...copied, miniBossCooldownSeconds: 8 });
    assert.deepEqual(numericSettings((await getInteractions()).pacing), { ...copied, miniBossCooldownSeconds: 8 });
    accepted(await setPesky({ minimumInterval: 4, maximumInterval: 5 }));
    assert.equal((await getInteractions()).pacing.minimumInterval, 1.2);
    // Restore uses snapshot defaults and the normal save endpoints.
    accepted(await setPesky(peskyDefaults));
    assert.equal((await getInteractions()).pacing.lightMaximumBatch, 5);
    accepted(await setInteractions({ ...initialInteractions.defaultPacing, enabled: beforeInteractions.pacing.enabled }));
    const afterPesky = await getPesky();
    const afterInteractions = await getInteractions();
    assert.deepEqual(numericSettings(afterPesky), peskyDefaults);
    assert.deepEqual(afterInteractions.pacing, initialInteractions.defaultPacing);
    assert.deepEqual(select(afterPesky, ["enabled", "names", "disabledItems"]),
      select(beforePesky, ["enabled", "names", "disabledItems"]));
    assert.deepEqual(select(afterInteractions, ["interactionsEnabled", "queuePaused", "queue", "maxActive", "showGiftImage", "maxMiniBosses"]),
      select(beforeInteractions, ["interactionsEnabled", "queuePaused", "queue", "maxActive", "showGiftImage", "maxMiniBosses"]));
  });
});
