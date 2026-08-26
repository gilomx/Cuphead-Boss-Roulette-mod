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
  streamRules: StreamRulesConfigState | null;
  optimisticInteractionQueue: InteractionQueueEntry[];
  interactionTesting: boolean;
  status: ConnectionStatus;
  applyDraft: (draft: ForceDraft) => void;
  applyChallenge: (id: number, enabled: boolean) => void;
  applyInteractionMaxActive: (value: number) => void;
  applyInteractionRandomTest: (enabled: boolean) => void;
  applyInteractionPhaseTransitionProtection: (enabled: boolean) => void;
  applyPeskyEnabled: (enabled: boolean) => void;
  applyPeskyNames: (names: string) => void;
  applyPeskyItem: (item: string, enabled: boolean) => void;
  saveStreamRule: (draft: StreamRuleDraft) => void;
  deleteStreamRule: (id: number) => void;
  duplicateStreamRule: (id: number) => void;
  toggleStreamRule: (id: number, enabled: boolean) => void;
  testInteraction: (item: string, donor: string, quantity: number, delay: number) => void;
}

interface PendingPeskyChange {
  targetRevision: number;
  apply: (state: PeskyModeConfigState) => PeskyModeConfigState;
}

interface DesiredInteractionRandomTest {
  enabled: boolean;
  baselineRandomTestRevision: number;
  requestRevision: number;
}

