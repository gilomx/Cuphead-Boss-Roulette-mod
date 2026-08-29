import { useMemo } from "react";
import { SearchableSelectField } from "../../components/SearchableSelectField";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRuleDraft, StreamRuleTrigger, TikTokGift } from "../../model";
import { interactionItemFor, interactionItems } from "./interactionCatalog";
import { TikTokGiftPicker } from "./TikTokGiftPicker";

interface StreamRuleFormProps {
  draft: StreamRuleDraft;
  gifts: TikTokGift[];
  maxEvery: number;
  maxQuantity: number;
  saving: boolean;
  onChange: (draft: StreamRuleDraft) => void;
  onCancel: () => void;
  onSave: (draft: StreamRuleDraft) => void;
}

function boundedInteger(value: string, maximum: number) {
  return Math.max(1, Math.min(maximum, Math.floor(Number(value)) || 1));
}

export function StreamRuleForm({
  draft,
  gifts,
  maxEvery,
  maxQuantity,
  saving,
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
    (!hasThreshold || (draft.every >= 1 && draft.every <= maxEvery)) &&
    draft.quantity >= 1 && draft.quantity <= maxQuantity,
  );

  const triggerName = draft.eventType === "gift"
    ? selectedGift?.name ?? ""
    : t(`interactions.rules.editor.${draft.eventType}Name`);

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
      <label className="stream-rule-form__enabled stream-rule-form__wide">
        <span>
          <strong>{t("interactions.rules.editor.enabled")}</strong>
          <small>{t("interactions.rules.editor.enabledHint")}</small>
        </span>
        <input
          type="checkbox"
          disabled={saving}
          checked={draft.enabled}
          onChange={(event) => onChange({ ...draft, enabled: event.target.checked })}
        />
      </label>

      <label className="stream-rule-form__wide">
        <span>{t("interactions.rules.editor.triggerType")}</span>
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
              every: eventType === "follow" ? 1 : draft.every,
              name: eventType === "gift"
                ? nextGift?.name ?? ""
                : t(`interactions.rules.editor.${eventType}Name`),
            });
          }}
        >
          <option value="gift">{t("interactions.rules.editor.triggerGift")}</option>
          <option value="like">{t("interactions.rules.editor.triggerLike")}</option>
          <option value="follow">{t("interactions.rules.editor.triggerFollow")}</option>
        </select>
        <small>{t(`interactions.rules.editor.${draft.eventType}TriggerHint`)}</small>
      </label>

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
              <small>{t(draft.eventType === "like"
                ? "interactions.rules.editor.likeEveryHint"
                : "interactions.rules.editor.everyHint")}</small>
            </label>
          ) : null}

          <div className="stream-rule-execution__interaction">
            <SearchableSelectField
              id="stream-rule-interaction"
              label={t("interactions.rules.editor.interaction")}
              options={interactionItems}
              selectedKey={draft.interaction}
              placeholder={t("interactions.rules.editor.interactionPlaceholder")}
              noResults={t("interactions.rules.editor.noInteractionResults")}
              disabled={saving}
              getKey={(item) => item.id}
              getLabel={(item) => t(item.titleKey)}
              getImage={(item) => item.image}
              getMeta={(item) => t(item.typeKey)}
              getSearchTerms={(item) => [item.id, t(item.typeKey)]}
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
            <small>{t("interactions.rules.editor.quantityHint")}</small>
          </label>
        </div>
        {draft.eventType === "follow" ? (
          <p className="stream-rule-execution__notice">
            {t("interactions.rules.editor.followOnceHint")}
          </p>
        ) : null}
      </fieldset>

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
