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
  interactionsEnabled: boolean;
  masterRevision: number;
  queuePaused: boolean;
  queueControlRevision: number;
  pendingClearProjected: boolean;
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
  pendingCount: number;
  backlogCount: number;
  deferredTestCount: number;
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
  maxActive: number;
  queue: InteractionQueueEntry[];
  blockedByPeskyBattle?: boolean;
}

export type PeskyBattlePhase =
  | "off"
  | "recruiting"
  | "ready"
  | "waiting_level"
  | "active"
  | "won";

export interface PeskyBattleTrigger {
  giftId: string;
  giftName: string;
  giftImagePath: string;
  coinsPerUnit: number;
}

export interface PeskyBattleParticipant {
  slot: number;
  userId: string;
  userName: string;
  displayName: string;
  avatarUrl: string;
  joinedAt: string;
}

export interface PeskyBattleConfigState {
  ready: boolean;
  schemaVersion: number;
  revision: number;
  phase: PeskyBattlePhase;
  sessionId: number;
  attempt: number;
  capacity: number;
  exclusive: boolean;
  gameplayAvailable: boolean;
  targetLevel: string;
  trigger: PeskyBattleTrigger;
  allowStreamAttacks: boolean;
  participants: PeskyBattleParticipant[];
  items: string[];
  disabledItems: string[];
  feedback: string;
  error: boolean;
}

export type LiveEventId = "pesky_battle" | "tap_farming";

export interface LiveEventsConfigState {
  ready: boolean;
  schemaVersion?: number;
  revision: number;
  activeEvent: LiveEventId | "";
  status: "idle" | "active" | "stopping";
  stoppingEvent: LiveEventId | "";
  feedback: string;
  error: boolean;
}

export type TapFarmingPhase =
  | "off"
  | "collecting"
  | "active"
  | "transition"
  | "completed"
  | "stopping";

export interface TapFarmingPhaseProgress {
  index: number;
  label?: string;
  status: "pending" | "active" | "complete";
  progress: number;
  reserveHealth?: number;
}

export interface TapFarmingConfigState {
  ready: boolean;
  schemaVersion: number;
  revision: number;
  phase: TapFarmingPhase;
  sessionId: number;
  blockedByLiveEvent: LiveEventId | "";
  conversion: {
    tapsPerConversion: number;
    healthPointsPerConversion: number;
    tapsPerHealthPoint?: number;
  };
  counters: {
    totalTaps: number;
    bankedTaps: number;
    unconvertedTaps: number;
    convertedHealth: number;
    reserveHealth: number;
    spentHealth: number;
  };
  bossName: string;
  levelId: string;
  attempt: number;
  boss: {
    currentHealth: number;
    totalHealth: number;
    progress: number;
  };
  effectiveHealth?: {
    available: boolean;
    current: number;
    total: number;
    ratio: number;
  };
  phaseIndex: number;
  phaseCount: number;
  overallProgress: number;
  phases: TapFarmingPhaseProgress[];
  feedback: string;
  error: boolean;
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
