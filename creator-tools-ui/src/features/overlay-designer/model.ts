import type { ChatChoosesState, StreamRule, StreamRulesConfigState } from "../../model";

export type OverlayProfileId = "vertical" | "horizontal";
export type OverlayComponentId = "tap_farming" | "pesky_battle" | "chat_chooses" | "roulette" | "interactions";
export type OverlayVariant = "default" | "compact" | "minimal";
export type OverlayTextFont = "clean" | "system" | "rounded" | "serif";

export interface OverlayCanvasSize {
  width: number;
  height: number;
}

export interface OverlayComposerComponent {
  id: OverlayComponentId;
  x: number;
  y: number;
  width: number;
  height: number;
  enabled: boolean;
  locked: boolean;
  layer: number;
  opacity: number;
  variant: OverlayVariant;
  showTitle: boolean;
  showDetails: boolean;
  motion: boolean;
  liquidColor: string;
  collectingColor: string;
  textColor: string;
  outlineColor: string;
  voteOutlineColor: string;
  textFont?: OverlayTextFont;
  textWeight?: number;
  textShadowColor?: string;
  textShadowX?: number;
  textShadowY?: number;
  textShadowBlur?: number;
  rouletteAlignment?: "left" | "center" | "right";
  rouletteTextFirst?: boolean;
  rouletteLogo?: boolean;
  rouletteRetry?: "keep" | "reappear";
  interactionsTemplate?: "cards" | "floating";
  interactionsLayout?: "strip" | "pages";
  interactionsTikTok?: boolean;
  interactionsTwitch?: boolean;
  interactionsYouTube?: boolean;
  interactionsPageSeconds?: number;
  interactionsSpeed?: number;
  interactionsSize?: number;
  interactionsAdaptive?: boolean;
  interactionsPaymentSize?: number;
  interactionsCardColor?: string;
  interactionsCardNoise?: number;
  interactionsImageOutlineColor?: string;
  interactionsImageOutlineWidth?: number;
  interactionsTextOutlineColor?: string;
  interactionsTextOutlineWidth?: number;
}

export interface OverlayComposerProfile {
  id: OverlayProfileId;
  canvas: OverlayCanvasSize;
  components: OverlayComposerComponent[];
}

export interface OverlayComposerConfigState {
  ready: boolean;
  schemaVersion: number;
  revision: number;
  interactionsBattleOnly?: boolean;
  profiles: OverlayComposerProfile[];
  feedback: string;
  error: boolean;
}

export type OverlayComposerOperation = "update" | "reset" | "copy" | "interactions_visibility";

export interface OverlayComposerCommand {
  schemaVersion: 1;
  expectedRevision: number;
  operation: OverlayComposerOperation;
  profileId: OverlayProfileId;
  componentId?: OverlayComponentId;
  sourceProfileId?: OverlayProfileId;
  interactionsBattleOnly?: boolean;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  enabled?: boolean;
  locked?: boolean;
  layer?: number;
  opacity?: number;
  variant?: OverlayVariant;
  showTitle?: boolean;
  showDetails?: boolean;
  motion?: boolean;
  liquidColor?: string;
  collectingColor?: string;
  textColor?: string;
  outlineColor?: string;
  voteOutlineColor?: string;
  textFont?: OverlayTextFont;
  textWeight?: number;
  textShadowColor?: string;
  textShadowX?: number;
  textShadowY?: number;
  textShadowBlur?: number;
  rouletteAlignment?: "left" | "center" | "right";
  rouletteTextFirst?: boolean;
  rouletteLogo?: boolean;
  rouletteRetry?: "keep" | "reappear";
  interactionsTemplate?: "cards" | "floating";
  interactionsLayout?: "strip" | "pages";
  interactionsTikTok?: boolean;
  interactionsTwitch?: boolean;
  interactionsYouTube?: boolean;
  interactionsPageSeconds?: number;
  interactionsSpeed?: number;
  interactionsSize?: number;
  interactionsAdaptive?: boolean;
  interactionsPaymentSize?: number;
  interactionsCardColor?: string;
  interactionsCardNoise?: number;
  interactionsImageOutlineColor?: string;
  interactionsImageOutlineWidth?: number;
  interactionsTextOutlineColor?: string;
  interactionsTextOutlineWidth?: number;
}

