import { Copy, Pencil, Trash2 } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRule, TikTokGift } from "../../model";
import { interactionItemFor } from "./interactionCatalog";
import { StreamRuleDeleteConfirmation } from "./StreamRuleDeleteConfirmation";

interface StreamRuleRowProps {
  rule: StreamRule;
  gift?: TikTokGift;
  disabled: boolean;
  highlighted: boolean;
  confirmingDelete: boolean;
  deleting: boolean;
  onEdit: (rule: StreamRule) => void;
  onToggle: (id: number, enabled: boolean) => void;
  onDuplicate: (id: number) => void;
  onRequestDelete: (id: number) => void;
  onCancelDelete: () => void;
  onConfirmDelete: (id: number) => void;
}

export function StreamRuleRow({
  rule,
  gift,
  disabled,
  highlighted,
  confirmingDelete,
  deleting,
  onEdit,
  onToggle,
  onDuplicate,
  onRequestDelete,
  onCancelDelete,
  onConfirmDelete,
}: StreamRuleRowProps) {
  const { t } = useLocalization();
  const interaction = interactionItemFor(rule.interaction);
  const deleteButtonRef = useRef<HTMLButtonElement>(null);
  const returnAnimationTimerRef = useRef<number | null>(null);
  const [returningFromDelete, setReturningFromDelete] = useState(false);
  const ruleName = rule.eventType === "gift"
    ? gift?.name || rule.giftName || rule.name
    : t(`interactions.rules.editor.${rule.eventType}Name`);
  const triggerDetail = rule.eventType === "gift"
    ? `${rule.coinsPerUnit ?? 0} ${t("interactions.rules.coins")} · ${rule.every === 1
      ? t("interactions.rules.list.everyOne")
      : t("interactions.rules.list.every").replace("{count}", String(rule.every))}`
    : rule.eventType === "like"
      ? t("interactions.rules.list.likeEvery").replace("{count}", String(rule.every))
      : t("interactions.rules.list.followOnce");

  useEffect(() => () => {
    if (returnAnimationTimerRef.current !== null) {
      window.clearTimeout(returnAnimationTimerRef.current);
    }
  }, []);

  const cancelDelete = () => {
    if (deleting) return;
    setReturningFromDelete(true);
    onCancelDelete();
    window.requestAnimationFrame(() => deleteButtonRef.current?.focus());
    if (returnAnimationTimerRef.current !== null) {
      window.clearTimeout(returnAnimationTimerRef.current);
    }
    returnAnimationTimerRef.current = window.setTimeout(() => {
      setReturningFromDelete(false);
      returnAnimationTimerRef.current = null;
    }, 220);
  };

  return (
    <tr
      className="stream-rule-row"
      data-rule-id={rule.id}
      data-enabled={rule.enabled}
      data-highlighted={highlighted && !confirmingDelete}
      data-step={confirmingDelete ? "confirm" : "control"}
      data-returning={returningFromDelete}
    >
      {confirmingDelete ? (
        <StreamRuleDeleteConfirmation
          ruleId={rule.id}
          ruleName={ruleName}
          disabled={disabled}
          deleting={deleting}
          onCancel={cancelDelete}
          onConfirm={() => onConfirmDelete(rule.id)}
        />
      ) : (
        <>
          <td>
            <label className="stream-rule-switch">
              <input
                type="checkbox"
                checked={rule.enabled}
                disabled={disabled}
                aria-label={t("interactions.rules.list.toggleLabel").replace(
                  "{name}",
                  ruleName,
                )}
                onChange={(event) => onToggle(rule.id, event.target.checked)}
              />
              <span className="stream-rule-switch__track" aria-hidden="true">
                <i />
              </span>
              <small>{t(rule.enabled
                ? "interactions.rules.list.enabled"
                : "interactions.rules.list.disabled")}</small>
            </label>
          </td>
          <td>
            <div className="stream-rule-table__gift">
              {rule.eventType === "gift" && gift ? (
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
                <strong>{ruleName}</strong>
                <small>
                  #{rule.id} · {triggerDetail}
                </small>
              </span>
            </div>
          </td>
          <td>
            <div className="stream-rule-table__interaction">
              {interaction ? <img src={interaction.image} alt="" /> : null}
              <span>
                <strong>
                  {interaction ? t(interaction.titleKey) : rule.interaction}
                </strong>
                <small>
                  {t("interactions.rules.list.quantity").replace(
                    "{count}",
                    String(rule.quantity),
                  )}
                </small>
              </span>
            </div>
          </td>
          <td>
            <div
              className="stream-rule-row__actions"
              role="group"
              aria-label={t("interactions.rules.actions.label")}
            >
              <button
                type="button"
                disabled={disabled}
                aria-label={t("interactions.rules.actions.edit")}
                title={t("interactions.rules.actions.edit")}
                onClick={() => onEdit(rule)}
              >
                <Pencil aria-hidden="true" />
              </button>
              <button
                type="button"
                disabled={disabled}
                aria-label={t("interactions.rules.actions.duplicate")}
                title={t("interactions.rules.actions.duplicate")}
                onClick={() => onDuplicate(rule.id)}
              >
                <Copy aria-hidden="true" />
              </button>
              <button
                type="button"
                className="stream-rule-row__delete"
                ref={deleteButtonRef}
                disabled={disabled}
                aria-label={t("interactions.rules.actions.delete")}
                title={t("interactions.rules.actions.delete")}
                onClick={() => onRequestDelete(rule.id)}
              >
                <Trash2 aria-hidden="true" />
              </button>
            </div>
          </td>
        </>
      )}
    </tr>
  );
}
