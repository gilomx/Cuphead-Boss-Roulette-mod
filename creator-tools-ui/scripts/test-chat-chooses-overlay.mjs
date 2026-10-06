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
const finalSelection = () => ({ ...ballot(), phase: 'result', selected: Object.fromEntries(
  ['boss', 'weapon1', 'weapon2', 'super', 'charm', 'modifier'].map((stage, index) => [stage, option(index + 1)])) });

class Element {
  children = []; dataset = {}; attributes = {}; style = { setProperty(key, value) { this[key] = value; } }; hidden = false; offsetHeight = 1000; offsetWidth = 1480;
  animations = [];
  append(...children) { this.children.push(...children); }
  replaceChildren(...children) { this.children = children; }
  setAttribute(key, value) { this.attributes[key] = value; }
  animate(frames, options) {
    const animation = { frames, options, playState: 'running' };
    animation.finished = new Promise((resolve, reject) => {
      animation.finish = () => { if (animation.playState === 'running') { animation.playState = 'finished'; resolve(); } };
      animation.cancel = () => { if (animation.playState === 'running') reject(new Error('cancelled')); animation.playState = 'idle'; };
    });
    this.animations.push(animation);
    return animation;
  }
}
async function boot(search = '', reducedMotion = false) {
  const nodes = Object.fromEntries(['event', 'options', 'result', 'heading', 'voting-title', 'title', 'boss', 'equipment'].map(id => [id, new Element()]));
  let state = ballot(), failed = false, now = 0;
  const listeners = {}, posted = [], fetched = [];
  const parent = { postMessage: message => posted.push(message) };
  const timers = [], created = [];
  let nextTimer = 0;
  const document = { getElementById: id => nodes[id], createElement: () => { const node = new Element(); created.push(node); return node; }, documentElement: {} };
  vm.runInNewContext(code, { document, location: { search, origin: 'http://overlay.test' }, URLSearchParams, innerWidth: 1280, innerHeight: 720,
    getComputedStyle: () => ({ opacity: '1', transform: 'none' }),
    window: { parent, matchMedia: () => ({ matches: reducedMotion }), addEventListener: (type, callback) => { listeners[type] = callback; } }, Date: { now: () => now },
    setTimeout: (callback, delay) => { const id = ++nextTimer; timers.push({ id, callback, delay }); return id; },
    clearTimeout: id => { const index = timers.findIndex(timer => timer.id === id); if (index >= 0) timers.splice(index, 1); },
    fetch: async url => {
      fetched.push(url);
      if (url.includes('labels.json')) return { json: async () => labels };
      if (failed) throw new Error('offline');
      return { ok: true, json: async () => structuredClone(state) };
    } });
  await flush();
  return { nodes, fetched, posted, send(value, origin = 'http://overlay.test', source = parent) {
    listeners.message?.({ source, origin, data: { type: 'creator-tools-overlay-preview', version: 1, overlay: 'chat-chooses', state: value } });
  }, setState(value) { state = value; }, fail() { failed = true; },
    animations: () => [...Object.values(nodes), ...created].flatMap(node => node.animations),
    async finishAnimations() { [...Object.values(nodes), ...created].flatMap(node => node.animations).forEach(animation => animation.finish()); await flush(); },
    async finishTransitionTimeout() {
      const index = timers.findIndex(timer => timer.delay !== 250);
      assert.ok(index >= 0, 'transition fallback scheduled');
      timers.splice(index, 1)[0].callback(); await flush();
    },
    async poll(time = now) { now = time; const index = timers.findIndex(timer => timer.delay === 250); assert.ok(index >= 0, 'poll scheduled'); timers.splice(index, 1)[0].callback(); await flush(); } };
}
const countDigits = card => card.children[0].children[2].children.map(node => node.textContent);

