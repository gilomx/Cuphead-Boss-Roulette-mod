import { useMemo } from "react";
import { SearchableSelectField } from "../../components/SearchableSelectField";
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
    selectedGift && selectedInteraction &&
    draft.every >= 1 && draft.every <= maxEvery &&
    draft.quantity >= 1 && draft.quantity <= maxQuantity,
  );

  return (
    <form
      className="stream-rule-form"
      aria-busy={saving}
      onSubmit={(event) => {
        event.preventDefault();
        if (canSave && selectedGift) {
          onSave({
            ...draft,
            name: selectedGift.name.trim().slice(0, 64),
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
