import { useMemo } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRule, TikTokGift } from "../../model";
import { StreamRuleRow } from "./StreamRuleRow";

interface StreamRulesTableProps {
  rules: StreamRule[];
  gifts: TikTokGift[];
  canCreate: boolean;
  disabled: boolean;
  highlightedRuleId: number | null;
  onCreate: () => void;
  onEdit: (rule: StreamRule) => void;
  onToggle: (id: number, enabled: boolean) => void;
  onDuplicate: (id: number) => void;
  onDelete: (id: number) => void;
}

export function StreamRulesTable({
  rules,
  gifts,
  canCreate,
  disabled,
  highlightedRuleId,
  onCreate,
  onEdit,
  onToggle,
  onDuplicate,
  onDelete,
}: StreamRulesTableProps) {
  const { t } = useLocalization();
  const giftsById = useMemo(
    () => new Map(gifts.map((gift) => [gift.giftId, gift])),
    [gifts],
  );

  if (rules.length === 0) {
    return (
      <div className="stream-rules-table__empty">
        <span aria-hidden="true">+</span>
        <strong>{t("interactions.rules.list.emptyTitle")}</strong>
        <p>{t("interactions.rules.list.emptyDescription")}</p>
        <button type="button" onClick={onCreate} disabled={!canCreate}>
          {t("interactions.rules.list.createFirst")}
        </button>
      </div>
    );
  }

  return (
    <div className="interaction-table-wrap stream-rules-table-wrap">
      <table className="interaction-table stream-rules-table">
        <thead>
          <tr>
            <th scope="col">{t("interactions.rules.list.status")}</th>
            <th scope="col">{t("interactions.rules.list.rule")}</th>
            <th scope="col">{t("interactions.rules.list.trigger")}</th>
            <th scope="col">{t("interactions.rules.list.result")}</th>
            <th scope="col">{t("interactions.rules.list.actions")}</th>
          </tr>
        </thead>
        <tbody>
          {rules.map((rule) => (
            <StreamRuleRow
              rule={rule}
              gift={giftsById.get(rule.giftId)}
              disabled={disabled}
              highlighted={highlightedRuleId === rule.id}
              onEdit={onEdit}
              onToggle={onToggle}
              onDuplicate={onDuplicate}
              onDelete={onDelete}
              key={rule.id}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}
