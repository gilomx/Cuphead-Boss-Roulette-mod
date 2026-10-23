import type { StreamRule, StreamRulesConfigState } from "../../model";
import { interactionItems } from "../interactions/interactionCatalog";
import type { InteractionsPreviewSnapshot } from "./model";

/** Examples stay inside the designer message; they never become saved rules. */
export function createInteractionsPreview(
  config: StreamRulesConfigState | null,
  translate: (key: string) => string,
): InteractionsPreviewSnapshot {
  if (config?.rules.length) {
    return {
      preview: true,
      examples: false,
      rules: config.rules.filter(rule => rule.enabled),
      communityGift: config.communityGift,
    };
  }
  const examples = interactionItems.filter(item => [
    "help_extra_life", "help_ghost", "challenge_no_dash", "challenge_no_ex",
    "challenge_half_damage", "challenge_black_and_white", "attack_potato",
    "attack_carrot", "attack_onion", "attack_frog", "attack_flower", "attack_slime",
  ].includes(item.id));
  // Keep enough items to preview more than one nine-item page as the catalog evolves.
  const items = [...examples, ...interactionItems.filter(item => !examples.includes(item))].slice(0, 18);
  const rules: StreamRule[] = items.flatMap((item, index) =>
    (["tiktok", "twitch", "youtube"] as const).map((platform, platformIndex) => ({
      id: -1 - index * 3 - platformIndex,
      name: translate(item.titleKey),
      enabled: true,
      platform,
      connectionId: "all",
      eventType: platform === "tiktok" ? "gift" : platform === "twitch" ? "currency" : "jewels",
      giftId: platform === "tiktok" ? "5655" : "",
      giftName: platform === "tiktok" ? "Rose" : "",
      every: platform === "tiktok" ? 1 + index % 3 : (index + 1) * 100,
      interaction: item.id,
      quantity: 1,
      userCooldownSeconds: 0,
      globalCooldownSeconds: 0,
    })));
  return { preview: true, examples: true, rules };
}
