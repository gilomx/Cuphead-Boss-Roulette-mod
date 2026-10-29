import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, mkdtempSync, rmSync } from "node:fs";
import { createRequire } from "node:module";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const uiRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const temporary = mkdtempSync(join(tmpdir(), "pichi-stream-groups-"));
let helpers;
try {
  const compiled = spawnSync(process.execPath, [
    "node_modules/typescript/bin/tsc", "src/features/interactions/streamRuleGroups.ts",
    "--ignoreConfig", "--target", "es2022", "--module", "commonjs", "--outDir", temporary, "--skipLibCheck",
  ], { cwd: uiRoot, encoding: "utf8", windowsHide: true });
  assert.equal(compiled.status, 0, compiled.error?.message || compiled.stdout + compiled.stderr);
  const path = existsSync(join(temporary, "streamRuleGroups.js")) ? join(temporary, "streamRuleGroups.js")
    : join(temporary, "features/interactions/streamRuleGroups.js");
  helpers = createRequire(import.meta.url)(path);
} finally {
  rmSync(temporary, { recursive: true, force: true });
}

const { groupStreamRules, createStreamRuleGroupDraft, createStreamRuleValueDraft,
  draftForStreamRuleGroup, sameStreamRuleGroupDraft, validStreamRuleGroupDraft } = helpers;
const gift = { giftId: "5655", name: "Rose", imagePath: "/rose.webp", aliases: [], coinsPerUnit: 1, sourceGiftType: 1 };
const limits = { maxEvery: 1000000, maxQuantity: 50, maxCooldownSeconds: 3600 };
const rule = (id, platform, changes = {}) => ({
  id, name: "Legacy " + id, enabled: true, platform, connectionId: "all",
  eventType: platform === "tiktok" ? "gift" : platform === "twitch" ? "currency" : "jewels",
  giftId: platform === "tiktok" ? gift.giftId : "", giftName: platform === "tiktok" ? gift.name : "",
  every: platform === "tiktok" ? 1 : 100, interaction: "help_extra_life", quantity: 1,
  userCooldownSeconds: 0, globalCooldownSeconds: 0, ...changes,
});

test("legacy rules form stable one-value-per-platform cohorts without losing duplicates or metadata", () => {
  const rules = [rule(8, "tiktok", { enabled: false }), rule(4, "tiktok"), rule(7, "twitch"),
    rule(1, "twitch", { rewardName: "Kept metadata" }), rule(12, "youtube"), rule(11, "youtube")];
  const before = JSON.stringify(rules);
  const groups = groupStreamRules(rules);
  assert.deepEqual(groups.map(group => group.id), [7, 1]);
  assert.deepEqual(groups.map(group => group.ruleIds), [[8, 7, 12], [4, 1, 11]]);
  assert.equal(groups[0].rules[0], rules[0]);
  assert.equal(groups[1].rules[1].rewardName, "Kept metadata");
  assert.equal(groups[0].rules[0].enabled, false, "a disabled value still belongs to its attack");
  assert.equal(new Set(groups.flatMap(group => group.ruleIds)).size, rules.length);
  assert.equal(JSON.stringify(rules), before, "grouping never rewrites legacy data");
});

test("shared settings must match exactly, with legacy duration/countdown defaults", () => {
  const grouped = groupStreamRules([rule(1, "tiktok"), rule(2, "twitch", { durationSeconds: 15, countdownSeconds: 3 })]);
  assert.equal(grouped.length, 1);
  assert.equal(grouped[0].durationSeconds, 15);
  assert.equal(grouped[0].countdownSeconds, 3);
  for (const changes of [{ interaction: "help_ghost" }, { quantity: 2 }, { userCooldownSeconds: 5 },
    { globalCooldownSeconds: 6 }, { durationSeconds: 20 }, { countdownSeconds: 0 }]) {
    assert.equal(groupStreamRules([rule(1, "tiktok"), rule(2, "twitch", changes)]).length, 2, JSON.stringify(changes));
  }
  assert.deepEqual(groupStreamRules([]), []);
});

