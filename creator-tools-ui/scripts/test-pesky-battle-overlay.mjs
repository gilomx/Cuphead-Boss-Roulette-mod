import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import test from "node:test";

const source = readFileSync(new URL("../../assets/creator-tools/pesky-battle-overlay.js", import.meta.url), "utf8");
class Element {
  dataset = {};
  attributes = {};
  hidden = false;
  textContent = "";
  children = [];
  events = {};
  srcWrites = 0;
  set src(value) { this.attributes.src = value; this.srcWrites++; }
  get src() { return this.attributes.src; }
  setAttribute(key, value) { this.attributes[key] = value; }
  removeAttribute(key) { delete this.attributes[key]; }
  addEventListener(name, callback) { this.events[name] = callback; }
  append(fragment) { this.children.push(fragment.querySelector(".battle-slot")); }
  replaceChildren() { this.children = []; }
}
function harness() {
  const elements = Object.fromEntries(["battle", "battle-status", "battle-roster"].map(id => [id, new Element()]));
  elements["battle-slot-template"] = { content: { cloneNode() {
    const nodes = Object.fromEntries(["battle-slot", "battle-slot__avatar", "battle-slot__gift", "battle-slot__coin", "battle-slot__initial", "battle-slot__name"].map(name => [name, new Element()]));
    nodes["battle-slot"].nodes = nodes;
    return { querySelector: selector => nodes[selector.slice(1)] };
  } } };
  const document = { documentElement: {}, getElementById: id => elements[id] };
  let config;
  runInNewContext(source, { document, URL, URLSearchParams, window: {
    location: { origin: "http://localhost:18091", search: "" },
    LiveEventOverlayRuntime: { create(value) { config = value; } },
  } });
  return { render: config.render, root: elements.battle, status: elements["battle-status"],
    roster: elements["battle-roster"], document };
}
const entry = { giftName: "Rosa", giftImagePath: "/assets/creator-tools/gifts/images/5655.webp" };
const state = (capacity, participants = []) => ({ phase: "recruiting", capacity, trigger: entry, participants });
const player = (slot, name = "Test Player") => ({ slot, displayName: name, avatarUrl: "/avatar.webp" });

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
  assert.equal(avatar.hidden, false);
  assert.equal(original[0].nodes["battle-slot__gift"].hidden, true);
  assert.equal(avatar.srcWrites, 1);
  h.render(state(5, [player(1), player(2, "Second Player")]));
  assert.deepEqual(h.roster.children, original);
  assert.equal(avatar.srcWrites, 1);
  h.render({ ...state(5, [player(1), player(2)]), phase: "ready" });
  assert.equal(h.status.textContent, "Jugadores listos");
  assert.equal(avatar.srcWrites, 1);
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
  assert.equal(h.roster.children[0].nodes["battle-slot__name"].textContent, "Test Player");
  assert.equal(h.roster.children[1].dataset.filled, "false");
});

test("preserves locale, phase visibility and compositor presentation controls", () => {
  const h = harness();
  h.render({ ...state(4), locale: "en-US", presentation: { showTitle: false, showDetails: false, motion: false } });
  assert.equal(h.status.textContent, "Waiting for players");
  assert.equal(h.document.documentElement.lang, "en");
  assert.equal(h.root.dataset.showTitle, "false");
  assert.equal(h.root.dataset.showDetails, "false");
  assert.equal(h.root.dataset.motion, "false");
  h.render({ ...state(4), phase: "off" });
  assert.equal(h.root.dataset.visible, "false");
});
