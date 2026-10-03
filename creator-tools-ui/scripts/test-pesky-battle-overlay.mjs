import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import test from "node:test";

const source = readFileSync(new URL("../../assets/creator-tools/pesky-battle-overlay.js", import.meta.url), "utf8");
const catalogSource = readFileSync(new URL("../../assets/creator-tools/overlay-interactions.js", import.meta.url), "utf8");
class Element {
  dataset = {};
  attributes = {};
  hidden = false;
  textContent = "";
  children = [];
  events = {};
  srcWrites = 0;
  style = { values: {}, setProperty(key, value) { this.values[key] = value; } };
  set src(value) { this.attributes.src = value; this.srcWrites++; }
  get src() { return this.attributes.src; }
  setAttribute(key, value) { this.attributes[key] = value; }
  removeAttribute(key) { delete this.attributes[key]; }
  addEventListener(name, callback) { this.events[name] = callback; }
  append(fragment) { this.children.push(fragment.querySelector(".battle-slot")); }
  replaceChildren() { this.children = []; }
}
function harness(reducedMotion = false) {
  const timers = new Map();
  let timerId = 0;
  let now = Date.now();
  class Clock extends Date { static now() { return now; } }
  const advance = milliseconds => {
    const until = now + milliseconds;
    while (true) {
      const next = [...timers].sort((a, b) => a[1].at - b[1].at)[0];
      if (!next || next[1].at > until) break;
      now = next[1].at;
      timers.delete(next[0]);
      next[1].callback();
    }
    now = until;
  };
  const elements = Object.fromEntries(["battle", "battle-status", "battle-roster"].map(id => [id, new Element()]));
  elements["battle-slot-template"] = { content: { cloneNode() {
    const nodes = Object.fromEntries(["battle-slot", "battle-slot__avatar", "battle-slot__gift", "battle-slot__coin", "battle-slot__initial", "battle-slot__name", "battle-slot__attack", "battle-slot__attack-visual", "battle-slot__attack-image", "battle-slot__challenge", "battle-slot__challenge-visual", "battle-slot__challenge-image"].map(name => [name, new Element()]));
    nodes["battle-slot"].nodes = nodes;
    return { querySelector: selector => nodes[selector.slice(1)] };
  } } };
  const document = { documentElement: {}, getElementById: id => elements[id] };
  let config;
  runInNewContext(catalogSource + source, { document, URL, URLSearchParams, Date: Clock, window: {
    location: { origin: "http://localhost:18091", search: "" },
    LiveEventOverlayRuntime: { create(value) { config = value; } },
    setTimeout(callback, delay) { timers.set(++timerId, { callback, at: now + delay }); return timerId; },
    clearTimeout(id) { timers.delete(id); },
    matchMedia() { return { matches: reducedMotion }; },
  } });
  return { render: config.render, root: elements.battle, status: elements["battle-status"],
    roster: elements["battle-roster"], document, timers, advance, now: () => now };
}
const entry = { giftName: "Rosa", giftImagePath: "/assets/creator-tools/gifts/images/5655.webp" };
const state = (capacity, participants = []) => ({ phase: "recruiting", capacity, trigger: entry, participants });
const player = (slot, name = "Test Player") => ({ slot, displayName: name, avatarUrl: "/avatar.webp" });

test("batches queue three native attacks per player, run different players concurrently and deduplicate polls", () => {
  const h = harness();
  const events = [1, 2, 3].map(id => ({ id: String(id), slot: 1, startedAt: h.now(),
    item: ["baroness_head_toss", "dragon_fireballs", "devil_fire_circle"][id - 1] }));
  // Use the runtime ids from the generated catalog (keeps renamed assets covered).
  const catalog = {};
  runInNewContext(catalogSource, { window: catalog });
  const ids = Object.keys(catalog.CreatorToolsOverlayInteractions).filter(id =>
    /baroness-head-toss|dragon-fireballs|devil-fire-circle/.test(catalog.CreatorToolsOverlayInteractions[id].imagePath));
  events.forEach((event, index) => event.item = ids[index]);
  const snapshot = { ...state(2, [player(1, "Same"), player(2, "Same")]), phase: "active",
    sessionId: 4, attempt: 1, eventEpoch: 2, serverTime: h.now(),
    attacks: [...events, { ...events[0], id: "4", slot: 2 }], challenges: [] };
  h.render(snapshot);
  const first = h.roster.children[0];
  const second = h.roster.children[1];
  assert.equal(first.dataset.attacking, "true");
  assert.equal(second.dataset.attacking, "true");
  const image = first.nodes["battle-slot__attack-image"];
  const expected = events.map(event => catalog.CreatorToolsOverlayInteractions[event.item].imagePath);
  assert.ok(image.src.endsWith(expected[0]));
  assert.equal(first.nodes["battle-slot__attack-visual"].style.values["--battle-asset-size"], "82%");
  h.advance(700);
  h.render(snapshot);
  assert.equal(image.srcWrites, 1, "same poll never replaces or duplicates the displayed event");
  h.advance(1100);
  assert.ok(image.src.endsWith(expected[1]));
  assert.equal(first.nodes["battle-slot__attack-visual"].style.values["--battle-asset-size"], "103%");
  assert.equal(second.dataset.attacking, "false");
  h.advance(1800);
  assert.ok(image.src.endsWith(expected[2]));
  assert.equal(first.nodes["battle-slot__attack-visual"].style.values["--battle-asset-size"], "105%");
  h.advance(1800);
  assert.equal(first.dataset.attacking, "false");
  h.render(snapshot);
  assert.equal(image.srcWrites, 3, "all three events were displayed exactly once");
});

