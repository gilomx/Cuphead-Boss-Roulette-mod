export type ModifierKind = "plane" | "ground" | "both";

export interface CatalogOption {
  id: number;
  name: string;
}

export interface BossOption extends CatalogOption {
  plane: boolean;
}

export interface WeaponOption extends CatalogOption {
  empty: boolean;
}

export interface ModifierOption extends CatalogOption {
  kind: ModifierKind;
  none: boolean;
  enabled: boolean;
  canDisable: boolean;
}

export interface DisplayOption extends CatalogOption {
  key: string;
  image: string;
}

export interface DisplayBossOption extends BossOption, DisplayOption {}
export interface DisplayWeaponOption extends WeaponOption, DisplayOption {}
export interface DisplayModifierOption extends ModifierOption, DisplayOption {}

export interface RouletteSelection {
  boss: number;
  weapon1: number;
  weapon2: number;
  super: number;
  charm: number;
  modifier: number;
}

export interface ForceDraft extends RouletteSelection {
  enabled: boolean;
}

export interface RouletteConfigState {
  ready: boolean;
  enabled: boolean;
  selection: RouletteSelection;
  bosses: BossOption[];
  weapons: WeaponOption[];
  supers: CatalogOption[];
  charms: CatalogOption[];
  modifiers: ModifierOption[];
}

export interface InteractionConfigState {
  ready: boolean;
  available: boolean;
  suspendedByPesky: boolean;
  randomTestEnabled: boolean;
  randomTestRevision: number;
  phaseTransitionProtectionEnabled: boolean;
  phaseTransitionProtectionRevision: number;
  showGiftImage?: boolean;
  settingsRevision?: number;
  item: string;
  items: string[];
  lastItem: string;
  feedback: string;
  error: boolean;
  revision: number;
  queueCount: number;
  activeCount: number;
  maxActive: number;
  maxActiveLimit: number;
  maxBatch: number;
  maxDelay: number;
  queue: InteractionQueueEntry[];
}

export interface PeskyModeConfigState {
  ready: boolean;
  available: boolean;
  enabled: boolean;
  running: boolean;
  startingBattle: boolean;
  waitingForInteractions: boolean;
  revision: number;
  feedback: string;
  error: boolean;
  minimumInterval: number;
  maximumInterval: number;
  names: string[];
  items: string[];
  disabledItems: string[];
  queueCount: number;
  activeCount: number;
  pausedInteractionCount: number;
  pausedInteractionActiveCount: number;
  maxActive: number;
  queue: InteractionQueueEntry[];
}

export interface InteractionQueueEntry {
  id: number;
  item: string;
  donor: string;
  delaySeconds: number;
  status: "active" | "queued" | "scheduled" | "waiting_game";
}

export interface TikTokGift {
  giftId: string;
  name: string;
  aliases: string[];
  coinsPerUnit: number;
  sourceGiftType: number;
  imagePath: string;
}

export interface TikTokGiftCatalog {
  schemaVersion: number;
  catalogVersion: string;
  platform: "tiktok";
  locale: string;
  giftCount: number;
  gifts: TikTokGift[];
}

export type StreamRuleTrigger = "gift" | "like" | "follow";

export interface StreamRule {
  id: number;
  name: string;
  enabled: boolean;
  platform: "tiktok";
  connectionId: "all";
  eventType: StreamRuleTrigger;
  giftId: string;
  giftName: string;
  coinsPerUnit?: number;
  every: number;
  interaction: string;
  quantity: number;
}

export interface StreamRuleDraft {
  id?: number;
  name: string;
  enabled: boolean;
  eventType: StreamRuleTrigger;
  giftId: string;
  every: number;
  interaction: string;
  quantity: number;
}

export interface StreamRulesConfigState {
  ready: boolean;
  schemaVersion: number;
  revision: number;
  engineActive: boolean;
  catalogVersion: string;
  feedback: string;
  error: boolean;
  maxRules: number;
  maxEvery: number;
  maxQuantity: number;
  rules: StreamRule[];
}

export type StreamPlatform = "tiktok" | "twitch" | "youtube";

export type StreamEventType =
  | "gift"
  | "currency"
  | "like"
  | "follow"
  | "subscription"
  | "redemption";

export interface DashboardConnection {
  id: string;
  platform: string;
  connector: string;
  label: string;
  status: string;
  account?: string;
  message?: string;
  messageCode?: string;
  retryAttempt?: number;
  lastEventAt?: string | null;
}

export interface DashboardCounters {
  received: number;
  matched: number;
  queued: number;
  ignored: number;
  gifts: number;
  valued: number;
  likes: number;
  follows: number;
  subscriptions: number;
  coins: number;
  bits: number;
}

export interface DashboardEvent {
  schemaVersion?: number;
  sequence?: number;
  id: string;
  eventId?: string;
  idempotencyKey?: string;
  connectionId?: string;
  streamSessionId?: string;
  receivedAt: string;
  platform: string;
  connector: string;
  type: string;
  user?: string;
  userId?: string | null;
  summary?: string;
  message?: string;
  messageCode?: string;
  status: string;
  rule?: string;
  action?: string;
  amount?: number;
  unitValue?: number;
  totalValue?: number;
  unit?: string;
  currency?: string | null;
  count?: number;
  itemName?: string;
  itemId?: string | null;
  itemImageUrl?: string | null;
  streakId?: string | null;
  streakState?: "none" | "progress" | "final";
  rawEventType?: string | null;
  simulated?: boolean;
}

export interface DashboardState {
  schemaVersion?: number;
  ready: boolean;
  revision: number;
  engineStatus: string;
  streamSessionId?: string;
  connections: DashboardConnection[];
  counters: DashboardCounters;
  events: DashboardEvent[];
}

export type ConnectionStatus =
  | "connecting"
  | "saved"
  | "saving"
  | "pending"
  | "error";
