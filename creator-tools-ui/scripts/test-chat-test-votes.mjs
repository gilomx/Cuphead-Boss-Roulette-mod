import assert from 'node:assert/strict';
import test from 'node:test';
import { createChatChoosesMock } from './mock-chat-chooses.mjs';

function fixture(developmentTools = true) {
  let now = 0, seed = 42;
  const chat = createChatChoosesMock({ developmentTools, clock: () => now,
    random: () => ((seed = Math.imul(seed, 1664525) + 1013904223 >>> 0) / 4294967296) });
  const command = operation => {
    const state = chat.snapshot();
    return chat.command(new URLSearchParams({ operation, mode: 'with', sessionId: state.sessionId, round: state.round }));
  };
  command('start');
  return { chat, command, advance: ms => { now += ms; } };
}

test('Dev fixture emits 50–80 distinct votes spread over ten seconds, with a clear leader', () => {
  const { chat, command, advance } = fixture();
  command('test_votes');
  const total = chat.snapshot().testVotes.total;
  assert.ok(total >= 50 && total <= 80);
  assert.equal(chat.takeDueTestVotes().length, 0);
  assert.equal(command('test_votes').feedback, 'test_votes_running');
  const events = [], deliveries = [];
  for (let tick = 1; tick <= 200; tick++) {
    advance(50);
    const batch = chat.takeDueTestVotes();
    if (batch.length) deliveries.push(tick);
    for (const event of batch) { assert.equal(chat.vote(event), 'chat_vote_counted'); events.push(event); }
    if (tick === 100) assert.ok(events.length > 0 && events.length < total);
    if (tick === 199) assert.ok(events.length < total);
  }
  assert.equal(events.length, total);
  assert.equal(new Set(events.map(e => e.userId)).size, total);
  assert.ok(deliveries.length > 20);
  assert.equal(deliveries.at(-1), 200);
  const counts = chat.snapshot().options.map(o => o.votes);
  assert.equal(counts.filter(n => n === Math.max(...counts)).length, 1);
  assert.equal(chat.snapshot().testVotes.active, false);
  command('test_votes'); advance(10000);
  for (const event of chat.takeDueTestVotes()) assert.equal(chat.vote(event), 'chat_vote_counted');
  assert.ok(chat.snapshot().totalVotes >= total + 50);
});

test('changing round or stopping cancels pending votes and fences extracted events', () => {
  const { chat, command, advance } = fixture();
  command('test_votes'); advance(1000);
  const old = chat.takeDueTestVotes();
  assert.ok(old.length > 0);
  command('next'); advance(2000);
  for (const event of old) assert.equal(chat.vote(event), '');
  advance(10000);
  assert.equal(chat.takeDueTestVotes().length, 0);
  assert.equal(chat.snapshot().totalVotes, 0);
  command('test_votes'); command('stop'); advance(10000);
  assert.equal(chat.takeDueTestVotes().length, 0);
  assert.equal(chat.snapshot().testVotes.active, false);
});

test('Release fixture rejects test votes even when called directly', () => {
  const { chat, command, advance } = fixture(false);
  assert.equal(chat.snapshot().developmentTools, false);
  assert.equal(command('test_votes').feedback, 'development_only');
  advance(10000);
  assert.equal(chat.takeDueTestVotes().length, 0);
  assert.equal(chat.snapshot().totalVotes, 0);
});

test('supers keep their numbering and the final selection waits indefinitely for native Play', () => {
  const { chat, command, advance } = fixture();
  for (let stage = 0; stage < 6; stage++) {
    const state = chat.snapshot();
    if (state.stage === 'super') {
      assert.deepEqual(state.options.map(o => o.image), ['supers/super1.png', 'supers/super2.png', 'supers/super3.png', 'creator-tools/empty.png']);
      assert.deepEqual(state.options.map(o => o.number), [1, 2, 3, 4]);
    }
    if (state.stage === 'modifier') assert.ok(state.options.every(o => o.none || o.image.startsWith('creator-tools/chat-chooses-modifiers/')));
    const option = state.options.find(o => o.flag !== 'plane');
    chat.vote({ platform: 'tiktok', userId: 'viewer', chatText: String(option.number) });
    command('next'); advance(2000);
  }
  assert.equal(chat.snapshot().phase, 'result');
  assert.ok(chat.snapshot().selected.modifier.none ||
    chat.snapshot().selected.modifier.image.startsWith('creator-tools/chat-chooses-modifiers/'),
    'the final selection retains the same edited challenge artwork as the panel ballot');
  const selection = JSON.stringify(chat.snapshot().selected);
  advance(3600000);
  assert.equal(chat.snapshot().phase, 'result');
  assert.equal(chat.snapshot().remainingSeconds, 0);
  assert.equal(JSON.stringify(chat.snapshot().selected), selection);
});

test('plane voting skips both weapons and super and only selects compatible challenges', () => {
  const { chat, command, advance } = fixture();
  const plane = chat.snapshot().options.find(o => o.flag === 'plane');
  assert.ok(plane);
  chat.vote({ platform: 'tiktok', userId: 'plane-viewer', chatText: String(plane.number) });
  command('next'); advance(2000);
  assert.equal(chat.snapshot().stage, 'charm');
  assert.equal(chat.snapshot().round, 2);
  command('next'); advance(2000);
  assert.equal(chat.snapshot().stage, 'modifier');
  assert.ok(chat.snapshot().options.every(o => o.flag !== 'ground'));
  command('next'); advance(2000);
  assert.equal(chat.snapshot().phase, 'result');
  assert.equal(chat.snapshot().round, 3);
  assert.deepEqual(Object.keys(chat.snapshot().selected), ['boss', 'charm', 'modifier']);
});