interface DesiredPhaseTransitionProtection {
  enabled: boolean;
  baselineRevision: number;
  requestRevision: number;
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
  const [streamRules, setStreamRules] = useState<StreamRulesConfigState | null>(null);
  const [optimisticInteractionQueue, setOptimisticInteractionQueue] = useState<
    InteractionQueueEntry[]
  >([]);
  const [interactionTesting, setInteractionTesting] = useState(false);
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const desiredForceRef = useRef<ForceDraft | null>(null);
  const desiredChallengesRef = useRef(new Map<number, boolean>());
  const loadRequestRevisionRef = useRef(0);
  const lastAppliedLoadRevisionRef = useRef(0);
  const interactionRevisionRef = useRef<number | null>(null);
  const nextOptimisticInteractionIdRef = useRef(-1);
  const desiredInteractionRandomTestRef = useRef<
    DesiredInteractionRandomTest | null
  >(null);
  const randomTestRequestRevisionRef = useRef(0);
  const randomTestWriteChainRef = useRef<Promise<void>>(Promise.resolve());
  const desiredPhaseTransitionProtectionRef = useRef<
    DesiredPhaseTransitionProtection | null
  >(null);
  const phaseTransitionProtectionRequestRevisionRef = useRef(0);
  const phaseTransitionProtectionWriteChainRef = useRef<Promise<void>>(
    Promise.resolve(),
  );
  const confirmedPeskyRevisionRef = useRef(0);
  const pendingPeskyChangesRef = useRef<PendingPeskyChange[]>([]);
  const peskyWriteChainRef = useRef<Promise<void>>(Promise.resolve());
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
        streamRulesResponse,
      ] = await Promise.all([
        fetch("/api/config", { cache: "no-store" }),
        fetch("/api/config/interactions", { cache: "no-store" }),
        fetch("/api/config/pesky", { cache: "no-store" }),
        fetch("/api/config/interactions/rules", { cache: "no-store" }),
      ]);
      if (!configResponse.ok) throw new Error(`HTTP ${configResponse.status}`);
      if (!interactionResponse.ok) throw new Error(`HTTP ${interactionResponse.status}`);
      if (!peskyResponse.ok) throw new Error(`HTTP ${peskyResponse.status}`);
      if (!streamRulesResponse.ok) throw new Error(`HTTP ${streamRulesResponse.status}`);
      const next = (await configResponse.json()) as RouletteConfigState;
      const nextInteraction = (await interactionResponse.json()) as InteractionConfigState;
      const nextPesky = (await peskyResponse.json()) as PeskyModeConfigState;
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
      let visibleInteraction = nextInteraction;
      let randomTestPending = false;
      const desiredRandomTest = desiredInteractionRandomTestRef.current;
      if (desiredRandomTest !== null) {
        const confirmed =
          nextInteraction.randomTestRevision >
            desiredRandomTest.baselineRandomTestRevision &&
          nextInteraction.randomTestEnabled === desiredRandomTest.enabled;
        if (confirmed) {
          desiredInteractionRandomTestRef.current = null;
        } else {
          randomTestPending = true;
          visibleInteraction = {
            ...nextInteraction,
            randomTestEnabled: desiredRandomTest.enabled,
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
          !nextStreamRules.ready) {
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
          randomTestPending || phaseTransitionProtectionPending ||
          peskyPending || streamRulesPending
          ? "pending"
          : "saved",
      );
    } catch {
      if (
        mountedRef.current &&
        loadRevision >= lastAppliedLoadRevisionRef.current
      ) {
        lastAppliedLoadRevisionRef.current = loadRevision;
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

  const applyInteractionMaxActive = useCallback(
    (value: number) => {
      if (!interaction?.ready) return;
      const normalized = Math.max(
        1,
        Math.min(interaction.maxActiveLimit ?? 20, Math.floor(value) || 1),
      );
      setInteraction((current) => current
        ? { ...current, maxActive: normalized }
        : current);
      setStatus("saving");

      const query = new URLSearchParams({ maxActive: String(normalized) });
      void fetch("/api/config/interactions/set?" + query, { cache: "no-store" })
        .then((response) => {
          if (!response.ok) throw new Error("HTTP " + response.status);
          window.setTimeout(() => void load(), 160);
        })
        .catch(() => {
          if (mountedRef.current) setStatus("error");
        });
    },
    [interaction, load],
  );

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

  const applyInteractionRandomTest = useCallback(
    (enabled: boolean) => {
      if (!interaction?.ready) return;
      const requestRevision = randomTestRequestRevisionRef.current + 1;
      randomTestRequestRevisionRef.current = requestRevision;
      desiredInteractionRandomTestRef.current = {
        enabled,
        baselineRandomTestRevision: interaction.randomTestRevision,
        requestRevision,
      };
      setInteraction((current) => current
        ? {
            ...current,
            randomTestEnabled: enabled,
            feedback: enabled
              ? "random_test_enabled"
              : "random_test_disabled",
            error: false,
          }
        : current);
      if (enabled && pesky?.enabled) {
        const pending = pendingPeskyChangesRef.current;
        const previousTarget = pending.length > 0
          ? pending[pending.length - 1].targetRevision
          : confirmedPeskyRevisionRef.current;
        const applyPeskySwitch = (state: PeskyModeConfigState) => ({
          ...state,
          enabled: false,
          running: false,
          waitingForInteractions: false,
          feedback: "disabled_by_random_test",
          error: false,
        });
        pending.push({
          targetRevision: Math.max(
            confirmedPeskyRevisionRef.current,
            previousTarget,
          ) + 1,
          apply: applyPeskySwitch,
        });
        setPesky((current) => current
          ? applyPeskySwitch(current)
          : current);
      }
      setStatus("saving");

      const query = new URLSearchParams({
        randomTestEnabled: enabled ? "1" : "0",
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
            randomTestRequestRevisionRef.current !== requestRevision ||
            desiredInteractionRandomTestRef.current?.requestRevision !==
              requestRevision
          ) return;
          desiredInteractionRandomTestRef.current = null;
          if (mountedRef.current) {
            setStatus("error");
            void load();
          }
        });
      randomTestWriteChainRef.current =
        randomTestWriteChainRef.current.then(send, send);
    },
    [interaction, pesky, load],
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
      if (enabled && interaction?.randomTestEnabled) {
        const requestRevision = randomTestRequestRevisionRef.current + 1;
        randomTestRequestRevisionRef.current = requestRevision;
        desiredInteractionRandomTestRef.current = {
          enabled: false,
          baselineRandomTestRevision: interaction.randomTestRevision,
          requestRevision,
        };
        setInteraction((current) => current
          ? {
              ...current,
              randomTestEnabled: false,
              feedback: "random_test_disabled_by_pesky",
              error: false,
            }
          : current);
      }
      sendPeskyUpdate(
        new URLSearchParams({ enabled: enabled ? "1" : "0" }),
        (state) => ({
          ...state,
          enabled,
          feedback: enabled ? "enabled" : "disabled",
          error: false,
          running: enabled ? state.running : false,
          waitingForInteractions: enabled
            ? state.waitingForInteractions
            : false,
        }),
      );
    },
    [interaction, pesky, sendPeskyUpdate],
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

  const testInteraction = useCallback(
    (item: string, donor: string, quantity: number, delay: number) => {
      if (!interaction?.ready) return;
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
      if (!streamRules?.ready) return;
      streamRulesRevisionRef.current = streamRules.revision;
      setStatus("saving");
      const send = () => fetch(
        "/api/config/interactions/rules/set?" + query,
        { cache: "no-store" },
      )
        .then((response) => {
          if (!response.ok) throw new Error("HTTP " + response.status);
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
    },
    [load, streamRules],
  );

  const saveStreamRule = useCallback(
    (draft: StreamRuleDraft) => {
      const query = new URLSearchParams({
        action: draft.id === undefined ? "create" : "update",
        name: draft.name.trim(),
        enabled: draft.enabled ? "1" : "0",
        giftId: draft.giftId,
        every: String(draft.every),
        interaction: draft.interaction,
        quantity: String(draft.quantity),
      });
      if (draft.id !== undefined) query.set("id", String(draft.id));
      sendStreamRuleUpdate(query);
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
      streamRules,
      optimisticInteractionQueue,
      interactionTesting,
      status,
      applyDraft,
      applyChallenge,
      applyInteractionMaxActive,
      applyInteractionRandomTest,
      applyInteractionPhaseTransitionProtection,
      testInteraction,
      applyPeskyEnabled,
      applyPeskyNames,
      applyPeskyItem,
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
      streamRules,
      optimisticInteractionQueue,
      interactionTesting,
      status,
      applyDraft,
      applyChallenge,
      applyInteractionMaxActive,
      applyInteractionRandomTest,
      applyInteractionPhaseTransitionProtection,
      testInteraction,
      applyPeskyEnabled,
      applyPeskyNames,
      applyPeskyItem,
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
