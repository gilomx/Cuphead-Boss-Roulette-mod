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
  RouletteConfigState,
  RouletteSelection,
} from "../model";

interface ConfigValue {
  config: RouletteConfigState | null;
  draft: ForceDraft | null;
  status: ConnectionStatus;
  applyDraft: (draft: ForceDraft) => void;
  applyChallenge: (id: number, enabled: boolean) => void;
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
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const desiredForceRef = useRef<ForceDraft | null>(null);
  const desiredChallengesRef = useRef(new Map<number, boolean>());
  const mountedRef = useRef(true);

  const load = useCallback(async () => {
    try {
      const response = await fetch("/api/config", { cache: "no-store" });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const next = (await response.json()) as RouletteConfigState;
      if (!next.ready) {
        if (mountedRef.current) setStatus("connecting");
        return;
      }
      if (!mountedRef.current) return;

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
      setStatus(forcePending || challengePending ? "pending" : "saved");
    } catch {
      if (mountedRef.current) setStatus("error");
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

  const value = useMemo(
    () => ({ config, draft, status, applyDraft, applyChallenge }),
    [config, draft, status, applyDraft, applyChallenge],
  );
  return <ConfigContext.Provider value={value}>{children}</ConfigContext.Provider>;
}

export function useConfig() {
  const value = useContext(ConfigContext);
  if (!value) throw new Error("useConfig must be used inside ConfigProvider");
  return value;
}
