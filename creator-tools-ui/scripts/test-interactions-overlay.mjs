import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = name => readFileSync(new URL(`../../assets/creator-tools/${name}`, import.meta.url), 'utf8');
const modelCode = source('interactions-overlay.model.js');
const runtimeCode = source('interactions-overlay.js');
const scope = {};
vm.runInNewContext(modelCode, scope);
const model = scope.CreatorToolsInteractionsOverlayModel;
const artwork = Object.fromEntries(Array.from({ length: 20 }, (_, index) => [`effect_${index}`, {
  imagePath: `/assets/creator-tools/interactions/effect-${index}.png`, names: { es: `Efecto ${index}`, en: `Effect ${index}` }, category: 'attack',
}]));
const catalog = { gifts: [{ giftId: '5655', name: 'Rosa', imagePath: '/assets/creator-tools/gifts/images/5655.webp' }] };
const rule = (overrides = {}) => ({ id: 1, enabled: true, platform: 'tiktok', eventType: 'gift', giftId: '5655', every: 1,
  interaction: 'effect_0', quantity: 1, durationSeconds: 20, countdownSeconds: 0, ...overrides });
const project = (rules, view = {}, extra = {}) => model.items({ rules, ...extra }, view, catalog, artwork, 'es');
const plain = value => JSON.parse(JSON.stringify(value));

test('only enabled configured rules from selected networks become offers; prices are every, not coin values', () => {
  const items = project([rule(), rule({ enabled: false, interaction: 'effect_1' }), rule({ platform: 'youtube', eventType: 'jewels', every: 400 }),
    rule({ platform: 'twitch', eventType: 'currency', every: 125 }), rule({ interaction: 'missing' })], { interactionsYouTube: false });
  assert.equal(items.length, 1);
  assert.deepEqual(plain(items[0].offers.map(offer => [offer.platform, offer.every])), [['tiktok', 1], ['twitch', 125]]);
  assert.equal(items[0].offers[0].image, catalog.gifts[0].imagePath);
  assert.deepEqual(plain(project([rule()], { interactionsTikTok: false })), []);
});

test('quantity, duration and countdown distinguish effects while equivalent triggers share a card', () => {
  const items = project([rule(), rule({ platform: 'twitch', eventType: 'currency', every: 100 }),
    rule({ quantity: 2 }), rule({ durationSeconds: 25 }), rule({ countdownSeconds: 3 })]);
  assert.equal(items.length, 4);
  assert.equal(items[0].offers.length, 2);
  assert.equal(items[1].quantity, 2);
});

test('duplicate offers collapse; distinct gift quantities and redemption names stay distinct', () => {
  const items = project([rule(), rule(), rule({ every: 2 }), rule({ platform: 'twitch', eventType: 'redemption', rewardName: '<img src=x onerror=alert(1)>' }),
    rule({ platform: 'twitch', eventType: 'redemption', rewardName: 'Otro canje' })]);
  assert.equal(items[0].offers.length, 4);
  assert.match(items[0].offers[2].rewardName, /<img/);
});

test('gift images come from trusted local catalog paths and custom community placeholder, never rule URLs', () => {
  const items = project([rule({ giftId: 'custom', imagePath: 'https://evil.test/x.png', giftName: 'Custom' })]);
  assert.equal(items[0].offers[0].image, '');
  const communityGift = { giftId: '999', name: 'Comunidad', imagePath: 'https://gift.test/custom.png',
    placeholderImagePath: '/assets/creator-tools/gifts/images/0.webp' };
  const custom = project([rule({ giftId: '999' })], {}, { communityGift });
  assert.equal(custom[0].offers[0].image, communityGift.placeholderImagePath);
  assert.equal(custom[0].offers[0].giftName, 'Comunidad');
  for (const value of ['https://evil.test/image.png', '//evil.test/image.png', '/assets/creator-tools/interactions/../secret.png',
    '/assets/creator-tools/interactions/%2e%2e.png', 'data:image/png;base64,a', '/assets/creator-tools/interactions/test.svg']) assert.equal(model.localImage(value), '');
});

test('visible payment count controls size and disabling adaptive keeps a fixed size', () => {
  assert.ok(model.paymentSize(1) > model.paymentSize(2));
  assert.ok(model.paymentSize(2) > model.paymentSize(3));
  assert.equal(model.paymentSize(1, false), model.paymentSize(3, false));
  assert.equal(model.presentation({ interactionsPageSeconds: 100, interactionsSpeed: -3 }).interactionsPageSeconds, 60);
  assert.equal(model.presentation({ interactionsPageSeconds: 100, interactionsSpeed: -3 }).interactionsSpeed, 10);
});