export interface OverlayPreviewCommand {
  schemaVersion: 1;
  operation: "start" | "update" | "stop";
  profileId: OverlayProfileId;
  componentId: OverlayComponentId;
  sessionId: string;
  expectedRevision?: number;
  simulationActive: boolean;
  layoutJson: string;
  scenario: string;
  bossName: string;
  levelId: string;
  totalTaps: number;
  tapDelta: number;
  damageDelta: number;
  reserveHealth: number;
  spentHealth: number;
  currentHealth: number;
  totalHealth: number;
  overallProgress: number;
  phaseIndex: number;
  phaseCount: number;
  attempt: number;
  participantCount: number;
  capacity: number;
  attackId: string;
  attackSlot: number;
  attackName: string;
  attackImagePath: string;
  attackStartedAt: number;
  eventEpoch: number;
  battleSignalsJson: string;
  chatStateJson: string;
}

export interface OverlayPreviewState {
  ready?: boolean;
  revision: number;
  runId?: number;
  active: boolean;
  simulationActive?: boolean;
  layout?: OverlayComposerProfile | null;
  profileId?: OverlayProfileId;
  sessionId?: string;
  componentId?: OverlayComponentId | "";
  feedback?: string;
  error?: boolean;
}

export interface TapFarmingPreviewSnapshot {
  revision: number;
  phase: "collecting" | "active" | "transition" | "completed";
  bossName: string;
  levelId: string;
  attempt: number;
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
  boss: {
    currentHealth: number;
    totalHealth: number;
    progress: number;
  };
  effectiveHealth: {
    available: boolean;
    current: number;
    total: number;
    ratio: number;
    scope?: "boss" | "arena";
  };
  phaseIndex: number;
  phaseCount: number;
  overallProgress: number;
  phases: Array<{
    index: number;
    status: "pending" | "active" | "complete";
    progress: number;
  }>;
  tapDelta: number;
  damageDelta: number;
}

export interface PeskyBattlePreviewParticipant {
  slot: number;
  userId: string;
  userName: string;
  displayName: string;
  avatarUrl: string;
}

export interface PeskyBattlePreviewSnapshot {
  eventEpoch: number;
  attacks?: NonNullable<PeskyBattlePreviewSnapshot["attack"]>[];
  challenges?: Array<{
    id: string;
    slot: number;
    name: string;
    imagePath: string;
    phase: "countdown" | "active";
    secondsRemaining: number;
    startedAt: number;
  }>;
  revision: number;
  phase: "off" | "recruiting" | "ready" | "waiting_level" | "active" | "won";
  capacity: number;
  attempt: number;
  trigger: {
    giftId: string;
    giftName: string;
    giftImagePath: string;
  };
  participants: PeskyBattlePreviewParticipant[];
  attack?: {
    id: string;
    slot: number;
    name: string;
    imagePath: string;
    startedAt: number;
    sequence: number;
  };
}

export interface InteractionsPreviewSnapshot {
  rules: StreamRule[];
  communityGift?: StreamRulesConfigState["communityGift"];
  preview: true;
  examples: boolean;
}

export interface OverlayComposerDesignMessage {
  type: "creator-tools-overlay-composer-design";
  version: 1;
  profileId: OverlayProfileId;
  profile: OverlayComposerProfile;
  selectedComponentId: OverlayComponentId;
  states: {
    tap_farming: TapFarmingPreviewSnapshot;
    pesky_battle: PeskyBattlePreviewSnapshot;
    chat_chooses: ChatChoosesState;
    roulette: ReturnType<typeof roulettePreview>;
    interactions: InteractionsPreviewSnapshot;
  };
  locale: string;
  background: "alpha" | "light" | "dark";
}

export const PROFILE_DIMENSIONS: Record<OverlayProfileId, OverlayCanvasSize> = {
  vertical: { width: 1080, height: 1920 },
  horizontal: { width: 1920, height: 1080 },
};

export const COMPONENT_IDS: OverlayComponentId[] = [
  "tap_farming",
  "pesky_battle",
  "chat_chooses",
  "roulette",
  "interactions",
];

export function minimumComponentSize(
  componentId: OverlayComponentId,
): OverlayCanvasSize {
  return componentId === "tap_farming" || componentId === "interactions"
    ? { width: 120, height: 100 }
    : { width: 320, height: 180 };
}

