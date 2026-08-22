import type { ConnectionStatus } from "../model";
import { useLocalization } from "../i18n/LocalizationContext";

export function StatusBadge({ status }: { status: ConnectionStatus }) {
  const { t } = useLocalization();
  return (
    <div className="status-badge" data-status={status} role="status" aria-live="polite">
      <span className="status-badge__dot" aria-hidden="true" />
      <span>{t(`status.${status}`)}</span>
    </div>
  );
}
