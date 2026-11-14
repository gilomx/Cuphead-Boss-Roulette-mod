import { useId, useState } from "react";
import { ChevronDown, Plus, Trash2 } from "lucide-react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRulePlatform, StreamRuleTrigger, TikTokGift } from "../../model";
import { InteractionPicker } from "./InteractionPicker";
import { interactionItemFor } from "./interactionCatalog";
import { StreamPlatformIcon, StreamTriggerIcon } from "./StreamRuleIcons";
import { TikTokGiftPicker } from "./TikTokGiftPicker";
import { TwitchRewardFields } from "./TwitchRewardFields";
import type { TwitchRewards } from "./useTwitchRewards";
import { platformTriggers } from "./streamRuleDraft";
import {
  createStreamRuleValueDraft,
  streamRuleValueSlot,
  validStreamRuleGroupDraft,
  type StreamRuleGroupDraft,
  type StreamRuleValueDraft,
} from "./streamRuleGroups";
import "../../styles/stream-rule-groups.css";

export interface StreamRuleGroupFormProps {
  draft: StreamRuleGroupDraft;
  gifts: TikTokGift[];
  maxEvery: number;
  maxQuantity: number;
  maxCooldownSeconds: number;
  maxValues?: number;
  saving: boolean;
  rewardBusy: boolean;
  rewards: TwitchRewards;
  rewardSaveError: string;
  onRewardBusy: (busy: boolean) => void;
  onOpenAdvancedSettings: () => void;
  onChange: (draft: StreamRuleGroupDraft) => void;
  onCancel: () => void;
  onSave: (draft: StreamRuleGroupDraft) => void;
}

const platforms: StreamRulePlatform[] = ["tiktok", "twitch", "youtube"];

function bounded(value: string, minimum: number, maximum: number) {
  const parsed = Math.floor(Number(value));
  return Number.isFinite(parsed) ? Math.max(minimum, Math.min(maximum, parsed)) : minimum;
}

interface ValueFieldsProps {
  value: StreamRuleValueDraft;
  gifts: TikTokGift[];
  allowSlot: (platform: StreamRulePlatform, trigger: StreamRuleTrigger) => boolean;
  effectId: string;
  effectName: string;
  effectImage?: string;
  rewards: TwitchRewards;
  rewardSaveError: string;
  onRewardBusy: (busy: boolean) => void;
  maxEvery: number;
  saving: boolean;
  canRemove: boolean;
  onChange: (value: StreamRuleValueDraft) => void;
  onRemove: () => void;
}

