const triggers = {
  tiktok: ['gift', 'like', 'follow'],
  twitch: ['follow', 'currency', 'subscription', 'subscription_gift', 'resubscription', 'redemption'],
  youtube: ['jewels'],
};
const integer = (value, min, max) => Number.isSafeInteger(value) && value >= min && value <= max;
const maximumRewardCost = 1_000_000_000;
const slot = rule => rule.platform === 'twitch' && rule.eventType === 'redemption' ? 'twitch_redemption' : rule.platform;
const sharedKey = rule => JSON.stringify([rule.interaction, rule.quantity,
  rule.userCooldownSeconds ?? 0, rule.globalCooldownSeconds ?? 0,
  rule.durationSeconds ?? 15, rule.countdownSeconds ?? 3]);
const changed = (before, after) => !after || before.enabled && !after.enabled || [
  'platform', 'eventType', 'giftId', 'rewardName', 'rewardId', 'rewardBroadcasterId', 'every', 'interaction', 'quantity',
  'userCooldownSeconds', 'globalCooldownSeconds', 'durationSeconds', 'countdownSeconds',
].some(key => before[key] !== after[key]);

export function mockRewardMetadata(eventType, value) {
  const { rewardId = '', rewardBroadcasterId = '', rewardCost = 0, rewardManaged = false } = value;
  if (typeof rewardId !== 'string' || typeof rewardBroadcasterId !== 'string' ||
      !integer(rewardCost, 0, maximumRewardCost) || typeof rewardManaged !== 'boolean') return null;
  if (rewardId === '') return !rewardBroadcasterId && !rewardCost && !rewardManaged ? {} : null;
  return eventType === 'redemption' && /^[\w-]{1,96}$/.test(rewardId) && /^\d{1,32}$/.test(rewardBroadcasterId)
    ? { rewardId, rewardBroadcasterId, rewardCost, rewardManaged } : null;
}

/** Pure atomic counterpart of the local runtime's group endpoint. */
export function applyStreamRuleGroup(state, command, { giftsById, interactionItems }) {
  const fail = (feedback = 'invalid_group', status = 400) => ({ ...state, feedback, error: true, status, resetIds: [] });
  if (!command || command.schemaVersion !== 1 || !integer(command.expectedRevision, 0, Number.MAX_SAFE_INTEGER)) return fail();
  if (command.expectedRevision !== state.revision) return fail('revision_conflict', 409);
  const { action, ruleIds } = command;
  if (!['save', 'delete', 'toggle', 'duplicate'].includes(action) || !Array.isArray(ruleIds) || ruleIds.length > 4 ||
      ruleIds.some(id => !integer(id, 1, Number.MAX_SAFE_INTEGER)) || new Set(ruleIds).size !== ruleIds.length ||
      action !== 'save' && ruleIds.length === 0) return fail();
  const references = state.rules.filter(rule => ruleIds.includes(rule.id));
  if (references.length !== ruleIds.length || new Set(references.map(sharedKey)).size > 1 ||
      new Set(references.map(slot)).size !== references.length) return fail();
  let nextId = state.nextId;
  let replacements = [];
  let feedback;
  if (action === 'save') {
    const { values } = command;
    if (!interactionItems.includes(command.interaction) || !integer(command.quantity, 1, 50) ||
        !integer(command.userCooldownSeconds, 0, 3600) || !integer(command.globalCooldownSeconds, 0, 3600) ||
        !integer(command.durationSeconds, 1, 120) || !integer(command.countdownSeconds, 0, 30) ||
        !Array.isArray(values) || values.length < 1 || values.length > 4 || values.some(value => !value) ||
        new Set(values.map(slot)).size !== values.length) return fail();
    const seen = new Set();
    for (const value of values) {
      const id = value.id;
      const name = typeof value.name === 'string' ? value.name.trim().slice(0, 64) : '';
      const rewardName = typeof value.rewardName === 'string' ? value.rewardName.trim() : '';
      const reward = mockRewardMetadata(value.eventType, value);
      if (!name || typeof value.enabled !== 'boolean' || !triggers[value.platform]?.includes(value.eventType) ||
          !integer(value.every, 1, 1000000) ||
          value.eventType === 'gift' && !giftsById.has(value.giftId) ||
          value.eventType === 'redemption' && (!rewardName || rewardName.length > 64) || !reward ||
          id !== undefined && (!ruleIds.includes(id) || seen.has(id))) return fail();
      if (id !== undefined) seen.add(id);
      replacements.push({
        id: id ?? nextId++, name, enabled: value.enabled,
        platform: value.platform, eventType: value.eventType,
        giftId: value.eventType === 'gift' ? value.giftId : '',
        rewardName: value.eventType === 'redemption' ? rewardName : '',
        ...reward,
        every: value.eventType === 'follow' ? 1 : value.every,
        interaction: command.interaction, quantity: command.quantity,
        durationSeconds: command.durationSeconds, countdownSeconds: command.countdownSeconds,
        userCooldownSeconds: command.userCooldownSeconds, globalCooldownSeconds: command.globalCooldownSeconds,
      });
    }
    feedback = ruleIds.length ? 'updated' : 'created';
  } else if (action === 'toggle') {
    if (typeof command.enabled !== 'boolean') return fail();
    replacements = references.map(rule => ({ ...rule, enabled: command.enabled }));
    feedback = command.enabled ? 'enabled' : 'disabled';
  } else if (action === 'duplicate') {
    replacements = references.map(rule => ({ ...rule, id: nextId++, name: (rule.name + ' (copia)').slice(0, 64) }));
    feedback = 'duplicated';
  } else feedback = 'deleted';
  let rules;
  if (action === 'duplicate') {
    const after = Math.max(...state.rules.map((rule, index) => ruleIds.includes(rule.id) ? index : -1)) + 1;
    rules = [...state.rules.slice(0, after), ...replacements, ...state.rules.slice(after)];
  } else if (!references.length) rules = [...state.rules, ...replacements];
  else {
    const at = state.rules.findIndex(rule => ruleIds.includes(rule.id));
    rules = state.rules.filter(rule => !ruleIds.includes(rule.id));
    rules.splice(at, 0, ...replacements);
  }
  if (rules.length > 100) return fail('rules_limit');
  const resetIds = action === 'duplicate' ? [] : references.filter(rule => changed(rule, replacements.find(next => next.id === rule.id))).map(rule => rule.id);
  return { rules, nextId, revision: state.revision + 1, feedback, error: false, status: 200, resetIds };
}