test('six numbered options update votes in place and replace cards on the next round', async () => {
  const view = await boot();
  const { options, event } = view.nodes;
  assert.equal(event.hidden, false);
  assert.equal(options.children.length, 6);
  assert.equal(options.dataset.count, 6);
  assert.equal(view.nodes['voting-title'].textContent, 'Votaciones');
  assert.equal(view.nodes.title.textContent, labels.es.stages.boss);
  const first = options.children[0];
  assert.equal(first.children[0].children[1].textContent, 1, 'choice number remains inside the portrait');
  const state = ballot(); state.options[0].votes = 0; state.options[1].votes = 1;
  view.setState(state); await view.poll();
  await view.finishAnimations();
  assert.equal(options.children[0], first, 'poll does not recreate cards');
  assert.deepEqual(countDigits(first), ['0']);
  assert.deepEqual(countDigits(options.children[1]), ['1']);
  assert.equal(options.children[1].children[0].children[2].attributes['aria-label'], '1 voto');
  state.round = 2; state.stage = 'super'; state.options = state.options.slice(0, 4);
  view.setState(state); await view.poll();
  assert.equal(options.children.length, 6, 'the old round remains until its exit finishes');
  await view.finishAnimations();
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
  assert.equal(view.nodes['voting-title'].textContent, 'Votaciones');
  state.phase = 'active'; view.setState(state); await view.poll();
  assert.equal(view.nodes.event.hidden, true);
});

test('leaders follow incoming and changed votes, including ties and an empty ballot', async () => {
  const view = await boot();
  const state = ballot();
  const cards = view.nodes.options.children;
  assert.equal(cards[0].dataset.leader, 'true');
  assert.equal(cards[1].dataset.leader, 'false');
  state.options[1].votes = 1; state.totalVotes = 2;
  view.setState(state); await view.poll();
  assert.equal(cards[0].dataset.leader, 'true');
  assert.equal(cards[1].dataset.leader, 'true');
  state.options[0].votes = 0; state.options[1].votes = 2;
  view.setState(state); await view.poll();
  assert.equal(cards[0].dataset.leader, 'false');
  assert.equal(cards[1].dataset.leader, 'true');
  state.options[1].votes = 0; state.totalVotes = 0;
  view.setState(state); await view.poll();
  assert.ok(cards.every(card => card.dataset.leader === 'false'));
  state.phase = 'reveal'; state.winnerNumber = 3; state.outcome = 'no_votes';
  view.setState(state); await view.poll();
  assert.equal(cards[2].dataset.winner, 'true', 'a random winner can be highlighted without votes');
  assert.ok(cards.every(card => card.dataset.leader === 'false'));
});

test('final selection shows no voting heading or start countdown', async () => {
  const view = await boot();
  const state = { ...ballot(), phase: 'result', remainingSeconds: 0 };
  view.setState(state); await view.poll();
  await view.finishAnimations();
  assert.equal(view.nodes.heading.hidden, true);
  assert.equal(view.nodes['voting-title'].textContent, '');
  assert.equal(view.nodes.result.hidden, false);
  state.phase = 'countdown'; view.setState(state); await view.poll();
  await view.finishAnimations();
  assert.equal(view.nodes.event.hidden, true, 'obsolete countdown has no overlay scene');
});

test('final preview keeps compatible plane equipment, no challenge, and safe local paths', async () => {
  const view = await boot('?lang=en&preview=1');
  const state = { ...ballot(), phase: 'active', plane: true, selected: {
    boss: { name: 'Plane boss', image: '../private.png' }, charm: { name: 'Charm', image: 'charms/test.png' } } };
  view.setState(state); await view.poll();
  await view.finishAnimations();
  assert.equal(view.nodes.event.hidden, false);
  assert.equal(view.nodes.heading.hidden, true);
  assert.equal(view.nodes.boss.children[0].children[0].src, '/assets/creator-tools/empty.png');
  assert.equal(view.nodes.equipment.children.length, 2, 'skip ground weapons and super');
  assert.equal(view.nodes.equipment.children[1].children[0].children[0].src, '/assets/creator-tools/empty.png');
  assert.equal(view.nodes.equipment.children[1].children.length, 1, 'slot labels and selected names are absent');
});

