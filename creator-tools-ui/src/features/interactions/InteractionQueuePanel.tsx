import type { MouseEvent } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItemFor } from "./interactionCatalog";

interface InteractionQueuePanelProps {
  className?: string;
  onConfigure?: () => void;
}

export function InteractionQueuePanel({ className, onConfigure }: InteractionQueuePanelProps) {
  const { interaction, optimisticInteractionQueue } = useConfig();
  const { t } = useLocalization();
  const available = interaction?.available ?? false;
  const queue = [
    ...(interaction?.queue ?? []),
    ...optimisticInteractionQueue,
  ];
  const classes = ["interaction-panel", "interaction-queue", className]
    .filter(Boolean)
    .join(" ");

  return (
    <section className={classes} aria-labelledby="interaction-queue-title">
      <div className="interaction-panel__heading">
        <div>
          <h2 id="interaction-queue-title">{t("interactions.queue.title")}</h2>
          <p>{t("interactions.queue.description")}</p>
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
        </div>
        <span className="interaction-count" aria-label={t("interactions.queue.countLabel")}>
          {queue.length}
        </span>
      </div>

      {queue.length === 0 ? (
        <div className="interaction-queue__empty">
          <strong>{t("interactions.queue.emptyTitle")}</strong>
          <span>{t("interactions.queue.emptyDescription")}</span>
        </div>
      ) : (
        <div className="interaction-table-wrap">
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
        </div>
      )}
    </section>
  );
}
