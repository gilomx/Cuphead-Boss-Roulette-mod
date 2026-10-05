import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const code = readFileSync(new URL('../../assets/creator-tools/chat-chooses-overlay.js', import.meta.url), 'utf8');
const labels = JSON.parse(readFileSync(new URL('../../assets/creator-tools/chat-chooses-labels.json', import.meta.url), 'utf8'));
const flush = () => new Promise(resolve => setImmediate(resolve));
const option = (number, votes = 0) => ({ id: number, number, votes, name: `Option ${number}`, image: 'weapons/test.png' });
const ballot = () => ({ phase: 'voting', sessionId: 1, round: 1, stage: 'boss', totalVotes: 1,
  winnerNumber: 0, options: Array.from({ length: 6 }, (_, i) => option(i + 1, i === 0 ? 1 : 0)), selected: {} });

class Element {
  children = []; dataset = {}; style = {}; hidden = false; offsetHeight = 1000;
  append(...children) { this.children.push(...children); }
  replaceChildren(...children) { this.children = children; }
}
async function boot(search = '') {
  const nodes = Object.fromEntries(['event', 'options', 'result', 'eyebrow', 'title', 'instructions', 'status', 'summary', 'boss', 'equipment'].map(id => [id, new Element()]));
  let state = ballot(), failed = false, now = 0;
  const timers = [];
  const document = { getElementById: id => nodes[id], createElement: () => new Element(), documentElement: {} };
  vm.runInNewContext(code, { document, location: { search }, URLSearchParams, innerWidth: 1280, innerHeight: 720,
    window: { addEventListener() {} }, Date: { now: () => now }, setTimeout: callback => timers.push(callback),
    fetch: async url => {
      if (url.includes('labels.json')) return { json: async () => labels };
      if (failed) throw new Error('offline');
      return { ok: true, json: async () => structuredClone(state) };
    } });
  await flush();
  return { nodes, setState(value) { state = value; }, fail() { failed = true; },
    async poll(time = now) { now = time; assert.ok(timers.length, 'poll scheduled'); timers.shift()(); await flush(); } };
}

test('six numbered options update votes in place and replace cards on the next round', async () => {
  const view = await boot();
  const { options, event, summary } = view.nodes;
  assert.equal(event.hidden, false);
  assert.equal(options.children.length, 6);
  assert.equal(options.dataset.count, 6);
  assert.equal(summary.textContent, '1 voto');
  const first = options.children[0];
  const state = ballot(); state.options[0].votes = 0; state.options[1].votes = 1;
  view.setState(state); await view.poll();
  assert.equal(options.children[0], first, 'poll does not recreate cards');
  assert.equal(first.children[4].textContent, '0 votos · 0%');
  assert.equal(options.children[1].children[4].textContent, '1 voto · 100%');
  state.round = 2; state.stage = 'super'; state.options = state.options.slice(0, 4);
  view.setState(state); await view.poll();
  assert.equal(options.children.length, 4);
  assert.equal(options.dataset.count, 4);
  assert.notEqual(options.children[0], first);
});

test('reveal highlights the winner and the real OBS source hides at gameplay', async () => {
  const view = await boot();
  const state = ballot(); state.phase = 'reveal'; state.winnerNumber = 2; state.outcome = 'tie';
  view.setState(state); await view.poll();
  assert.equal(view.nodes.options.children[1].dataset.winner, 'true');
  assert.equal(view.nodes.options.children[0].dataset.winner, 'false');
  assert.equal(view.nodes.status.textContent, labels.es.outcome.tie);
  state.phase = 'active'; view.setState(state); await view.poll();
  assert.equal(view.nodes.event.hidden, true);
});

test('final preview keeps compatible plane equipment, no challenge, and safe local paths', async () => {
  const view = await boot('?lang=en&preview=1');
  const state = { ...ballot(), phase: 'active', plane: true, selected: {
    boss: { name: 'Plane boss', image: '../private.png' }, charm: { name: 'Charm', image: 'charms/test.png' } } };
  view.setState(state); await view.poll();
  assert.equal(view.nodes.event.hidden, false);
  assert.equal(view.nodes.title.textContent, labels.en.phase.result);
  assert.equal(view.nodes.boss.children[0].src, '/assets/weapons/vacio.png');
  assert.equal(view.nodes.equipment.children.length, 2, 'skip ground weapons and super');
  assert.equal(view.nodes.equipment.children[1].children[1].src, '/assets/creator-tools/empty.png');
  assert.equal(view.nodes.equipment.children[1].children[2].textContent, labels.en.withoutChallenge);
});

test('lost connection hides stale voting after the grace period', async () => {
  const view = await boot(); view.fail();
  await view.poll(1000); assert.equal(view.nodes.event.hidden, false);
  await view.poll(4100); assert.equal(view.nodes.event.hidden, true);
});
