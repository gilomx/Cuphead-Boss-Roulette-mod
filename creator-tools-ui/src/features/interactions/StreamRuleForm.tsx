import { useMemo } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRuleDraft, TikTokGift } from "../../model";
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
  const canSave = Boolean(
    draft.name.trim() && selectedGift && selectedInteraction &&
    draft.every >= 1 && draft.every <= maxEvery &&
    draft.quantity >= 1 && draft.quantity <= maxQuantity,
  );

  return (
    <form
      className="stream-rule-form"
      aria-busy={saving}
      onSubmit={(event) => {
        event.preventDefault();
        if (canSave) onSave(draft);
      }}
    >
      <div className="stream-rule-form__identity stream-rule-form__wide">
        <label>
          <span>{t("interactions.rules.editor.name")}</span>
          <input
            type="text"
            maxLength={64}
            autoFocus
            disabled={saving}
            value={draft.name}
            onChange={(event) => onChange({ ...draft, name: event.target.value })}
          />
        </label>
        <label className="stream-rule-form__enabled">
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
      </div>

      <div className="stream-rule-fixed-fields stream-rule-form__wide">
        <div><span>{t("interactions.rules.editor.platform")}</span><strong>TikTok</strong></div>
        <div><span>{t("interactions.rules.editor.connection")}</span><strong>{t("interactions.rules.editor.allConnections")}</strong></div>
        <div><span>{t("interactions.rules.editor.event")}</span><strong>{t("interactions.rules.editor.gift")}</strong></div>
      </div>

      <TikTokGiftPicker
        gifts={gifts}
        selectedId={draft.giftId}
        disabled={saving}
        onSelect={(gift) => {
          const shouldFollowGiftName = !draft.name.trim() || draft.name === selectedGift?.name;
          onChange({
            ...draft,
            giftId: gift.giftId,
            name: shouldFollowGiftName ? gift.name : draft.name,
          });
        }}
      />

      <fieldset className="stream-rule-execution stream-rule-form__wide">
        <legend>{t("interactions.rules.editor.executionTitle")}</legend>
        <div className="stream-rule-execution__grid">
          <label>
            <span>{t("interactions.rules.editor.every")}</span>
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
            <small>{t("interactions.rules.editor.everyHint")}</small>
          </label>

          <label className="stream-rule-execution__interaction">
            <span>{t("interactions.rules.editor.interaction")}</span>
            <div>
              {selectedInteraction ? <img src={selectedInteraction.image} alt="" /> : null}
              <select
                value={draft.interaction}
                disabled={saving}
                onChange={(event) => onChange({ ...draft, interaction: event.target.value })}
              >
                {interactionItems.map((item) => (
                  <option value={item.id} key={item.id}>{t(item.titleKey)}</option>
                ))}
              </select>
            </div>
          </label>

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
