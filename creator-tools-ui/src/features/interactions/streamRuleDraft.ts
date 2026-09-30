import type { StreamRule, StreamRuleDraft, TikTokGift } from "../../model";
import { interactionItems } from "./interactionCatalog";

export function createStreamRuleDraft(gift?: TikTokGift): StreamRuleDraft {
  return {
    name: gift?.name ?? "",
    enabled: true,
    eventType: "gift",
    giftId: gift?.giftId ?? "",
    every: 1,
    interaction: interactionItems.find((item) => item.category === "attack")!.id,
    durationSeconds: 15,
    countdownSeconds: 3,
    quantity: 1,
    userCooldownSeconds: 0,
    globalCooldownSeconds: 0,
  };
}

export function draftForStreamRule(rule: StreamRule): StreamRuleDraft {
  return {
    id: rule.id,
    name: rule.name,
    enabled: rule.enabled,
    eventType: rule.eventType,
    giftId: rule.giftId,
    every: rule.every,
    interaction: rule.interaction,
    quantity: rule.quantity,
    userCooldownSeconds: rule.userCooldownSeconds ?? 0,
    globalCooldownSeconds: rule.globalCooldownSeconds ?? 0,
    durationSeconds: rule.durationSeconds ?? 15,
    countdownSeconds: rule.countdownSeconds ?? 3,
  };
}

export function sameStreamRuleDraft(
  left: StreamRuleDraft | null,
  right: StreamRuleDraft | null,
) {
  if (!left || !right) return left === right;
  return left.id === right.id &&
    left.name === right.name &&
    left.enabled === right.enabled &&
    left.eventType === right.eventType &&
    left.giftId === right.giftId &&
    left.every === right.every &&
    left.interaction === right.interaction &&
    left.quantity === right.quantity &&
    left.userCooldownSeconds === right.userCooldownSeconds &&
    left.globalCooldownSeconds === right.globalCooldownSeconds &&
    left.durationSeconds === right.durationSeconds &&
    left.countdownSeconds === right.countdownSeconds;
}
