import type {
  OverlayComposerProfile,
  OverlayComponentId,
  OverlayPreviewCommand,
  OverlayProfileId,
  PeskyBattlePreviewSnapshot,
  TapFarmingPreviewSnapshot,
} from "./model";

export type TapSimulationAction =
  | { type: "scenario"; scenario: TapFarmingPreviewSnapshot["phase"] }
  | {
      type: "conversion";
      tapsPerConversion: number;
      healthPointsPerConversion: number;
    }
  | { type: "add_taps"; amount: number }
  | { type: "damage"; amount: number }
  | { type: "next_phase" }
  | { type: "retry" }
  | { type: "reset" };

export type BattleSimulationAction =
  | { type: "scenario"; scenario: PeskyBattlePreviewSnapshot["phase"] }
  | { type: "gift"; trigger: PeskyBattlePreviewSnapshot["trigger"] }
  | { type: "capacity"; capacity: number }
  | { type: "participants"; count: number }
  | { type: "attempt"; attempt: number }
  | { type: "attack"; name: string; imagePath: string; startedAt: number; slot?: number }
  | { type: "challenge"; name: string; imagePath: string; startedAt: number; slot: number }
  | { type: "challenge_tick"; now: number }
  | { type: "clear_challenge" }
  | { type: "reset" };

const BATTLE_NAMES = [
  "La Pichi",
  "Don Taza",
  "Srita. Cáliz",
  "Mugman MX",
  "CupFan",
];
const BATTLE_AVATARS = ["cuphead", "cuphead-coins", "knight", "cup-trio", "mugman"];

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value));
}

function tapPhases(
  phaseCount: number,
  phaseIndex: number,
  progress: number,
  completed = false,
) {
  return Array.from({ length: phaseCount }, (_, offset) => {
    const index = offset + 1;
    const status = completed || index < phaseIndex
      ? "complete"
      : index === phaseIndex
        ? "active"
        : "pending";
    return {
      index,
      status: status as "pending" | "active" | "complete",
      progress: status === "complete" ? 1 : status === "active" ? progress : 0,
    };
  });
}

function withTapProgress(state: TapFarmingPreviewSnapshot) {
  const hasBoss = state.phase !== "collecting" && state.boss.totalHealth > 0;
  const total = hasBoss ? Math.max(1, state.boss.totalHealth) : 0;
  const current = total > 0 ? clamp(state.boss.currentHealth, 0, total) : 0;
  const progress = state.phase === "completed"
    ? 1
    : total > 0
      ? clamp(1 - current / total, 0, 1)
      : 0;
  const phaseCount = Math.max(1, state.phaseCount);
  const phaseIndex = clamp(Math.round(state.phaseIndex), 1, phaseCount);
  const completed = state.phase === "completed";
  const effectiveAvailable = completed || hasBoss;
  const effectiveTotal = effectiveAvailable
    ? total + state.counters.reserveHealth + state.counters.spentHealth
    : 0;
  const effectiveCurrent = completed
    ? 0
    : effectiveAvailable
      ? Math.min(effectiveTotal, current + state.counters.reserveHealth)
      : state.counters.reserveHealth;
  return {
    ...state,
    revision: state.revision + 1,
    boss: { ...state.boss, currentHealth: current, totalHealth: total, progress },
    effectiveHealth: {
      available: effectiveAvailable,
      current: effectiveCurrent,
      total: effectiveTotal,
      ratio: effectiveAvailable && effectiveTotal > 0
        ? clamp(effectiveCurrent / effectiveTotal, 0, 1)
        : 0,
    },
    phaseIndex,
    phaseCount,
    overallProgress: completed
      ? 1
      : clamp(((phaseIndex - 1) + progress) / phaseCount, 0, 1),
    phases: tapPhases(phaseCount, phaseIndex, progress, completed),
  };
}

