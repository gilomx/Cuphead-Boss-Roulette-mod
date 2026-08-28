import { useLocalization } from "../../i18n/LocalizationContext";

interface StreamRuleDeleteConfirmationProps {
  ruleId: number;
  ruleName: string;
  disabled: boolean;
  deleting: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export function StreamRuleDeleteConfirmation({
  ruleId,
  ruleName,
  disabled,
  deleting,
  onCancel,
  onConfirm,
}: StreamRuleDeleteConfirmationProps) {
  const { t } = useLocalization();
  const titleId = `stream-rule-delete-${ruleId}-title`;
  const descriptionId = `stream-rule-delete-${ruleId}-description`;

  return (
    <td className="stream-rule-delete-confirmation-cell" colSpan={4}>
      <div
        className="stream-rule-delete-confirmation"
        role="group"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        aria-busy={deleting}
        onKeyDown={(event) => {
          if (event.key === "Escape" && !deleting) {
            event.preventDefault();
            onCancel();
          }
        }}
      >
        <div className="mode-switch-confirmation__copy">
          <strong id={titleId}>
            {t("interactions.rules.actions.deleteConfirm")}
          </strong>
          <p id={descriptionId}>
            {t("interactions.rules.actions.deleteDescription").replace(
              "{name}",
              ruleName,
            )}
          </p>
        </div>
        <div className="mode-switch-confirmation__actions">
          <button
            type="button"
            className="mode-switch-action mode-switch-action--cancel"
            disabled={deleting}
            autoFocus
            onClick={onCancel}
          >
            {t("interactions.rules.actions.cancel")}
          </button>
          <button
            type="button"
            className="mode-switch-action mode-switch-action--danger"
            disabled={disabled || deleting}
            onClick={onConfirm}
          >
            {t(deleting
              ? "interactions.rules.actions.deleting"
              : "interactions.rules.actions.deleteSubmit")}
          </button>
        </div>
      </div>
    </td>
  );
}
