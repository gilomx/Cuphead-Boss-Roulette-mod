import type { StreamRule, StreamRulePlatform, StreamRuleTrigger, TikTokGift } from "../../model";
import { interactionItemFor, interactionItems } from "./interactionCatalog";
import { platformTriggers } from "./streamRuleDraft";

export interface StreamRuleGroupShared {
  interaction: string;
  quantity: number;
  userCooldownSeconds: number;
  globalCooldownSeconds: number;
  durationSeconds: number;
  countdownSeconds: number;
}

export interface StreamRuleValueDraft {
  id?: number;
  name: string;
  enabled: boolean;
  platform: StreamRulePlatform;
  eventType: StreamRuleTrigger;
  giftId: string;
  rewardName: string;
  every: number;
}

export interface StreamRuleGroup extends StreamRuleGroupShared {
  id: number;
  ruleIds: number[];
  rules: StreamRule[];
}

export interface StreamRuleGroupDraft extends StreamRuleGroupShared {
  /** Concurrency metadata from the editor; it is not a saved rule field. */
  expectedRevision?: number;
  ruleIds: number[];
  values: StreamRuleValueDraft[];
}

function sharedFields(rule: StreamRule): StreamRuleGroupShared {
  return {
    interaction: rule.interaction,
    quantity: rule.quantity,
    userCooldownSeconds: rule.userCooldownSeconds ?? 0,
    globalCooldownSeconds: rule.globalCooldownSeconds ?? 0,
    durationSeconds: rule.durationSeconds ?? 15,
    countdownSeconds: rule.countdownSeconds ?? 3,
  };
}

/** Preserve every legacy rule, splitting repeated platforms into separate cohorts. */
export function groupStreamRules(rules: StreamRule[]): StreamRuleGroup[] {
  const result: StreamRuleGroup[] = [];
  const cohorts = new Map<string, StreamRuleGroup[]>();
  for (const rule of rules) {
    const shared = sharedFields(rule);
    const signature = JSON.stringify(Object.values(shared));
    const candidates = cohorts.get(signature) ?? [];
    let group = candidates.find(candidate => !candidate.rules.some(value =>
      (value.platform ?? "tiktok") === (rule.platform ?? "tiktok")));
    if (!group) {
      group = { ...shared, id: rule.id, ruleIds: [], rules: [] };
      candidates.push(group);
      cohorts.set(signature, candidates);
      result.push(group);
    }
    group.id = Math.min(group.id, rule.id);
    group.ruleIds.push(rule.id);
    group.rules.push(rule);
  }
  return result;
}

export function createStreamRuleValueDraft(
  gift?: TikTokGift,
  platform: StreamRulePlatform = "tiktok",
): StreamRuleValueDraft {
  return {
    name: platform === "tiktok" ? gift?.name ?? "" : "",
    enabled: true,
    platform,
    eventType: platform === "tiktok" ? "gift" : platform === "twitch" ? "currency" : "jewels",
    giftId: platform === "tiktok" ? gift?.giftId ?? "" : "",
    rewardName: "",
    every: platform === "tiktok" ? 1 : 100,
  };
}

export function createStreamRuleGroupDraft(
  gift?: TikTokGift,
  platform: StreamRulePlatform = "tiktok",
): StreamRuleGroupDraft {
  return {
    ruleIds: [],
    interaction: interactionItems.find(item => item.category === "attack")!.id,
    quantity: 1,
    userCooldownSeconds: 0,
    globalCooldownSeconds: 0,
    durationSeconds: 15,
    countdownSeconds: 3,
    values: [createStreamRuleValueDraft(gift, platform)],
  };
}

export function draftForStreamRuleGroup(group: StreamRuleGroup): StreamRuleGroupDraft {
  return {
    interaction: group.interaction,
    quantity: group.quantity,
    userCooldownSeconds: group.userCooldownSeconds,
    globalCooldownSeconds: group.globalCooldownSeconds,
    durationSeconds: group.durationSeconds,
    countdownSeconds: group.countdownSeconds,
    ruleIds: [...group.ruleIds],
    values: group.rules.map(rule => ({
      id: rule.id,
      name: rule.name,
      enabled: rule.enabled,
      platform: rule.platform ?? "tiktok",
      eventType: rule.eventType,
      giftId: rule.giftId,
      rewardName: rule.rewardName ?? "",
      every: rule.every,
    })),
  };
}

export function sameStreamRuleGroupDraft(
  left: StreamRuleGroupDraft | null,
  right: StreamRuleGroupDraft | null,
) {
  if (!left || !right) return left === right;
  const sharedKeys = ["interaction", "quantity", "userCooldownSeconds", "globalCooldownSeconds", "durationSeconds", "countdownSeconds"] as const;
  const valueKeys = ["id", "name", "enabled", "platform", "eventType", "giftId", "rewardName", "every"] as const;
  return sharedKeys.every(key => left[key] === right[key]) &&
    left.ruleIds.length === right.ruleIds.length &&
    left.ruleIds.every((id, index) => id === right.ruleIds[index]) &&
    left.values.length === right.values.length &&
    left.values.every((value, index) => valueKeys.every(key => value[key] === right.values[index][key]));
}

export interface StreamRuleGroupLimits {
  maxEvery: number;
  maxQuantity: number;
  maxCooldownSeconds: number;
  maxValues?: number;
}

export function validStreamRuleGroupDraft(
  draft: StreamRuleGroupDraft,
  gifts: TikTokGift[],
  limits: StreamRuleGroupLimits,
) {
  const integer = (value: number, minimum: number, maximum: number) =>
    Number.isInteger(value) && value >= minimum && value <= maximum;
  const item = interactionItemFor(draft.interaction);
  if (!item || !integer(draft.quantity, 1, limits.maxQuantity) ||
      !integer(draft.userCooldownSeconds, 0, limits.maxCooldownSeconds) ||
      !integer(draft.globalCooldownSeconds, 0, limits.maxCooldownSeconds) ||
      !integer(draft.values.length, 1, Math.min(3, limits.maxValues ?? 3)) ||
      new Set(draft.values.map(value => value.platform)).size !== draft.values.length ||
      new Set(draft.ruleIds).size !== draft.ruleIds.length ||
      draft.ruleIds.some(id => !integer(id, 1, Number.MAX_SAFE_INTEGER)) ||
      (item.group === "challenge" && (!integer(draft.durationSeconds, 1, 120) || !integer(draft.countdownSeconds, 0, 30)))) return false;
  const valueIds = draft.values.flatMap(value => value.id === undefined ? [] : [value.id]);
  if (new Set(valueIds).size !== valueIds.length) return false;
  return draft.values.every(value =>
    (value.id === undefined || draft.ruleIds.includes(value.id)) &&
    typeof value.enabled === "boolean" &&
    platformTriggers[value.platform]?.includes(value.eventType) &&
    (value.eventType === "follow" || integer(value.every, 1, limits.maxEvery)) &&
    (value.eventType !== "gift" || gifts.some(gift => gift.giftId === value.giftId)) &&
    (value.eventType !== "redemption" || (value.rewardName.trim().length > 0 && value.rewardName.trim().length <= 64)));
}
