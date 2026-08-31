import { Pause, Play, Trash2, X } from "lucide-react";
import { useEffect, useRef, useState, type MouseEvent } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItemFor } from "./interactionCatalog";

interface InteractionQueuePanelProps {
  className?: string;
  onConfigure?: () => void;
}

export function InteractionQueuePanel({ className, onConfigure }: InteractionQueuePanelProps) {
  const {
    interaction,
    optimisticInteractionQueue,
    applyInteractionQueuePaused,
    clearPendingInteractions,
  } = useConfig();
  const { locale, t } = useLocalization();
  const [confirmingClear, setConfirmingClear] = useState(false);
  const confirmationRef = useRef<HTMLDivElement>(null);
  const available = interaction?.available ?? false;
  const enabled = interaction?.interactionsEnabled ?? false;
  const paused = interaction?.queuePaused ?? false;
  const queue = [
    ...(interaction?.queue ?? []),
    ...optimisticInteractionQueue,
  ];
  const classes = ["interaction-panel", "interaction-queue", className]
    .filter(Boolean)
    .join(" ");
  const activeCount = interaction?.activeCount ?? 0;
  const backlogCount = Math.max(0, interaction?.backlogCount ?? 0);
  const pendingCount = Math.max(
    0,
    (interaction?.pendingCount ?? 0) +
      backlogCount +
      optimisticInteractionQueue.length,
  );
  const formatCount = (value: number) =>
    value.toLocaleString(locale === "es" ? "es-MX" : "en-US");

  useEffect(() => {
    if (!confirmingClear) return;
    const cancelOnOutsideClick = (event: PointerEvent) => {
      if (!confirmationRef.current?.contains(event.target as Node)) {
        setConfirmingClear(false);
      }
    };
    const cancelOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setConfirmingClear(false);
    };
    document.addEventListener("pointerdown", cancelOnOutsideClick);
    document.addEventListener("keydown", cancelOnEscape);
    return () => {
      document.removeEventListener("pointerdown", cancelOnOutsideClick);
      document.removeEventListener("keydown", cancelOnEscape);
    };
  }, [confirmingClear]);

  return (
    <section className={classes} aria-labelledby="interaction-queue-title">
      <div className="interaction-panel__heading">
        <div>
          <h2 id="interaction-queue-title">{t("interactions.queue.title")}</h2>
          <p>
            {t("interactions.queue.description")}
            {onConfigure ? (
              <a
                className="interaction-queue__configure"
                href="/config/interactions"
                onClick={(event: MouseEvent<HTMLAnchorElement>) => {
                  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
                    return;
                  }
                  event.preventDefault();
                  onConfigure();
                }}
              >
                {t("interactions.queue.configure")}
                <span aria-hidden="true">→</span>
              </a>
            ) : null}
          </p>
        </div>
        <div className="interaction-queue__heading-actions">
          <span className="interaction-queue__totals" aria-label={t("interactions.queue.countLabel")}>
            <strong>{formatCount(activeCount)}</strong>
            <span>{t("interactions.queue.activeShort")}</span>
            <i aria-hidden="true">·</i>
            <strong>{formatCount(pendingCount)}</strong>
            <span>{t("interactions.queue.pendingShort")}</span>
          </span>
          <button
            className="interaction-queue__round-action interaction-queue__pause"
            type="button"
            data-paused={paused}
            disabled={!interaction?.ready || !enabled}
            aria-label={t(paused
              ? "interactions.queue.resume"
              : "interactions.queue.pause")}
            title={t(paused
              ? "interactions.queue.resume"
              : "interactions.queue.pause")}
            onClick={() => applyInteractionQueuePaused(!paused)}
          >
            {paused ? <Play aria-hidden="true" /> : <Pause aria-hidden="true" />}
          </button>
          <div className="interaction-queue__clear-control" ref={confirmationRef}>
            <button
              className="interaction-queue__round-action interaction-queue__clear"
              type="button"
              disabled={!interaction?.ready || pendingCount === 0}
              aria-label={t("interactions.queue.clear")}
              title={t("interactions.queue.clear")}
              aria-expanded={confirmingClear}
              onClick={() => setConfirmingClear((current) => !current)}
            >
              <Trash2 aria-hidden="true" />
            </button>
            {confirmingClear ? (
              <div className="interaction-queue__clear-confirmation" role="dialog" aria-modal="false">
                <div>
                  <strong>{t("interactions.queue.clearConfirmTitle").replace(
                    "{count}",
                    formatCount(pendingCount),
                  )}</strong>
                  <span>{t("interactions.queue.clearConfirmDescription")}</span>
                </div>
                <button
                  className="interaction-queue__confirmation-action"
                  type="button"
                  aria-label={t("interactions.queue.clearCancel")}
                  title={t("interactions.queue.clearCancel")}
                  onClick={() => setConfirmingClear(false)}
                >
                  <X aria-hidden="true" />
                </button>
                <button
                  className="interaction-queue__confirmation-action interaction-queue__confirmation-action--danger"
                  type="button"
                  aria-label={t("interactions.queue.clearSubmit")}
                  title={t("interactions.queue.clearSubmit")}
                  onClick={() => {
                    clearPendingInteractions();
                    setConfirmingClear(false);
                  }}
                >
                  <Trash2 aria-hidden="true" />
                </button>
              </div>
            ) : null}
          </div>
        </div>
      </div>

      {paused ? (
        <div className="interaction-queue__paused-notice" role="status">
          <Pause aria-hidden="true" />
          <span>{t("interactions.queue.pausedDescription")}</span>
        </div>
      ) : null}

      {queue.length === 0 && backlogCount === 0 ? (
        <div className="interaction-queue__empty">
          <strong>{t("interactions.queue.emptyTitle")}</strong>
          <span>{t("interactions.queue.emptyDescription")}</span>
        </div>
      ) : (
        <div className="interaction-table-wrap interaction-queue__table-wrap">
          <table className="interaction-table queue-table">
            <thead>
              <tr>
                <th scope="col">{t("interactions.queue.position")}</th>
                <th scope="col">{t("interactions.queue.item")}</th>
                <th scope="col">{t("interactions.queue.donor")}</th>
                <th scope="col">{t("interactions.queue.status")}</th>
              </tr>
            </thead>
            <tbody>
              {queue.map((entry, index) => {
                const item = interactionItemFor(entry.item);
                const displayStatus = entry.status === "queued" && !available
                  ? "waiting_game"
                  : entry.status;
                return (
                  <tr key={entry.id}>
                    <td className="queue-table__position">{index + 1}</td>
                    <td>
                      <div className="interaction-item-label">
                        {item ? <img src={item.image} alt="" /> : null}
                        <span>{item ? t(item.titleKey) : entry.item}</span>
                      </div>
                    </td>
                    <td className="queue-table__donor">{entry.donor}</td>
                    <td>
                      <span className="queue-status" data-status={displayStatus}>
                        {t(`interactions.queue.${displayStatus}`)}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          {backlogCount > 0 ? (
            <p className="interaction-queue__backlog" role="status">
              {t("interactions.queue.morePending").replace(
                "{count}",
                formatCount(backlogCount),
              )}
            </p>
          ) : null}
        </div>
      )}
    </section>
  );
}
