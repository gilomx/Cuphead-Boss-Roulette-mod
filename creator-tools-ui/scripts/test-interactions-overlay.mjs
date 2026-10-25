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
  assert.equal(model.presentation({ interactionsPageSeconds: 100, interactionsSpeed: -3 }).interactionsSpeed, 40);
});

test('strip speed has the saved 40 to 140 range and preserves intermediate values', () => {
  assert.equal(model.presentation().interactionsSpeed, 40);
  assert.equal(model.presentation({ interactionsSpeed: -4 }).interactionsSpeed, 40);
  assert.equal(model.presentation({ interactionsSpeed: 999 }).interactionsSpeed, 140);
  assert.equal(model.presentation({ interactionsSpeed: 95 }).interactionsSpeed, 95);
  assert.equal(model.presentation({ interactionsSpeed: 'not-a-number' }).interactionsSpeed, 40);
});

test('card appearance accepts only full hex colors and bounded integer paper intensity', () => {
  const defaults = model.presentation();
  assert.equal(defaults.interactionsCardColor, '#23262bee');
  assert.equal(defaults.interactionsCardNoise, 18);
  assert.equal(model.presentation({ interactionsCardColor: '#faf3d8' }).interactionsCardColor, '#faf3d8');
  assert.equal(model.presentation({ interactionsCardColor: '#10203080' }).interactionsCardColor, '#10203080');
  for (const color of ['#abc', 'red', 'url(https://evil.test/a)', '#123456;opacity:0']) {
    assert.equal(model.presentation({ interactionsCardColor: color }).interactionsCardColor, '#23262bee');
  }
  assert.equal(model.presentation({ interactionsCardNoise: -10 }).interactionsCardNoise, 0);
  assert.equal(model.presentation({ interactionsCardNoise: 300 }).interactionsCardNoise, 100);
  assert.equal(model.presentation({ interactionsCardNoise: 31.8 }).interactionsCardNoise, 32);
  assert.equal(model.presentation({ interactionsCardNoise: 'wrong' }).interactionsCardNoise, 18);
});

test('confirmed floating layout is fixed per profile and temporary settings no longer affect presentation', () => {
  assert.deepEqual(plain(model.floatingLayout('vertical')), { overlap: 29, gap: 35 });
  assert.deepEqual(plain(model.floatingLayout('horizontal')), { overlap: 12, gap: 6 });
  assert.equal('interactionsFloatingOverlap' in model.presentation({ interactionsFloatingOverlap: 48 }), false);
  assert.equal('interactionsFloatingGap' in model.presentation({ interactionsFloatingGap: 0 }), false);
});

test('legacy name settings cannot enable labels and inspector typography is preserved', () => {
  assert.equal(model.presentation().showTitle, false);
  const view = model.presentation({ showTitle: true, textFont: 'serif', textWeight: 400, textShadowColor: '#ff0000',
    textShadowX: -4, textShadowY: 7, textShadowBlur: 5 });
  assert.equal(view.showTitle, false);
  assert.equal(view.textFont, 'serif');
  assert.equal(view.textWeight, 400);
  assert.equal(view.textShadowColor, '#ff0000');
  assert.equal(view.textShadowX, -4);
  assert.equal(view.textShadowY, 7);
  assert.equal(view.textShadowBlur, 5);
  assert.equal(model.presentation({ textFont: 'url(evil)', textWeight: 666 }).textFont, 'clean');
});

