import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const source = readFileSync(new URL('../../assets/creator-tools/overlay-text-style.js', import.meta.url), 'utf8');
function style(presentation) {
  const window = {}, values = {};
  vm.runInNewContext(source, { window });
  window.CreatorToolsOverlayText.apply({ style: { setProperty: (key, value) => { values[key] = value; } } }, presentation);
  return values;
}
test('shared default typography matches the clean designer text with a fully transparent shadow', () => {
  const values = style({});
  assert.equal(values['--overlay-font-family'], 'sans-serif');
  assert.equal(values['--overlay-font-weight'], '700');
  assert.equal(values['--overlay-text-color'], '#ffffff');
  assert.equal(values['--overlay-text-shadow'], '2px 3px 2px #00000000');
  assert.equal(values['text-transform'], 'uppercase');
});
test('CSS and SVG text receive the same shadow alpha and configurable offsets', () => {
  const values = style({ textFont: 'rounded', textWeight: 400, textColor: '#aabbcc80', textShadowColor: '#11223340', textShadowX: -8, textShadowY: 5, textShadowBlur: 12 });
  assert.equal(values['--overlay-font-family'], '"Trebuchet MS", "Segoe UI", sans-serif');
  assert.equal(values['--overlay-font-weight'], '400');
  assert.equal(values['--overlay-text-color'], '#aabbcc80');
  assert.equal(values['--overlay-text-shadow'], '-8px 5px 12px #11223340');
  assert.equal(values['--overlay-text-drop-shadow'], values['--overlay-text-shadow']);
});
test('malformed presentation cannot inject CSS or create unbounded text effects', () => {
  const values = style({ textFont: 'url(invalid)', textWeight: 650, textColor: 'red;display:none', textShadowColor: 'url(invalid)', textShadowX: 500, textShadowY: -500, textShadowBlur: -3 });
  assert.equal(values['--overlay-font-family'], 'sans-serif');
  assert.equal(values['--overlay-font-weight'], '700');
  assert.equal(values['--overlay-text-color'], '#ffffff');
  assert.equal(values['--overlay-text-shadow'], '20px -20px 0px #00000000');
});
