import { Copy, Pencil, Trash2 } from "lucide-react";
import { useEffect, useMemo, useRef } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamRule, StreamRulePlatform, TikTokGift } from "../../model";
import { interactionItemFor } from "./interactionCatalog";
import { StreamPlatformIcon, StreamTriggerIcon } from "./StreamRuleIcons";
import type { StreamRuleGroup } from "./streamRuleGroups";
import "../../styles/stream-rule-group-table.css";

interface StreamRuleGroupsTableProps {
  groups: StreamRuleGroup[];
  gifts: TikTokGift[];
  canCreate: boolean;
  availableRuleSlots: number;
  disabled: boolean;
  highlightedRuleId: number | null;
  confirmingDeleteId: number | null;
  deletingRuleId: number | null;
  onCreate: () => void;
  onEdit: (group: StreamRuleGroup) => void;
  onToggle: (ruleIds: number[], enabled: boolean) => void;
  onDuplicate: (ruleIds: number[]) => void;
  onRequestDelete: (id: number) => void;
  onCancelDelete: () => void;
  onConfirmDelete: (group: StreamRuleGroup) => void;
}

const platforms: StreamRulePlatform[] = ["tiktok", "twitch", "youtube"];

function GroupRow({ group, giftsById, disabled, duplicateDisabled, highlighted, confirmingDelete, deleting,
  onEdit, onToggle, onDuplicate, onRequestDelete, onCancelDelete, onConfirmDelete,
}: {
  group: StreamRuleGroup;
  giftsById: Map<string, TikTokGift>;
  disabled: boolean;
  duplicateDisabled: boolean;
  highlighted: boolean;
  confirmingDelete: boolean;
  deleting: boolean;
} & Pick<StreamRuleGroupsTableProps, "onEdit" | "onToggle" | "onDuplicate" | "onRequestDelete" | "onCancelDelete" | "onConfirmDelete">) {
  const { t } = useLocalization();
  const interaction = interactionItemFor(group.interaction);
  const title = interaction ? t(interaction.titleKey) : group.interaction;
  const enabledCount = group.rules.filter(rule => rule.enabled).length;
  const mixed = enabledCount > 0 && enabledCount < group.rules.length;
  const toggleRef = useRef<HTMLInputElement>(null);
  const deleteRef = useRef<HTMLButtonElement>(null);
  const wasConfirming = useRef(false);
  useEffect(() => { if (toggleRef.current) toggleRef.current.indeterminate = mixed; }, [mixed]);
  useEffect(() => {
    if (wasConfirming.current && !confirmingDelete && !deleting) deleteRef.current?.focus();
    wasConfirming.current = confirmingDelete;
  }, [confirmingDelete, deleting]);

  const cooldowns = [
    group.userCooldownSeconds > 0 ? t("interactions.rules.list.userCooldown").replace("{seconds}", String(group.userCooldownSeconds)) : "",
    group.globalCooldownSeconds > 0 ? t("interactions.rules.list.globalCooldown").replace("{seconds}", String(group.globalCooldownSeconds)) : "",
  ].filter(Boolean).join(" · ");

  const cost = (rule: StreamRule) => {
    const gift = giftsById.get(rule.giftId);
    const name = rule.eventType === "gift" ? gift?.name || rule.giftName || rule.name
      : rule.eventType === "redemption" ? rule.rewardName || rule.name
      : t(`interactions.rules.editor.${rule.eventType}Name`);
    const label = `${rule.every} ${name} · ${t(rule.enabled ? "interactions.rules.list.enabled" : "interactions.rules.list.disabled")}`;
    return <div className="stream-rule-group-cost" data-enabled={rule.enabled} key={rule.id} title={label}>
      {rule.eventType === "gift" && gift ? <img src={gift.imagePath} alt="" aria-hidden="true" onError={event => { event.currentTarget.hidden = true; }} />
        : rule.platform === "twitch" && rule.eventType === "currency" ? <img src="/assets/creator-tools/twitch/bits.png" alt="" aria-hidden="true" />
        : <StreamTriggerIcon eventType={rule.eventType} />}
      <span><strong>{rule.every}</strong><small>{name}</small></span>
      {!rule.enabled ? <span className="stream-rule-group-cost__disabled" aria-label={t("interactions.rules.list.disabled")}>○</span> : null}
    </div>;
  };

  return <tr className="stream-rule-row stream-rule-group-row" data-rule-id={group.id}
    data-step={confirmingDelete ? "confirm" : "control"} data-enabled={enabledCount > 0} data-highlighted={highlighted}>
    {confirmingDelete ? <td colSpan={6} className="stream-rule-delete-confirmation-cell">
      <div className="stream-rule-delete-confirmation" role="group" aria-labelledby={`stream-rule-group-delete-${group.id}`}
        aria-busy={deleting} onKeyDown={event => { if (event.key === "Escape" && !deleting) { event.preventDefault(); onCancelDelete(); } }}>
        <div className="mode-switch-confirmation__copy">
          <strong id={`stream-rule-group-delete-${group.id}`}>{t("interactions.rules.actions.deleteConfirm")}</strong>
          <p>{t("interactions.rules.actions.deleteDescription").replace("{name}", title)}</p>
        </div>
        <div className="mode-switch-confirmation__actions">
          <button type="button" autoFocus className="mode-switch-action mode-switch-action--cancel" disabled={deleting} onClick={onCancelDelete}>
            {t("interactions.rules.actions.cancel")}
          </button>
          <button type="button" className="mode-switch-action mode-switch-action--danger" disabled={disabled || deleting} onClick={() => onConfirmDelete(group)}>
            {t(deleting ? "interactions.rules.actions.deleting" : "interactions.rules.actions.deleteSubmit")}
          </button>
        </div>
      </div>
    </td> : <>
      <td><label className="stream-rule-switch">
        <input ref={toggleRef} type="checkbox" checked={enabledCount > 0} disabled={disabled}
          aria-label={t("interactions.rules.list.toggleLabel").replace("{name}", title)}
          onChange={event => onToggle(group.ruleIds, event.target.checked)} />
        <span className="stream-rule-switch__track" aria-hidden="true"><i /></span>
        <small>{mixed ? t("interactions.rules.groups.mixedStatus", "Algunos activos")
          : t(enabledCount ? "interactions.rules.list.enabled" : "interactions.rules.list.disabled")}</small>
      </label></td>
      <td><div className="stream-rule-group-result">
        {interaction ? <img src={interaction.image} alt="" aria-hidden="true" /> : null}
        <span><strong>{title}</strong>
          <small>{t("interactions.rules.list.quantity").replace("{count}", String(group.quantity))}
            {interaction?.group === "challenge" ? ` · ${group.durationSeconds} s` : ""}</small>
          {interaction?.group === "challenge" && group.countdownSeconds > 0 ? <small>{t("interactions.rules.groups.countdown", "Inicio en {seconds} s").replace("{seconds}", String(group.countdownSeconds))}</small> : null}
          {cooldowns ? <small>{cooldowns}</small> : null}
        </span>
      </div></td>
      {platforms.map(platform => <td className="stream-rule-group-network" data-platform={platform} key={platform}>
        {group.rules.some(rule => rule.platform === platform) ? group.rules.filter(rule => rule.platform === platform).map(cost)
          : <span className="stream-rule-group-network__empty" aria-label={t("interactions.rules.groups.noValue", "Sin valor")}>—</span>}
      </td>)}
      <td><div className="stream-rule-row__actions" role="group" aria-label={t("interactions.rules.actions.label")}>
        <button type="button" disabled={disabled} aria-label={t("interactions.rules.actions.edit")} title={t("interactions.rules.actions.edit")} onClick={() => onEdit(group)}><Pencil aria-hidden="true" /></button>
        <button type="button" disabled={disabled || duplicateDisabled} aria-label={t("interactions.rules.actions.duplicate")}
          title={t(duplicateDisabled ? "interactions.rules.list.limitReached" : "interactions.rules.actions.duplicate")} onClick={() => onDuplicate(group.ruleIds)}><Copy aria-hidden="true" /></button>
        <button type="button" ref={deleteRef} className="stream-rule-row__delete" disabled={disabled}
          aria-label={t("interactions.rules.actions.delete")} title={t("interactions.rules.actions.delete")} onClick={() => onRequestDelete(group.id)}><Trash2 aria-hidden="true" /></button>
      </div></td>
    </>}
  </tr>;
}

