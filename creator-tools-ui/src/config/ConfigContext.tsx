import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import type {
  ConnectionStatus,
  ForceDraft,
  InteractionConfigState,
  InteractionQueueEntry,
  PeskyBattleConfigState,
  PeskyModeConfigState,
  RouletteConfigState,
  RouletteSelection,
  StreamRuleDraft,
  StreamRulesConfigState,
} from "../model";

interface ConfigValue {
  config: RouletteConfigState | null;
  draft: ForceDraft | null;
  interaction: InteractionConfigState | null;
  pesky: PeskyModeConfigState | null;
  peskyBattle: PeskyBattleConfigState | null;
  streamRules: StreamRulesConfigState | null;
  optimisticInteractionQueue: InteractionQueueEntry[];
  interactionTesting: boolean;
  interactionSettingsStatus: InteractionSettingsStatus;
  status: ConnectionStatus;
  applyDraft: (draft: ForceDraft) => void;
  applyChallenge: (id: number, enabled: boolean) => void;
  applyInteractionSettings: (maxActive: number, showGiftImage: boolean) => void;
  applyInteractionsEnabled: (enabled: boolean) => void;
  applyInteractionQueuePaused: (paused: boolean) => void;
  clearPendingInteractions: () => void;
  applyInteractionPhaseTransitionProtection: (enabled: boolean) => void;
  applyPeskyEnabled: (enabled: boolean) => void;
  applyPeskyNames: (names: string) => void;
  applyPeskyItem: (item: string, enabled: boolean) => void;
  applyPeskyBattleGift: (giftId: string) => void;
  applyPeskyBattleStreamAttacks: (enabled: boolean) => void;
  applyPeskyBattleItem: (item: string, enabled: boolean) => void;
  armPeskyBattle: (giftId: string) => void;
  startPeskyBattle: () => void;
  cancelPeskyBattle: () => void;
  disablePeskyBattle: () => void;
  resetPeskyBattle: () => void;
  saveStreamRule: (draft: StreamRuleDraft) => boolean;
  deleteStreamRule: (id: number) => boolean;
  duplicateStreamRule: (id: number) => void;
  toggleStreamRule: (id: number, enabled: boolean) => void;
  testInteraction: (item: string, donor: string, quantity: number, delay: number) => void;
}

interface PendingPeskyChange {
  targetRevision: number;
  apply: (state: PeskyModeConfigState) => PeskyModeConfigState;
}

interface PendingPeskyBattleChange {
  targetRevision: number;
  apply: (state: PeskyBattleConfigState) => PeskyBattleConfigState;
}

interface DesiredPhaseTransitionProtection {
  enabled: boolean;
  baselineRevision: number;
  requestRevision: number;
}

type InteractionSettingsStatus =
  | "idle"
  | "saving"
  | "pending"
  | "reconnecting"
  | "saved"
  | "error";

interface DesiredInteractionSettings {
  maxActive: number;
  showGiftImage: boolean;
  baselineRevision: number;
  requestRevision: number;
  accepted: boolean;
}

interface DesiredInteractionMaster {
  enabled: boolean;
  baselineRevision: number;
}

interface DesiredInteractionQueuePause {
  paused: boolean;
  baselineRevision: number;
}

interface DesiredInteractionQueueClear {
  baselineRevision: number;
}

const ConfigContext = createContext<ConfigValue | null>(null);

function toDraft(state: RouletteConfigState): ForceDraft {
  return { enabled: state.enabled, ...state.selection };
}

function selectionMatches(state: RouletteConfigState, desired: ForceDraft) {
  const keys: Array<keyof RouletteSelection> = [
    "boss",
    "weapon1",
    "weapon2",
    "super",
    "charm",
    "modifier",
  ];
  return state.enabled === desired.enabled && keys.every((key) => state.selection[key] === desired[key]);
}

function queryFor(draft: ForceDraft) {
  return new URLSearchParams({
    enabled: draft.enabled ? "1" : "0",
    boss: String(draft.boss),
    weapon1: String(draft.weapon1),
    weapon2: String(draft.weapon2),
    super: String(draft.super),
    charm: String(draft.charm),
    modifier: String(draft.modifier),
  });
}