test('floating names are optional and inspector typography is preserved in the presentation contract', () => {
  assert.equal(model.presentation().showTitle, false);
  const view = model.presentation({ showTitle: true, textFont: 'serif', textWeight: 400, textShadowColor: '#ff0000',
    textShadowX: -4, textShadowY: 7, textShadowBlur: 5 });
  assert.equal(view.showTitle, true);
  assert.equal(view.textFont, 'serif');
  assert.equal(view.textWeight, 400);
  assert.equal(view.textShadowColor, '#ff0000');
  assert.equal(view.textShadowX, -4);
  assert.equal(view.textShadowY, 7);
  assert.equal(view.textShadowBlur, 5);
  assert.equal(model.presentation({ textFont: 'url(evil)', textWeight: 666 }).textFont, 'clean');
});

test('nine static items per page wrap without omissions and strip repeats enough whole cycles', () => {
  const items = project(Array.from({ length: 20 }, (_, index) => rule({ interaction: `effect_${index}` })));
  const pages = model.pages(items);
  assert.deepEqual(plain(pages.map(page => page.length)), [9, 9, 2]);
  assert.equal(model.nextPage(2, pages.length), 0);
  const width = 235, viewport = 1920;
  const count = model.repetitions(width, viewport);
  assert.ok((count - 1) * width >= viewport, 'enough duplicates cover the viewport at the end of a cycle');
  assert.equal(model.offset(230, 10, 1000, width), 5);
  assert.equal(model.offset(5, 10, -1000, width), 5);
});

test('opaque bounds normalize visible artwork height without deforming its aspect ratio', () => {
  const data = new Uint8ClampedArray(6 * 8 * 4);
  for (let y = 2; y <= 5; y++) for (let x = 1; x <= 2; x++) data[(y * 6 + x) * 4 + 3] = 255;
  const metrics = model.opaqueBounds(6, 8, data);
  assert.deepEqual(plain(metrics), { ratio: .5, width: 1.5, height: 2, left: -.25, top: -.5 });
  assert.equal((6 / metrics.width) / (8 / metrics.height), 1, 'same scale factor in both dimensions');
});

class Element {
  children = []; dataset = {}; attributes = {}; style = { setProperty(key, value) { this[key] = value; } };
  hidden = false; clientWidth = 700; clientHeight = 420; offsetWidth = 600; offsetHeight = 302; scrollWidth = 600; scrollHeight = 300;
  append(...nodes) { for (const node of nodes) { node.parent = this; this.children.push(node); } }
  replaceChildren(...nodes) { this.children = []; this.append(...nodes); }
  setAttribute(key, value) { this.attributes[key] = value; }
  get lastElementChild() { return this.children.at(-1); }
  remove() { this.parent.children.splice(this.parent.children.indexOf(this), 1); }
}
const flush = () => new Promise(resolve => setImmediate(resolve));
async function boot(search = '?mode=embedded') {
  const nodes = Object.fromEntries(['interactions-overlay', 'interactions-scene', 'interactions-viewport', 'interactions-content', 'interactions-heading', 'interactions-pagination'].map(id => [id, new Element()]));
  const listeners = {}, posted = [], fetched = [], created = [], timers = [], rafs = new Map();
  const parent = { postMessage: message => posted.push(message) };
  let nextId = 0, nextState = { rules: [rule()] }, component = { id: 'interactions', enabled: true, interactionsTemplate: 'floating', interactionsLayout: 'pages' };
  const document = { getElementById: id => nodes[id], documentElement: { dataset: {} },
    createElement: tag => { const node = new Element(); node.tagName = tag; created.push(node); return node; },
    createElementNS: (_namespace, tag) => { const node = new Element(); node.tagName = tag; created.push(node); return node; } };
  const scope = { document, location: { search, origin: 'http://overlay.test' }, URLSearchParams, AbortController, console,
    Image: class {},
    requestAnimationFrame: callback => { const id = ++nextId; rafs.set(id, callback); return id; },
    cancelAnimationFrame: id => rafs.delete(id),
    setTimeout: (callback, delay) => { const id = ++nextId; timers.push({ id, callback, delay }); return id; },
    clearTimeout: id => { const i = timers.findIndex(timer => timer.id === id); if (i >= 0) timers.splice(i, 1); },
    fetch: async url => {
      fetched.push(url);
      return { ok: true, json: async () => structuredClone(url.includes('catalog.json') ? catalog : url.includes('composer/config')
        ? { profiles: [{ id: 'horizontal', components: [component] }] } : nextState) };
    },
    window: { parent, innerWidth: 700, innerHeight: 420, CreatorToolsOverlayInteractions: artwork,
      matchMedia: () => ({ matches: false }), addEventListener: (type, fn) => { listeners[type] = fn; },
      removeEventListener: type => { delete listeners[type]; } },
  };
  vm.runInNewContext(modelCode, scope); vm.runInNewContext(runtimeCode, scope); await flush();
  const send = (state, origin = 'http://overlay.test', source = parent) => listeners.message?.({ origin, source,
    data: { type: 'creator-tools-overlay-preview', version: 1, overlay: 'interactions', state } });
  return { nodes, fetched, posted, created, send, setState: state => { nextState = state; },
    async poll() { const index = timers.findIndex(timer => timer.delay === 1000); assert.ok(index >= 0); timers.splice(index, 1)[0].callback(); await flush(); },
    frame(time) { const callbacks = [...rafs.values()]; rafs.clear(); callbacks.forEach(callback => callback(time)); },
    dispose() { listeners.pagehide(); }, listeners };
}

