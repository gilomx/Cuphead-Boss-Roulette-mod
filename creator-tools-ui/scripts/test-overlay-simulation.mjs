import assert from "node:assert/strict";
import { existsSync, mkdtempSync, rmSync } from "node:fs";
import { createRequire } from "node:module";
import { spawnSync } from "node:child_process";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

// Run the production reducer on every supported Node version, including Node 20.
const uiRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const temporary = mkdtempSync(join(tmpdir(), "pichi-overlay-simulation-"));
const exports = {};
try {
  const compiled = spawnSync(process.execPath, [
    "node_modules/typescript/bin/tsc", "src/features/overlay-designer/simulation.ts",
    "--ignoreConfig", "--target", "es2022", "--module", "commonjs",
    "--outDir", temporary, "--skipLibCheck",
  ], { cwd: uiRoot, encoding: "utf8", windowsHide: true });
  assert.equal(compiled.status, 0, compiled.error?.message || compiled.stdout + compiled.stderr);
  const folder = existsSync(join(temporary, "simulation.js")) ? temporary : join(temporary, "features/overlay-designer");
  const require = createRequire(import.meta.url);
  Object.assign(exports, require(join(folder, "simulation.js")), require(join(folder, "chatSimulation.js")));
} finally {
  rmSync(temporary, { recursive: true, force: true });
}
const { createBattleSimulation, battleSimulationReducer, previewCommand } = exports;

test("chat designer scenarios and votes travel to OBS independently of real events", () => {
  const { createChatSimulation, chatSimulationReducer } = exports;
  let chat = createChatSimulation("en");
  chat = chatSimulationReducer(chat, { type: "vote", number: 1 });
  assert.equal(chat.totalVotes, 46);
  assert.equal(chat.options[0].votes, 9);
  chat = chatSimulationReducer(chat, { type: "stage", stage: "super" });
  assert.equal(chat.options.length, 4);
  assert.deepEqual(chat.options.map(option => option.image), ["supers/super1.png", "supers/super2.png", "supers/super3.png", "creator-tools/empty.png"]);
  chat = chatSimulationReducer(chat, { type: "count", count: 2 });
  assert.equal(chat.options.length, 4, "super always preserves all four candidates");
  chat = chatSimulationReducer(chat, { type: "scenario", phase: "reveal" });
  const before = JSON.stringify(chat.options);
  chat = chatSimulationReducer(chat, { type: "vote", number: 1 });
  assert.equal(JSON.stringify(chat.options), before, "reveal does not accept votes");
  chat = chatSimulationReducer(chat, { type: "scenario", phase: "result" });
  const command = previewCommand("update", "vertical", "chat_chooses", "preview-session",
    exports.createTapSimulation(), createBattleSimulation(),
    { id: "vertical", canvas: { width: 1080, height: 1920 }, components: [] }, true, chat);
  assert.equal(command.scenario, "result");
  assert.equal(JSON.parse(command.chatStateJson).selected.weapon1.name, "Peashooter");
  const reset = chatSimulationReducer(chat, { type: "reset" });
  assert.equal(reset.options[0].name, "Ribby and Croaks");
  assert.ok(reset.sessionId > chat.sessionId);
});

test("the selected entry gift survives scenario changes, capacity, joining and reset", () => {
  const trigger = { giftId: "1261956", giftName: "Fuego", giftImagePath: "/assets/creator-tools/gifts/images/1261956.webp" };
  let state = battleSimulationReducer(createBattleSimulation(), { type: "gift", trigger });
  assert.equal(battleSimulationReducer(state, { type: "gift", trigger }), state, "same config poll does not restart the preview");
  for (const action of [
    { type: "scenario", scenario: "active" },
    { type: "capacity", capacity: 4 },
    { type: "reset" },
    { type: "participants", count: 2 },
    { type: "scenario", scenario: "recruiting" },
  ]) {
    state = battleSimulationReducer(state, action);
    assert.equal(state.trigger.giftId, trigger.giftId);
    assert.equal(state.trigger.giftName, trigger.giftName);
    assert.equal(state.trigger.giftImagePath, trigger.giftImagePath);
  }
});