test("retry, roster changes and cancellation clear pending attacks and persistent challenges", () => {
  const h = harness();
  const snapshot = { ...state(2, [player(1), player(2)]), phase: "active", attempt: 1,
    attacks: [1, 2, 3].map(id => ({ id: String(id), slot: 1, startedAt: h.now(), imagePath: "/attack.png" })),
    challenges: [{ id: "c", slot: 1, imagePath: "/challenge.png", phase: "active", secondsRemaining: 15 }] };
  h.render(snapshot);
  const first = h.roster.children[0];
  assert.equal(first.dataset.challenge, "true");
  h.render({ ...snapshot, attempt: 2, attacks: [], challenges: [] });
  assert.equal(first.dataset.attacking, "false");
  assert.equal(first.dataset.challenge, "false");
  h.advance(6000);
  assert.equal(first.nodes["battle-slot__attack-image"].srcWrites, 1, "old queued attacks never enter a retry");
  h.render({ ...snapshot, eventEpoch: 2, attacks: [{ id: "5", slot: 1, startedAt: h.now(), imagePath: "/new.png" }] });
  h.render({ ...snapshot, phase: "off" });
  h.advance(400);
  assert.equal(h.roster.children.length, 0);
  assert.equal(h.timers.size, 0);
});

test("challenge state follows native snapshots without rendering a countdown", () => {
  const h = harness();
  const challenge = { id: "c", slot: 2, item: "challenge_no_dash", phase: "countdown", secondsRemaining: 3 };
  const snapshot = { ...state(2, [player(1), player(2)]), phase: "active", attacks: [], challenges: [challenge] };
  h.render(snapshot);
  const first = h.roster.children[0];
  const second = h.roster.children[1];
  const nodes = second.nodes;
  assert.equal(first.dataset.challenge, "false");
  assert.equal(second.dataset.challenge, "true");
  assert.equal(nodes["battle-slot__challenge"].attributes["aria-label"], "NO DASH / NO MINIAVIÓN");
  assert.equal(nodes["battle-slot__challenge-visual"].style.values["--battle-asset-size"], "100%");
  h.advance(30000);
  h.render(snapshot);
  assert.equal(nodes["battle-slot__challenge-image"].srcWrites, 1);
  h.render({ ...snapshot, challenges: [{ ...challenge, phase: "active", secondsRemaining: 15 }] });
  assert.equal(nodes["battle-slot__challenge"].dataset.phase, "active");
  assert.equal(nodes["battle-slot__challenge"].attributes["aria-label"], "NO DASH / NO MINIAVIÓN");
  h.render({ ...snapshot, challenges: [] });
  assert.equal(second.dataset.challenge, "false");
  h.render({ ...snapshot, challenges: [{ ...challenge, slot: 3 }] });
  assert.equal(second.dataset.challenge, "false", "invalid participant never receives a badge");
});

test("first connection ignores stale history and accepts native server clocks from another PC", () => {
  const h = harness();
  const clock = h.now() + 600000;
  const snapshot = { ...state(2, [player(1)]), phase: "active", serverTime: clock,
    attacks: [{ id: "old", slot: 1, startedAt: clock, ageMs: 8000, imagePath: "/old.png" },
      { id: "fresh", slot: 1, startedAt: clock - 100, imagePath: "/fresh.png" }] };
  h.render(snapshot);
  const image = h.roster.children[0].nodes["battle-slot__attack-image"];
  assert.ok(image.src.endsWith("/fresh.png"));
  h.advance(1800);
  h.render(snapshot);
  assert.equal(image.srcWrites, 1);
});

