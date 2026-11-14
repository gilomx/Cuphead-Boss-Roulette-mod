import { useEffect, useRef, useState } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { twitchRewardCostMax, type StreamRuleValueDraft } from "./streamRuleGroups";
import {
  linkedTwitchReward, linkReward, rewardLimitChanges, rewardLimitFields, validRewardCost,
  type TwitchReward, type TwitchRewardLimitDraft, type TwitchRewards,
} from "./useTwitchRewards";

const textKey = "interactions.rules.twitchRewards.";
const createOption = ":create";
const savedOption = ":saved";

function RewardLimits({ draft, original, disabled, onChange }: {
  draft: TwitchRewardLimitDraft;
  original?: TwitchReward;
  disabled: boolean;
  onChange: (draft: TwitchRewardLimitDraft) => void;
}) {
  const { t } = useLocalization();
  return <fieldset className="stream-rule-reward-fields__limits">
    <legend>{t(textKey + "limits")}</legend>
    <div className="stream-rule-reward-fields__controls">
      {rewardLimitFields.map(({ key, label, max, multiplier }) => {
        const previous = (original?.[key] ?? 0) / multiplier;
        return <label key={key}>
          <span>{t(textKey + label)}</span>
          <input type="number" min={0} max={max} step={multiplier === 60 ? "any" : 1}
            inputMode="numeric" value={draft[key] ?? Number(previous.toFixed(4))}
            disabled={disabled} onChange={event => onChange({ ...draft, [key]: event.target.value })}
            aria-invalid={draft[key] !== undefined && rewardLimitChanges({ [key]: draft[key] }) === null} />
        </label>;
      })}
    </div>
    <small>{t(textKey + "limitsHint")}</small>
  </fieldset>;
}

function imageElement(source: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = reject;
    image.src = source;
  });
}

interface RewardFieldsProps {
  value: StreamRuleValueDraft;
  effectId: string;
  effectName: string;
  effectImage?: string;
  rewards: TwitchRewards;
  disabled: boolean;
  saveError: string;
  onChange: (value: StreamRuleValueDraft) => void;
  onBusy: (busy: boolean) => void;
}