function ValueFields({ value, gifts, allowSlot, effectId, effectName, effectImage,
  maxEvery, saving, canRemove, onChange, onRemove, onRewardBusy, rewards, rewardSaveError }: ValueFieldsProps) {
  const { t } = useLocalization();
  const [giftOpen, setGiftOpen] = useState(false);
  const selectedGift = gifts.find(gift => gift.giftId === value.giftId);
  const everyLabel = value.eventType === "like" ? "likeEvery"
    : value.eventType === "currency" ? "bitsEvery"
    : value.eventType === "jewels" ? "jewelsEvery"
    : value.eventType === "redemption" ? "redemptionsEvery" : "every";
  const availablePlatforms = platforms.filter(platform => platformTriggers[platform].some(trigger => allowSlot(platform, trigger)));
  const availableTriggers = platformTriggers[value.platform].filter(trigger => allowSlot(value.platform, trigger));
  return (
    <section className="stream-rule-group-value" data-platform={value.platform}
      aria-label={t("interactions.rules.groups.valueFor").replace("{platform}", t(`dashboard.platforms.${value.platform}`))}>
      <div className="stream-rule-group-value__fields">
        <label className="stream-rule-group-value__platform">
          <span><StreamPlatformIcon platform={value.platform} />{t("interactions.rules.editor.platform")}</span>
          <select disabled={saving} value={value.platform} onChange={event => {
            setGiftOpen(false);
            const platform = event.target.value as StreamRulePlatform;
            const defaultDraft = createStreamRuleValueDraft(selectedGift ?? gifts[0], platform);
            const nextDraft = allowSlot(platform, defaultDraft.eventType) ? defaultDraft
              : createStreamRuleValueDraft(gifts[0], platform, platformTriggers[platform].find(trigger => allowSlot(platform, trigger)));
            onChange({ ...nextDraft,
              id: value.id, enabled: value.enabled });
          }}>
            {availablePlatforms.map(platform => <option key={platform} value={platform}>{t(`dashboard.platforms.${platform}`)}</option>)}
          </select>
        </label>
        <label>
          <span><StreamTriggerIcon eventType={value.eventType} />{t("interactions.rules.editor.triggerType")}</span>
          <select disabled={saving} value={value.eventType} onChange={event => {
            const eventType = event.target.value as StreamRuleTrigger;
            setGiftOpen(false);
            onChange({ ...createStreamRuleValueDraft(selectedGift ?? gifts[0], value.platform, eventType),
              id: value.id, enabled: value.enabled });
          }}>
            {availableTriggers.map(eventType => <option key={eventType} value={eventType}>{t(`interactions.rules.editor.${eventType}Name`)}</option>)}
          </select>
        </label>
        {value.eventType !== "follow" ? <label>
          <span>{t(`interactions.rules.editor.${everyLabel}`)}</span>
          <input type="number" min={1} max={maxEvery} step={1} required value={value.every} disabled={saving}
            onChange={event => onChange({ ...value, every: bounded(event.target.value, 1, maxEvery) })} />
        </label> : <p className="stream-rule-group-value__follow">{t("interactions.rules.editor.followOnceHint")}</p>}
        <div className="stream-rule-group-value__actions">
          <label className="stream-rule-group-value__enabled">
            <input type="checkbox" checked={value.enabled} disabled={saving}
              onChange={event => onChange({ ...value, enabled: event.target.checked })} />
            <span>{t("interactions.rules.groups.enabledValue")}</span>
          </label>
          {canRemove ? <button type="button" className="stream-rule-group-value__remove" disabled={saving}
            aria-label={t("interactions.rules.groups.removeValue").replace("{platform}", t(`dashboard.platforms.${value.platform}`))}
            title={t("interactions.rules.groups.removeValue").replace("{platform}", t(`dashboard.platforms.${value.platform}`))}
            onClick={onRemove}><Trash2 aria-hidden="true" /></button> : null}
        </div>
      </div>
      {value.eventType === "gift" ? <div className="stream-rule-group-value__gift">
        <button type="button" className="stream-rule-group-value__gift-trigger" disabled={saving}
          aria-expanded={giftOpen} onClick={() => setGiftOpen(open => !open)}>
          {selectedGift ? <img src={selectedGift.imagePath} alt="" /> : null}
          <span><strong>{selectedGift?.name ?? t("interactions.rules.editor.selectGift")}</strong>
            <small>{t("interactions.rules.groups.changeGift")}</small></span>
          <ChevronDown aria-hidden="true" />
        </button>
        {giftOpen ? <TikTokGiftPicker gifts={gifts} selectedId={value.giftId} disabled={saving}
          onSelect={gift => { onChange({ ...value, giftId: gift.giftId, name: gift.name }); setGiftOpen(false); }} /> : null}
      </div> : null}
      {value.eventType === "redemption" ? <TwitchRewardFields value={value} effectId={effectId} effectName={effectName}
        effectImage={effectImage} rewards={rewards} disabled={saving} saveError={rewardSaveError}
        onChange={onChange} onBusy={onRewardBusy} /> : null}
      <small className="stream-rule-group-value__hint">{t(`interactions.rules.editor.${value.eventType}TriggerHint`)}</small>
    </section>
  );
}