test('manual height is optional and constrained without changing automatic sizing in old scenes', () => {
  assert.equal(model.presentation().interactionsSize, 0);
  assert.equal(model.presentation({ interactionsSize: 0 }).interactionsSize, 0);
  assert.equal(model.presentation({ interactionsSize: 1 }).interactionsSize, 25);
  assert.equal(model.presentation({ interactionsSize: 999 }).interactionsSize, 100);
  assert.equal(model.presentation({ interactionsSize: 67.4 }).interactionsSize, 67);
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

test('wide and tall artwork fits completely inside the same square slot without deformation', () => {
  for (const ratio of [.25, 1, 2.6]) {
    const bounds = { ratio, width: ratio * 2, height: 2, left: -.2, top: -.1 };
    const fitted = model.containArtwork(bounds);
    assert.ok(fitted.cropWidth <= 1 && fitted.cropHeight <= 1);
    assert.equal(Math.max(fitted.cropWidth, fitted.cropHeight), 1);
    assert.ok(Math.abs(fitted.cropWidth / fitted.cropHeight - ratio) < 1e-10);
    assert.ok(Math.abs(fitted.width / fitted.height - bounds.width / bounds.height) < 1e-10);
    assert.ok(Math.abs(fitted.left / fitted.width - bounds.left / bounds.width) < 1e-10,
      'transparent margins are removed at the same scale as the image');
  }
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
  nodes['interactions-overlay'].hidden = true;
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
        ? { profiles: [{ id: 'horizontal', components: [component] }] }
        : url.includes('/roulette-overlay/state') ? { interactionBattleActive: true } : nextState) };
    },
    window: { parent, innerWidth: 700, innerHeight: 420, CreatorToolsOverlayInteractions: artwork,
      matchMedia: () => ({ matches: false }), addEventListener: (type, fn) => { listeners[type] = fn; },
      removeEventListener: type => { delete listeners[type]; } },
  };
  vm.runInNewContext(modelCode, scope); vm.runInNewContext(runtimeCode, scope); await flush();
  const send = (state, origin = 'http://overlay.test', source = parent) => listeners.message?.({ origin, source,
    data: { type: 'creator-tools-overlay-preview', version: 1, overlay: 'interactions', state } });
  return { nodes, fetched, posted, created, send, setState: state => { nextState = state; },
    finishExit() { const index = timers.findIndex(timer => timer.delay === 280); if (index >= 0) timers.splice(index, 1)[0].callback(); },
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
  assert.equal(runtime.nodes['interactions-content'].children[0].children.length, 1);
  runtime.send({ ...state, preview: false });
  assert.equal(runtime.nodes['interactions-overlay'].dataset.presence, 'exiting');
  runtime.finishExit(); assert.equal(runtime.nodes['interactions-overlay'].hidden, true);
});

test('composer embedded=1 query uses the parent transport without polling real state', async () => {
  const runtime = await boot('?embedded=1&profile=vertical');
  assert.equal(runtime.fetched.length, 1);
  assert.equal(runtime.posted[0].mode, 'embedded');
  runtime.send({ rules: [rule()], presentation: { interactionsTemplate: 'floating' } });
  assert.equal(runtime.nodes['interactions-overlay'].dataset.count, '1');
});

