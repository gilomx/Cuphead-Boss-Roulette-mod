import { useMemo } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRuleDraft, StreamRulePlatform, StreamRuleTrigger, TikTokGift } from "../../model";
import { platformTriggers } from "./streamRuleDraft";
import { StreamPlatformIcon, StreamTriggerIcon } from "./StreamRuleIcons";
import { interactionItemFor } from "./interactionCatalog";
import { InteractionPicker } from "./InteractionPicker";
import { TikTokGiftPicker } from "./TikTokGiftPicker";

interface StreamRuleFormProps {
  draft: StreamRuleDraft;
  gifts: TikTokGift[];
  maxEvery: number;
  maxQuantity: number;
  maxCooldownSeconds: number;
  saving: boolean;
  onOpenAdvancedSettings: () => void;
  onChange: (draft: StreamRuleDraft) => void;
  onCancel: () => void;
  onSave: (draft: StreamRuleDraft) => void;
}

function boundedInteger(value: string, maximum: number) {
  return Math.max(1, Math.min(maximum, Math.floor(Number(value)) || 1));
}

function boundedCooldown(value: string, maximum: number) {
  const parsed = Math.floor(Number(value));
  return Number.isFinite(parsed) ? Math.max(0, Math.min(maximum, parsed)) : 0;
}

function fillTemplate(
  template: string,
  values: Record<string, string | number>,
) {
  return Object.entries(values).reduce(
    (text, [key, value]) => text.replaceAll(`{${key}}`, String(value)),
    template,
  );
}