test("creates exactly 2–5 gift circles and changes capacity", () => {
  const h = harness();
  for (const capacity of [2, 3, 4, 5]) {
    h.render(state(capacity));
    assert.equal(h.roster.children.length, capacity);
    assert.equal(h.root.dataset.capacity, String(capacity));
    for (const item of h.roster.children) {
      assert.equal(item.dataset.filled, "false");
      assert.equal(item.nodes["battle-slot__gift"].hidden, false);
      assert.equal(item.nodes["battle-slot__initial"].hidden, true);
      assert.equal(item.nodes["battle-slot__name"].hidden, true);
    }
  }
  for (const invalid of [0, 1, 6, 2.5, "bad"]) {
    h.render(state(invalid));
    assert.equal(h.roster.children.length, 5);
  }
});

test("fills only the joining player's circle without reloading existing portraits", () => {
  const h = harness();
  h.render(state(5));
  const original = [...h.roster.children];
  h.render(state(5, [player(1)]));
  const avatar = original[0].nodes["battle-slot__avatar"];
  assert.equal(original[0].dataset.playerEntry, "true", "joining an already visible empty circle keeps the player entrance");
  assert.equal(avatar.hidden, false);
  assert.equal(original[0].nodes["battle-slot__gift"].hidden, true);
  assert.equal(avatar.srcWrites, 1);
  assert.equal(original[0].dataset.playerEntry, "true", "ordinary polls do not restart the join effect");
  h.render(state(5, [player(1), player(2, "Second Player")]));
  assert.deepEqual(h.roster.children, original);
  assert.equal(avatar.srcWrites, 1);
  h.render({ ...state(5, [player(1), player(2)]), phase: "ready" });
  h.advance(400);
  assert.equal(h.status.textContent, "Jugadores listos");
  assert.equal(avatar.srcWrites, 1);
});

test("loads the chosen gift from older native disk paths through the local asset URL", () => {
  const h = harness();
  const snapshot = { ...state(4, [player(1)]), trigger: {
    giftId: "1261956", giftName: "Fuego",
    giftImagePath: "C:\\Launcher\\runtime\\assets\\creator-tools\\gifts\\images\\1261956.webp",
  } };
  h.render(snapshot);
  const slots = [...h.roster.children];
  const gift = slots[1].nodes["battle-slot__gift"];
  assert.equal(gift.src, "http://localhost:18091/assets/creator-tools/gifts/images/1261956.webp");
  assert.equal(gift.alt, "Fuego");
  assert.equal(gift.hidden, false);
  assert.equal(slots[1].nodes["battle-slot__coin"].hidden, true);
  h.render(snapshot);
  assert.equal(gift.srcWrites, 1, "polling preserves the loaded gift");
  h.render({ ...snapshot, trigger: { ...entry, giftId: "5655" } });
  assert.deepEqual(h.roster.children, slots);
  assert.ok(gift.src.endsWith("/5655.webp"), "changing gift updates each empty slot");
  h.render({ ...snapshot, trigger: { giftId: "../1261956", giftImagePath: "file:///private.png" } });
  assert.equal(gift.hidden, true, "invalid IDs cannot create asset paths");
  assert.equal(slots[1].nodes["battle-slot__coin"].hidden, false);
});

test("falls back to a coin for unavailable gifts and initials for missing portraits", () => {
  const h = harness();
  h.render({ ...state(2), trigger: {} });
  let nodes = h.roster.children[0].nodes;
  assert.equal(nodes["battle-slot__coin"].hidden, false);
  h.render(state(2));
  nodes["battle-slot__gift"].events.error();
  assert.equal(nodes["battle-slot__coin"].hidden, false);
  h.render(state(2, [player(1)]));
  nodes["battle-slot__avatar"].events.error();
  assert.equal(nodes["battle-slot__avatar"].hidden, true);
  assert.equal(nodes["battle-slot__initial"].hidden, false);
  assert.equal(nodes["battle-slot__initial"].textContent, "TP");
  h.render(state(2, [player(1)]));
  assert.equal(nodes["battle-slot__avatar"].hidden, true, "same snapshot does not retry a broken image");
});

test("rejects unsafe image URLs and invalid or duplicate slot numbers", () => {
  const h = harness();
  h.render(state(2, [{ ...player(1), avatarUrl: "javascript:alert(1)" }, player(1, "Duplicate"), player(3), null]));
  assert.equal(h.roster.children[0].nodes["battle-slot__avatar"].hidden, true);
  assert.equal(h.roster.children[0].attributes["aria-label"], "Test Player");
  assert.equal(h.roster.children[1].dataset.filled, "false");
});