export function createTapSimulation(
  scenario: TapFarmingPreviewSnapshot["phase"] = "active",
  conversion: TapFarmingPreviewSnapshot["conversion"] = {
    tapsPerConversion: 2,
    healthPointsPerConversion: 1,
  },
): TapFarmingPreviewSnapshot {
  const base: TapFarmingPreviewSnapshot = {
    revision: 1,
    phase: scenario,
    bossName: "Rey Dado",
    levelId: "DicePalaceMain",
    attempt: 2,
    conversion: {
      tapsPerConversion: Math.max(1, Math.floor(conversion.tapsPerConversion)),
      healthPointsPerConversion: Math.max(
        1,
        Math.floor(conversion.healthPointsPerConversion),
      ),
    },
    counters: {
      totalTaps: 12486,
      bankedTaps: 3684,
      unconvertedTaps: 0,
      convertedHealth: 6243,
      reserveHealth: 1842,
      spentHealth: 4401,
    },
    boss: { currentHealth: 1900, totalHealth: 3000, progress: 0 },
    effectiveHealth: { available: true, current: 0, total: 0, ratio: 0 },
    phaseIndex: 2,
    phaseCount: 3,
    overallProgress: 0,
    phases: [],
    tapDelta: 0,
    damageDelta: 0,
  };
  if (scenario === "collecting") {
    base.bossName = "";
    base.attempt = 1;
    base.phaseIndex = 1;
    base.boss.currentHealth = 0;
    base.boss.totalHealth = 0;
  } else if (scenario === "transition") {
    base.boss.currentHealth = 0;
  } else if (scenario === "completed") {
    base.phaseIndex = base.phaseCount;
    base.boss.currentHealth = 0;
    base.counters.reserveHealth = 0;
    base.counters.bankedTaps = 0;
  }
  return withTapProgress({ ...base, revision: 0 });
}

export function tapSimulationReducer(
  state: TapFarmingPreviewSnapshot,
  action: TapSimulationAction,
): TapFarmingPreviewSnapshot {
  if (action.type === "reset") return createTapSimulation("active", state.conversion);
  if (action.type === "scenario") {
    return createTapSimulation(action.scenario, state.conversion);
  }
  if (action.type === "conversion") {
    return {
      ...state,
      revision: state.revision + 1,
      conversion: {
        tapsPerConversion: Math.max(1, Math.floor(action.tapsPerConversion)),
        healthPointsPerConversion: Math.max(
          1,
          Math.floor(action.healthPointsPerConversion),
        ),
      },
    };
  }
  if (action.type === "add_taps") {
    const amount = Math.max(0, Math.floor(action.amount));
    const tapsPerConversion = Math.max(
      1,
      Math.floor(
        state.conversion.tapsPerConversion ??
          state.conversion.tapsPerHealthPoint ??
          1,
      ),
    );
    const healthPointsPerConversion = Math.max(
      1,
      Math.floor(state.conversion.healthPointsPerConversion ?? 1),
    );
    const combined = state.counters.unconvertedTaps + amount;
    const conversions = Math.floor(combined / tapsPerConversion);
    const healthAdded = conversions * healthPointsPerConversion;
    const unconvertedTaps = combined % tapsPerConversion;
    const reserveHealth = state.counters.reserveHealth + healthAdded;
    return withTapProgress({
      ...state,
      tapDelta: amount,
      damageDelta: 0,
      counters: {
        ...state.counters,
        totalTaps: state.counters.totalTaps + amount,
        unconvertedTaps,
        convertedHealth: state.counters.convertedHealth + healthAdded,
        reserveHealth,
        bankedTaps: Math.floor(
          reserveHealth * tapsPerConversion / healthPointsPerConversion,
        ) + unconvertedTaps,
      },
    });
  }
  if (action.type === "damage") {
    const amount = Math.max(0, action.amount);
    const absorbed = Math.min(state.counters.reserveHealth, amount);
    const nativeDamage = Math.max(0, amount - absorbed);
    const currentHealth = Math.max(0, state.boss.currentHealth - nativeDamage);
    const reserveHealth = Math.max(0, state.counters.reserveHealth - absorbed);
    const phase = currentHealth <= 0
      ? state.phaseIndex >= state.phaseCount ? "completed" : "transition"
      : "active";
    return withTapProgress({
      ...state,
      phase,
      tapDelta: 0,
      damageDelta: amount,
      counters: {
        ...state.counters,
        reserveHealth,
        spentHealth: state.counters.spentHealth + absorbed,
        bankedTaps: Math.floor(
          reserveHealth * state.conversion.tapsPerConversion /
            state.conversion.healthPointsPerConversion,
        ) + state.counters.unconvertedTaps,
      },
      boss: { ...state.boss, currentHealth },
    });
  }
  if (action.type === "next_phase") {
    if (state.phaseIndex >= state.phaseCount) {
      return withTapProgress({
        ...state,
        phase: "completed",
        tapDelta: 0,
        damageDelta: 0,
        boss: { ...state.boss, currentHealth: 0 },
      });
    }
    return withTapProgress({
      ...state,
      phase: "active",
      phaseIndex: state.phaseIndex + 1,
      tapDelta: 0,
      damageDelta: 0,
      boss: { ...state.boss, currentHealth: state.boss.totalHealth },
    });
  }
  return withTapProgress({
    ...state,
    phase: "active",
    attempt: state.attempt + 1,
    phaseIndex: 1,
    tapDelta: 0,
    damageDelta: 0,
    counters: { ...state.counters, spentHealth: 0 },
    boss: { ...state.boss, currentHealth: state.boss.totalHealth },
  });
}

