import type { StreamRule, StreamRuleDraft, StreamRulePlatform, StreamRuleTrigger, TikTokGift } from "../../model";
import { interactionItems } from "./interactionCatalog";

export const platformTriggers: Record<StreamRulePlatform, StreamRuleTrigger[]> = {
  tiktok: ["gift", "like", "follow"],
  twitch: ["follow", "currency", "subscription", "subscription_gift", "resubscription", "redemption"],
  youtube: ["jewels"],
};

export function createStreamRuleDraft(gift?: TikTokGift, platform: StreamRulePlatform = "tiktok"): StreamRuleDraft {
  return {
    name: platform === "tiktok" ? gift?.name ?? "" : "",
    enabled: true,
    platform,
    rewardName: "",
    eventType: platformTriggers[platform][0],
    giftId: platform === "tiktok" ? gift?.giftId ?? "" : "",
    every: platform === "youtube" ? 100 : 1,
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
    platform: rule.platform ?? "tiktok",
    rewardName: rule.rewardName ?? "",
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
    left.platform === right.platform &&
    left.rewardName === right.rewardName &&
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