export function ConfigProvider({ children }: { children: ReactNode }) {
  const [config, setConfig] = useState<RouletteConfigState | null>(null);
  const [draft, setDraft] = useState<ForceDraft | null>(null);
  const [interaction, setInteraction] = useState<InteractionConfigState | null>(null);
  const [pesky, setPesky] = useState<PeskyModeConfigState | null>(null);
  const [peskyBattle, setPeskyBattle] = useState<PeskyBattleConfigState | null>(null);
  const [streamRules, setStreamRules] = useState<StreamRulesConfigState | null>(null);
  const [optimisticInteractionQueue, setOptimisticInteractionQueue] = useState<
    InteractionQueueEntry[]
  >([]);
  const [interactionTesting, setInteractionTesting] = useState(false);
  const [interactionSettingsStatus, setInteractionSettingsStatus] = useState<
    InteractionSettingsStatus
  >("idle");
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const desiredForceRef = useRef<ForceDraft | null>(null);
  const desiredChallengesRef = useRef(new Map<number, boolean>());
  const loadRequestRevisionRef = useRef(0);
  const lastAppliedLoadRevisionRef = useRef(0);
  const interactionRevisionRef = useRef<number | null>(null);
  const nextOptimisticInteractionIdRef = useRef(-1);
  const desiredPhaseTransitionProtectionRef = useRef<
    DesiredPhaseTransitionProtection | null
  >(null);
  const phaseTransitionProtectionRequestRevisionRef = useRef(0);
  const phaseTransitionProtectionWriteChainRef = useRef<Promise<void>>(
    Promise.resolve(),
  );
  const desiredInteractionSettingsRef = useRef<
    DesiredInteractionSettings | null
  >(null);
  const interactionSettingsRequestRevisionRef = useRef(0);
  const interactionSettingsWriteChainRef = useRef<Promise<void>>(
    Promise.resolve(),
  );
  const interactionControlWriteChainRef = useRef<Promise<void>>(
    Promise.resolve(),
  );
  const desiredInteractionMasterRef = useRef<DesiredInteractionMaster | null>(null);
  const desiredInteractionQueuePauseRef = useRef<DesiredInteractionQueuePause | null>(null);
  const desiredInteractionQueueClearRef = useRef<DesiredInteractionQueueClear | null>(null);
  const interactionSettingsStatusTimerRef = useRef<number | null>(null);
  const confirmedPeskyRevisionRef = useRef(0);
  const pendingPeskyChangesRef = useRef<PendingPeskyChange[]>([]);
  const peskyWriteChainRef = useRef<Promise<void>>(Promise.resolve());
  const confirmedPeskyBattleRevisionRef = useRef(0);
  const pendingPeskyBattleChangesRef = useRef<PendingPeskyBattleChange[]>([]);
  const peskyBattleWriteChainRef = useRef<Promise<void>>(Promise.resolve());
  const streamRulesRevisionRef = useRef<number | null>(null);
  const streamRulesWriteChainRef = useRef<Promise<void>>(Promise.resolve());
  const mountedRef = useRef(true);

  const load = useCallback(async () => {
    const loadRevision = loadRequestRevisionRef.current + 1;
    loadRequestRevisionRef.current = loadRevision;
    try {
      const [
        configResponse,
        interactionResponse,
        peskyResponse,
        peskyBattleResponse,
        streamRulesResponse,
      ] = await Promise.all([
        fetch("/api/config", { cache: "no-store" }),
        fetch("/api/config/interactions", { cache: "no-store" }),
        fetch("/api/config/pesky", { cache: "no-store" }),
        fetch("/api/config/pesky-battle", { cache: "no-store" }),
        fetch("/api/config/interactions/rules", { cache: "no-store" }),
      ]);
      if (!configResponse.ok) throw new Error(`HTTP ${configResponse.status}`);
      if (!interactionResponse.ok) throw new Error(`HTTP ${interactionResponse.status}`);
      if (!peskyResponse.ok) throw new Error(`HTTP ${peskyResponse.status}`);
      if (!peskyBattleResponse.ok) throw new Error(`HTTP ${peskyBattleResponse.status}`);
      if (!streamRulesResponse.ok) throw new Error(`HTTP ${streamRulesResponse.status}`);
      const next = (await configResponse.json()) as RouletteConfigState;
      const nextInteraction = (await interactionResponse.json()) as InteractionConfigState;
      const nextPesky = (await peskyResponse.json()) as PeskyModeConfigState;
      const nextPeskyBattle = (await peskyBattleResponse.json()) as PeskyBattleConfigState;
      const nextStreamRules = (await streamRulesResponse.json()) as StreamRulesConfigState;
      if (
        !mountedRef.current ||
        loadRevision < lastAppliedLoadRevisionRef.current
      ) return;
      lastAppliedLoadRevisionRef.current = loadRevision;

      let interactionPending = false;
      const interactionRevision = interactionRevisionRef.current;
      if (interactionRevision !== null) {
        if (nextInteraction.revision === interactionRevision) {
          interactionPending = true;
        } else {
          interactionRevisionRef.current = null;
          setOptimisticInteractionQueue([]);
          setInteractionTesting(false);
        }
      }
      let visibleInteraction = {
        ...nextInteraction,
        interactionsEnabled: nextInteraction.interactionsEnabled === true,
        masterRevision: nextInteraction.masterRevision ?? 0,
        queuePaused: nextInteraction.queuePaused === true,
        queueControlRevision: nextInteraction.queueControlRevision ?? 0,
        pendingCount: nextInteraction.pendingCount ?? Math.max(
          0,
          (nextInteraction.queueCount ?? 0) -
            (nextInteraction.activeCount ?? 0),
        ),
        backlogCount: nextInteraction.backlogCount ?? 0,
        showGiftImage: nextInteraction.showGiftImage !== false,
        settingsRevision: nextInteraction.settingsRevision ?? 0,
      };
      let interactionMasterPending = false;
      const desiredInteractionMaster = desiredInteractionMasterRef.current;
      if (desiredInteractionMaster !== null) {
        const confirmed =
          visibleInteraction.masterRevision > desiredInteractionMaster.baselineRevision &&
          visibleInteraction.interactionsEnabled === desiredInteractionMaster.enabled;
        if (confirmed) {
          desiredInteractionMasterRef.current = null;
        } else {
          interactionMasterPending = true;
          visibleInteraction = {
            ...visibleInteraction,
            interactionsEnabled: desiredInteractionMaster.enabled,
            available: desiredInteractionMaster.enabled
              ? visibleInteraction.available
              : false,
            queuePaused: desiredInteractionMaster.enabled
              ? visibleInteraction.queuePaused
              : false,
            queue: desiredInteractionMaster.enabled
              ? visibleInteraction.queue
              : visibleInteraction.queue.filter((entry) => entry.status === "active"),
            queueCount: desiredInteractionMaster.enabled
              ? visibleInteraction.queueCount
              : visibleInteraction.activeCount,
            pendingCount: desiredInteractionMaster.enabled
              ? visibleInteraction.pendingCount
              : 0,
            backlogCount: desiredInteractionMaster.enabled
              ? visibleInteraction.backlogCount
              : 0,
          };
        }
      }
      let interactionQueuePausePending = false;
      const desiredInteractionQueuePause = desiredInteractionQueuePauseRef.current;
      if (desiredInteractionQueuePause !== null) {
        const confirmed =
          visibleInteraction.queueControlRevision >
            desiredInteractionQueuePause.baselineRevision &&
          visibleInteraction.queuePaused === desiredInteractionQueuePause.paused;
        if (confirmed) {
          desiredInteractionQueuePauseRef.current = null;
        } else if (visibleInteraction.interactionsEnabled) {
          interactionQueuePausePending = true;
          visibleInteraction = {
            ...visibleInteraction,
            queuePaused: desiredInteractionQueuePause.paused,
          };
        }
      }
      let interactionQueueClearPending = false;
      const desiredInteractionQueueClear = desiredInteractionQueueClearRef.current;
      if (desiredInteractionQueueClear !== null) {
        const confirmed =
          visibleInteraction.queueControlRevision >
            desiredInteractionQueueClear.baselineRevision;
        if (confirmed) {
          desiredInteractionQueueClearRef.current = null;
        } else {
          interactionQueueClearPending = true;
          visibleInteraction = {
            ...visibleInteraction,
            queue: visibleInteraction.queue.filter((entry) => entry.status === "active"),
            queueCount: visibleInteraction.activeCount,
            pendingCount: 0,
            backlogCount: 0,
          };
        }
      }
      let phaseTransitionProtectionPending = false;
      const desiredPhaseTransitionProtection =
        desiredPhaseTransitionProtectionRef.current;
      if (desiredPhaseTransitionProtection !== null) {
        const confirmed =
          nextInteraction.phaseTransitionProtectionRevision >
            desiredPhaseTransitionProtection.baselineRevision &&
          nextInteraction.phaseTransitionProtectionEnabled ===
            desiredPhaseTransitionProtection.enabled;
        if (confirmed) {
          desiredPhaseTransitionProtectionRef.current = null;
        } else {
          phaseTransitionProtectionPending = true;
          visibleInteraction = {
            ...visibleInteraction,
            phaseTransitionProtectionEnabled:
              desiredPhaseTransitionProtection.enabled,
          };
        }
      }
      let interactionSettingsPending = false;
      const desiredInteractionSettings = desiredInteractionSettingsRef.current;
      if (desiredInteractionSettings !== null) {
        const confirmed =
          (nextInteraction.settingsRevision ?? 0) >
            desiredInteractionSettings.baselineRevision &&
          nextInteraction.maxActive === desiredInteractionSettings.maxActive &&
          (nextInteraction.showGiftImage !== false) ===
            desiredInteractionSettings.showGiftImage;
        if (confirmed) {
          const confirmedRequestRevision =
            desiredInteractionSettings.requestRevision;
          desiredInteractionSettingsRef.current = null;
          setInteractionSettingsStatus("saved");
          if (interactionSettingsStatusTimerRef.current !== null) {
            window.clearTimeout(interactionSettingsStatusTimerRef.current);
          }
          interactionSettingsStatusTimerRef.current = window.setTimeout(() => {
            if (
              mountedRef.current &&
              interactionSettingsRequestRevisionRef.current ===
                confirmedRequestRevision &&
              desiredInteractionSettingsRef.current === null
            ) {
              setInteractionSettingsStatus("idle");
            }
            interactionSettingsStatusTimerRef.current = null;
          }, 3200);
        } else {
          interactionSettingsPending = true;
          setInteractionSettingsStatus(
            desiredInteractionSettings.accepted ? "pending" : "saving",
          );
          visibleInteraction = {
            ...visibleInteraction,
            maxActive: desiredInteractionSettings.maxActive,
            showGiftImage: desiredInteractionSettings.showGiftImage,
          };
        }
      }
      setInteraction(visibleInteraction);
      confirmedPeskyRevisionRef.current = nextPesky.revision;
      const pendingPeskyChanges = pendingPeskyChangesRef.current.filter(
        (change) => change.targetRevision > nextPesky.revision,
      );
      pendingPeskyChangesRef.current = pendingPeskyChanges;
      const visiblePesky = pendingPeskyChanges.reduce(
        (state, change) => change.apply(state),
        nextPesky,
      );
      setPesky(visiblePesky);
      const peskyPending = pendingPeskyChanges.length > 0;
      confirmedPeskyBattleRevisionRef.current = nextPeskyBattle.revision;
      const pendingPeskyBattleChanges = pendingPeskyBattleChangesRef.current.filter(
        (change) => change.targetRevision > nextPeskyBattle.revision,
      );
      pendingPeskyBattleChangesRef.current = pendingPeskyBattleChanges;
      const visiblePeskyBattle = pendingPeskyBattleChanges.reduce(
        (state, change) => change.apply(state),
        nextPeskyBattle,
      );
      setPeskyBattle(visiblePeskyBattle);
      const peskyBattlePending = pendingPeskyBattleChanges.length > 0;
      let streamRulesPending = false;
      const streamRulesRevision = streamRulesRevisionRef.current;
      if (streamRulesRevision !== null) {
        if (nextStreamRules.revision === streamRulesRevision) {
          streamRulesPending = true;
        } else {
          streamRulesRevisionRef.current = null;
        }
      }
      setStreamRules(nextStreamRules);
      if (!next.ready || !nextInteraction.ready || !nextPesky.ready ||
          !nextPeskyBattle.ready || !nextStreamRules.ready) {
        setStatus("connecting");
        return;
      }

      const desiredForce = desiredForceRef.current;
      let forcePending = false;
      if (desiredForce) {
        if (selectionMatches(next, desiredForce)) {
          desiredForceRef.current = null;
          setDraft(toDraft(next));
        } else {
          forcePending = true;
        }
      } else {
        setDraft(toDraft(next));
      }

      for (const modifier of next.modifiers) {
        const desiredEnabled = desiredChallengesRef.current.get(modifier.id);
        const rejectedLastDisable = desiredEnabled === false &&
          modifier.enabled &&
          !modifier.canDisable;
        if (desiredEnabled === modifier.enabled || rejectedLastDisable) {
          desiredChallengesRef.current.delete(modifier.id);
        }
      }
      const challengePending = desiredChallengesRef.current.size > 0;
      setConfig(challengePending
        ? {
            ...next,
            modifiers: next.modifiers.map((modifier) => ({
              ...modifier,
              enabled: desiredChallengesRef.current.get(modifier.id) ?? modifier.enabled,
            })),
          }
        : next);
      setStatus(
        forcePending || challengePending || interactionPending ||
          interactionMasterPending || interactionQueuePausePending ||
          interactionQueueClearPending ||
          phaseTransitionProtectionPending ||
          interactionSettingsPending ||
          peskyPending || peskyBattlePending || streamRulesPending
          ? "pending"
          : "saved",
      );
    } catch {
      if (
        mountedRef.current &&
        loadRevision >= lastAppliedLoadRevisionRef.current
      ) {
        lastAppliedLoadRevisionRef.current = loadRevision;
        if (desiredInteractionSettingsRef.current !== null) {
          setInteractionSettingsStatus("reconnecting");
        }
        setStatus("error");
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    void load();
    const timer = window.setInterval(() => void load(), 900);
    return () => {
      mountedRef.current = false;
      window.clearInterval(timer);
      if (interactionSettingsStatusTimerRef.current !== null) {
        window.clearTimeout(interactionSettingsStatusTimerRef.current);
      }
    };
  }, [load]);

  const applyDraft = useCallback(
    (next: ForceDraft) => {
      desiredForceRef.current = next;
      setDraft(next);
      setStatus("saving");

      void fetch(`/api/config/set?${queryFor(next)}`, { cache: "no-store" })
        .then((response) => {
          if (!response.ok) throw new Error(`HTTP ${response.status}`);
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          if (mountedRef.current) setStatus("error");
        });
    },
    [load],
  );

  const applyChallenge = useCallback(
    (id: number, enabled: boolean) => {
      desiredChallengesRef.current.set(id, enabled);
      setConfig((current) => current
        ? {
            ...current,
            modifiers: current.modifiers.map((modifier) =>
              modifier.id === id ? { ...modifier, enabled } : modifier),
          }
        : current);
      setStatus("saving");

      const query = new URLSearchParams({
        challenge: String(id),
        challengeEnabled: enabled ? "1" : "0",
      });
      void fetch(`/api/config/set?${query}`, { cache: "no-store" })
        .then((response) => {
          if (!response.ok) throw new Error(`HTTP ${response.status}`);
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          if (mountedRef.current) setStatus("error");
        });
    },
    [load],
  );

  const applyInteractionSettings = useCallback(
    (value: number, showGiftImage: boolean) => {
      if (!interaction?.ready) return;
      const normalized = Math.max(
        1,
        Math.min(interaction.maxActiveLimit ?? 20, Math.floor(value) || 1),
      );
      const requestRevision = interactionSettingsRequestRevisionRef.current + 1;
      interactionSettingsRequestRevisionRef.current = requestRevision;
      desiredInteractionSettingsRef.current = {
        maxActive: normalized,
        showGiftImage,
        baselineRevision: interaction.settingsRevision ?? 0,
        requestRevision,
        accepted: false,
      };
      if (interactionSettingsStatusTimerRef.current !== null) {
        window.clearTimeout(interactionSettingsStatusTimerRef.current);
        interactionSettingsStatusTimerRef.current = null;
      }
      setInteraction((current) => current
        ? {
            ...current,
            maxActive: normalized,
            showGiftImage,
            feedback: "settings_saved",
            error: false,
          }
        : current);
      setInteractionSettingsStatus("saving");
      setStatus("saving");

      const query = new URLSearchParams({
        maxActive: String(normalized),
        showGiftImage: showGiftImage ? "1" : "0",
      });
      const send = () => fetch(
        "/api/config/interactions/set?" + query,
        { cache: "no-store" },
      )
        .then((response) => {
          if (!response.ok) throw new Error("HTTP " + response.status);
          if (
            interactionSettingsRequestRevisionRef.current !== requestRevision ||
            desiredInteractionSettingsRef.current?.requestRevision !== requestRevision
          ) return;
          desiredInteractionSettingsRef.current.accepted = true;
          if (mountedRef.current) {
            setInteractionSettingsStatus("pending");
            setStatus("pending");
          }
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          if (
            interactionSettingsRequestRevisionRef.current !== requestRevision ||
            desiredInteractionSettingsRef.current?.requestRevision !== requestRevision
          ) return;
          desiredInteractionSettingsRef.current = null;
          if (interactionSettingsStatusTimerRef.current !== null) {
            window.clearTimeout(interactionSettingsStatusTimerRef.current);
            interactionSettingsStatusTimerRef.current = null;
          }
          if (mountedRef.current) {
            setInteractionSettingsStatus("error");
            setStatus("error");
            void load();
          }
        });
      interactionSettingsWriteChainRef.current =
        interactionSettingsWriteChainRef.current.then(send, send);
    },
    [interaction, load],
  );

  const sendInteractionControl = useCallback(
    (query: URLSearchParams, onError: () => void) => {
      setStatus("saving");
      const send = () => fetch(
        "/api/config/interactions/set?" + query,
        { cache: "no-store" },
      )
        .then((response) => {
          if (!response.ok) throw new Error("HTTP " + response.status);
          if (mountedRef.current) setStatus("pending");
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          onError();
          if (mountedRef.current) {
            setStatus("error");
            void load();
          }
        });
      interactionControlWriteChainRef.current =
        interactionControlWriteChainRef.current.then(send, send);
    },
    [load],
  );

  const applyInteractionsEnabled = useCallback(
    (enabled: boolean) => {
      if (!interaction?.ready) return;
      const desired: DesiredInteractionMaster = {
        enabled,
        baselineRevision: interaction.masterRevision ?? 0,
      };
      desiredInteractionMasterRef.current = desired;
      if (!enabled) {
        desiredInteractionQueuePauseRef.current = null;
        desiredInteractionQueueClearRef.current = null;
      }
      setInteraction((current) => current
        ? {
            ...current,
            interactionsEnabled: enabled,
            queuePaused: enabled ? current.queuePaused : false,
            queue: enabled
              ? current.queue
              : current.queue.filter((entry) => entry.status === "active"),
            queueCount: enabled ? current.queueCount : current.activeCount,
            pendingCount: enabled ? current.pendingCount : 0,
            backlogCount: enabled ? current.backlogCount : 0,
            feedback: enabled ? "interactions_enabled" : "interactions_disabled",
            error: false,
          }
        : current);
      if (!enabled) setOptimisticInteractionQueue([]);
      sendInteractionControl(
        new URLSearchParams({ interactionsEnabled: enabled ? "1" : "0" }),
        () => {
          if (desiredInteractionMasterRef.current === desired) {
            desiredInteractionMasterRef.current = null;
          }
        },
      );
    },
    [interaction, sendInteractionControl],
  );

  const applyInteractionQueuePaused = useCallback(
    (paused: boolean) => {
      if (!interaction?.ready || !interaction.interactionsEnabled) return;
      const desired: DesiredInteractionQueuePause = {
        paused,
        baselineRevision: interaction.queueControlRevision ?? 0,
      };
      desiredInteractionQueuePauseRef.current = desired;
      setInteraction((current) => current
        ? {
            ...current,
            queuePaused: paused,
            feedback: paused ? "queue_paused" : "queue_resumed",
            error: false,
          }
        : current);
      sendInteractionControl(
        new URLSearchParams({ queuePaused: paused ? "1" : "0" }),
        () => {
          if (desiredInteractionQueuePauseRef.current === desired) {
            desiredInteractionQueuePauseRef.current = null;
          }
        },
      );
    },
    [interaction, sendInteractionControl],
  );

  const clearPendingInteractions = useCallback(() => {
    if (!interaction?.ready) return;
    const desired: DesiredInteractionQueueClear = {
      baselineRevision: interaction.queueControlRevision ?? 0,
    };
    desiredInteractionQueueClearRef.current = desired;
    setOptimisticInteractionQueue([]);
    setInteraction((current) => current
      ? {
          ...current,
          queue: current.queue.filter((entry) => entry.status === "active"),
          queueCount: current.activeCount,
          pendingCount: 0,
          backlogCount: 0,
          feedback: "pending_cleared",
          error: false,
        }
      : current);
    sendInteractionControl(
      new URLSearchParams({ clearPending: "1" }),
      () => {
        if (desiredInteractionQueueClearRef.current === desired) {
          desiredInteractionQueueClearRef.current = null;
        }
      },
    );
  }, [interaction, sendInteractionControl]);

  const sendPeskyUpdate = useCallback(
    (
      query: URLSearchParams,
      apply: (state: PeskyModeConfigState) => PeskyModeConfigState,
    ) => {
      const pending = pendingPeskyChangesRef.current;
      const previousTarget = pending.length > 0
        ? pending[pending.length - 1].targetRevision
        : confirmedPeskyRevisionRef.current;
      pending.push({
        targetRevision: Math.max(
          confirmedPeskyRevisionRef.current,
          previousTarget,
        ) + 1,
        apply,
      });
      setPesky((current) => current ? apply(current) : current);
      setStatus("saving");
      const send = () => fetch(
        "/api/config/pesky/set?" + query,
        { cache: "no-store" },
      )
        .then((response) => {
          if (!response.ok) throw new Error("HTTP " + response.status);
          if (mountedRef.current) setStatus("pending");
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          pendingPeskyChangesRef.current = [];
          if (mountedRef.current) {
            setStatus("error");
            void load();
          }
        });
      peskyWriteChainRef.current = peskyWriteChainRef.current.then(send, send);
    },
    [load],
  );

  const sendPeskyBattleUpdate = useCallback(
    (
      query: URLSearchParams,
      apply: (state: PeskyBattleConfigState) => PeskyBattleConfigState,
    ) => {
      const pending = pendingPeskyBattleChangesRef.current;
      const previousTarget = pending.length > 0
        ? pending[pending.length - 1].targetRevision
        : confirmedPeskyBattleRevisionRef.current;
      pending.push({
        targetRevision: Math.max(
          confirmedPeskyBattleRevisionRef.current,
          previousTarget,
        ) + 1,
        apply,
      });
      setPeskyBattle((current) => current ? apply(current) : current);
      setStatus("saving");
      const send = () => fetch(
        "/api/config/pesky-battle/set?" + query,
        { cache: "no-store" },
      )
        .then((response) => {
          if (!response.ok) throw new Error("HTTP " + response.status);
          if (mountedRef.current) setStatus("pending");
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          pendingPeskyBattleChangesRef.current = [];
          if (mountedRef.current) {
            setStatus("error");
            void load();
          }
        });
      peskyBattleWriteChainRef.current =
        peskyBattleWriteChainRef.current.then(send, send);
    },
    [load],
  );

  const applyInteractionPhaseTransitionProtection = useCallback(
    (enabled: boolean) => {
      if (!interaction?.ready) return;
      const requestRevision =
        phaseTransitionProtectionRequestRevisionRef.current + 1;
      phaseTransitionProtectionRequestRevisionRef.current = requestRevision;
      desiredPhaseTransitionProtectionRef.current = {
        enabled,
        baselineRevision:
          interaction.phaseTransitionProtectionRevision,
        requestRevision,
      };
      setInteraction((current) => current
        ? {
            ...current,
            phaseTransitionProtectionEnabled: enabled,
            feedback: enabled
              ? "phase_transition_protection_enabled"
              : "phase_transition_protection_disabled",
            error: false,
          }
        : current);
      setStatus("saving");

      const query = new URLSearchParams({
        phaseTransitionProtectionEnabled: enabled ? "1" : "0",
      });
      const send = () => fetch(
        "/api/config/interactions/set?" + query,
        { cache: "no-store" },
      )
        .then((response) => {
          if (!response.ok) throw new Error("HTTP " + response.status);
          if (mountedRef.current) setStatus("pending");
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          if (
            phaseTransitionProtectionRequestRevisionRef.current !==
              requestRevision ||
            desiredPhaseTransitionProtectionRef.current?.requestRevision !==
              requestRevision
          ) return;
          desiredPhaseTransitionProtectionRef.current = null;
          if (mountedRef.current) {
            setStatus("error");
            void load();
          }
        });
      phaseTransitionProtectionWriteChainRef.current =
        phaseTransitionProtectionWriteChainRef.current.then(send, send);
    },
    [interaction, load],
  );

  const applyPeskyEnabled = useCallback(
    (enabled: boolean) => {
      if (!pesky?.ready) return;
      sendPeskyUpdate(
        new URLSearchParams({ enabled: enabled ? "1" : "0" }),
        (state) => ({
          ...state,
          enabled,
          feedback: enabled ? "enabled" : "disabled",
          error: false,
          running: enabled ? state.running : false,
          startingBattle: enabled ? state.startingBattle : false,
          queue: enabled ? state.queue : [],
          queueCount: enabled ? state.queueCount : 0,
          activeCount: enabled ? state.activeCount : 0,
        }),
      );
    },
    [pesky, sendPeskyUpdate],
  );

  const applyPeskyNames = useCallback(
    (names: string) => {
      const nextNames = names.split(/\r?\n|\r/).filter(Boolean);
      sendPeskyUpdate(
        new URLSearchParams({ names }),
        (state) => ({
          ...state,
          names: nextNames,
          feedback: "names_saved",
          error: false,
        }),
      );
    },
    [sendPeskyUpdate],
  );

  const applyPeskyItem = useCallback(
    (item: string, enabled: boolean) => {
      sendPeskyUpdate(
        new URLSearchParams({
          item,
          itemEnabled: enabled ? "1" : "0",
        }),
        (state) => ({
            ...state,
            feedback: "items_saved",
            error: false,
            disabledItems: enabled
              ? state.disabledItems.filter((candidate) => candidate !== item)
              : state.disabledItems.includes(item)
                ? state.disabledItems
                : [...state.disabledItems, item],
          }),
      );
    },
    [sendPeskyUpdate],
  );

  const applyPeskyBattleGift = useCallback(
    (giftId: string) => {
      if (!peskyBattle?.ready || peskyBattle.phase !== "off") return;
      sendPeskyBattleUpdate(
        new URLSearchParams({ giftId }),
        (state) => ({
          ...state,
          trigger: {
            ...state.trigger,
            giftId,
            giftName: state.trigger.giftId === giftId
              ? state.trigger.giftName
              : "",
            giftImagePath: state.trigger.giftId === giftId
              ? state.trigger.giftImagePath
              : "",
            coinsPerUnit: state.trigger.giftId === giftId
              ? state.trigger.coinsPerUnit
              : 0,
          },
          feedback: "gift_saved",
          error: false,
        }),
      );
    },
    [peskyBattle, sendPeskyBattleUpdate],
  );

  const applyPeskyBattleStreamAttacks = useCallback(
    (enabled: boolean) => {
      if (!peskyBattle?.ready) return;
      sendPeskyBattleUpdate(
        new URLSearchParams({ allowStreamAttacks: enabled ? "1" : "0" }),
        (state) => ({
          ...state,
          allowStreamAttacks: enabled,
          feedback: enabled
            ? "stream_attacks_allowed"
            : "stream_attacks_blocked",
          error: false,
        }),
      );
    },
    [peskyBattle, sendPeskyBattleUpdate],
  );

  const applyPeskyBattleItem = useCallback(
    (item: string, enabled: boolean) => {
      if (!peskyBattle?.ready || peskyBattle.phase !== "off") return;
      sendPeskyBattleUpdate(
        new URLSearchParams({
          item,
          itemEnabled: enabled ? "1" : "0",
        }),
        (state) => ({
          ...state,
          disabledItems: enabled
            ? state.disabledItems.filter((candidate) => candidate !== item)
            : state.disabledItems.includes(item)
              ? state.disabledItems
              : [...state.disabledItems, item],
          feedback: "items_saved",
          error: false,
        }),
      );
    },
    [peskyBattle, sendPeskyBattleUpdate],
  );

  const armPeskyBattle = useCallback(
    (giftId: string) => {
      if (!peskyBattle?.ready || peskyBattle.phase !== "off") return;
      setPesky((current) => current
        ? {
            ...current,
            enabled: false,
            running: false,
            startingBattle: false,
            blockedByPeskyBattle: true,
            feedback: current.enabled
              ? "disabled_by_pesky_battle"
              : current.feedback,
            queue: [],
            queueCount: 0,
            activeCount: 0,
          }
        : current);
      sendPeskyBattleUpdate(
        new URLSearchParams({ action: "arm", giftId }),
        (state) => ({
          ...state,
          phase: "recruiting",
          exclusive: true,
          participants: [],
          attempt: 0,
          trigger: { ...state.trigger, giftId },
          feedback: "battle_armed",
          error: false,
        }),
      );
    },
    [peskyBattle, sendPeskyBattleUpdate],
  );

  const startPeskyBattle = useCallback(() => {
    if (!peskyBattle?.ready || peskyBattle.phase !== "ready" ||
        peskyBattle.participants.length < peskyBattle.capacity) return;
    sendPeskyBattleUpdate(
      new URLSearchParams({ action: "start" }),
      (state) => ({
        ...state,
        phase: "waiting_level",
        exclusive: true,
        feedback: "waiting_for_level",
        error: false,
      }),
    );
  }, [peskyBattle, sendPeskyBattleUpdate]);

  const finishPeskyBattle = useCallback(
    (action: "cancel" | "off" | "reset") => {
      if (!peskyBattle?.ready) return;
      setPesky((current) => current
        ? { ...current, blockedByPeskyBattle: false }
        : current);
      sendPeskyBattleUpdate(
        new URLSearchParams({ action }),
        (state) => ({
          ...state,
          phase: "off",
          exclusive: false,
          gameplayAvailable: false,
          targetLevel: "",
          participants: [],
          attempt: 0,
          feedback: "battle_cancelled",
          error: false,
        }),
      );
    },
    [peskyBattle, sendPeskyBattleUpdate],
  );

  const cancelPeskyBattle = useCallback(
    () => finishPeskyBattle("cancel"),
    [finishPeskyBattle],
  );
  const disablePeskyBattle = useCallback(
    () => finishPeskyBattle("off"),
    [finishPeskyBattle],
  );
  const resetPeskyBattle = useCallback(
    () => finishPeskyBattle("reset"),
    [finishPeskyBattle],
  );

  const testInteraction = useCallback(
    (item: string, donor: string, quantity: number, delay: number) => {
      if (!interaction?.ready || !interaction.interactionsEnabled) return;
      const normalizedQuantity = Math.max(
        1,
        Math.min(interaction.maxBatch ?? 50, Math.floor(quantity) || 1),
      );
      const normalizedDelay = Math.max(
        0,
        Math.min(interaction.maxDelay ?? 3600, Number(delay) || 0),
      );
      const temporaryIds: number[] = [];
      const optimisticEntries = Array.from({ length: normalizedQuantity }, () => {
        const id = nextOptimisticInteractionIdRef.current;
        nextOptimisticInteractionIdRef.current -= 1;
        temporaryIds.push(id);
        return {
          id,
          item,
          donor: donor.trim(),
          delaySeconds: normalizedDelay,
          status: "waiting_game" as const,
        };
      });
      setOptimisticInteractionQueue((current) => [...current, ...optimisticEntries]);
      interactionRevisionRef.current = interaction.revision;
      setInteractionTesting(true);
      setStatus("saving");

      const query = new URLSearchParams({
        item,
        donor: donor.trim(),
        quantity: String(normalizedQuantity),
        delay: String(normalizedDelay),
      });
      void fetch(`/api/config/interactions/test?${query}`, { cache: "no-store" })
        .then((response) => {
          if (!response.ok) throw new Error(`HTTP ${response.status}`);
          if (mountedRef.current) setStatus("pending");
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          interactionRevisionRef.current = null;
          setOptimisticInteractionQueue((current) => current.filter(
            (entry) => !temporaryIds.includes(entry.id),
          ));
          if (mountedRef.current) {
            setInteractionTesting(false);
            setStatus("error");
          }
        });
    },
    [interaction, load],
  );

  const sendStreamRuleUpdate = useCallback(
    (query: URLSearchParams) => {
      if (!streamRules?.ready) return false;
      streamRulesRevisionRef.current = streamRules.revision;
      setStatus("saving");
      const send = () => fetch(
        "/api/config/interactions/rules/set?" + query,
        { cache: "no-store" },
      )
        .then(async (response) => {
          if (!response.ok) throw new Error("HTTP " + response.status);
          const next = await response.json() as Partial<StreamRulesConfigState>;
          if (next.ready && typeof next.revision === "number" &&
              Array.isArray(next.rules)) {
            streamRulesRevisionRef.current = null;
            if (mountedRef.current) {
              setStreamRules(next as StreamRulesConfigState);
              setStatus(next.error ? "error" : "saved");
            }
            return;
          }
          if (mountedRef.current) setStatus("pending");
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          streamRulesRevisionRef.current = null;
          if (mountedRef.current) {
            setStatus("error");
            void load();
          }
        });
      streamRulesWriteChainRef.current =
        streamRulesWriteChainRef.current.then(send, send);
      return true;
    },
    [load, streamRules],
  );

  const saveStreamRule = useCallback(
    (draft: StreamRuleDraft) => {
      const query = new URLSearchParams({
        action: draft.id === undefined ? "create" : "update",
        name: draft.name.trim(),
        enabled: draft.enabled ? "1" : "0",
        eventType: draft.eventType,
        giftId: draft.giftId,
        every: String(draft.every),
        interaction: draft.interaction,
        quantity: String(draft.quantity),
      });
      if (draft.id !== undefined) query.set("id", String(draft.id));
      return sendStreamRuleUpdate(query);
    },
    [sendStreamRuleUpdate],
  );

  const deleteStreamRule = useCallback(
    (id: number) => sendStreamRuleUpdate(new URLSearchParams({
      action: "delete",
      id: String(id),
    })),
    [sendStreamRuleUpdate],
  );

  const duplicateStreamRule = useCallback(
    (id: number) => sendStreamRuleUpdate(new URLSearchParams({
      action: "duplicate",
      id: String(id),
    })),
    [sendStreamRuleUpdate],
  );

  const toggleStreamRule = useCallback(
    (id: number, enabled: boolean) => sendStreamRuleUpdate(new URLSearchParams({
      action: "toggle",
      id: String(id),
      enabled: enabled ? "1" : "0",
    })),
    [sendStreamRuleUpdate],
  );

  const value = useMemo(
    () => ({
      config,
      draft,
      interaction,
      pesky,
      peskyBattle,
      streamRules,
      optimisticInteractionQueue,
      interactionTesting,
      interactionSettingsStatus,
      status,
      applyDraft,
      applyChallenge,
      applyInteractionSettings,
      applyInteractionsEnabled,
      applyInteractionQueuePaused,
      clearPendingInteractions,
      applyInteractionPhaseTransitionProtection,
      testInteraction,
      applyPeskyEnabled,
      applyPeskyNames,
      applyPeskyItem,
      applyPeskyBattleGift,
      applyPeskyBattleStreamAttacks,
      applyPeskyBattleItem,
      armPeskyBattle,
      startPeskyBattle,
      cancelPeskyBattle,
      disablePeskyBattle,
      resetPeskyBattle,
      saveStreamRule,
      deleteStreamRule,
      duplicateStreamRule,
      toggleStreamRule,
    }),
    [
      config,
      draft,
      interaction,
      pesky,
      peskyBattle,
      streamRules,
      optimisticInteractionQueue,
      interactionTesting,
      interactionSettingsStatus,
      status,
      applyDraft,
      applyChallenge,
      applyInteractionSettings,
      applyInteractionsEnabled,
      applyInteractionQueuePaused,
      clearPendingInteractions,
      applyInteractionPhaseTransitionProtection,
      testInteraction,
      applyPeskyEnabled,
      applyPeskyNames,
      applyPeskyItem,
      applyPeskyBattleGift,
      applyPeskyBattleStreamAttacks,
      applyPeskyBattleItem,
      armPeskyBattle,
      startPeskyBattle,
      cancelPeskyBattle,
      disablePeskyBattle,
      resetPeskyBattle,
      saveStreamRule,
      deleteStreamRule,
      duplicateStreamRule,
      toggleStreamRule,
    ],
  );
  return <ConfigContext.Provider value={value}>{children}</ConfigContext.Provider>;
}

export function useConfig() {
  const value = useContext(ConfigContext);
  if (!value) throw new Error("useConfig must be used inside ConfigProvider");
  return value;
}
