import type { StreamRule, StreamRulesConfigState } from "../../model";
import { interactionItems } from "../interactions/interactionCatalog";
import type { InteractionsPreviewSnapshot } from "./model";

/** Examples stay inside the designer message; they never become saved rules. */
export function createInteractionsPreview(
  config: StreamRulesConfigState | null,
  translate: (key: string) => string,
  paginated = false,
): InteractionsPreviewSnapshot {
  const configured = config?.rules.filter(rule => rule.enabled) ?? [];
  const platforms = ["tiktok", "twitch", "youtube"] as const;
  const usedEffects = Object.fromEntries(platforms.map(platform => [platform,
    new Set(configured.filter(rule => rule.platform === platform).map(rule => rule.interaction)),
  ])) as Record<typeof platforms[number], Set<string>>;
  if (config?.rules.length && (!paginated || platforms.every(platform => usedEffects[platform].size >= 18))) {
    return {
      preview: true,
      examples: false,
      rules: configured,
      communityGift: config.communityGift,
    };
  }
  const examples = interactionItems.filter(item => [
    "help_extra_life", "help_ghost", "challenge_no_dash", "challenge_no_ex",
    "challenge_half_damage", "challenge_black_and_white", "attack_potato",
    "attack_carrot", "attack_onion", "attack_frog", "attack_flower", "attack_slime",
  ].includes(item.id));
  // Keep enough items to preview more than one nine-item page as the catalog evolves.
  const items = [...examples, ...interactionItems.filter(item => !examples.includes(item))];
  const gifts = [
    ["5655", "Rosa"], ["5487", "Corazón coreano"], ["5658", "Perfume"],
    ["5827", "Cono de helado"], ["6064", "GG"], ["6093", "Fútbol"],
  ];
  const rules: StreamRule[] = items.flatMap((item, index) =>
    platforms.flatMap((platform, platformIndex) => {
      if (usedEffects[platform].size >= 18 || usedEffects[platform].has(item.id)) return [];
      usedEffects[platform].add(item.id);
      return [{
        id: -1 - index * 3 - platformIndex,
        name: translate(item.titleKey),
        enabled: true,
        platform,
        connectionId: "all",
        eventType: platform === "tiktok" ? "gift" : platform === "twitch" ? "currency" : "jewels",
        giftId: platform === "tiktok" ? gifts[index % gifts.length][0] : "",
        giftName: platform === "tiktok" ? gifts[index % gifts.length][1] : "",
        every: platform === "tiktok" ? 1 + index % 3 : (index + 1) * 100,
        interaction: item.id,
        quantity: 1,
        userCooldownSeconds: 0,
        globalCooldownSeconds: 0,
      }];
    }));
  return { preview: true, examples: true, rules: [...configured, ...rules], communityGift: config?.communityGift };
}