export function createBattleSimulation(
  scenario: PeskyBattlePreviewSnapshot["phase"] = "recruiting",
  participantCount?: number,
  requestedCapacity = 5,
  trigger: PeskyBattlePreviewSnapshot["trigger"] = {
    giftId: "preview-gift", giftName: "Rosa", giftImagePath: "/assets/creator-tools/gifts/images/5655.webp",
  },
): PeskyBattlePreviewSnapshot {
  const capacity = Number.isInteger(requestedCapacity) && requestedCapacity >= 2 && requestedCapacity <= 5
    ? requestedCapacity : 5;
  const defaultCount = scenario === "recruiting" ? Math.min(3, capacity - 1) : scenario === "off" ? 0 : capacity;
  const count = scenario === "off" ? 0 : clamp(Math.floor(participantCount ?? defaultCount), 0, capacity);
  return {
    revision: 1,
    phase: scenario,
    capacity,
    eventEpoch: 1,
    attempt: scenario === "active" || scenario === "won" ? 2 : 1,
    trigger: { ...trigger },
    participants: BATTLE_NAMES.slice(0, count).map((displayName, index) => ({
      slot: index + 1,
      userId: `preview-${index + 1}`,
      userName: `preview${index + 1}`,
      displayName,
      avatarUrl: `/assets/creator-tools/simulator/avatars/${BATTLE_AVATARS[index]}.jpg`,
    })),
  };
}