test('neither template creates names, categories, headings or reward labels even from old scenes', async () => {
  const runtime = await boot('?embedded=1');
  for (const template of ['floating', 'cards']) {
    for (const showTitle of [false, true, false]) {
      runtime.send({ rules: [rule({ platform: 'twitch', eventType: 'redemption', rewardName: 'canjesito' })],
        presentation: { interactionsLayout: 'pages', interactionsTemplate: template, showTitle, showDetails: true } });
      assert.ok(!runtime.created.some(node => ['interaction-name', 'interaction-category', 'interaction-reward', 'interaction-brand'].includes(node.className)));
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

test('pages slide at the configured interval despite repeated polling and keep the same scale', async () => {
  const runtime = await boot();
  const state = { rules: Array.from({ length: 10 }, (_, index) => rule({ interaction: `effect_${index}` })),
    presentation: { interactionsTemplate: 'floating', interactionsLayout: 'pages', interactionsPageSeconds: 3 } };
  runtime.send(state);
  assert.deepEqual(runtime.nodes['interactions-content'].children.map(page => page.children.length), [9, 1]);
  const scale = runtime.nodes['interactions-scene'].style.transform;
  runtime.frame(1); runtime.frame(1001); runtime.send(structuredClone(state)); runtime.frame(2001); runtime.frame(3001);
  assert.equal(runtime.nodes['interactions-overlay'].dataset.page, '1');
  assert.match(runtime.nodes['interactions-content'].style.transition, /380ms/);
  assert.equal(runtime.nodes['interactions-content'].style.transform, 'translateX(-600px)');
  assert.equal(runtime.nodes['interactions-scene'].style.transform, scale);
  runtime.send(structuredClone(state));
  assert.equal(runtime.nodes['interactions-content'].style.transform, 'translateX(-600px)', 'polling does not interrupt the slide');
  runtime.send({ ...state, presentation: { ...state.presentation, motion: false } });
  runtime.frame(4001); runtime.frame(5001); runtime.frame(6001);
  assert.equal(runtime.nodes['interactions-content'].style.transition, 'none', 'motion disabled switches pages without sliding');
  runtime.send({ ...state, rules: [rule()] });
  assert.equal(runtime.nodes['interactions-overlay'].dataset.page, '0');
  assert.equal(runtime.nodes['interactions-content'].children[0].children.length, 1);
});

test('reward names stay accessible without visible text and no data-controlled HTML is parsed', async () => {
  const runtime = await boot();
  const name = '<img src=x onerror=alert(1)>';
  runtime.send({ rules: [rule({ platform: 'twitch', eventType: 'redemption', rewardName: name })], presentation: { interactionsLayout: 'pages' } });
  assert.ok(!runtime.created.some(node => node.className === 'interaction-reward'));
  assert.ok(runtime.created.some(node => node.className === 'interaction-payment' && node.attributes['aria-label'].includes(name)));
  assert.ok(!runtimeCode.includes('innerHTML'));
});

test('currency artwork uses catalog gifts, official Twitch Bits and YouTube Jewels', async () => {
  const runtime = await boot();
  runtime.send({ rules: [rule(), rule({ platform: 'twitch', eventType: 'currency' }), rule({ platform: 'youtube', eventType: 'jewels' })] });
  const images = runtime.created.filter(node => node.tagName === 'img').map(node => node.src);
  assert.ok(runtime.created.filter(node => node.tagName === 'img').every(node => node.alt === ''),
    'failed image loads cannot display item or gift names as fallback text');
  assert.ok(images.includes(catalog.gifts[0].imagePath));
  assert.ok(images.includes('/assets/creator-tools/twitch/bits.png'));
  assert.ok(images.includes('/assets/creator-tools/youtube/jewels.png'));
});

test('every template and layout shows only exchange artwork plus its value while keeping platform accessibility', async () => {
  const runtime = await boot();
  const rules = [rule(), rule({ platform: 'twitch', eventType: 'currency', every: 125 }),
    rule({ platform: 'youtube', eventType: 'jewels', every: 400 })];
  for (const template of ['cards', 'floating']) for (const layout of ['pages', 'strip']) {
    const before = runtime.created.length;
    runtime.send({ rules, presentation: { interactionsTemplate: template, interactionsLayout: layout } });
    const created = runtime.created.slice(before);
    const payments = created.filter(node => node.className === 'interaction-payment');
    assert.equal(payments.length, 3);
    for (const [index, label] of ['TikTok:', 'Twitch:', 'YouTube:'].entries()) {
      assert.ok(payments[index].attributes['aria-label'].startsWith(label));
      assert.deepEqual(payments[index].children.map(node => node.className), ['interaction-currency', 'interaction-cost']);
      assert.equal(payments[index].children[0].children.length, 1, 'currency contains no extra platform insignia');
      assert.equal(payments[index].children[0].children[0].tagName, 'img');
    }
    assert.ok(!created.some(node => ['interaction-platform-mark', 'interaction-brand'].includes(node.className)));
  }
});

test('live card appearance updates its background controls without changing grouped offers or artwork', async () => {
  const runtime = await boot();
  const rules = [rule(), rule({ platform: 'twitch', eventType: 'currency', every: 125 })];
  let grainPosition;
  for (const [color, noise] of [['#23262bee', 18], ['#f4e5bd', 0], ['#050a1080', 85]]) {
    runtime.send({ rules, presentation: { interactionsTemplate: 'cards', interactionsLayout: 'pages',
      interactionsCardColor: color, interactionsCardNoise: noise } });
    const root = runtime.nodes['interactions-overlay'];
    assert.equal(root.style['--paper'], color);
    assert.equal(root.style['--card-noise'], String(noise / 100));
    assert.equal(root.dataset.count, '1', 'the two offers remain on one effect card');
    const card = runtime.nodes['interactions-content'].children[0].children[0].children[0];
    assert.equal(card.children[1].children.length, 2);
    assert.equal(card.children[0].children[0].children[0].children[0].src, artwork.effect_0.imagePath);
    if (grainPosition) assert.equal(card.style['--card-grain-position'], grainPosition, 'texture stays static when intensity changes');
    grainPosition = card.style['--card-grain-position'];
  }
});

test('one to four items remain fully static without duplicate copies in both templates', async () => {
  const runtime = await boot();
  for (const template of ['cards', 'floating']) for (const count of [1, 2, 3, 4]) {
    runtime.send({ rules: Array.from({ length: count }, (_, index) => rule({ interaction: `effect_${index}` })),
      presentation: { interactionsLayout: 'strip', interactionsTemplate: template } });
    assert.equal(runtime.nodes['interactions-overlay'].dataset.scrolling, 'false');
    assert.equal(runtime.nodes['interactions-content'].children.length, 1);
    assert.equal(runtime.nodes['interactions-content'].children[0].children.length, count);
    const position = runtime.nodes['interactions-content'].style.transform;
    runtime.frame(1); runtime.frame(1001); runtime.frame(2001);
    assert.equal(runtime.nodes['interactions-content'].style.transform, position);
    assert.equal(position, 'translateX(12px)', 'keeps an inset at the first image');
  }
});

test('five or more items loop through a four-item window and return to a complete static group when reduced', async () => {
  const runtime = await boot();
  runtime.send({ rules: Array.from({ length: 20 }, (_, index) => rule({ interaction: `effect_${index}` })),
    presentation: { interactionsLayout: 'strip' } });
  assert.equal(runtime.nodes['interactions-content'].children[0].children.length, 20);
  assert.equal(runtime.nodes['interactions-viewport'].style.width, `${4 * 600 + 3 * 6 + 24}px`);
  assert.equal(runtime.nodes['interactions-overlay'].dataset.scrolling, 'true');
  assert.ok(runtime.nodes['interactions-content'].children.length >= 2);
  runtime.frame(1); runtime.frame(1001);
  assert.notEqual(runtime.nodes['interactions-content'].style.transform, 'translateX(12px)');
  const state = { rules: Array.from({ length: 5 }, (_, index) => rule({ interaction: `effect_${index}` })),
    presentation: { interactionsLayout: 'strip' } };
  runtime.send(state);
  const copies = runtime.nodes['interactions-content'].children;
  assert.deepEqual(copies[0].children.map(item => item.dataset.interaction), copies[1].children.map(item => item.dataset.interaction));
  runtime.send({ ...state, rules: state.rules.slice(0, 4) });
  assert.equal(runtime.nodes['interactions-content'].children.length, 1);
  assert.equal(runtime.nodes['interactions-overlay'].dataset.scrolling, 'false');
  assert.equal(runtime.nodes['interactions-content'].style.transform, 'translateX(12px)');
});

test('all cards share the largest natural width and height across pages and strips', async () => {
  const runtime = await boot();
  const rules = [rule(), rule({ interaction: 'effect_1' })];
  for (const layout of ['pages', 'strip']) {
    runtime.send({ rules, presentation: { interactionsLayout: layout, interactionsTemplate: 'cards' } });
    const cards = runtime.created.filter(node => node.className === 'interaction-item').slice(-2);
    cards[0].offsetWidth = 180; cards[0].offsetHeight = 128;
    cards[1].offsetWidth = 210; cards[1].offsetHeight = 160;
    runtime.listeners.resize();
    assert.equal(runtime.nodes['interactions-overlay'].style['--item-width'], '210px');
    assert.equal(runtime.nodes['interactions-overlay'].style['--item-height'], '160px');
  }
});

test('manual strip height enlarges items, fills the moving viewport and preserves complete static groups', async () => {
  const runtime = await boot();
  const root = runtime.nodes['interactions-overlay'], viewport = runtime.nodes['interactions-viewport'];
  const content = runtime.nodes['interactions-content'], scene = runtime.nodes['interactions-scene'];
  root.clientWidth = 900; root.clientHeight = 250;
  const rules = Array.from({ length: 5 }, (_, index) => rule({ interaction: `effect_${index}` }));
  const sizes = {};
  for (const template of ['cards', 'floating']) for (const count of [5, 4, 1]) for (const height of [25, 100]) {
    runtime.send({ rules: rules.slice(0, count), presentation: {
      interactionsLayout: 'strip', interactionsTemplate: template, interactionsSize: height,
    } });
    for (const card of content.children[0].children) { card.offsetWidth = 180; card.offsetHeight = 130; }
    runtime.listeners.resize();
    const scale = Number(scene.style.transform.match(/scale\(([^)]+)\)/)[1]);
    sizes[`${template}-${count}-${height}`] = scale;
    assert.ok(Number.parseFloat(viewport.style.height) * scale <= root.clientHeight + 1e-8);
    assert.ok(Number.parseFloat(viewport.style.width) * scale <= root.clientWidth + 1e-8);
    assert.equal(root.dataset.scrolling, String(count > 4));
    if (count > 4) {
      assert.ok((content.children.length - 1) * count * (180 + 20) >= Number.parseFloat(viewport.style.width),
        'copies cover the entire viewport even at the smallest manual height');
      runtime.frame(1); runtime.frame(1001);
      assert.notEqual(content.style.transform, 'translateX(12px)');
    } else {
      assert.equal(content.children.length, 1);
      runtime.frame(2001); runtime.frame(3001);
      assert.equal(content.style.transform, 'translateX(12px)');
    }
  }
  for (const template of ['cards', 'floating']) for (const count of [5, 4, 1])
    assert.ok(sizes[`${template}-${count}-100`] > sizes[`${template}-${count}-25`]);
  assert.ok(sizes['cards-4-100'] < sizes['cards-5-100'], 'four items respect the width needed by the whole group');
});

test('manual page height respects the overlay bounds and stays constant on the next page', async () => {
  const runtime = await boot();
  const root = runtime.nodes['interactions-overlay'], scene = runtime.nodes['interactions-scene'];
  const viewport = runtime.nodes['interactions-viewport'];
  root.clientWidth = 1800; root.clientHeight = 420;
  const rules = Array.from({ length: 10 }, (_, index) => rule({ interaction: `effect_${index}` }));
  let smaller;
  for (const height of [25, 100]) {
    runtime.send({ rules, presentation: { interactionsLayout: 'pages', interactionsSize: height, interactionsPageSeconds: 3 } });
    const scale = Number(scene.style.transform.match(/scale\(([^)]+)\)/)[1]);
    assert.ok(Number.parseFloat(viewport.style.height) * scale <= root.clientHeight + 1e-8);
    assert.ok(Number.parseFloat(viewport.style.width) * scale <= root.clientWidth + 1e-8);
    if (height === 25) smaller = scale;
    else assert.ok(scale > smaller);
    const before = scene.style.transform;
    const previousPage = root.dataset.page;
    runtime.frame(1); runtime.frame(1001); runtime.frame(2001); runtime.frame(3001);
    assert.notEqual(root.dataset.page, previousPage);
    assert.equal(scene.style.transform, before);
  }
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

test('battle visibility animates exits, cancels stale exits and permits explicit previews', async () => {
  const runtime = await boot();
  const root = runtime.nodes['interactions-overlay'], content = runtime.nodes['interactions-content'];
  const state = { rules: [rule()], interactionsBattleOnly: true, battleActive: false };
  runtime.send(state);
  assert.equal(root.hidden, true, 'map state has no offers');
  runtime.send({ ...state, battleActive: true });
  assert.equal(root.hidden, false);
  assert.equal(root.dataset.presence, 'visible');
  runtime.send(state);
  assert.equal(root.dataset.presence, 'exiting');
  assert.ok(content.children.length > 0, 'outgoing offers stay in the DOM through the transition');
  runtime.send({ ...state, battleActive: true });
  runtime.finishExit();
  assert.equal(root.hidden, false, 'a new battle cancels the old exit');
  runtime.send(state); runtime.finishExit();
  assert.equal(root.hidden, true);
  assert.equal(content.children.length, 0);
  runtime.send({ ...state, battlePreview: true });
  assert.equal(root.hidden, false, 'explicit OBS preview bypasses the battle condition');
  runtime.send({ ...state, presentation: { motion: false } });
  assert.equal(root.hidden, true, 'disabled motion exits immediately');
  runtime.send({ ...state, interactionsBattleOnly: false });
  assert.equal(root.hidden, false, 'always visible mode works outside a battle');
});

test('floating strip uses each item width and closes every cycle without reserving the widest card for all', async () => {
  const runtime = await boot();
  runtime.send({ rules: Array.from({ length: 5 }, (_, index) => rule({ interaction: `effect_${index}` })),
    presentation: { interactionsTemplate: 'floating', interactionsLayout: 'strip' } });
  const originals = runtime.nodes['interactions-content'].children[0].children;
  [110, 180, 125, 160, 140].forEach((width, index) => { originals[index].offsetWidth = width; });
  runtime.listeners.resize();
  assert.equal(runtime.nodes['interactions-overlay'].style['--item-width'], 'max-content');
  assert.equal(runtime.nodes['interactions-overlay'].style['--strip-gap'], '6px');
  const cycleWidth = 110 + 180 + 125 + 160 + 140 + 5 * 6;
  const viewportWidth = Number.parseFloat(runtime.nodes['interactions-viewport'].style.width);
  assert.ok((runtime.nodes['interactions-content'].children.length - 1) * cycleWidth >= viewportWidth);
  runtime.frame(1); runtime.frame(1001);
  assert.notEqual(runtime.nodes['interactions-content'].style.transform, 'translateX(12px)');
});

test('confirmed vertical spacing covers the strip and ignores retired tuning fields', async () => {
  const runtime = await boot('?embedded=1&profile=vertical');
  const state = { rules: Array.from({ length: 5 }, (_, index) => rule({ interaction: `effect_${index}` })),
    presentation: { interactionsTemplate: 'floating', interactionsLayout: 'strip', interactionsSize: 25,
      interactionsFloatingOverlap: 24, interactionsFloatingGap: 3 } };
  const root = runtime.nodes['interactions-overlay'], viewport = runtime.nodes['interactions-viewport'];
  for (const gap of [0, 3, 48]) {
    runtime.send({ ...state, presentation: { ...state.presentation, interactionsFloatingGap: gap } });
    assert.equal(root.style['--floating-payment-offset'], '-29px');
    assert.equal(root.style['--strip-gap'], '35px');
    const copies = runtime.nodes['interactions-content'].children.length;
    assert.ok((copies - 1) * 5 * (600 + 35) >= Number.parseFloat(viewport.style.width));
    assert.equal(root.dataset.scrolling, 'true');
  }
  runtime.send({ ...state, presentation: { ...state.presentation, interactionsTemplate: 'cards' } });
  assert.equal(root.style['--strip-gap'], '20px', 'card spacing remains independent');
  runtime.send({ ...state, rules: state.rules.slice(0, 4) });
  assert.equal(root.dataset.scrolling, 'false', 'small groups remain static with confirmed spacing');
});
