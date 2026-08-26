import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRule, TikTokGift } from "../../model";
import { interactionItemFor } from "./interactionCatalog";

interface StreamRuleRowProps {
  rule: StreamRule;
  gift?: TikTokGift;
  disabled: boolean;
  highlighted: boolean;
  onEdit: (rule: StreamRule) => void;
  onToggle: (id: number, enabled: boolean) => void;
  onDuplicate: (id: number) => void;
  onDelete: (id: number) => void;
}

export function StreamRuleRow({
  rule,
  gift,
  disabled,
  highlighted,
  onEdit,
  onToggle,
  onDuplicate,
  onDelete,
}: StreamRuleRowProps) {
  const { t } = useLocalization();
  const interaction = interactionItemFor(rule.interaction);

  return (
    <tr
      className="stream-rule-row"
      data-enabled={rule.enabled}
      data-highlighted={highlighted}
    >
      <td>
        <label className="stream-rule-switch">
          <input
            type="checkbox"
            checked={rule.enabled}
            disabled={disabled}
            aria-label={t("interactions.rules.list.toggleLabel").replace("{name}", rule.name)}
            onChange={(event) => onToggle(rule.id, event.target.checked)}
          />
          <span className="stream-rule-switch__track" aria-hidden="true"><i /></span>
          <small>{t(rule.enabled
            ? "interactions.rules.list.enabled"
            : "interactions.rules.list.disabled")}</small>
        </label>
      </td>
      <td>
        <div className="stream-rule-table__name">
          <strong>{rule.name}</strong>
          <small>TikTok · #{rule.id}</small>
        </div>
      </td>
      <td>
        <div className="stream-rule-table__gift">
          {gift ? (
            <img
              src={gift.imagePath}
              alt=""
              aria-hidden="true"
              onLoad={(event) => {
                event.currentTarget.hidden = false;
              }}
              onError={(event) => {
                event.currentTarget.hidden = true;
              }}
            />
          ) : null}
          <span>
            <strong>{gift?.name ?? rule.giftName}</strong>
            <small>
              {rule.coinsPerUnit} {t("interactions.rules.coins")} · {rule.every === 1
                ? t("interactions.rules.list.everyOne")
                : t("interactions.rules.list.every").replace("{count}", String(rule.every))}
            </small>
          </span>
        </div>
      </td>
      <td>
        <div className="stream-rule-table__interaction">
          {interaction ? <img src={interaction.image} alt="" /> : null}
          <span>
            <strong>{interaction ? t(interaction.titleKey) : rule.interaction}</strong>
            <small>{t("interactions.rules.list.quantity").replace("{count}", String(rule.quantity))}</small>
          </span>
        </div>
      </td>
      <td>
        <div className="stream-rule-row__actions" aria-label={t("interactions.rules.actions.label")}>
          <button type="button" disabled={disabled} onClick={() => onEdit(rule)}>
            {t("interactions.rules.actions.edit")}
          </button>
          <button type="button" disabled={disabled} onClick={() => onDuplicate(rule.id)}>
            {t("interactions.rules.actions.duplicate")}
          </button>
          <button
            type="button"
            className="stream-rule-row__delete"
            disabled={disabled}
            onClick={() => {
              if (window.confirm(t("interactions.rules.actions.deleteConfirm"))) {
                onDelete(rule.id);
              }
            }}
          >
            {t("interactions.rules.actions.delete")}
          </button>
        </div>
      </td>
    </tr>
  );
}
