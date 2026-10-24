import test from 'node:test';
import assert from 'node:assert/strict';
import { applyStreamRuleGroup } from './mock-stream-rule-groups.mjs';

const options = { giftsById: new Map([['5655', { name: 'Rose' }]]), interactionItems: ['help_extra_life', 'challenge_no_dash'] };
const value = (platform, overrides = {}) => ({ platform, name: platform, enabled: true,
  eventType: platform === 'tiktok' ? 'gift' : platform === 'twitch' ? 'currency' : 'jewels',
  giftId: platform === 'tiktok' ? '5655' : '', rewardName: '', every: platform === 'tiktok' ? 1 : 100, ...overrides });
const command = overrides => ({ schemaVersion: 1, expectedRevision: 0, action: 'save', ruleIds: [],
  interaction: 'help_extra_life', quantity: 1, userCooldownSeconds: 0, globalCooldownSeconds: 5,
  durationSeconds: 15, countdownSeconds: 3, values: ['tiktok', 'twitch', 'youtube'].map(platform => value(platform)), ...overrides });
const empty = () => ({ rules: [], nextId: 1, revision: 0 });
const send = (state, overrides) => applyStreamRuleGroup(state, command({ expectedRevision: state.revision, ...overrides }), options);

test('one attack creates three values together and changes shared settings and retained prices in one revision', () => {
  const initial = empty();
  const created = send(initial);
  assert.equal(created.status, 200); assert.equal(created.feedback, 'created');
  assert.deepEqual(initial, empty(), 'the input collection is never partially mutated');
  assert.deepEqual(created.rules.map(rule => rule.id), [1, 2, 3]);
  const updated = send(created, { ruleIds: [1, 2, 3], interaction: 'challenge_no_dash', quantity: 2, durationSeconds: 25,
    values: [value('tiktok', { id: 1, every: 3 }), value('youtube', { id: 3, every: 700, enabled: false })] });
  assert.equal(updated.revision, 2); assert.equal(updated.feedback, 'updated');
  assert.deepEqual(updated.rules.map(rule => [rule.id, rule.interaction, rule.quantity, rule.durationSeconds, rule.every]),
    [[1, 'challenge_no_dash', 2, 25, 3], [3, 'challenge_no_dash', 2, 25, 700]]);
  assert.equal(updated.nextId, 4); assert.deepEqual(updated.resetIds, [1, 2, 3]);
  assert.equal(created.rules.length, 3);
});

test('invalid rows, duplicate networks, foreign ids and stale revisions preserve the whole attack', () => {
  const state = send(empty());
  for (const overrides of [
    { expectedRevision: 0 }, { values: [value('tiktok'), value('tiktok')] },
    { values: [value('twitch', { every: 0 })] }, { values: [value('tiktok', { giftId: 'unknown' })] },
    { values: [value('twitch', { eventType: 'redemption', rewardName: '' })] },
    { values: [value('youtube', { id: 99 })] }, { ruleIds: [1, 1] }, { ruleIds: [99] },
    { values: [] }, { values: [null] }, { quantity: 0 }, { durationSeconds: 121 },
  ]) {
    const result = send(state, { ruleIds: [1, 2, 3], ...overrides });
    assert.equal(result.error, true, JSON.stringify(overrides));
    assert.equal(result.revision, state.revision);
    assert.deepEqual(result.rules, state.rules);
    assert.equal(result.nextId, state.nextId);
  }
});

test('group actions operate on all its prices without changing unrelated legacy rules', () => {
  const created = send(empty());
  const unrelated = { ...created.rules[0], id: 50, interaction: 'challenge_no_dash' };
  const state = { ...created, rules: [...created.rules, unrelated], nextId: 51 };
  const toggled = send(state, { action: 'toggle', ruleIds: [1, 2, 3], enabled: false });
  assert.ok(toggled.rules.slice(0, 3).every(rule => !rule.enabled));
  assert.deepEqual(toggled.rules.at(-1), unrelated);
  const duplicated = send(toggled, { action: 'duplicate', ruleIds: [1, 2, 3] });
  assert.equal(duplicated.rules.length, 7);
  assert.deepEqual(duplicated.rules.slice(3, 6).map(rule => rule.id), [51, 52, 53]);
  assert.deepEqual(duplicated.resetIds, []);
  const deleted = send(duplicated, { action: 'delete', ruleIds: [1, 2, 3] });
  assert.equal(deleted.rules.length, 4);
  assert.deepEqual(deleted.rules.at(-1), unrelated);
});

test('capacity is checked for the complete replacement and unrelated legacy configurations cannot be merged', () => {
  const one = send(empty());
  const state = { rules: Array.from({ length: 99 }, (_, i) => ({ ...one.rules[0], id: i + 1 })), nextId: 100, revision: 8 };
  assert.equal(send(state).feedback, 'rules_limit');
  assert.equal(send(state, { action: 'duplicate', ruleIds: [1] }).status, 200);
  assert.equal(send(state, { action: 'duplicate', ruleIds: [1, 2] }).error, true, 'two values from one network are not a group');
  const foreign = { ...one, rules: [one.rules[0], { ...one.rules[1], quantity: 2 }] };
  assert.equal(send(foreign, { action: 'delete', ruleIds: [1, 2] }).error, true);
});
