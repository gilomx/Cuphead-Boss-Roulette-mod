import { useState } from "react";
import { Settings } from "lucide-react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItemFor, interactionItems } from "../interactions/interactionCatalog";
import { PeskyInteractionCatalog } from "./PeskyInteractionCatalog";
import { PeskySettingsPanel } from "./PeskySettingsPanel";

type PeskyWorkspaceView = "queue" | "settings";

export function PeskyModeView() {
  const { interaction, pesky, applyPeskyEnabled } = useConfig();
  const { t } = useLocalization();
  const [workspaceView, setWorkspaceView] = useState<PeskyWorkspaceView>("queue");
  const disabledItems = new Set(pesky?.disabledItems ?? []);
  const enabledItemCount = interactionItems.filter(
    (item) => (pesky?.items ?? []).includes(item.id) && !disabledItems.has(item.id),
  ).length;
  const blockedByPeskyBattle = pesky?.blockedByPeskyBattle ?? false;
  const canEnable = (pesky?.ready ?? false) &&
    !blockedByPeskyBattle && enabledItemCount > 0;
  const queue = pesky?.queue ?? [];
  const statusKey = pesky?.running
    ? "running"
    : pesky?.enabled && pesky?.startingBattle
      ? "startingBattle"
      : pesky?.enabled
        ? "waitingGame"
        : "disabled";
  const showInteractionsNotice = (pesky?.enabled ?? false) &&
    (interaction?.interactionsEnabled ?? false);

  return (
    <div className="page page--pesky">
      <header className="page-header pesky-page-header">
        <div>
          <h1>{t("pesky.title")}</h1>
          <p>{t("pesky.description")}</p>
        </div>
      </header>

      <section className="pesky-hero mode-switch-shell"
        data-active={pesky?.enabled ?? false} data-step="control">
        <div className="mode-switch-stage">
          <div className="mode-switch-pane mode-switch-pane--primary pesky-hero__pane">
            <div className="pesky-hero__copy">
              <span className="pesky-status" data-status={statusKey}>
                {t(`pesky.status.${statusKey}`)}
              </span>
              <h2>{t("pesky.control.title")}</h2>
              <p className="pesky-feedback" data-error={pesky?.error ?? false}
                role="status" aria-live="polite">
                {t(`pesky.feedback.${pesky?.feedback ?? "ready"}`)}
              </p>
            </div>
            <button className="pesky-toggle" type="button"
              aria-pressed={pesky?.enabled ?? false}
              aria-describedby={blockedByPeskyBattle ? "pesky-battle-block-notice" : undefined}
              data-active={pesky?.enabled ?? false}
              disabled={!pesky?.ready || (!(pesky?.enabled ?? false) && !canEnable)}
              onClick={() => applyPeskyEnabled(!(pesky?.enabled ?? false))}>
              {t(`pesky.control.${pesky?.enabled ? "disable" : "enable"}`)}
            </button>
          </div>
        </div>
      </section>

      {blockedByPeskyBattle ? (
        <p className="pesky-interactions-notice" id="pesky-battle-block-notice" role="status">
          {t("pesky.battleBlocked")}
        </p>
      ) : null}

      {showInteractionsNotice ? (
        <p className="pesky-interactions-notice" role="status">
          {t("pesky.interactionsNotice")}
        </p>
      ) : null}

      <div className="interaction-workspace pesky-workspace">
        <div className="interaction-workspace__view" data-view={workspaceView}
          key={workspaceView}>
          {workspaceView === "settings" ? (
            <PeskySettingsPanel onBack={() => setWorkspaceView("queue")} />
          ) : (
            <>
              <section className="interaction-panel interaction-queue"
                aria-labelledby="pesky-queue-title">
                <div className="interaction-panel__heading">
                  <div>
                    <h2 id="pesky-queue-title">{t("pesky.queue.title")}</h2>
                    <p>{t("pesky.queue.description")}</p>
                  </div>
                  <div className="stream-rules-panel__tools">
                    <span className="interaction-count"
                      aria-label={t("pesky.queue.countLabel")}>{queue.length}</span>
                    <button type="button" className="stream-rule-tool stream-rule-settings"
                      onClick={() => setWorkspaceView("settings")}
                      aria-label={t("pesky.intervals.open")}
                      title={t("pesky.intervals.open")}>
                      <Settings aria-hidden="true" />
                    </button>
                  </div>
                </div>

                {queue.length === 0 ? (
                  <div className="interaction-queue__empty">
                    <strong>{t("pesky.queue.emptyTitle")}</strong>
                    <span>{t("pesky.queue.emptyDescription")}</span>
                  </div>
                ) : (
                  <div className="interaction-table-wrap">
                    <table className="interaction-table queue-table">
                      <thead>
                        <tr>
                          <th scope="col">{t("pesky.queue.position")}</th>
                          <th scope="col">{t("pesky.queue.item")}</th>
                          <th scope="col">{t("pesky.queue.name")}</th>
                          <th scope="col">{t("pesky.queue.status")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {queue.map((entry, index) => {
                          const item = interactionItemFor(entry.item);
                          const displayStatus = entry.countingDown
                            ? "countdown"
                            : entry.status === "queued" && !pesky?.available
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
                                  {t(`pesky.queue.${displayStatus}`)}
                                  {entry.countingDown
                                    ? ` · ${entry.countdownRemaining ?? 0} s`
                                    : entry.remainingSeconds !== undefined
                                      ? ` · ${entry.remainingSeconds} s`
                                      : ""}
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

              <PeskyInteractionCatalog />
            </>
          )}
        </div>
      </div>
    </div>
  );
}