test("preserves locale, phase visibility and compositor presentation controls", () => {
  const h = harness();
  h.render({ ...state(4), locale: "en-US", presentation: { showTitle: false, showDetails: false, motion: false } });
  assert.equal(h.status.hidden, true);
  assert.equal(h.document.documentElement.lang, "en");
  assert.equal(h.root.dataset.showTitle, "false");
  assert.equal(h.root.dataset.showDetails, "false");
  assert.equal(h.root.dataset.motion, "false");
  h.render({ ...state(4), locale: "en-US", presentation: { motion: false } });
  assert.equal(h.status.textContent, "Waiting for players");
  h.render({ ...state(4), phase: "off" });
  h.advance(400);
  assert.equal(h.root.dataset.visible, "false");
  assert.equal(h.root.attributes["aria-hidden"], "true");
  assert.equal(h.roster.children.length, 0);
  assert.equal(h.status.textContent, "");
});

test("attack previews expire, do not replay on polling, and cannot target empty slots", () => {
  const h = harness();
  const active = { ...state(3, [player(1), player(2)]), phase: "active" };
  const attack = { id: "first", slot: 2, name: "Bomba", imagePath: "/attack.png", startedAt: h.now() };
  h.render({ ...active, attack });
  const item = h.roster.children[1];
  assert.equal(item.dataset.attacking, "true");
  assert.equal(item.nodes["battle-slot__attack"].attributes["aria-label"], "Bomba");
  assert.equal(item.nodes["battle-slot__attack"].textContent, "", "the attack has an image only, no visible label");
  h.advance(400);
  h.render({ ...active, attack });
  assert.equal(item.nodes["battle-slot__attack-image"].srcWrites, 1);
  assert.equal(h.timers.size, 1);
  h.advance(1400);
  h.render({ ...active, attack });
  assert.equal(item.dataset.attacking, "false", "polling cannot replay a completed attack");
  for (const invalid of [
    { ...attack, id: "empty", slot: 3 },
    { ...attack, id: "expired", startedAt: h.now() - 3000 },
    { ...attack, id: "unsafe", imagePath: "javascript:alert(1)" },
  ]) {
    h.render({ ...active, attack: invalid });
    assert.equal(h.roster.children.every(slot => slot.dataset.attacking !== "true"), true);
  }
  h.render({ ...active, attack: { ...attack, id: "second", startedAt: h.now() } });
  h.render({ ...active, phase: "won" });
  h.advance(560);
  assert.equal(item.dataset.attacking, "false");
  assert.equal(h.timers.size, 0, "leaving combat cancels attack timers");
  assert.equal(item.nodes["battle-slot__avatar"].srcWrites, 1);
});

test("uses editable colors with alpha and rejects unsafe CSS", () => {
  const h = harness();
  h.render({ ...state(2), presentation: {
    outlineColor: "#abcdef80", liquidColor: "#123456", textColor: "#fedcba", collectingColor: "url(example)",
  } });
  assert.equal(h.root.style.values["--battle-border"], "#abcdef80");
  assert.equal(h.root.style.values["--battle-player-border"], "#123456");
  assert.equal(h.root.style.values["--battle-text"], "#fedcba");
  assert.equal(h.root.style.values["--battle-accent"], "#f4c76f");
});

test("stays empty until recruitment and restarts cleanly after cancellation", () => {
  const h = harness();
  for (const phase of ["off", "stopping", "unknown"]) {
    h.render({ ...state(3, [player(1)]), phase });
    assert.equal(h.roster.children.length, 0);
    assert.equal(h.root.dataset.visible, "false");
  }
  h.render(state(3));
  assert.equal(h.root.dataset.visible, "true");
  assert.equal(h.root.attributes["aria-hidden"], "false");
  const original = [...h.roster.children];
  h.render(state(3, [player(1)]));
  assert.deepEqual(h.roster.children, original, "joining does not replay the roster entrance");
  h.render({ ...state(3), phase: "off" });
  h.advance(400);
  h.render(state(2));
  assert.equal(h.roster.children.length, 2);
  assert.notEqual(h.roster.children[0], original[0], "a new recruitment gets a fresh entrance");
});