export function TwitchRewardFields({ value, effectId, effectName, effectImage, rewards: { catalog, write }, disabled, saveError, onChange, onBusy }: RewardFieldsProps) {
  const { t } = useLocalization();
  const [createBroadcasterId, setCreateBroadcasterId] = useState("");
  const [title, setTitle] = useState("");
  const [createCost, setCreateCost] = useState("100");
  const [createLimits, setCreateLimits] = useState<TwitchRewardLimitDraft>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const mounted = useRef(false);

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const creating = Boolean(createBroadcasterId);
  const currentAccount = value.rewardBroadcasterId === catalog.broadcasterId;
  const selected = linkedTwitchReward(value, catalog);
  const canUpdate = Boolean(selected?.manageable && catalog.canManage);
  const pending = Boolean((currentAccount || !catalog.broadcasterId) && value.rewardId &&
    catalog.pendingRewardIds?.includes(value.rewardId));
  const actualCost = selected?.cost ?? value.rewardCost;
  const newLimits = rewardLimitChanges(createLimits);
  const login = catalog.broadcasterLogin;
  const dashboardUrl = typeof login === "string" && /^[a-zA-Z0-9_]{1,25}$/.test(login)
    ? `https://dashboard.twitch.tv/u/${login}/viewer-rewards/channel-points/rewards` : undefined;
  const displayedError = error || saveError || (pending ? catalog.syncError : "");

  async function createReward() {
    if (!newLimits) return;
    setBusy(true);
    onBusy(true);
    setError("");
    try {
      const reward = await write({ operation: "create", title: title.trim(), cost: Number(createCost),
        broadcasterId: createBroadcasterId, ...newLimits });
      if (!mounted.current) return;
      onChange(linkReward(value, reward, reward.broadcasterId));
      setCreateBroadcasterId("");
    } catch (failure) {
      if (mounted.current) setError(failure instanceof Error ? failure.message : "unavailable");
    } finally {
      if (mounted.current) setBusy(false);
      onBusy(false);
    }
  }

  const download = async (size: number) => {
    if (!effectImage) return;
    setError("");
    try {
      const image = await imageElement(effectImage);
      const canvas = document.createElement("canvas");
      canvas.width = canvas.height = size;
      const context = canvas.getContext("2d");
      if (!context || !image.naturalWidth || !image.naturalHeight) throw new Error();
      const scale = size / Math.max(image.naturalWidth, image.naturalHeight);
      const width = image.naturalWidth * scale;
      const height = image.naturalHeight * scale;
      context.imageSmoothingQuality = "high";
      context.drawImage(image, (size - width) / 2, (size - height) / 2, width, height);
      const link = document.createElement("a");
      link.download = `${effectId}-${size}.png`;
      link.href = canvas.toDataURL("image/png");
      link.click();
    } catch { setError("image_error"); }
  };

  const selectReward = (id: string) => {
    setError("");
    if (id === createOption) {
      setTitle((value.rewardName || effectName).trim().slice(0, 45));
      setCreateCost("100");
      setCreateLimits({});
      setCreateBroadcasterId(catalog.broadcasterId);
      return;
    }
    setCreateBroadcasterId("");
    const reward = catalog.rewards.find(entry => entry.id === id);
    onChange(reward ? linkReward(value, reward, catalog.broadcasterId)
      : { ...value, rewardId: undefined, rewardCost: undefined, rewardManaged: undefined,
        rewardBroadcasterId: undefined, rewardEdits: undefined });
  };

  return <div className="stream-rule-reward-fields" aria-busy={busy}>
    {catalog.ready && catalog.broadcasterId ? <label>
      <span>{t(textKey + "existing")}</span>
      <select value={creating ? createOption : selected?.id ?? (value.rewardId ? savedOption : "")} disabled={disabled || busy}
        onChange={event => selectReward(event.target.value)}>
        <option value="">{t(textKey + "byTitle")}</option>
        {value.rewardId && !selected ? <option value={savedOption} disabled>{value.rewardName}</option> : null}
        {catalog.rewards.map(reward => <option key={reward.id} value={reward.id}>
          {reward.title} · {reward.cost.toLocaleString()}{reward.enabled ? "" : ` · ${t(textKey + "disabled")}`}
        </option>)}
        {catalog.canManage || creating ? <option value={createOption} disabled={!catalog.canManage}>
          {t(textKey + "newReward")}
        </option> : null}
      </select>
    </label> : <small role="status">{t(textKey + (catalog.error ? "offline" : "loading"))}</small>}
    {catalog.broadcasterId && !catalog.canManage ? <small>{t(textKey + "reauthorize")}{" "}
      <a href="/config/settings?connection=twitch">{t(textKey + "settings")}</a>
    </small> : null}
    {!creating && (value.rewardId ? <p className="stream-rule-reward-fields__linked">
      <strong>{selected?.title ?? value.rewardName}</strong>
      {!canUpdate && actualCost ? <small>{t(textKey + "costDisplay").replace("{cost}", actualCost.toLocaleString())}</small> : null}
      {value.rewardManaged && selected && !selected.enabled && !pending ? <small>{t(textKey + "disabledHint")}</small> : null}
      {!value.rewardManaged ? <small>{t(textKey + "manualHint")}</small> : null}
      {pending ? <small role="status">{t(textKey + "pending")}</small> : null}
      {!catalog.ready ? <button type="button" disabled={disabled || busy} onClick={() => selectReward("")}>
        {t(textKey + "byTitle")}
      </button> : null}
    </p> : <label>
      <span>{t("interactions.rules.editor.rewardName")}</span>
      <input type="text" maxLength={64} required value={value.rewardName} disabled={disabled || busy}
        onChange={event => onChange({ ...value, rewardName: event.target.value })} />
      <small>{t("interactions.rules.editor.rewardNameHint")}</small>
    </label>)}
    {!creating && canUpdate && selected ? <div className="stream-rule-reward-fields__editor">
      <label>
        <span>{t(textKey + "cost")}</span>
        <input type="number" min={1} max={twitchRewardCostMax} step={1} required
          value={value.rewardEdits?.cost ?? actualCost ?? ""} disabled={disabled || busy}
          onChange={event => onChange({ ...value, rewardEdits: { ...value.rewardEdits, cost: event.target.value } })} />
      </label>
      <RewardLimits draft={value.rewardEdits?.limits ?? {}} original={selected} disabled={disabled || busy}
        onChange={limits => onChange({ ...value, rewardEdits: { ...value.rewardEdits, limits } })} />
    </div> : null}
    {creating ? <div className="stream-rule-reward-fields__editor">
      <div className="stream-rule-reward-fields__controls">
        <label><span>{t(textKey + "title")}</span>
          <input maxLength={45} value={title} disabled={disabled || busy}
            onChange={event => setTitle(event.target.value)} />
        </label>
        <label><span>{t(textKey + "cost")}</span>
          <input type="number" min={1} max={twitchRewardCostMax} step={1} value={createCost}
            disabled={disabled || busy} onChange={event => setCreateCost(event.target.value)} />
        </label>
      </div>
      <RewardLimits draft={createLimits} disabled={disabled || busy} onChange={setCreateLimits} />
      <div className="stream-rule-reward-fields__controls">
        <button type="button" disabled={disabled || busy || !catalog.canManage || catalog.broadcasterId !== createBroadcasterId ||
          !title.trim() || !validRewardCost(Number(createCost)) || !newLimits}
          onClick={() => void createReward()}>{t(textKey + "create")}</button>
        <button type="button" disabled={disabled || busy} onClick={() => setCreateBroadcasterId("")}>
          {t("interactions.rules.actions.cancel")}
        </button>
      </div>
      <small>{t(textKey + "createHint")}</small>
    </div> : null}
    {dashboardUrl ? <small>{t(textKey + "dashboardPrefix")}{" "}
      <a href={dashboardUrl} target="_blank" rel="noopener noreferrer">{t(textKey + "dashboardLink")}</a>
    </small> : null}
    {effectImage ? <div className="stream-rule-reward-fields__download">
      <small>{t(textKey + "download")}</small>
      {[28, 56, 112].map(size => <button key={size} type="button" disabled={disabled || busy}
        onClick={() => void download(size)}>{size} × {size}</button>)}
    </div> : null}
    {displayedError ? <small role="alert">
      {displayedError === "revision_conflict" ? t("interactions.rules.feedback.revision_conflict")
        : t(textKey + `errors.${displayedError}`, t(textKey + "errors.default"))}
    </small> : null}
  </div>;
}
