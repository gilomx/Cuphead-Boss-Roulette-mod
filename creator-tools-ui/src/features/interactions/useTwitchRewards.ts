import { useCallback, useEffect, useRef, useState } from "react";
import { startForegroundPolling } from "../../config/foregroundPolling";
import { twitchRewardCostMax, type StreamRuleValueDraft } from "./streamRuleGroups";

export interface TwitchRewardLimits {
  cooldownSeconds: number;
  maxPerStream: number;
  maxPerUserPerStream: number;
}

export interface TwitchReward extends Partial<TwitchRewardLimits> {
  id: string;
  title: string;
  cost: number;
  enabled: boolean;
  manageable: boolean;
}

export const rewardLimitFields = [
  { key: "cooldownSeconds", label: "cooldownMinutes", max: 10_080, multiplier: 60 },
  { key: "maxPerStream", label: "maxPerStream", max: 2_147_483_647, multiplier: 1 },
  { key: "maxPerUserPerStream", label: "maxPerUserPerStream", max: 2_147_483_647, multiplier: 1 },
] as const;

export type TwitchRewardLimitDraft = Partial<Record<keyof TwitchRewardLimits, string>>;

export interface TwitchRewardEdits {
  cost?: string;
  limits?: TwitchRewardLimitDraft;
}

type RewardCommand = Partial<TwitchRewardLimits> & { cost: number; broadcasterId: string } & (
  { operation: "create"; title: string } | { operation: "update"; id: string });

interface RewardCatalog {
  ready: boolean;
  canManage: boolean;
  broadcasterId: string;
  broadcasterLogin?: string;
  rewards: TwitchReward[];
  pendingRewardIds?: string[];
  syncError?: string;
  error?: string;
}

const emptyCatalog: RewardCatalog = { ready: false, canManage: false, broadcasterId: "", rewards: [] };

export function rewardLimitChanges(draft: TwitchRewardLimitDraft, original?: TwitchReward) {
  const changes: Partial<TwitchRewardLimits> = {};
  for (const { key, max, multiplier } of rewardLimitFields) {
    if (draft[key] === undefined) continue;
    const value = Number(draft[key]);
    if (!Number.isInteger(value) || value < 0 || value > max) return null;
    const converted = value * multiplier;
    if (converted !== (original?.[key] ?? 0)) changes[key] = converted;
  }
  return changes;
}

export function validRewardCost(cost: number) {
  return Number.isInteger(cost) && cost >= 1 && cost <= twitchRewardCostMax;
}

export function linkedTwitchReward(value: Pick<StreamRuleValueDraft, "rewardId" | "rewardBroadcasterId">, catalog: RewardCatalog) {
  return value.rewardBroadcasterId === catalog.broadcasterId
    ? catalog.rewards.find(reward => reward.id === value.rewardId)
    : undefined;
}

export function linkReward(value: StreamRuleValueDraft, reward: TwitchReward, broadcasterId: string): StreamRuleValueDraft {
  return {
    ...value,
    rewardId: reward.id,
    rewardName: reward.title,
    rewardCost: reward.cost,
    rewardManaged: reward.manageable || Boolean(value.rewardManaged && value.rewardId === reward.id &&
      value.rewardBroadcasterId === broadcasterId),
    rewardBroadcasterId: broadcasterId,
    rewardEdits: undefined,
  };
}

export function useTwitchRewards(active: boolean) {
  const [catalog, setCatalog] = useState(emptyCatalog);
  const readVersion = useRef(0);

  const refresh = useCallback(async (signal?: AbortSignal) => {
    const version = ++readVersion.current;
    try {
      const response = await fetch("/api/twitch/rewards", { cache: "no-store", signal });
      if (!response.ok) throw new Error("unavailable");
      const next = await response.json() as RewardCatalog;
      if (!Array.isArray(next.rewards)) throw new Error("unavailable");
      if (version === readVersion.current) setCatalog(next);
    } catch {
      if (!signal?.aborted && version === readVersion.current) setCatalog({ ...emptyCatalog, error: "unavailable" });
    }
  }, []);

  const write = useCallback(async (command: RewardCommand) => {
    const signal = AbortSignal.timeout(23000);
    const connection = await fetch("/api/twitch", { cache: "no-store", signal });
    if (!connection.ok) throw new Error("unavailable");
    const { controlToken } = await connection.json() as { controlToken?: string };
    if (!controlToken) throw new Error("not_connected");
    const response = await fetch("/api/twitch/rewards/set", {
      method: "POST", cache: "no-store", signal,
      headers: { "Content-Type": "application/json", "X-Pichi-Twitch-Control": controlToken },
      body: JSON.stringify(command),
    });
    const result = await response.json() as { ok?: boolean; reward?: TwitchReward & { broadcasterId: string }; error?: string };
    if (!response.ok || !result.ok || !result.reward) throw new Error(result.error ?? "unavailable");
    const reward = result.reward;
    if (reward.broadcasterId !== command.broadcasterId ||
        (command.operation === "update" && reward.id !== command.id)) throw new Error("unavailable");
    // Ignore reads started before Twitch acknowledged the write.
    readVersion.current++;
    setCatalog(current => current.broadcasterId === reward.broadcasterId ? {
      ...current,
      rewards: [...current.rewards.filter(entry => entry.id !== reward.id), reward],
    } : current);
    void refresh();
    return reward;
  }, [refresh]);

  useEffect(() => {
    if (!active) return;
    const controller = new AbortController();
    if (document.hidden || !document.hasFocus()) void refresh(controller.signal);
    const stopPolling = startForegroundPolling(() => void refresh(controller.signal), 2500);
    return () => { readVersion.current++; controller.abort(); stopPolling(); };
  }, [active, refresh]);

  return { catalog: active ? catalog : emptyCatalog, write };
}

export type TwitchRewards = ReturnType<typeof useTwitchRewards>;

export async function saveTwitchReward(value: StreamRuleValueDraft, { catalog, write }: TwitchRewards) {
  const reward = linkedTwitchReward(value, catalog);
  const edits = value.rewardEdits;
  if (!edits) return reward && reward.cost !== value.rewardCost ? { ...value, rewardCost: reward.cost } : value;

  if (!catalog.broadcasterId) throw new Error(catalog.error ?? "not_connected");
  if (catalog.broadcasterId !== value.rewardBroadcasterId) throw new Error("wrong_broadcaster");
  if (!catalog.canManage) throw new Error("manage_permission_required");
  if (!catalog.ready) throw new Error(catalog.error ?? "unavailable");
  if (!reward?.manageable) throw new Error("reward_not_manageable");
  const cost = edits.cost === undefined ? reward.cost : Number(edits.cost);
  const limits = rewardLimitChanges(edits.limits ?? {}, reward);
  if (!validRewardCost(cost) || !limits) throw new Error("invalid_request");
  const confirmed = cost !== reward.cost || Object.keys(limits).length
    ? await write({ operation: "update", id: reward.id, broadcasterId: catalog.broadcasterId, cost, ...limits })
    : reward;
  if (confirmed.cost !== cost || rewardLimitFields.some(({ key }) =>
    limits[key] !== undefined && (confirmed[key] ?? 0) !== limits[key])) throw new Error("unconfirmed");
  return linkReward(value, confirmed, catalog.broadcasterId);
}