export function proportionalComponentSize(
  component: Pick<OverlayComposerComponent, "id" | "width" | "height">,
  requestedScale: number,
  maximum: OverlayCanvasSize,
): OverlayCanvasSize {
  const sourceWidth = Math.max(1, component.width);
  const sourceHeight = Math.max(1, component.height);
  const minimum = minimumComponentSize(component.id);
  const maximumWidth = Math.max(1, Math.floor(maximum.width));
  const maximumHeight = Math.max(1, Math.floor(maximum.height));
  const minimumScale = Math.max(
    minimum.width / sourceWidth,
    minimum.height / sourceHeight,
  );
  const maximumScale = Math.min(
    maximumWidth / sourceWidth,
    maximumHeight / sourceHeight,
  );
  const lowerScale = Math.min(minimumScale, maximumScale);
  const safeRequestedScale = Number.isFinite(requestedScale)
    ? requestedScale
    : requestedScale > 0
      ? maximumScale
      : lowerScale;
  const scale = Math.min(
    maximumScale,
    Math.max(lowerScale, safeRequestedScale),
  );

  return {
    width: Math.min(
      maximumWidth,
      Math.max(
        Math.min(minimum.width, maximumWidth),
        Math.round(sourceWidth * scale),
      ),
    ),
    height: Math.min(
      maximumHeight,
      Math.max(
        Math.min(minimum.height, maximumHeight),
        Math.round(sourceHeight * scale),
      ),
    ),
  };
}

export type OverlayResizeHandle = "nw" | "ne" | "sw" | "se";

export function resizeOverlayComponent(
  start: Pick<OverlayComposerComponent, "id" | "x" | "y" | "width" | "height">,
  handle: OverlayResizeHandle,
  dx: number,
  dy: number,
  canvas: OverlayCanvasSize,
) {
  const fromWest = handle.includes("w");
  const fromNorth = handle.includes("n");
  const right = start.x + start.width;
  const bottom = start.y + start.height;
  const requestedWidth = start.width + (fromWest ? -dx : dx);
  const requestedHeight = start.height + (fromNorth ? -dy : dy);
  const maximum = {
    width: fromWest ? right : canvas.width - start.x,
    height: fromNorth ? bottom : canvas.height - start.y,
  };
  let size: OverlayCanvasSize;
  if (start.id === "interactions") {
    const minimum = minimumComponentSize(start.id);
    size = {
      width: Math.min(maximum.width, Math.max(minimum.width, Math.round(requestedWidth))),
      height: Math.min(maximum.height, Math.max(minimum.height, Math.round(requestedHeight))),
    };
  } else {
    const horizontalScale = requestedWidth / Math.max(1, start.width);
    const verticalScale = requestedHeight / Math.max(1, start.height);
    const requestedScale = Math.abs(horizontalScale - 1) >= Math.abs(verticalScale - 1)
      ? horizontalScale
      : verticalScale;
    size = proportionalComponentSize(start, requestedScale, maximum);
  }
  return {
    x: fromWest ? right - size.width : start.x,
    y: fromNorth ? bottom - size.height : start.y,
    ...size,
  };
}

export function isOverlayProfileId(value: string | null): value is OverlayProfileId {
  return value === "vertical" || value === "horizontal";
}

export function isOverlayComponentId(value: string | null): value is OverlayComponentId {
  return value === "tap_farming" || value === "pesky_battle" || value === "chat_chooses" || value === "roulette" || value === "interactions";
}

export type RouletteScenario = "hud" | "logo" | "hidden" | "retry";

export function roulettePreview(scenario: RouletteScenario = "hud", replay = 0) {
  return {
    type: "state", active: scenario !== "hidden", battleActive: scenario === "hud" || scenario === "retry",
    visible: scenario === "hud", preview: true, session: -1 - replay,
    revealed: 5, textVisible: true, labelRevision: 0,
    challengeText: "NO DASH", completeExit: scenario === "retry", fastRetryExit: scenario === "retry",
    icons: ["weapons/lanzaguisantes.png", "weapons/rastreador.png", "supers/super1.png", "charms/bombadehumo.png", "modifiers/nodash_01.png"],
  };
}

export function cloneProfiles(profiles: OverlayComposerProfile[]) {
  return profiles.map((profile) => ({
    ...profile,
    canvas: { ...profile.canvas },
    components: profile.components.map((component) => ({ ...component })),
  }));
}

export function profileById(
  profiles: OverlayComposerProfile[],
  profileId: OverlayProfileId,
) {
  return profiles.find((profile) => profile.id === profileId) ?? null;
}

export function componentById(
  profile: OverlayComposerProfile | null,
  componentId: OverlayComponentId,
) {
  return profile?.components.find((component) => component.id === componentId) ?? null;
}
