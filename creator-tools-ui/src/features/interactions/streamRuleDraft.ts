import type { StreamRule, StreamRuleDraft, TikTokGift } from "../../model";
import { interactionItems } from "./interactionCatalog";

export function createStreamRuleDraft(gift?: TikTokGift): StreamRuleDraft {
  return {
    name: gift?.name ?? "",
    enabled: true,
    giftId: gift?.giftId ?? "",
    every: 1,
    interaction: interactionItems[0].id,
    quantity: 1,
  };
}

export function draftForStreamRule(rule: StreamRule): StreamRuleDraft {
  return {
    id: rule.id,
    name: rule.name,
    enabled: rule.enabled,
    giftId: rule.giftId,
    every: rule.every,
    interaction: rule.interaction,
    quantity: rule.quantity,
  };
}