test('final result contains only portraits and retains the designer normal outline color', async () => {
  const view = await boot('?embedded=1');
  const state = finalSelection();
  state.presentation = { outlineColor: '#12345680', voteOutlineColor: '#abcdef80', collectingColor: '#ff0000', showDetails: true };
  view.send(state);
  assert.equal(view.nodes.event.style['--outline-color'], '#12345680');
  assert.equal(view.nodes.event.style['--vote-outline-color'], '#abcdef80', 'vote border has its own color and alpha');
  assert.equal(view.nodes.heading.hidden, true);
  assert.equal(view.nodes.boss.children.length, 1);
  assert.equal(view.nodes.equipment.children.length, 5);
  assert.ok(view.nodes.equipment.children.every(card => card.children.length === 1));
  assert.equal(view.nodes.equipment.children[0].attributes['aria-label'], labels.es.stages.weapon1, 'slot remains accessible without visible labels');
  state.presentation.voteOutlineColor = 'url(https://invalid.test)';
  view.send(state);
  assert.equal(view.nodes.event.style['--vote-outline-color'], '#ffffff', 'invalid vote border falls back to white');
  assert.equal(view.nodes.event.style['--outline-color'], '#12345680', 'vote border does not change the image frame');
});

test('voting and results use the main roulette framed assets and native empty slot', async () => {
  const view = await boot('?embedded=1');
  const paths = ['bosses/hoscoytosco.png', 'weapons/lanzaguisantes.png', 'supers/super1.png',
    'charms/corazon.png', 'creator-tools/modifiers/nodash_01.png', 'weapons/vacio.png'];
  const expected = ['bosses/hoscoytosco.png', 'creator-tools/weapons/lanzaguisantes.png',
    'creator-tools/supers/super1.png', 'creator-tools/charms/corazon.png',
    'creator-tools/modifiers/nodash_01.png', 'creator-tools/empty.png'];
  const state = ballot(); state.presentation = { motion: false };
  state.options = state.options.map((option, index) => ({ ...option, image: paths[index] }));
  view.send(state);
  assert.equal(view.nodes.event.style['--outline-color'], '#d3af93', 'default matches the native frame');
  assert.equal(view.nodes.event.style['--vote-outline-color'], '#ffffff', 'older presentation defaults to a white vote border');
  const portraits = view.nodes.options.children.map(card => card.children[0].children[0]);
  portraits.forEach((artwork, index) => {
    assert.equal(artwork.children[0].src, `/assets/${expected[index]}`);
    assert.equal(artwork.dataset.framed, String(index < 5), 'equipment and bosses share the configurable frame');
    assert.equal(artwork.dataset.boss, String(index === 0), 'boss portraits fit inside the frame');
    assert.ok(readFileSync(new URL(`../../assets/${expected[index]}`, import.meta.url)).length > 0);
  });
  state.phase = 'result';
  state.selected = Object.fromEntries(['boss', 'weapon1', 'weapon2', 'super', 'charm', 'modifier']
    .map((stage, index) => [stage, state.options[index]]));
  view.send(state);
  const finalPortraits = [view.nodes.boss.children[0], ...view.nodes.equipment.children.map(card => card.children[0])];
  finalPortraits.forEach((artwork, index) => assert.equal(artwork.children[0].src, `/assets/${expected[index]}`));
  assert.equal(finalPortraits[0].dataset.framed, 'true', 'the selected boss keeps its frame in the result');
});

test('unsafe image paths always use the native empty slot without a frame tint', async () => {
  const view = await boot('?embedded=1');
  const state = ballot(); state.presentation = { motion: false };
  const paths = ['../private.png', 'https://external.test/icon.png', '/private.png', 'weapons\\private.png', null, 'weapons/vacio.png'];
  state.options = state.options.map((option, index) => ({ ...option, image: paths[index] }));
  view.send(state);
  for (const card of view.nodes.options.children) {
    const artwork = card.children[0].children[0];
    assert.equal(artwork.children[0].src, '/assets/creator-tools/empty.png');
    assert.equal(artwork.dataset.framed, 'false');
  }
});