export function StreamRuleForm({
  draft,
  gifts,
  maxEvery,
  maxQuantity,
  maxCooldownSeconds,
  saving,
  onOpenAdvancedSettings,
  onChange,
  onCancel,
  onSave,
}: StreamRuleFormProps) {
  const { t } = useLocalization();
  const selectedGift = useMemo(
    () => gifts.find((gift) => gift.giftId === draft.giftId),
    [draft.giftId, gifts],
  );
  const selectedInteraction = interactionItemFor(draft.interaction);
  const needsGift = draft.eventType === "gift";
  const hasThreshold = draft.eventType !== "follow";
  const canSave = Boolean(
    (!needsGift || selectedGift) && selectedInteraction &&
    (draft.eventType !== "redemption" || Boolean(draft.rewardName.trim())) &&
    (!hasThreshold || (draft.every >= 1 && draft.every <= maxEvery)) &&
    draft.quantity >= 1 && draft.quantity <= maxQuantity &&
    Number.isInteger(draft.userCooldownSeconds) &&
    draft.userCooldownSeconds >= 0 && draft.userCooldownSeconds <= maxCooldownSeconds &&
    Number.isInteger(draft.globalCooldownSeconds) &&
    draft.globalCooldownSeconds >= 0 && draft.globalCooldownSeconds <= maxCooldownSeconds &&
    (selectedInteraction?.group !== "challenge" ||
      (Number.isInteger(draft.durationSeconds ?? 15) &&
       (draft.durationSeconds ?? 15) >= 1 && (draft.durationSeconds ?? 15) <= 120 &&
       Number.isInteger(draft.countdownSeconds ?? 3) &&
       (draft.countdownSeconds ?? 3) >= 0 && (draft.countdownSeconds ?? 3) <= 30)),
  );

  const triggerName = draft.eventType === "gift"
    ? selectedGift?.name ?? ""
    : draft.eventType === "redemption" ? draft.rewardName.trim()
    : t(`interactions.rules.editor.${draft.eventType}Name`);
  const interactionName = selectedInteraction
    ? t(selectedInteraction.titleKey)
    : t("interactions.rules.editor.interactionPlaceholder");
  const executionSummaryKey = draft.eventType === "gift"
    ? draft.every === 1
      ? "interactions.rules.editor.executionGiftSummaryOne"
      : "interactions.rules.editor.executionGiftSummaryMany"
    : draft.eventType === "like"
      ? "interactions.rules.editor.executionLikeSummary"
      : draft.eventType === "follow" ? "interactions.rules.editor.executionFollowSummary"
        : `interactions.rules.editor.execution${draft.eventType}Summary`;
  const executionSummary = fillTemplate(t(executionSummaryKey), {
    every: draft.every,
    trigger: triggerName,
    quantity: draft.quantity,
    interaction: interactionName,
  });
  const cooldownSummaryKey = draft.userCooldownSeconds > 0
    ? draft.globalCooldownSeconds > 0
      ? "interactions.rules.editor.cooldownSummaryBoth"
      : "interactions.rules.editor.cooldownSummaryUser"
    : draft.globalCooldownSeconds > 0
      ? "interactions.rules.editor.cooldownSummaryGlobal"
      : "interactions.rules.editor.cooldownSummaryNone";
  const cooldownSummary = fillTemplate(t(cooldownSummaryKey), {
    user: draft.userCooldownSeconds,
    global: draft.globalCooldownSeconds,
  });

  return (
    <form
      className="stream-rule-form"
      aria-busy={saving}
      onSubmit={(event) => {
        event.preventDefault();
        if (canSave) {
          onSave({
            ...draft,
            every: draft.eventType === "follow" ? 1 : draft.every,
            name: triggerName.trim().slice(0, 64),
          });
        }
      }}
    >
      <label className="stream-rule-form__wide">
        <span>{t("interactions.rules.editor.platform")}</span>
        <select disabled={saving} value={draft.platform} onChange={(event) => {
          const platform = event.target.value as StreamRulePlatform;
          const eventType = platformTriggers[platform].includes(draft.eventType) ? draft.eventType : platformTriggers[platform][0];
          onChange({ ...draft, platform, eventType, every: 1,
            giftId: platform === "tiktok" ? selectedGift?.giftId ?? gifts[0]?.giftId ?? "" : "",
            rewardName: "" });
        }}>
          <option value="tiktok">TikTok</option>
          <option value="twitch">Twitch</option>
        </select>
      </label>
      <label className="stream-rule-form__wide">
        <span className="stream-rule-trigger-label"><StreamPlatformIcon platform={draft.platform} />{t("interactions.rules.editor.triggerType")}</span>
        <div className="stream-rule-trigger-select">
        <StreamTriggerIcon eventType={draft.eventType} />
        <select
          disabled={saving}
          value={draft.eventType}
          onChange={(event) => {
            const eventType = event.target.value as StreamRuleTrigger;
            const nextGift = selectedGift ?? gifts[0];
            onChange({
              ...draft,
              eventType,
              giftId: eventType === "gift" ? nextGift?.giftId ?? "" : draft.giftId,
              every: eventType === "currency" ? 100 : 1,
              name: eventType === "gift"
                ? nextGift?.name ?? ""
                : t(`interactions.rules.editor.${eventType}Name`),
            });
          }}
        >
          {platformTriggers[draft.platform].map((eventType) => (
            <option key={eventType} value={eventType}>{t(`interactions.rules.editor.${eventType}Name`)}</option>
          ))}
        </select>
        </div>
        <small>{t(`interactions.rules.editor.${draft.eventType}TriggerHint`)}</small>
      </label>

      {draft.eventType === "redemption" ? (
        <label className="stream-rule-form__wide">
          <span>{t("interactions.rules.editor.rewardName")}</span>
          <input type="text" maxLength={64} required value={draft.rewardName} disabled={saving}
            onChange={(event) => onChange({ ...draft, rewardName: event.target.value })} />
          <small>{t("interactions.rules.editor.rewardNameHint")}</small>
        </label>
      ) : null}

      {draft.eventType === "gift" ? (
        <TikTokGiftPicker
          gifts={gifts}
          selectedId={draft.giftId}
          disabled={saving}
          onSelect={(gift) => {
            onChange({
              ...draft,
              giftId: gift.giftId,
              name: gift.name,
            });
          }}
        />
      ) : null}

      <fieldset className="stream-rule-execution stream-rule-form__wide">
        <legend>{t("interactions.rules.editor.executionTitle")}</legend>
        <div className="stream-rule-execution__grid" data-has-threshold={hasThreshold}>
          {hasThreshold ? (
            <label>
              <span>{t(draft.eventType === "like"
                ? "interactions.rules.editor.likeEvery"
                : draft.eventType === "currency" ? "interactions.rules.editor.bitsEvery"
                  : draft.eventType === "redemption" ? "interactions.rules.editor.redemptionsEvery"
                    : "interactions.rules.editor.every")}</span>
              <input
                type="number"
                min={1}
                max={maxEvery}
                disabled={saving}
                value={draft.every}
                onChange={(event) => onChange({
                  ...draft,
                  every: boundedInteger(event.target.value, maxEvery),
                })}
              />
            </label>
          ) : null}

          <div className="stream-rule-execution__interaction">
            <InteractionPicker
              id="stream-rule-interaction"
              label={t("interactions.rules.editor.interaction")}
              selectedKey={draft.interaction}
              disabled={saving}
              onSelect={(item) => onChange({
                ...draft,
                interaction: item.id,
              })}
            />
          </div>

          <label>
            <span>{t("interactions.rules.editor.quantity")}</span>
            <input
              type="number"
              min={1}
              max={maxQuantity}
              disabled={saving}
              value={draft.quantity}
              onChange={(event) => onChange({
                ...draft,
                quantity: boundedInteger(event.target.value, maxQuantity),
              })}
            />
          </label>
        </div>
        <p className="stream-rule-execution__notice stream-rule-execution__summary">
          {executionSummary}
        </p>
        {selectedInteraction?.category === "mini_boss" ? (
          <p className="stream-rule-execution__notice">
            {t("interactions.miniBoss.description")}{" "}
            {t("interactions.miniBoss.compatibility")}
          </p>
        ) : null}
      </fieldset>

      <fieldset className="stream-rule-execution stream-rule-cooldowns stream-rule-form__wide">
        <legend>{t("interactions.rules.editor.cooldownTitle")}</legend>
        <div className="stream-rule-execution__grid stream-rule-cooldowns__grid">
          <label>
            <span>{t("interactions.rules.editor.userCooldown")}</span>
            <input type="number" min={0} max={maxCooldownSeconds} step={1}
              disabled={saving} value={draft.userCooldownSeconds}
              onChange={(event) => onChange({ ...draft,
                userCooldownSeconds: boundedCooldown(event.target.value, maxCooldownSeconds),
              })} />
          </label>
          <label>
            <span>{t(draft.eventType === "gift"
              ? "interactions.rules.editor.giftCooldown"
              : "interactions.rules.editor.globalCooldown")}</span>
            <input type="number" min={0} max={maxCooldownSeconds} step={1}
              disabled={saving} value={draft.globalCooldownSeconds}
              onChange={(event) => onChange({ ...draft,
                globalCooldownSeconds: boundedCooldown(event.target.value, maxCooldownSeconds),
              })} />
          </label>
        </div>
        <p className="stream-rule-execution__notice stream-rule-execution__summary">
          {cooldownSummary}
        </p>
        <p className="stream-rule-execution__advanced-note">
          {t("interactions.rules.editor.cooldownAdvancedPrefix")}{" "}
          <button type="button" onClick={onOpenAdvancedSettings} disabled={saving}>
            {t("interactions.rules.editor.cooldownAdvancedLink")}
          </button>{" "}
          {t("interactions.rules.editor.cooldownAdvancedSuffix")}
        </p>
      </fieldset>

      {selectedInteraction?.group === "challenge" ? (
        <>
        <label className="stream-rule-form__wide">
          <span>{t("interactions.challenges.countdown")}</span>
          <input type="number" min={0} max={30} step={1} value={draft.countdownSeconds ?? 3}
            disabled={saving} onChange={(event) => onChange({ ...draft, countdownSeconds: Number(event.target.value) })} />
          <small>{t("interactions.challenges.countdownHint")}</small>
        </label>
        <label className="stream-rule-form__wide">
          <span>{t("interactions.challenges.duration")}</span>
          <input type="number" min={1} max={120} step={1} value={draft.durationSeconds ?? 15}
            disabled={saving} onChange={(event) => onChange({ ...draft, durationSeconds: Number(event.target.value) })} />
          <small>{t("interactions.challenges.durationHint")}</small>
        </label>
        </>
      ) : null}

      <div className="stream-rule-form__actions stream-rule-form__wide">
        <button type="button" onClick={onCancel} disabled={saving}>
          {t("interactions.rules.actions.cancel")}
        </button>
        <button type="submit" disabled={!canSave || saving}>
          {t(draft.id === undefined
            ? "interactions.rules.actions.create"
            : "interactions.rules.actions.save")}
        </button>
      </div>
    </form>
  );
}
