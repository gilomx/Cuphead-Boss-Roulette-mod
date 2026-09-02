export type OverlayProfileId = "vertical" | "horizontal";
export type OverlayComponentId = "tap_farming" | "pesky_battle";
export type OverlayVariant = "default" | "compact" | "minimal";

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
  variant: OverlayVariant;
  showTitle: boolean;
  showDetails: boolean;
  motion: boolean;
  liquidColor: string;
  collectingColor: string;
  textColor: string;
  outlineColor: string;
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
  profiles: OverlayComposerProfile[];
  feedback: string;
  error: boolean;
}

export type OverlayComposerOperation = "update" | "reset" | "copy";

export interface OverlayComposerCommand {
  schemaVersion: 1;
  expectedRevision: number;
  operation: OverlayComposerOperation;
  profileId: OverlayProfileId;
  componentId?: OverlayComponentId;
  sourceProfileId?: OverlayProfileId;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  enabled?: boolean;
  locked?: boolean;
  layer?: number;
  variant?: OverlayVariant;
  showTitle?: boolean;
  showDetails?: boolean;
  motion?: boolean;
  liquidColor?: string;
  collectingColor?: string;
  textColor?: string;
  outlineColor?: string;
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
  revision: number;
  phase: "recruiting" | "ready" | "waiting_level" | "active" | "won";
  capacity: number;
  attempt: number;
  trigger: {
    giftId: string;
    giftName: string;
    giftImagePath: string;
  };
  participants: PeskyBattlePreviewParticipant[];
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
];

export function minimumComponentSize(
  componentId: OverlayComponentId,
): OverlayCanvasSize {
  return componentId === "pesky_battle"
    ? { width: 320, height: 180 }
    : { width: 220, height: 220 };
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

export function isOverlayProfileId(value: string | null): value is OverlayProfileId {
  return value === "vertical" || value === "horizontal";
}

export function isOverlayComponentId(value: string | null): value is OverlayComponentId {
  return value === "tap_farming" || value === "pesky_battle";
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