export function StreamRuleGroupsTable({ groups, gifts, canCreate, availableRuleSlots, disabled,
  highlightedRuleId, confirmingDeleteId, deletingRuleId, onCreate, ...actions }: StreamRuleGroupsTableProps) {
  const { t } = useLocalization();
  const giftsById = useMemo(() => new Map(gifts.map(gift => [gift.giftId, gift])), [gifts]);
  if (groups.length === 0) return <div className="stream-rules-table__empty">
    <span aria-hidden="true">+</span><strong>{t("interactions.rules.list.emptyTitle")}</strong>
    <p>{t("interactions.rules.list.emptyDescription")}</p>
    <button type="button" disabled={!canCreate} onClick={onCreate}>{t("interactions.rules.list.createFirst")}</button>
  </div>;
  return <div className="interaction-table-wrap stream-rule-groups-table-wrap">
    <table className="interaction-table stream-rule-groups-table"><thead><tr>
      <th scope="col">{t("interactions.rules.list.status")}</th>
      <th scope="col">{t("interactions.rules.list.result")}</th>
      {platforms.map(platform => <th scope="col" key={platform}><span className="stream-rule-group-network-heading"><StreamPlatformIcon platform={platform} />{t(`dashboard.platforms.${platform}`)}</span></th>)}
      <th scope="col">{t("interactions.rules.list.actions")}</th>
    </tr></thead><tbody>{groups.map(group => <GroupRow key={group.id} group={group} giftsById={giftsById}
      disabled={disabled} duplicateDisabled={group.ruleIds.length > availableRuleSlots}
      highlighted={highlightedRuleId !== null && group.ruleIds.includes(highlightedRuleId)}
      confirmingDelete={confirmingDeleteId === group.id} deleting={deletingRuleId === group.id} {...actions} />)}</tbody></table>
  </div>;
}