test("status changes wait for exit and keep only the latest queued text", () => {
  const h = harness();
  h.render(state(3));
  h.advance(280);
  h.render({ ...state(3), phase: "ready" });
  assert.equal(h.status.textContent, "Esperando jugadores");
  assert.equal(h.status.dataset.textPhase, "exiting");
  h.advance(140);
  h.render({ ...state(3), phase: "active" });
  assert.equal(h.status.textContent, "Esperando jugadores");
  h.advance(139);
  assert.equal(h.status.textContent, "Esperando jugadores", "the old slide remains until its exit finishes");
  h.advance(1);
  assert.equal(h.status.textContent, "Batalla en curso", "skip the superseded ready message");
  assert.equal(h.status.dataset.textPhase, "entering");
  h.advance(280);
  assert.equal(h.status.dataset.textPhase, "shown");
  h.render({ ...state(3), phase: "won" });
  h.advance(100);
  h.render({ ...state(3), phase: "off" });
  h.advance(500);
  assert.equal(h.status.textContent, "");
  assert.equal(h.status.hidden, true);
  assert.equal(h.roster.children.length, 0);
  assert.equal(h.timers.size, 0, "cancelled battles cannot show a queued victory");
});

test("waiting states use compact messages and preserve instant reduced-motion updates", () => {
  const h = harness(true);
  for (const [locale, attempt, expected] of [
    ["es", 0, "Esperando nivel"], ["es", 1, "Esperando intento"],
    ["en", 0, "Waiting for level"], ["en", 1, "Waiting for retry"],
  ]) {
    h.render({ ...state(5), phase: "waiting_level", locale, attempt });
    assert.equal(h.status.textContent, expected);
    assert.equal(h.status.dataset.textPhase, "shown");
  }
  assert.equal(h.timers.size, 0);
});

test("names fade when phases or details change, with instant updates for reduced motion", () => {
  const h = harness();
  const active = { ...state(2, [player(1, "First")]), phase: "active" };
  h.render(active);
  h.advance(200);
  const name = h.roster.children[0].nodes["battle-slot__name"];
  h.render({ ...active, participants: [player(1, "Second")] });
  assert.equal(name.textContent, "First");
  h.advance(200);
  assert.equal(name.textContent, "Second");
  h.advance(200);
  h.render({ ...active, presentation: { showDetails: false } });
  assert.equal(name.dataset.textPhase, "exiting");
  h.advance(200);
  assert.equal(name.hidden, true);
  h.render({ ...active, presentation: { motion: false } });
  assert.equal(name.textContent, "First");
  h.render({ ...active, phase: "off", presentation: { motion: false } });
  assert.equal(h.roster.children.length, 0);
  assert.equal(h.timers.size, 0);
  const reduced = harness(true);
  reduced.render(active);
  reduced.render({ ...active, phase: "won" });
  assert.equal(reduced.status.textContent, "¡Victoria!");
  assert.equal(reduced.status.dataset.textPhase, "shown");
  assert.equal(reduced.timers.size, 0);
});

test("shows names throughout recruitment, truncates after fourteen characters and preserves identity", () => {
  const h = harness();
  const participants = [player(1, "Mugman MX"), player(2, "Cuphead")];
  for (const phase of ["recruiting", "ready", "waiting_level", "active", "won"]) {
    h.render({ ...state(3, participants), phase });
    h.advance(400);
    const [first, second, empty] = h.roster.children;
    assert.equal(first.dataset.playerEntry, "false", "initial filled roster uses one entrance instead of two overlapping effects");
    assert.equal(first.nodes["battle-slot__name"].textContent, "Mugman MX");
    assert.equal(first.nodes["battle-slot__name"].hidden, false);
    assert.equal(first.attributes["aria-label"], "Mugman MX", "keep full accessible identity");
    assert.equal(second.nodes["battle-slot__name"].textContent, "Cuphead");
    assert.equal(empty.nodes["battle-slot__name"].hidden, true);
  }
  for (const [full, expected] of [
    ["12345678901234", "12345678901234"], ["123456789012345", "12345678901234."],
    ["A\u0301ngel1234567890", "A\u0301ngel123456789."], ["👩‍🚀12345678901234", "👩‍🚀1234567890123."],
  ]) {
    h.render({ ...state(3, [player(1, full)]), presentation: { motion: false } });
    assert.equal(h.roster.children[0].nodes["battle-slot__name"].textContent, expected);
    assert.equal(h.roster.children[0].attributes["aria-label"], full);
  }
  h.render({ ...state(3, participants), presentation: { showDetails: false, motion: false } });
  assert.equal(h.roster.children[0].nodes["battle-slot__name"].hidden, true);
});