test('starting gameplay staggers the final result exit and waits for its last portrait', async () => {
  const view = await boot('?embedded=1');
  const state = finalSelection(); view.send(state); await view.finishAnimations();
  state.phase = 'active'; view.send(state);
  assert.equal(view.nodes.event.hidden, false, 'do not remove the result at the start of its exit');
  const exits = view.animations().filter(animation => animation.playState === 'running');
  assert.equal(exits.length, 6, 'boss and every selected item leave');
  assert.deepEqual(exits.map(animation => animation.options.delay), [0, 60, 120, 180, 240, 300]);
  exits.slice(0, -1).forEach(animation => animation.finish()); await flush();
  assert.equal(view.nodes.event.hidden, false, 'keep the result until the final staggered item finishes');
  view.send(state);
  assert.equal(view.animations().filter(animation => animation.playState === 'running').length, 1, 'state updates do not restart the exit');
  exits.at(-1).finish(); await flush();
  assert.equal(view.nodes.event.hidden, true);
  assert.equal(view.nodes.result.hidden, true);
});

test('a new event waits for the final result exit and enters with the newest votes', async () => {
  const view = await boot('?embedded=1');
  view.send(finalSelection()); await view.finishAnimations();
  view.send({ ...finalSelection(), phase: 'off' });
  const next = { ...ballot(), sessionId: 2 };
  view.send(next);
  next.options[0].votes = 8; view.send(next);
  assert.equal(view.nodes.result.hidden, false, 'old final selection stays mounted during its exit');
  await view.finishAnimations();
  assert.equal(view.nodes.result.hidden, true);
  assert.equal(view.nodes.options.children.length, 6);
  assert.deepEqual(countDigits(view.nodes.options.children[0]), ['8']);
  assert.equal(view.animations().filter(animation => animation.playState === 'running').length, 7, 'new heading and portraits enter after every old portrait left');
});

test('final exit completes if OBS throttles animation completion, or skips with reduced motion', async () => {
  const view = await boot('?embedded=1');
  view.send(finalSelection()); await view.finishAnimations();
  view.send({ ...finalSelection(), phase: 'completed' });
  await view.finishTransitionTimeout();
  assert.equal(view.nodes.event.hidden, true, 'fallback hides the final selection without an animation completion event');
  await view.finishAnimations();
  assert.equal(view.nodes.event.hidden, true, 'cancelled finish callbacks cannot restore the result');
  const reduced = await boot('?embedded=1', true);
  reduced.send(finalSelection());
  reduced.send({ ...finalSelection(), phase: 'active' });
  assert.equal(reduced.nodes.event.hidden, true);
  assert.equal(reduced.animations().length, 0);
});

test('lost connection hides stale voting after the grace period', async () => {
  const view = await boot(); view.fail();
  await view.poll(1000); assert.equal(view.nodes.event.hidden, false);
  await view.poll(4100); assert.equal(view.nodes.event.hidden, true);
});

test('embedded compositor owns transport, presentation and locale; unrelated messages cannot render', async () => {
  const view = await boot('?embedded=1');
  assert.equal(view.fetched.length, 1, 'embedded child only loads its labels');
  assert.equal(view.posted[0].overlay, 'chat-chooses');
  const state = { ...ballot(), locale: 'en', presentation: {
    liquidColor: '#12345680', textColor: 'url(evil)', motion: false, showDetails: false,
  } };
  view.send(state, 'http://untrusted.test');
  assert.equal(view.nodes.options.children.length, 0);
  view.send(state, 'http://overlay.test', {});
  assert.equal(view.nodes.options.children.length, 0);
  view.send(state);
  assert.equal(view.nodes.options.children.length, 6);
  assert.equal(view.nodes.event.style['--choice-color'], '#12345680');
  assert.equal(view.nodes.event.style['--text-color'], '#ffffff');
  assert.equal(view.nodes.event.dataset.motion, 'false');
  assert.equal(view.nodes.event.dataset.showDetails, 'false');
  assert.equal(view.nodes.title.textContent, labels.en.stages.boss);
  view.send({ ...state, phase: 'off' });
  assert.equal(view.nodes.event.hidden, true);
});

