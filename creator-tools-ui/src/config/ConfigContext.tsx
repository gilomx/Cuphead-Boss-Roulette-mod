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
  RouletteConfigState,
  RouletteSelection,
} from "../model";

interface ConfigValue {
  config: RouletteConfigState | null;
  draft: ForceDraft | null;
  interaction: InteractionConfigState | null;
  optimisticInteractionQueue: InteractionQueueEntry[];
  interactionTesting: boolean;
  status: ConnectionStatus;
  applyDraft: (draft: ForceDraft) => void;
  applyChallenge: (id: number, enabled: boolean) => void;
  applyInteractionMaxActive: (value: number) => void;
  applyInteractionRandomTest: (enabled: boolean) => void;
  testInteraction: (item: string, donor: string, quantity: number, delay: number) => void;
}

interface DesiredInteractionRandomTest {
  enabled: boolean;
  baselineRandomTestRevision: number;
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
  const [optimisticInteractionQueue, setOptimisticInteractionQueue] = useState<
    InteractionQueueEntry[]
  >([]);
  const [interactionTesting, setInteractionTesting] = useState(false);
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const desiredForceRef = useRef<ForceDraft | null>(null);
  const desiredChallengesRef = useRef(new Map<number, boolean>());
  const desiredInteractionRandomTestRef = useRef<
    DesiredInteractionRandomTest | null
  >(null);
  const randomTestRequestRevisionRef = useRef(0);
  const randomTestWriteChainRef = useRef<Promise<void>>(Promise.resolve());
  const loadRequestRevisionRef = useRef(0);
  const lastAppliedLoadRevisionRef = useRef(0);
  const interactionRevisionRef = useRef<number | null>(null);
  const nextOptimisticInteractionIdRef = useRef(-1);
  const mountedRef = useRef(true);

  const load = useCallback(async () => {
    const loadRevision = loadRequestRevisionRef.current + 1;
    loadRequestRevisionRef.current = loadRevision;
    try {
      const [configResponse, interactionResponse] = await Promise.all([
        fetch("/api/config", { cache: "no-store" }),
        fetch("/api/config/interactions", { cache: "no-store" }),
      ]);
      if (!configResponse.ok) throw new Error(`HTTP ${configResponse.status}`);
      if (!interactionResponse.ok) throw new Error(`HTTP ${interactionResponse.status}`);
      const next = (await configResponse.json()) as RouletteConfigState;
      const nextInteraction = (await interactionResponse.json()) as InteractionConfigState;
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
      setInteraction(visibleInteraction);
      if (!next.ready || !nextInteraction.ready) {
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
        forcePending || challengePending || interactionPending || randomTestPending
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
        ? { ...current, randomTestEnabled: enabled }
        : current);
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
    [interaction, load],
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

  const value = useMemo(
    () => ({
      config,
      draft,
      interaction,
      optimisticInteractionQueue,
      interactionTesting,
      status,
      applyDraft,
      applyChallenge,
      applyInteractionMaxActive,
      testInteraction,
      applyInteractionRandomTest,
    }),
    [
      config,
      draft,
      interaction,
      optimisticInteractionQueue,
      interactionTesting,
      status,
      applyDraft,
      applyChallenge,
      applyInteractionMaxActive,
      testInteraction,
      applyInteractionRandomTest,
    ],
  );
  return <ConfigContext.Provider value={value}>{children}</ConfigContext.Provider>;
}

export function useConfig() {
  const value = useContext(ConfigContext);
  if (!value) throw new Error("useConfig must be used inside ConfigProvider");
  return value;
}
