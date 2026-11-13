import type { StreamRule, StreamRulesConfigState } from "../../model";
import { interactionItemFor, interactionItems } from "../interactions/interactionCatalog";
import { platformTriggers } from "../interactions/streamRuleDraft";
import type { InteractionsPreviewSnapshot, OverlayComposerComponent } from "./model";

type InteractionsPreviewPresentation = Pick<OverlayComposerComponent,
  "interactionsLayout" | "interactionsTikTok" | "interactionsTwitch" | "interactionsYouTube">;

const platformSettings = [
  ["tiktok", "interactionsTikTok"],
  ["twitch", "interactionsTwitch"],
  ["youtube", "interactionsYouTube"],
] as const;

/** Examples stay inside the designer message; they never become saved rules. */
export function createInteractionsPreview(
  config: StreamRulesConfigState | null,
  translate: (key: string) => string,
  presentation: InteractionsPreviewPresentation = {},
): InteractionsPreviewSnapshot {
  const platforms = platformSettings.filter(([, key]) => presentation[key] !== false).map(([platform]) => platform);
  const configured = config?.rules.filter(rule => rule.enabled === true && platforms.includes(rule.platform) &&
    platformTriggers[rule.platform].includes(rule.eventType) && rule.every > 0 && interactionItemFor(rule.interaction)) ?? [];
  const paginated = presentation.interactionsLayout === "pages";
  if (platforms.length === 0 || (!paginated && configured.length > 0)) {
    return {
      preview: true,
      examples: false,
      rules: configured,
      communityGift: config?.communityGift,
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
  const usedEffects = platforms.map(platform =>
    new Set(configured.filter(rule => rule.platform === platform).map(rule => rule.interaction)));
  const rules: StreamRule[] = items.flatMap((item, index) =>
    platforms.flatMap((platform, platformIndex) => {
      const effects = usedEffects[platformIndex];
      if (effects.size >= 18 || effects.has(item.id)) return [];
      effects.add(item.id);
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
  return { preview: true, examples: rules.length > 0, rules: [...configured, ...rules], communityGift: config?.communityGift };
}