test('vote digits slide independently and rapid updates settle on the newest count', async () => {
  const view = await boot(); await view.finishAnimations();
  const first = view.nodes.options.children[0], second = view.nodes.options.children[1];
  const state = ballot(); state.options[0].votes = 2; state.totalVotes = 2;
  view.setState(state); await view.poll();
  assert.deepEqual(countDigits(first), ['1', '2']);
  assert.deepEqual(countDigits(second), ['0']);
  const running = view.animations().filter(animation => animation.playState === 'running');
  assert.equal(running.length, 2, 'only the changed digit animates');
  state.options[0].votes = 5;
  view.setState(state); await view.poll();
  assert.ok(running.every(animation => animation.playState === 'idle'));
  assert.deepEqual(countDigits(first), ['2', '5']);
  await view.finishAnimations();
  assert.deepEqual(countDigits(first), ['5']);
  assert.equal(view.nodes.options.children[0], first, 'counter updates keep the portrait');
});

test('round changes finish every staggered exit before building the latest incoming round', async () => {
  const view = await boot(); await view.finishAnimations();
  const old = view.nodes.options.children[0];
  const state = ballot(); state.round = 2; state.stage = 'weapon1';
  state.options = state.options.map(option => ({ ...option, image: 'weapons/next.png' }));
  view.setState(state); await view.poll();
  const exits = view.animations().filter(animation => animation.playState === 'running');
  assert.equal(exits.length, 7, 'stage heading and six portraits leave');
  assert.ok(exits[6].options.delay > exits[1].options.delay);
  exits.slice(0, -1).forEach(animation => animation.finish()); await flush();
  assert.equal(view.nodes.options.children[0], old, 'wait for the last portrait');
  state.round = 3; state.stage = 'super'; state.options = state.options.slice(0, 4);
  state.options[0].votes = 9;
  view.setState(state); await view.poll();
  assert.equal(view.animations().filter(animation => animation.playState === 'running').length, 1, 'polls do not restart the exit');
  exits.at(-1).finish(); await flush();
  assert.equal(view.nodes.options.children.length, 4);
  assert.notEqual(view.nodes.options.children[0], old);
  assert.equal(view.nodes.title.textContent, labels.es.stages.super);
  assert.deepEqual(countDigits(view.nodes.options.children[0]), ['9']);
  const arrivals = view.animations().filter(animation => animation.playState === 'running');
  assert.equal(arrivals.length, 5);
  assert.ok(arrivals[4].options.delay > arrivals[1].options.delay);
});

test('cancelling during a round exit prevents its queued scene from reappearing', async () => {
  const view = await boot(); await view.finishAnimations();
  const state = ballot(); state.round = 2;
  view.setState(state); await view.poll();
  state.phase = 'off'; view.setState(state); await view.poll();
  assert.equal(view.nodes.event.hidden, true);
  await view.finishAnimations();
  assert.equal(view.nodes.event.hidden, true);
  assert.equal(view.nodes.options.children.length, 0);
  state.phase = 'voting'; state.sessionId = 2;
  view.setState(state); await view.poll();
  assert.equal(view.nodes.event.hidden, false);
  assert.equal(view.nodes.options.children.length, 6);
});

test('reduced motion skips queued scenes and digit movement', async () => {
  const view = await boot('', true);
  const state = ballot(); state.round = 2; state.options = state.options.slice(0, 2); state.options[0].votes = 3;
  view.setState(state); await view.poll();
  assert.equal(view.nodes.options.children.length, 2);
  assert.deepEqual(countDigits(view.nodes.options.children[0]), ['3']);
  assert.equal(view.animations().length, 0);
});
