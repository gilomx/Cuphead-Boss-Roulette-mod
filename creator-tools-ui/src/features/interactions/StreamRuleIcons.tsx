import { Diamond, Gift, Hand, Star, Ticket, UserPlus, Repeat2 } from "lucide-react";
import type { StreamRulePlatform, StreamRuleTrigger } from "../../model";

export function StreamPlatformIcon({ platform }: { platform: StreamRulePlatform }) {
  return (
    <svg className="stream-platform-icon" data-platform={platform} viewBox="0 0 24 24"
      role="img" aria-label={platform === "twitch" ? "Twitch" : "TikTok"}>
      <title>{platform === "twitch" ? "Twitch" : "TikTok"}</title>
      {platform === "twitch" ? (
        <path fill="currentColor" d="M4 2 1 5v16h6v3l4-3h5l7-7V2H4zm17 11-4 4h-6l-4 4v-4H3V4h18v9zM16 7h2v6h-2V7zm-5 0h2v6h-2V7z" />
      ) : (
        <path fill="currentColor" d="M16.6 2c.4 2.5 1.8 4 4.4 4.2v3.9a9.3 9.3 0 0 1-4.4-1.2v7.6a6.5 6.5 0 1 1-6.5-6.5h1v4a2.6 2.6 0 1 0 1.6 2.4V2h3.9z" />
      )}
    </svg>
  );
}

export function StreamTriggerIcon({ eventType }: { eventType: StreamRuleTrigger }) {
  const Icon = { gift: Gift, like: Hand, follow: UserPlus, currency: Diamond,
    subscription: Star, subscription_gift: Gift, resubscription: Repeat2, redemption: Ticket }[eventType];
  return <Icon className="stream-trigger-icon" aria-hidden="true" />;
}