test('embedded messaging authenticates parent and origin, renders configured rules and previews disabled components', async () => {
  const runtime = await boot();
  assert.equal(runtime.posted[0].overlay, 'interactions');
  assert.equal(runtime.fetched.length, 1, 'embedded mode fetches only the image catalog');
  const state = { rules: [rule()], visible: false, preview: true, presentation: { interactionsTemplate: 'floating', interactionsLayout: 'pages' } };
  runtime.send(state, 'https://evil.test'); assert.equal(runtime.nodes['interactions-overlay'].dataset.count, undefined);
  runtime.send(state, undefined, {}); assert.equal(runtime.nodes['interactions-overlay'].dataset.count, undefined);
  runtime.send(state); assert.equal(runtime.nodes['interactions-overlay'].hidden, false);
  assert.equal(runtime.nodes['interactions-content'].children.length, 1);
  runtime.send({ ...state, preview: false }); assert.equal(runtime.nodes['interactions-overlay'].hidden, true);
});

test('composer embedded=1 query uses the parent transport without polling real state', async () => {
  const runtime = await boot('?embedded=1&profile=vertical');
  assert.equal(runtime.fetched.length, 1);
  assert.equal(runtime.posted[0].mode, 'embedded');
  runtime.send({ rules: [rule()], presentation: { interactionsTemplate: 'floating' } });
  assert.equal(runtime.nodes['interactions-overlay'].dataset.count, '1');
});

test('the inspector name control hides and reveals item names in both templates', async () => {
  const runtime = await boot('?embedded=1');
  for (const template of ['floating', 'cards']) {
    for (const showTitle of [false, true, false]) {
      runtime.send({ rules: [rule()], presentation: { interactionsLayout: 'pages', interactionsTemplate: template, showTitle } });
      const name = runtime.created.filter(node => node.className === 'interaction-name').at(-1);
      assert.equal(name.hidden, !showTitle, `${template} obeys showTitle=${showTitle}`);
    }
  }
});

test('height-constrained layouts include the scene border when scaling to the viewport', async () => {
  const runtime = await boot('?embedded=1');
  runtime.nodes['interactions-overlay'].clientHeight = 200;
  for (const layout of ['pages', 'strip']) {
    runtime.send({ rules: [rule()], presentation: { interactionsTemplate: 'cards', interactionsLayout: layout } });
    const scale = Number(runtime.nodes['interactions-scene'].style.transform.match(/scale\(([^)]+)\)/)[1]);
    assert.ok(scale * 302 <= 200 + 1e-8, `${layout} includes its two border pixels`);
  }
});

test('static pages advance on the configured interval despite repeated polling, then reconcile removed data', async () => {
  const runtime = await boot();
  const state = { rules: Array.from({ length: 10 }, (_, index) => rule({ interaction: `effect_${index}` })),
    presentation: { interactionsTemplate: 'floating', interactionsLayout: 'pages', interactionsPageSeconds: 3 } };
  runtime.send(state); assert.equal(runtime.nodes['interactions-content'].children.length, 9);
  runtime.frame(1); runtime.frame(1001); runtime.send(structuredClone(state)); runtime.frame(2001); runtime.frame(3001);
  assert.equal(runtime.nodes['interactions-overlay'].dataset.page, '1');
  assert.equal(runtime.nodes['interactions-content'].children.length, 1);
  runtime.send({ ...state, rules: [rule()] });
  assert.equal(runtime.nodes['interactions-overlay'].dataset.page, '0');
  assert.equal(runtime.nodes['interactions-content'].children.length, 1);
});

test('redemption names remain text nodes and no data-controlled HTML is parsed', async () => {
  const runtime = await boot();
  const name = '<img src=x onerror=alert(1)>';
  runtime.send({ rules: [rule({ platform: 'twitch', eventType: 'redemption', rewardName: name })], presentation: { interactionsLayout: 'pages' } });
  const reward = runtime.created.find(node => node.className === 'interaction-reward');
  assert.equal(reward.textContent, name);
  assert.equal(reward.children.length, 0);
  assert.ok(!runtimeCode.includes('innerHTML'));
});

test('standalone OBS mode reads saved profile and real rules then refreshes prices', async () => {
  const runtime = await boot('');
  assert.ok(runtime.fetched.includes('/api/config/interactions/rules'));
  assert.ok(runtime.fetched.includes('/api/overlay-composer/config'));
  assert.equal(runtime.nodes['interactions-overlay'].dataset.template, 'floating');
  assert.equal(runtime.nodes['interactions-overlay'].dataset.count, '1');
  runtime.setState({ rules: [rule({ every: 23 })] }); await runtime.poll();
  assert.ok(runtime.created.some(node => node.className === 'interaction-cost' && node.textContent === '23'));
  runtime.dispose(); assert.equal(runtime.listeners.resize, undefined);
});