test("new groups require an interaction selection and editing keeps the saved selection and row state", () => {
  for (const [platform, eventType, every] of [["tiktok", "gift", 1], ["twitch", "currency", 100], ["youtube", "jewels", 100]]) {
    const draft = createStreamRuleGroupDraft(gift, platform);
    assert.equal(draft.values.length, 1);
    assert.equal(draft.values[0].platform, platform);
    assert.equal(draft.values[0].eventType, eventType);
    assert.equal(draft.values[0].every, every);
    assert.equal(draft.values[0].enabled, true);
    assert.equal(draft.interaction, "");
    assert.equal(validStreamRuleGroupDraft(draft, [gift], limits), false);
    draft.interaction = "help_extra_life";
    assert.equal(validStreamRuleGroupDraft(draft, [gift], limits), true);
  }
  const original = groupStreamRules([rule(1, "tiktok"), rule(2, "twitch", { enabled: false })])[0];
  const draft = draftForStreamRuleGroup(original);
  assert.equal(draft.interaction, original.interaction);
  assert.equal(validStreamRuleGroupDraft(draft, [gift], limits), true);
  draft.values[1].enabled = true;
  draft.values[0].every = 17;
  draft.ruleIds.pop();
  assert.equal(original.rules[0].every, 1);
  assert.equal(original.rules[1].enabled, false);
  assert.deepEqual(original.ruleIds, [1, 2]);
});

test("draft dirty tracking ignores revision metadata but detects row and shared changes", () => {
  const initial = createStreamRuleGroupDraft(gift);
  const copy = structuredClone(initial);
  copy.expectedRevision = 99;
  assert.equal(sameStreamRuleGroupDraft(initial, copy), true);
  for (const change of [draft => { draft.interaction = "help_extra_life"; },
    draft => { draft.quantity = 2; }, draft => { draft.values[0].enabled = false; },
    draft => { draft.values[0].every = 5; }, draft => { draft.values.push(createStreamRuleValueDraft(gift, "twitch")); },
    draft => { draft.ruleIds.push(7); }, draft => { draft.countdownSeconds = 0; }]) {
    const changed = structuredClone(initial);
    change(changed);
    assert.equal(sameStreamRuleGroupDraft(initial, changed), false);
  }
  assert.equal(sameStreamRuleGroupDraft(null, null), true);
  assert.equal(sameStreamRuleGroupDraft(initial, null), false);
});

test("group validation checks every network, capacity, identities and shared settings before saving", () => {
  const draft = createStreamRuleGroupDraft(gift);
  draft.interaction = "help_extra_life";
  draft.values.push(createStreamRuleValueDraft(gift, "twitch"), createStreamRuleValueDraft(gift, "youtube"));
  assert.equal(validStreamRuleGroupDraft(draft, [gift], limits), true);
  assert.equal(validStreamRuleGroupDraft(draft, [gift], { ...limits, maxValues: 2 }), false);
  const disabled = structuredClone(draft);
  disabled.values.forEach(value => { value.enabled = false; });
  assert.equal(validStreamRuleGroupDraft(disabled, [gift], limits), true, "disabling a value does not delete its configured cost");
  for (const change of [
    value => { value.interaction = ""; },
    value => { value.values[1].platform = "tiktok"; },
    value => { value.values[1].eventType = "jewels"; },
    value => { value.values[0].giftId = "missing"; },
    value => { value.values[2].every = 1.5; },
    value => { value.values[1].eventType = "redemption"; value.values[1].rewardName = " "; },
    value => { value.quantity = 2.5; },
    value => { value.globalCooldownSeconds = 4000; },
    value => { value.values[0].id = 123; },
    value => { value.interaction = "challenge_no_dash"; value.durationSeconds = 0; },
    value => { value.interaction = "challenge_no_dash"; value.countdownSeconds = 31; },
  ]) {
    const invalid = structuredClone(draft);
    change(invalid);
    assert.equal(validStreamRuleGroupDraft(invalid, [gift], limits), false, JSON.stringify(invalid));
  }
  draft.values[1].eventType = "redemption";
  draft.values[1].rewardName = "My attack";
  assert.equal(validStreamRuleGroupDraft(draft, [gift], limits), true);
});