test("a burst retains every attack through OBS transport and challenge simulation has a full lifetime", () => {
  let state = createBattleSimulation("active", undefined, 3);
  for (let i = 0; i < 3; i++) state = battleSimulationReducer(state,
    { type: "attack", name: `Attack ${i}`, imagePath: `/attack-${i}.png`, startedAt: 1000, slot: 2 });
  assert.deepEqual(Array.from(state.attacks, event => event.slot), [2, 2, 2]);
  assert.equal(new Set(state.attacks.map(event => event.id)).size, 3);
  state = battleSimulationReducer(state, { type: "challenge", name: "No dash", imagePath: "/challenge.png", startedAt: 1000, slot: 2 });
  const command = previewCommand("update", "horizontal", "pesky_battle", "session", exports.createTapSimulation(), state,
    { id: "horizontal", canvas: { width: 1920, height: 1080 }, components: [] }, true);
  assert.equal(JSON.parse(command.battleSignalsJson).attacks.length, 3);
  assert.equal(JSON.parse(command.battleSignalsJson).challenges[0].slot, 2);
  state = battleSimulationReducer(state, { type: "challenge_tick", now: 3000 });
  assert.equal(state.challenges[0].phase, "countdown");
  assert.equal(state.challenges[0].secondsRemaining, 1);
  state = battleSimulationReducer(state, { type: "challenge_tick", now: 4000 });
  assert.equal(state.challenges[0].phase, "active");
  assert.equal(state.challenges[0].secondsRemaining, 15);
  state = battleSimulationReducer(state, { type: "challenge_tick", now: 19000 });
  assert.equal(state.challenges.length, 0);
  const epoch = state.eventEpoch;
  state = battleSimulationReducer(state, { type: "attempt", attempt: 3 });
  assert.ok(state.eventEpoch > epoch);
  assert.equal(state.attacks.length, 0);
});

test("the chosen capacity survives scenarios, joining, resets and OBS transport", () => {
  for (const capacity of [2, 3, 4, 5]) {
    let state = createBattleSimulation("off", undefined, capacity);
    assert.equal(state.participants.length, 0);
    state = battleSimulationReducer(state, { type: "scenario", scenario: "recruiting" });
    assert.equal(state.participants.length, 0);
    state = battleSimulationReducer(state, { type: "participants", count: capacity - 1 });
    assert.equal(state.phase, "recruiting");
    state = battleSimulationReducer(state, { type: "participants", count: capacity });
    assert.equal(state.phase, "ready");
    for (const scenario of ["ready", "waiting_level", "active", "won"]) {
      state = battleSimulationReducer(state, { type: "scenario", scenario });
      assert.equal(state.capacity, capacity);
      assert.equal(state.participants.length, capacity);
    }
    const command = previewCommand("update", "horizontal", "pesky_battle", "test-session",
      exports.createTapSimulation(), state,
      { id: "horizontal", canvas: { width: 1920, height: 1080 }, components: [] },
      true);
    assert.equal(command.capacity, capacity);
    assert.equal(command.participantCount, capacity);
    state = battleSimulationReducer(state, { type: "reset" });
    assert.equal(state.capacity, capacity);
    assert.equal(state.phase, "recruiting");
    assert.equal(state.participants.length, 0);
  }
});

test("changing capacity preserves complete teams and never activates an idle game", () => {
  let state = createBattleSimulation("active");
  for (const capacity of [2, 5, 3, 4]) {
    state = battleSimulationReducer(state, { type: "capacity", capacity });
    assert.equal(state.capacity, capacity);
    assert.equal(state.participants.length, capacity);
    assert.equal(state.phase, "active");
  }
  state = battleSimulationReducer(state, { type: "scenario", scenario: "off" });
  state = battleSimulationReducer(state, { type: "capacity", capacity: 2 });
  assert.equal(state.phase, "off");
  assert.equal(state.participants.length, 0);
  for (const capacity of [0, 1, 6, 2.5, NaN]) {
    assert.equal(battleSimulationReducer(state, { type: "capacity", capacity }), state);
  }
});

test("attack simulation rotates occupied players, travels to OBS and clears on reset", () => {
  const action = { type: "attack", name: "Bomba", imagePath: "/bomb.png", startedAt: Date.now() };
  for (const phase of ["off", "recruiting", "ready", "waiting_level", "won"]) {
    const state = createBattleSimulation(phase);
    assert.equal(battleSimulationReducer(state, action), state);
  }
  let state = createBattleSimulation("active", 0, 3);
  assert.equal(battleSimulationReducer(state, action), state);
  state = createBattleSimulation("active", undefined, 3);
  for (let index = 0; index < 7; index++) {
    state = battleSimulationReducer(state, action);
    assert.equal(state.attack.slot, index % 3 + 1);
    assert.equal(state.attack.sequence, index + 1);
  }
  const command = previewCommand("update", "horizontal", "pesky_battle", "test-session",
    exports.createTapSimulation(), state,
    { id: "horizontal", canvas: { width: 1920, height: 1080 }, components: [] }, true);
  assert.equal(command.attackId, state.attack.id);
  assert.equal(command.attackSlot, 1);
  assert.equal(command.attackName, "Bomba");
  assert.equal(command.attackStartedAt, action.startedAt);
  assert.equal(command.attackImagePath, "/bomb.png");
  state = battleSimulationReducer(state, { type: "reset" });
  assert.equal(state.attack, undefined);
});