export function battleSimulationReducer(
  state: PeskyBattlePreviewSnapshot,
  action: BattleSimulationAction,
): PeskyBattlePreviewSnapshot {
  if (action.type === "gift") {
    if (state.trigger.giftId === action.trigger.giftId &&
        state.trigger.giftName === action.trigger.giftName &&
        state.trigger.giftImagePath === action.trigger.giftImagePath) return state;
    return { ...state, trigger: { ...action.trigger }, revision: state.revision + 1 };
  }
  if (action.type === "attack") {
    if (state.phase !== "active" || state.participants.length === 0) return state;
    const sequence = (state.attack?.sequence ?? 0) + 1;
    const participant = action.slot === undefined
      ? state.participants[(sequence - 1) % state.participants.length]
      : state.participants.find(player => player.slot === action.slot);
    if (!participant) return state;
    const attack = {
      id: `${state.eventEpoch}-${action.startedAt}-${state.revision + 1}`,
      slot: participant.slot, name: action.name, imagePath: action.imagePath,
      startedAt: action.startedAt, sequence,
    };
    return {
      ...state,
      revision: state.revision + 1,
      attack,
      attacks: [...(state.attacks ?? []).filter(event => action.startedAt - event.startedAt <= 12000), attack].slice(-32),
    };
  }
  if (action.type === "challenge") {
    if (state.phase !== "active" || !state.participants.some(player => player.slot === action.slot)) return state;
    return { ...state, revision: state.revision + 1, challenges: [{ ...action,
      id: `challenge-${state.eventEpoch}-${state.revision + 1}`,
      phase: "countdown", secondsRemaining: 3,
    }] };
  }
  if (action.type === "clear_challenge") return { ...state, revision: state.revision + 1, challenges: [] };
  if (action.type === "challenge_tick") {
    if (!state.challenges?.length) return state;
    const challenges = state.challenges
      .filter(event => action.now - event.startedAt < 18000)
      .map(event => {
        const elapsed = Math.max(0, (action.now - event.startedAt) / 1000);
        return { ...event, phase: elapsed < 3 ? "countdown" as const : "active" as const,
          secondsRemaining: Math.ceil(elapsed < 3 ? 3 - elapsed : 18 - elapsed) };
      });
    if (challenges.length === state.challenges.length && challenges.every((event, index) =>
      event.phase === state.challenges![index].phase && event.secondsRemaining === state.challenges![index].secondsRemaining)) return state;
    return { ...state, revision: state.revision + 1, challenges };
  }
  if (action.type === "reset") return { ...createBattleSimulation("recruiting", 0, state.capacity, state.trigger), eventEpoch: state.eventEpoch + 1 };
  if (action.type === "scenario") {
    return { ...createBattleSimulation(action.scenario,
      action.scenario === "recruiting" ? Math.min(state.participants.length, state.capacity - 1) : undefined,
      state.capacity, state.trigger), eventEpoch: state.eventEpoch + 1 };
  }
  if (action.type === "capacity") {
    if (!Number.isInteger(action.capacity) || action.capacity < 2 || action.capacity > 5) return state;
    const count = state.phase === "recruiting"
      ? Math.min(state.participants.length, action.capacity)
      : state.phase === "off" ? 0 : action.capacity;
    const phase = state.phase === "recruiting" && count === action.capacity ? "ready" : state.phase;
    return {
      ...createBattleSimulation(phase, count, action.capacity, state.trigger),
      revision: state.revision + 1,
      attempt: state.attempt,
      eventEpoch: state.eventEpoch + 1,
    };
  }
  if (action.type === "participants") {
    const count = clamp(Math.floor(action.count), 0, state.capacity);
    const phase = state.phase === "recruiting" && count >= state.capacity
      ? "ready"
      : state.phase === "ready" && count < state.capacity
        ? "recruiting"
        : state.phase;
    return {
      ...createBattleSimulation(phase, count, state.capacity, state.trigger),
      revision: state.revision + 1,
      attempt: state.attempt,
      eventEpoch: state.eventEpoch + 1,
    };
  }
  return { ...state, revision: state.revision + 1, attempt: Math.max(1, action.attempt),
    eventEpoch: state.eventEpoch + 1, attack: undefined, attacks: [], challenges: [] };
}

export function previewCommand(
  operation: OverlayPreviewCommand["operation"],
  profileId: OverlayProfileId,
  componentId: OverlayComponentId,
  sessionId: string,
  tap: TapFarmingPreviewSnapshot,
  battle: PeskyBattlePreviewSnapshot,
  profile: OverlayComposerProfile,
  simulationActive: boolean,
): OverlayPreviewCommand {
  return {
    schemaVersion: 1,
    operation,
    profileId,
    componentId,
    sessionId,
    simulationActive,
    layoutJson: JSON.stringify(profile),
    scenario: componentId === "tap_farming" ? tap.phase : battle.phase,
    bossName: tap.bossName,
    levelId: tap.levelId,
    totalTaps: tap.counters.totalTaps,
    tapDelta: tap.tapDelta,
    damageDelta: tap.damageDelta,
    reserveHealth: tap.counters.reserveHealth,
    spentHealth: tap.counters.spentHealth,
    currentHealth: tap.boss.currentHealth,
    totalHealth: tap.boss.totalHealth,
    overallProgress: tap.overallProgress,
    phaseIndex: tap.phaseIndex,
    phaseCount: tap.phaseCount,
    attempt: componentId === "tap_farming" ? tap.attempt : battle.attempt,
    participantCount: battle.participants.length,
    capacity: battle.capacity,
    attackId: battle.attack?.id ?? "",
    attackSlot: battle.attack?.slot ?? 0,
    attackName: battle.attack?.name ?? "",
    attackImagePath: battle.attack?.imagePath ?? "",
    attackStartedAt: battle.attack?.startedAt ?? 0,
    eventEpoch: battle.eventEpoch,
    battleSignalsJson: JSON.stringify({ attacks: battle.attacks ?? [], challenges: battle.challenges ?? [] }),
  };
}
