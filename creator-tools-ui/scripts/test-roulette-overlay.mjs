import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const widget = readFileSync(new URL('../../assets/creator-tools/roulette-overlay.js', import.meta.url), 'utf8');
const bridge = readFileSync(new URL('../../assets/creator-tools/live-event-overlay-runtime.js', import.meta.url), 'utf8');
const composer = readFileSync(new URL('../../assets/creator-tools/live-overlay.js', import.meta.url), 'utf8');
const typography = readFileSync(new URL('../../assets/creator-tools/overlay-text-style.js', import.meta.url), 'utf8');
class Element {
  children = []; hidden = true; textContent = ''; style = { setProperty(key, value) { this[key] = value; } };
  offsetWidth = 640; scrollWidth = 640; offsetHeight = 260; scrollHeight = 260;
  classList = { values: new Set(), add(...values) { values.forEach(v => this.values.add(v)); },
    remove(...values) { values.forEach(v => this.values.delete(v)); }, contains(v) { return this.values.has(v); },
    toggle(v, active) { if (active) this.values.add(v); else this.values.delete(v); } };
  setAttribute() {} removeAttribute() {} appendChild(child) { this.children.push(child); }
  replaceChildren() { this.children = []; }
}
function boot() {
  const nodes = Object.fromEntries(['stage','content','result','icons','challenge','challenge-image','challenge-fallback','brand'].map(id => [id, new Element()]));
  const listeners = {}, posted = [], timers = new Map(); let nextTimer = 0;
  const parent = { postMessage: data => posted.push(data) };
  const window = { parent, location: { origin: 'http://roulette.test', search: '?embedded=1' },
    innerWidth: 960, innerHeight: 420,
    addEventListener: (key, fn) => { listeners[key] = fn; }, removeEventListener() {},
    requestAnimationFrame: fn => fn(), getComputedStyle: () => ({ fontSize: '34px' }),
    setTimeout: fn => { const id = ++nextTimer; timers.set(id, fn); return id; },
    clearTimeout: id => timers.delete(id) };
  vm.runInNewContext(typography, { window });
  vm.runInNewContext(bridge, { window, document: { documentElement: { dataset: {} }, body: { dataset: {} } }, URLSearchParams });
  vm.runInNewContext(widget, { window, document: { getElementById: id => nodes[id], createElement: () => new Element() },
    WebSocket: class { constructor() { throw Error('embedded widget must not connect'); } }, encodeURI });
  return { nodes, posted, get pendingTimers() { return timers.size; }, send(state, origin = window.location.origin, source = parent) {
    listeners.message({ origin, source, data: { type: 'creator-tools-overlay-preview', version: 1, overlay: 'roulette', state } });
  }, resize(width, height) {
    window.innerWidth = width; window.innerHeight = height; listeners.resize();
  }, finish() { for (let n=0; timers.size && n<30; n++) { const callbacks = [...timers.values()]; timers.clear(); callbacks.forEach(fn => fn()); } } };
}
async function bootComposer(designer = false) {
  const root = new Element(); root.dataset = {}; root.append = child => root.appendChild(child);
  const listeners = {}, intervals = new Map(), frames = [];
  const parent = { postMessage() {} };
  let preview = { active: false };
  const profile = { id: 'horizontal', canvas: { width: 1920, height: 1080 },
    components: [{ id: 'roulette', enabled: false, opacity: 75 }] };
  const window = { parent, location: { origin: 'http://roulette.test', pathname: '/overlay/horizontal', search: designer ? '?designer=1' : '' },
    addEventListener: (key, fn) => { listeners[key] = fn; }, removeEventListener() {},
    setInterval: (fn, ms) => { intervals.set(ms, fn); return ms; }, clearInterval() {},
    setTimeout: () => 1, clearTimeout() {} };
  vm.runInNewContext(composer, { window, URLSearchParams, encodeURIComponent,
    document: { documentElement: {}, getElementById: () => root, createElement(tag) {
      const element = new Element(); element.dataset = {};
      element.append = child => element.appendChild(child); element.addEventListener = () => {};
      if (tag === 'iframe') { element.posted = []; element.contentWindow = { postMessage: data => element.posted.push(data) }; frames.push(element); }
      return element;
    } }, fetch: async url => ({ ok: true, json: async () => url.includes('/preview?') ? preview
      : url === '/api/overlay-composer/config' ? { profiles: [profile] }
      : { type: 'state', active: true, visible: true, battleActive: true, session: 10 } }),
  });
  const flush = () => new Promise(resolve => setImmediate(resolve));
  await flush();
  return { profile, host: root.children.find(child => child.dataset.componentId === 'roulette'),
    frame: frames.find(frame => frame.title === 'roulette'),
    design(selectedComponentId) { listeners.message({ source: parent, origin: window.location.origin,
      data: { type: 'creator-tools-overlay-composer-design', version: 1, profileId: 'horizontal',
        profile, selectedComponentId, states: { roulette: { type: 'state', active: true, visible: true, preview: true } } } }); },
    async setPreview(next) { preview = next; intervals.get(100)(); await flush(); },
  };
}
function state(presentation = {}) {
  return { type: 'state', active: true, battleActive: true, visible: true, session: 1,
    revealed: 3, textVisible: true, challengeText: 'NO DASH', labelRevision: 0,
    icons: ['weapons/lanzaguisantes.png','supers/super1.png','charms/bombadehumo.png'], presentation };
}
test('designer bridge owns transport and applies the original editable settings', () => {
  const view = boot();
  assert.equal(view.posted[0].overlay, 'roulette');
  view.send(state(), 'http://untrusted.test');
  assert.equal(view.nodes.icons.children.length, 0);
  view.send(state({ rouletteSize: 150, rouletteAlignment: 'right', rouletteTextFirst: true }));
  view.finish();
  assert.equal(view.nodes.icons.children.length, 3);
  assert.equal(view.nodes.stage.classList.contains('scale-15'), false, 'legacy icon sizing does not affect the new component');
  assert.ok(view.nodes.stage.classList.contains('align-right'));
  assert.ok(view.nodes.stage.classList.contains('text-first'));
  assert.equal(view.nodes.stage.style['--overlay-opacity'], '1', 'compositor applies opacity once');
});
test('keep holds the current battle through retry but releases it when the battle ends', () => {
  const view = boot(); const first = state({ rouletteRetry: 'keep' });
  view.send(first); view.finish();
  view.send({ ...first, visible: false, revealed: 0, textVisible: false, completeExit: true }); view.finish();
  assert.equal(view.nodes.result.hidden, false);
  view.send({ ...first, battleActive: false, visible: false }); view.finish();
  assert.equal(view.nodes.result.hidden, true);
  assert.equal(view.nodes.brand.hidden, true);
});
test('reappear exits during retry and idle logo follows its own switch', () => {
  const view = boot(); const first = state({ rouletteRetry: 'reappear', rouletteLogo: true });
  view.send(first); view.finish();
  view.send({ ...first, visible: false, completeExit: true }); view.finish();
  assert.equal(view.nodes.result.hidden, true);
  assert.equal(view.nodes.brand.hidden, true, 'no logo inside a retry');
  view.send({ ...first, battleActive: false, visible: false }); view.finish();
  assert.equal(view.nodes.brand.hidden, false);
  view.send({ ...first, active: false });
  assert.equal(view.nodes.brand.hidden, true);
});
test('catalog fallback stays local and a small designer frame fits without cropping icons', () => {
  const view = boot(); const first = state(); first.icons = ['../private.png'];
  view.send(first); view.finish();
  assert.equal(view.nodes.icons.children[0].src, '/assets/creator-tools/empty.png');
  view.nodes.content.offsetWidth = 2000;
  view.send(first);
  assert.equal(view.nodes.content.style.transform, 'scale(0.4125)');
});
test('roulette grows and shrinks with the container while preserving proportions and animation clearance', () => {
  const view = boot(); view.send(state({ rouletteSize: 200 })); view.finish();
  const scale = () => Number(view.nodes.content.style.transform.slice(6, -1));
  const original = scale();
  assert.ok(original > 1, 'large frames grow the artwork');
  view.resize(1280, 840);
  assert.ok(scale() > original, 'a larger container enlarges its icons and text together');
  view.resize(320, 180);
  assert.ok(scale() < 1, 'small frames shrink the artwork');
  assert.ok(640 * scale() * 1.12 <= 320 - 36 + 0.001);
  assert.ok(260 * scale() * 1.12 <= 180 - 36 + 0.001);
  view.resize(960, 180);
  assert.ok(260 * scale() * 1.12 <= 180 - 36 + 0.001, 'short frames fit the height as well as the width');
  const before = scale();
  view.send(state({ rouletteSize: 100 }));
  assert.equal(scale(), before, 'stored legacy sizes cannot override the container');
});
test('long challenge labels keep stable artwork proportions across container sizes', () => {
  const view = boot();
  view.nodes['challenge-fallback'].scrollWidth = 2400;
  view.nodes.content.offsetWidth = 760;
  view.nodes.content.offsetHeight = 250;
  view.send({ ...state(), challengeText: 'A VERY LONG CHALLENGE NAME '.repeat(12) });
  const label = view.nodes['challenge-fallback'];
  assert.equal(label.style.fontSize, '20.4px');
  assert.equal(label.style.whiteSpace, 'normal');
  assert.equal(label.style.width, '760px');
  view.resize(480, 210);
  const small = Number(view.nodes.content.style.transform.slice(6, -1));
  view.resize(960, 420);
  assert.ok(Number(view.nodes.content.style.transform.slice(6, -1)) > small);
  assert.equal(label.style.fontSize, '20.4px', 'resizing scales text with its icons instead of independently shrinking it');
});
test('a disabled roulette can be inspected without enabling it or other hidden layers', async () => {
  const view = await bootComposer(true);
  assert.equal(view.host.dataset.enabled, 'false');
  view.design('roulette');
  assert.equal(view.host.dataset.enabled, 'true');
  assert.equal(view.host.dataset.configEnabled, 'false');
  assert.equal(view.profile.components[0].enabled, false);
  assert.equal(view.host.style['--component-opacity'], '0.75');
  view.design('tap_farming');
  assert.equal(view.host.dataset.enabled, 'false', 'deselecting restores configured visibility');
});
test('OBS only shows a disabled roulette during explicit simulation and restores it afterwards', async () => {
  const view = await bootComposer();
  assert.equal(view.host.dataset.enabled, 'false', 'a real battle does not enable a disabled layer');
  const preview = { active: true, revision: 1, componentId: 'roulette', scenario: 'hud',
    simulationActive: false, layout: view.profile, eventEpoch: 2 };
  await view.setPreview(preview);
  assert.equal(view.host.dataset.enabled, 'false', 'opening the designer alone does not force an OBS preview');
  await view.setPreview({ ...preview, revision: 2, simulationActive: true });
  assert.equal(view.host.dataset.enabled, 'true');
  assert.equal(view.frame.posted.at(-1).state.session, -3, 'replay survives OBS transport');
  await view.setPreview({ ...preview, revision: 3 });
  assert.equal(view.host.dataset.enabled, 'false');
  await view.setPreview({ active: false, revision: 4 });
  assert.equal(view.host.dataset.enabled, 'false');
  assert.equal(view.profile.components[0].enabled, false);
});
test('replay restarts entry animation while ordinary presentation updates keep the current preview', () => {
  const view = boot(); const first = { ...state(), preview: true, session: -1 };
  view.send(first); view.finish(); view.send(first);
  assert.equal(view.pendingTimers, 0);
  assert.ok(view.nodes.icons.children[0].classList.contains('settled'));
  view.send({ ...first, session: -2 });
  assert.ok(view.pendingTimers > 0);
  assert.equal(view.nodes.icons.children[0].classList.contains('settled'), false);
  view.finish();
  assert.ok(view.nodes.icons.children[0].classList.contains('reveal'));
});
test('roulette always uses the fixed uppercase web font and ignores saved or incoming game fonts', () => {
  const view = boot(); const first = { ...state(), challengeText: 'Reto sin Dash áéñ', labelRevision: 42 };
  view.send(first); view.finish();
  assert.equal(view.nodes['challenge-fallback'].style.display, 'block');
  assert.equal(view.nodes['challenge-fallback'].textContent, 'RETO SIN DASH ÁÉÑ');
  assert.equal(view.nodes.stage.style['--overlay-font-weight'], '700');
  assert.equal(view.nodes.stage.style['--overlay-text-shadow'], '2px 3px 2px #00000000');
  view.send({ ...first, presentation: { textFont: 'serif', textWeight: 900, textColor: '#abcdef80', textShadowColor: '#00000080', textShadowX: -3, textShadowY: 5, textShadowBlur: 9 } });
  assert.equal(view.nodes.stage.style['--overlay-font-family'], 'sans-serif');
  assert.equal(view.nodes.stage.style['--overlay-font-weight'], '700');
  assert.equal(view.nodes.stage.style['--overlay-text-color'], '#abcdef80');
  assert.equal(view.nodes.stage.style['--overlay-text-shadow'], '-3px 5px 9px #00000080');
  view.send({ ...first, presentation: { textFont: 'native' } });
  assert.equal(view.nodes.stage.style['--overlay-font-family'], 'sans-serif');
  assert.equal(view.nodes['challenge-image'].src, undefined, 'native labels are never requested');
  assert.equal(view.nodes['challenge-fallback'].style.display, 'block');
});

test('retired source explains how to replace its OBS URL and contains no live widget', () => {
  const html = readFileSync(new URL('../../assets/creator-tools/overlay.html', import.meta.url), 'utf8');
  assert.match(html, /El overlay de Ruleta cambió de lugar/);
  assert.match(html, /component=roulette/);
  assert.doesNotMatch(html, /src="\/overlay.js/);
});
