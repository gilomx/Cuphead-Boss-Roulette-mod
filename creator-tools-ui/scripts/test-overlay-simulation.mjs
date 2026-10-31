import assert from "node:assert/strict";
import { existsSync, mkdtempSync, rmSync, readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { spawnSync } from "node:child_process";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import { runInNewContext } from "node:vm";
import { matchesOverlayPreviewLayout } from "./mock-overlay-layout.mjs";

// Run the production reducer on every supported Node version, including Node 20.
const uiRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const temporary = mkdtempSync(join(tmpdir(), "pichi-overlay-simulation-"));
const exports = {};
try {
  const compiled = spawnSync(process.execPath, [
    "node_modules/typescript/bin/tsc", "src/features/overlay-designer/simulation.ts",
    "src/features/overlay-designer/interactionsPreview.ts",
    "src/features/overlay-designer/history.ts",
    "--ignoreConfig", "--target", "es2022", "--module", "commonjs",
    "--outDir", temporary, "--skipLibCheck",
  ], { cwd: uiRoot, encoding: "utf8", windowsHide: true });
  assert.equal(compiled.status, 0, compiled.error?.message || compiled.stdout + compiled.stderr);
  const folder = existsSync(join(temporary, "simulation.js")) ? temporary : join(temporary, "features/overlay-designer");
  const require = createRequire(import.meta.url);
  Object.assign(exports, require(join(folder, "simulation.js")), require(join(folder, "chatSimulation.js")),
    require(join(folder, "interactionsPreview.js")), require(join(folder, "model.js")),
    require(join(folder, "history.js")));
} finally {
  rmSync(temporary, { recursive: true, force: true });
}
const { createBattleSimulation, battleSimulationReducer, previewCommand } = exports;

test("tap previews grow above original boss health without raising their denominator", () => {
  const { createTapSimulation, tapSimulationReducer } = exports;
  let state = createTapSimulation("active");
  assert.equal(state.boss.totalHealth, 3000);
  assert.equal(state.effectiveHealth.total, 3000);
  assert.equal(state.effectiveHealth.current, 3742);
  assert.equal(state.effectiveHealth.ratio, 3742 / 3000);
  assert.equal(state.effectiveHealth.scope, "arena", "Dice Palace's fixture describes its current arena");
  state = tapSimulationReducer(state, { type: "add_taps", amount: 6000 });
  assert.equal(state.counters.reserveHealth, 4842);
  assert.equal(state.effectiveHealth.current, 6742);
  assert.equal(state.effectiveHealth.total, 3000);
  assert.equal(state.effectiveHealth.ratio, 6742 / 3000);
  state = tapSimulationReducer(state, { type: "damage", amount: 1000 });
  assert.equal(state.boss.currentHealth, 1900);
  assert.equal(state.effectiveHealth.current, 5742);
  assert.equal(state.effectiveHealth.total, 3000);
  assert.equal(state.effectiveHealth.ratio, 5742 / 3000);
  state = tapSimulationReducer(state, { type: "retry" });
  assert.equal(state.effectiveHealth.total, 3000);
  assert.equal(state.effectiveHealth.current, 6842);
  assert.ok(state.effectiveHealth.ratio > 2);
  const completed = createTapSimulation("completed");
  assert.equal(completed.effectiveHealth.total, 3000);
  assert.equal(completed.effectiveHealth.current, 0);
  assert.equal(completed.effectiveHealth.ratio, 0);
  const collecting = createTapSimulation("collecting");
  assert.equal(collecting.effectiveHealth.available, false);
  assert.equal(collecting.effectiveHealth.total, 0);
});

test("designer history retains ten edits, including redo, and branches after a new edit", () => {
  const { createEditHistory, recordEdit, undoEdit, redoEdit } = exports;
  let history = createEditHistory(0);
  for (let i = 1; i <= 15; i++) history = recordEdit(history, i);
  assert.equal(history.past.length, 10);
  for (let i = 0; i < 20; i++) history = undoEdit(history);
  assert.equal(history.present, 5);
  assert.equal(history.future.length, 10);
  for (let i = 0; i < 20; i++) history = redoEdit(history);
  assert.equal(history.present, 15);
  assert.equal(history.past.length, 10);
  history = undoEdit(history);
  history = recordEdit(history, 99);
  assert.equal(redoEdit(history).present, 99);
  assert.equal(history.future.length, 0);
});

test("continuous drag and layer swaps undo as one edit and remain separate from other gestures", () => {
  const { createEditHistory, recordEdit, undoEdit, redoEdit } = exports;
  const original = { x: 10, layers: [10, 20], battleOnly: true };
  let history = createEditHistory(original);
  for (let x = 11; x <= 200; x++) history = recordEdit(history, { ...original, x }, "drag-1");
  assert.equal(history.past.length, 1);
  assert.deepEqual(undoEdit(history).present, original);
  history = recordEdit(history, { ...history.present, layers: [20, 20] }, "layer-swap");
  history = recordEdit(history, { ...history.present, layers: [20, 10] }, "layer-swap");
  assert.equal(history.past.length, 2);
  const undone = undoEdit(history);
  assert.deepEqual(undone.present.layers, [10, 20]);
  assert.deepEqual(redoEdit(undone).present.layers, [20, 10]);
  const branch = recordEdit(undone, { ...undone.present, battleOnly: false }, "layer-swap");
  assert.equal(branch.past.length, 2, "an edit after undo starts a new step even with the same group id");
  assert.equal(branch.future.length, 0);
});

function mockComposer() {
  // Exercise the mock's actual API/normalizer without opening a localhost port.
  const source = readFileSync(join(uiRoot, "scripts/mock-server.mjs"), "utf8");
  const defaults = source.slice(source.indexOf("function defaultOverlayProfiles()"), source.indexOf("function tapFarmingState()"));
  const commands = source.slice(source.indexOf("function normalizeOverlayComponent("), source.indexOf("function serveAsset("));
  return runInNewContext(`let overlayComposerRevision = 1;\n${defaults}\n${commands}\n({
    normalize: normalizeOverlayComponent,
    state: overlayComposerState,
    send(command) { const response = {}; applyOverlayComposerCommand(command, response); return response; }
  })`, { json(response, body, status = 200) { response.body = body; response.status = status; } });
}

test("card appearance and expanded strip speeds persist through the mock API and copying profiles", () => {
  const mock = mockComposer();
  const card = (state, id = "vertical") => state.profiles.find(profile => profile.id === id).components.find(component => component.id === "interactions");
  const send = command => mock.send({ schemaVersion: 1, expectedRevision: mock.state().revision, profileId: "vertical", operation: "update", componentId: "interactions", ...command });
  assert.equal(card(mock.state()).interactionsCardColor, "#f8e5be");
  assert.equal(card(mock.state()).interactionsCardNoise, 80);
  const saved = send({ interactionsTemplate: "cards", interactionsCardColor: "#ABCDEF80", interactionsCardNoise: 77, interactionsSpeed: 140 });
  assert.equal(saved.status, 200);
  assert.equal(card(saved.body).interactionsCardColor, "#abcdef80");
  assert.equal(card(saved.body).interactionsCardNoise, 77);
  assert.equal(card(saved.body).interactionsSpeed, 140);
  const copied = send({ operation: "copy", profileId: "horizontal", sourceProfileId: "vertical" });
  assert.equal(copied.status, 200);
  assert.equal(card(copied.body, "horizontal").interactionsCardColor, "#abcdef80");
  assert.equal(card(copied.body, "horizontal").interactionsCardNoise, 77);
  assert.equal(card(copied.body, "horizontal").interactionsSpeed, 140);
  for (const command of [
    { interactionsCardColor: "#abc" }, { interactionsCardColor: "transparent" },
    { interactionsCardNoise: -1 }, { interactionsCardNoise: 101 }, { interactionsCardNoise: 18.5 },
    { interactionsSpeed: 39 }, { interactionsSpeed: 141 },
  ]) {
    const revision = mock.state().revision;
    assert.equal(send(command).status, 400, JSON.stringify(command));
    assert.equal(mock.state().revision, revision, "invalid options do not modify the saved profile");
  }
  assert.equal(send({ interactionsCardColor: "#123456", interactionsCardNoise: 0, interactionsSpeed: 40 }).status, 200);
  assert.equal(send({ interactionsCardNoise: 100 }).status, 200);
  const reset = send({ operation: "reset" });
  assert.equal(reset.status, 200);
  assert.equal(card(reset.body).interactionsCardColor, "#f8e5be");
  assert.equal(card(reset.body).interactionsCardNoise, 80);
});

test("old slow strip values migrate to the new minimum and missing card settings gain defaults", () => {
  const mock = mockComposer();
  const profile = mock.state().profiles.find(profile => profile.id === "vertical");
  const defaults = profile.components.find(component => component.id === "interactions");
  for (const speed of [10, 25, 39]) {
    const legacy = { ...defaults, interactionsSpeed: speed };
    delete legacy.interactionsCardColor;
    delete legacy.interactionsCardNoise;
    mock.normalize(legacy, profile.canvas);
    assert.equal(legacy.interactionsSpeed, 40);
    assert.equal(legacy.interactionsCardColor, "#f8e5be");
    assert.equal(legacy.interactionsCardNoise, 18);
  }
});

test("item outlines persist and copy independently while invalid edits leave the mock scene untouched", () => {
  const mock = mockComposer();
  const card = (state, profile = "vertical") => state.profiles.find(item => item.id === profile).components.find(item => item.id === "interactions");
  const send = command => mock.send({ operation: "update", expectedRevision: mock.state().revision,
    profileId: "vertical", componentId: "interactions", ...command });
  const saved = send({ interactionsImageOutlineColor: "#ABCDEF80", interactionsImageOutlineWidth: 8,
    interactionsTextOutlineColor: "#65432100", interactionsTextOutlineWidth: 3 });
  assert.equal(saved.status, 200);
  assert.equal(card(saved.body).interactionsImageOutlineColor, "#abcdef80");
  assert.equal(card(saved.body).interactionsTextOutlineColor, "#65432100");
  assert.equal(card(saved.body).interactionsImageOutlineWidth, 8);
  const copied = send({ operation: "copy", profileId: "horizontal", sourceProfileId: "vertical" });
  assert.equal(copied.status, 200);
  assert.equal(card(copied.body, "horizontal").interactionsTextOutlineWidth, 3);
  assert.equal(card(copied.body, "horizontal").interactionsImageOutlineColor, "#abcdef80");
  const before = JSON.stringify(mock.state());
  for (const invalid of [{ interactionsImageOutlineWidth: -1 }, { interactionsTextOutlineWidth: 9 },
    { interactionsImageOutlineWidth: 1.5 }, { interactionsTextOutlineWidth: null },
    { interactionsImageOutlineColor: "red" }, { interactionsTextOutlineColor: "#abc" },
    { componentId: "roulette", interactionsImageOutlineWidth: 2 }]) {
    assert.equal(send(invalid).status, 400);
    assert.equal(JSON.stringify(mock.state()), before);
  }
  const reset = send({ operation: "reset" });
  assert.equal(card(reset.body).interactionsImageOutlineWidth, 0);
  assert.equal(card(reset.body).interactionsTextOutlineWidth, 0);
});

test("global battle visibility changes independently of scene defaults and rejects malformed updates", () => {
  const mock = mockComposer();
  const scenes = JSON.stringify(mock.state().profiles);
  assert.equal(mock.state().interactionsBattleOnly, true);
  const send = value => mock.send({ operation: "interactions_visibility", profileId: "vertical",
    expectedRevision: mock.state().revision, interactionsBattleOnly: value });
  assert.equal(send(false).status, 200);
  assert.equal(mock.state().interactionsBattleOnly, false);
  assert.equal(JSON.stringify(mock.state().profiles), scenes);
  for (const invalid of ["true", null, 1]) assert.equal(send(invalid).status, 400);
  assert.equal(mock.state().interactionsBattleOnly, false);
});

test("mock preview accepts the complete five-component designer profile and rejects malformed component sets", () => {
  const profile = { id: "vertical", canvas: { width: 1080, height: 1920 },
    components: exports.COMPONENT_IDS.map(id => ({ id, enabled: false })) };
  const command = previewCommand("start", "vertical", "interactions", "preview-session",
    exports.createTapSimulation(), createBattleSimulation(), profile, false);
  const layout = JSON.parse(command.layoutJson);
  assert.equal(layout.components.length, 5);
  assert.equal(matchesOverlayPreviewLayout(layout, profile), true);
  assert.equal(matchesOverlayPreviewLayout({ ...layout, components: [...layout.components].reverse() }, profile), true);
  assert.equal(matchesOverlayPreviewLayout({ ...layout, components: layout.components.slice(0, 4) }, profile), false);
  assert.equal(matchesOverlayPreviewLayout({ ...layout, components: [...layout.components.slice(0, 4), layout.components[0]] }, profile), false);
  assert.equal(matchesOverlayPreviewLayout({ ...layout, components: [...layout.components.slice(0, 4), { id: "unknown" }] }, profile), false);
  assert.equal(matchesOverlayPreviewLayout({ ...layout, id: "horizontal" }, profile), false);
  assert.equal(matchesOverlayPreviewLayout({ ...layout, components: [null, ...layout.components.slice(1)] }, profile), false);
});

test("interaction designer examples remain separate from configured rules", () => {
  const example = exports.createInteractionsPreview({ rules: [] }, key => `translated:${key}`);
  assert.equal(example.examples, true);
  assert.equal(example.preview, true);
  assert.equal(new Set(example.rules.map(rule => rule.interaction)).size, 18, "two complete nine-item pages");
  assert.deepEqual([...new Set(example.rules.map(rule => rule.platform))], ["tiktok", "twitch", "youtube"]);
  assert.ok(example.rules.every(rule => rule.id < 0 && rule.enabled));
  assert.equal(exports.isOverlayComponentId("interactions"), true);
  assert.ok(exports.COMPONENT_IDS.includes("interactions"));

  const actual = { id: 1, enabled: true, name: "My price", every: 17, interaction: "help_extra_life", platform: "tiktok" };
  const disabled = { ...actual, id: 2, enabled: false };
  const config = { rules: [actual, disabled], communityGift: { giftId: "123", imagePath: "/gift.webp" } };
  const before = JSON.stringify(config);
  const preview = exports.createInteractionsPreview(config, key => key);
  assert.equal(preview.examples, false);
  assert.deepEqual(preview.rules, [actual]);
  assert.equal(preview.communityGift, config.communityGift);
  assert.equal(JSON.stringify(config), before, "preview does not alter saved prices or rules");
  const allDisabled = exports.createInteractionsPreview({ rules: [disabled] }, key => key);
  assert.equal(allDisabled.examples, false, "disabled rules must not be replaced by sample prices");
  assert.deepEqual(allDisabled.rules, []);

  const paginated = exports.createInteractionsPreview(config, key => key, true);
  assert.equal(paginated.examples, true);
  assert.equal(new Set(paginated.rules.map(rule => rule.interaction)).size, 18);
  assert.ok(paginated.rules.includes(actual));
  const gifts = new Set(paginated.rules.filter(rule => rule.platform === "tiktok" && rule.id < 0).map(rule => rule.giftId));
  assert.ok(gifts.size >= 6, "samples rotate through real gifts rather than repeating one placeholder");
  const catalog = JSON.parse(readFileSync(join(uiRoot, "../assets/creator-tools/gifts/catalog.json"), "utf8"));
  for (const id of gifts) {
    const gift = catalog.gifts.find(gift => gift.giftId === id);
    assert.ok(gift && existsSync(join(uiRoot, "..", gift.imagePath)), `real gift ${id} has bundled artwork`);
  }
  const twitchOnly = paginated.rules.filter(rule => rule.platform === "twitch");
  const everyNetwork = exports.createInteractionsPreview({ rules: twitchOnly }, key => key, true);
  for (const platform of ["tiktok", "twitch", "youtube"])
    assert.ok(new Set(everyNetwork.rules.filter(rule => rule.platform === platform).map(rule => rule.interaction)).size >= 18,
      `any platform selection still demonstrates pagination: ${platform}`);
  const emptyPages = exports.createInteractionsPreview({ rules: [disabled] }, key => key, true);
  assert.equal(new Set(emptyPages.rules.map(rule => rule.interaction)).size, 18);
  assert.equal(JSON.stringify(config), before);
});

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

test("roulette replay travels to OBS without changing the saved activation", () => {
  const profile = { id: "horizontal", canvas: { width: 1920, height: 1080 },
    components: [{ id: "roulette", enabled: false }] };
  const command = previewCommand("update", "horizontal", "roulette", "preview-session",
    exports.createTapSimulation(), createBattleSimulation(), profile, true, undefined, "hud", 3);
  assert.equal(command.scenario, "hud");
  assert.equal(command.eventEpoch, 3);
  assert.equal(JSON.parse(command.layoutJson).components[0].enabled, false);
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