export function StreamRuleGroupForm({ draft, gifts, maxEvery, maxQuantity, maxCooldownSeconds,
  maxValues = 4, saving, rewardBusy, onRewardBusy, rewards, rewardSaveError,
  onOpenAdvancedSettings, onChange, onCancel, onSave }: StreamRuleGroupFormProps) {
  const { t } = useLocalization();
  const reactId = useId().replace(/:/g, "");
  const selectedInteraction = interactionItemFor(draft.interaction);
  const usedSlots = new Set(draft.values.map(value => streamRuleValueSlot(value.platform, value.eventType)));
  const nextPlatform = platforms.find(platform => !usedSlots.has(platform));
  const valueLimit = Math.max(0, Math.min(4, Math.floor(maxValues)));
  const disabled = saving || rewardBusy;
  const canSave = validStreamRuleGroupDraft(draft, gifts, { maxEvery, maxQuantity, maxCooldownSeconds, maxValues: valueLimit });
  const updateValue = (index: number, value: StreamRuleValueDraft) => onChange({
    ...draft, values: draft.values.map((current, currentIndex) => currentIndex === index ? value : current),
  });
  return (
    <form className="stream-rule-form stream-rule-group-form" aria-busy={disabled} onSubmit={event => {
      event.preventDefault();
      if (!canSave || disabled) return;
      onSave({ ...draft, values: draft.values.map(value => {
        const gift = gifts.find(gift => gift.giftId === value.giftId);
        const name = value.eventType === "gift" ? gift?.name ?? ""
          : value.eventType === "redemption" ? value.rewardName.trim() : t(`interactions.rules.editor.${value.eventType}Name`);
        return { ...value, name: name.trim().slice(0, 64),
          every: value.eventType === "follow" ? 1 : value.every,
          giftId: value.eventType === "gift" ? value.giftId : "",
          rewardName: value.eventType === "redemption" ? value.rewardName.trim() : "" };
      }) });
    }}>
      <div className="stream-rule-group-form__result">
        <InteractionPicker id={`stream-rule-group-interaction-${reactId}`} label={t("interactions.rules.groups.interaction")}
          selectedKey={draft.interaction} disabled={disabled} onSelect={item => onChange({ ...draft, interaction: item.id })} />
        <label><span>{t("interactions.rules.editor.quantity")}</span>
          <input type="number" min={1} max={maxQuantity} step={1} required disabled={disabled} value={draft.quantity}
            onChange={event => onChange({ ...draft, quantity: bounded(event.target.value, 1, maxQuantity) })} />
        </label>
      </div>
      {selectedInteraction?.category === "mini_boss" ? <p className="stream-rule-group-form__hint">
        {t("interactions.miniBoss.description")} {t("interactions.miniBoss.compatibility")}
      </p> : null}
      {selectedInteraction?.group === "challenge" ? <div className="stream-rule-group-form__challenge">
        <label><span>{t("interactions.challenges.countdown")}</span>
          <input type="number" min={0} max={30} step={1} required disabled={disabled} value={draft.countdownSeconds}
            onChange={event => onChange({ ...draft, countdownSeconds: bounded(event.target.value, 0, 30) })} />
        </label>
        <label><span>{t("interactions.challenges.duration")}</span>
          <input type="number" min={1} max={120} step={1} required disabled={disabled} value={draft.durationSeconds}
            onChange={event => onChange({ ...draft, durationSeconds: bounded(event.target.value, 1, 120) })} />
        </label>
      </div> : null}
      <fieldset className="stream-rule-group-form__values">
        <legend>{t("interactions.rules.groups.values")}</legend>
        <p className="stream-rule-group-form__hint">{t("interactions.rules.groups.valuesHint")}</p>
        {draft.values.map((value, index) => {
          const slot = streamRuleValueSlot(value.platform, value.eventType);
          return <ValueFields key={value.id ?? slot} value={value} gifts={gifts}
            allowSlot={(platform, trigger) => {
              const nextSlot = streamRuleValueSlot(platform, trigger);
              return nextSlot === slot || !usedSlots.has(nextSlot);
            }}
            effectId={draft.interaction} effectName={selectedInteraction ? t(selectedInteraction.titleKey) : ""}
            effectImage={selectedInteraction?.image} rewards={rewards} rewardSaveError={rewardSaveError} onRewardBusy={onRewardBusy}
            maxEvery={maxEvery} saving={disabled} canRemove={draft.values.length > 1}
            onChange={next => updateValue(index, next)}
            onRemove={() => onChange({ ...draft, values: draft.values.filter((_, currentIndex) => currentIndex !== index) })} />;
        })}
        {nextPlatform && draft.values.length < valueLimit ? <button type="button" className="stream-rule-group-form__add"
          disabled={disabled} onClick={() => onChange({ ...draft, values: [...draft.values, createStreamRuleValueDraft(gifts[0], nextPlatform)] })}>
          <Plus aria-hidden="true" />{t("interactions.rules.groups.addValue")}
        </button> : null}
        {!usedSlots.has("twitch_redemption") && draft.values.length < valueLimit ? <button type="button" className="stream-rule-group-form__add"
          disabled={disabled} onClick={() => onChange({ ...draft, values: [...draft.values, createStreamRuleValueDraft(undefined, "twitch", "redemption")] })}>
          <Plus aria-hidden="true" />{t("interactions.rules.twitchRewards.add")}
        </button> : null}
      </fieldset>
      <fieldset className="stream-rule-group-form__cooldowns">
        <legend>{t("interactions.rules.editor.cooldownTitle")}</legend>
        <div className="stream-rule-group-form__cooldown-fields">
          <label><span>{t("interactions.rules.editor.userCooldown")}</span>
            <input type="number" min={0} max={maxCooldownSeconds} step={1} required disabled={disabled} value={draft.userCooldownSeconds}
              onChange={event => onChange({ ...draft, userCooldownSeconds: bounded(event.target.value, 0, maxCooldownSeconds) })} />
          </label>
          <label><span>{t("interactions.rules.editor.globalCooldown")}</span>
            <input type="number" min={0} max={maxCooldownSeconds} step={1} required disabled={disabled} value={draft.globalCooldownSeconds}
              onChange={event => onChange({ ...draft, globalCooldownSeconds: bounded(event.target.value, 0, maxCooldownSeconds) })} />
          </label>
        </div>
        <small>{t("interactions.rules.groups.sharedHint")}</small>
        <p className="stream-rule-group-form__advanced">{t("interactions.rules.editor.cooldownAdvancedPrefix")} <button type="button"
          disabled={disabled} onClick={onOpenAdvancedSettings}>{t("interactions.rules.editor.cooldownAdvancedLink")}</button> {t("interactions.rules.editor.cooldownAdvancedSuffix")}</p>
      </fieldset>
      <div className="stream-rule-group-form__actions">
        {!canSave ? <small>{t("interactions.rules.groups.completeValues")}</small> : null}
        <button type="button" disabled={disabled} onClick={onCancel}>{t("interactions.rules.actions.cancel")}</button>
        <button type="submit" disabled={disabled || !canSave}>{t(draft.ruleIds.length ? "interactions.rules.actions.save" : "interactions.rules.actions.create")}</button>
      </div>
    </form>
  );
}
