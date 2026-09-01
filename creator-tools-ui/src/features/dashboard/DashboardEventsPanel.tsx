import { useEffect, useRef, useState } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { DashboardEvent } from "../../model";
import {
  DashboardSimulatorForm,
  type DashboardSimulationResult,
} from "./DashboardSimulatorForm";

const SIMULATION_CONFIRMATION_DURATION_MS = 5_000;
const MAXIMUM_VISIBLE_EVENT_COUNT = 30;

interface DashboardEventsPanelProps {
  events: DashboardEvent[];
  live: boolean;
  onSimulationSubmitted: () => void | Promise<void>;
}

type DashboardEventsView = "events" | "simulator";

export function DashboardEventsPanel({
  events,
  live,
  onSimulationSubmitted,
}: DashboardEventsPanelProps) {
  const { locale, t } = useLocalization();
  const [view, setView] = useState<DashboardEventsView>("events");
  const [submittedDelaySeconds, setSubmittedDelaySeconds] = useState<number | null>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const previousView = useRef<DashboardEventsView>(view);
  const confirmationTimer = useRef<number | null>(null);
  const simulatorActive = view === "simulator";
  const recentEvents = events.slice(0, MAXIMUM_VISIBLE_EVENT_COUNT);

  useEffect(() => {
    if (previousView.current === view) return;
    previousView.current = view;
    titleRef.current?.focus({ preventScroll: true });
  }, [view]);

  useEffect(() => () => {
    if (confirmationTimer.current !== null) window.clearTimeout(confirmationTimer.current);
  }, []);

  const clearSimulationConfirmation = () => {
    if (confirmationTimer.current !== null) {
      window.clearTimeout(confirmationTimer.current);
      confirmationTimer.current = null;
    }
    setSubmittedDelaySeconds(null);
  };

  const handleSimulationSubmitted = async ({ delaySeconds }: DashboardSimulationResult) => {
    if (confirmationTimer.current !== null) window.clearTimeout(confirmationTimer.current);
    setSubmittedDelaySeconds(delaySeconds);
    setView("events");
    confirmationTimer.current = window.setTimeout(() => {
      setSubmittedDelaySeconds(null);
      confirmationTimer.current = null;
    }, SIMULATION_CONFIRMATION_DURATION_MS);
    await onSimulationSubmitted();
  };

  const simulationConfirmation = submittedDelaySeconds === null
    ? null
    : submittedDelaySeconds > 0
      ? t("dashboard.simulator.scheduled").replace(
          "{seconds}",
          submittedDelaySeconds.toLocaleString(locale === "es" ? "es-MX" : "en-US"),
        )
      : t("dashboard.simulator.sent");

  const formatDate = (value?: string | null) => {
    if (!value) return t("dashboard.time.never");
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return t("dashboard.time.never");
    return new Intl.DateTimeFormat(locale === "es" ? "es-MX" : "en-US", {
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    }).format(date);
  };

  const eventSummary = (event: DashboardEvent) => {
    if (event.itemName) {
      return `${event.count && event.count > 1 ? `${event.count} × ` : ""}${event.itemName}`;
    }
    const eventValue = typeof event.totalValue === "number"
      ? event.totalValue
      : event.amount;
    if ((event.type === "currency" || event.type === "gift") &&
        typeof eventValue === "number" && eventValue > 0) {
      const amount = eventValue.toLocaleString(locale === "es" ? "es-MX" : "en-US");
      const unit = event.currency || (event.unit
        ? t(`dashboard.units.${event.unit}`, event.unit)
        : t("dashboard.events.value"));
      return `${amount} ${unit}`;
    }
    if (event.count && event.count > 1) {
      return `${event.count.toLocaleString(locale === "es" ? "es-MX" : "en-US")} · ${t(`dashboard.eventTypes.${event.type}`, event.type)}`;
    }
    return t(`dashboard.eventTypes.${event.type}`, event.type);
  };

  return (
    <section
      className="dashboard-panel dashboard-event-panel"
      data-view={view}
      aria-labelledby="dashboard-event-panel-title"
    >
      <div className="dashboard-panel__heading dashboard-event-panel__heading">
        <div>
          <p className="dashboard-eyebrow">
            {t(simulatorActive ? "dashboard.simulator.eyebrow" : "dashboard.events.eyebrow")}
          </p>
          <h2 id="dashboard-event-panel-title" ref={titleRef} tabIndex={-1}>
            {t(simulatorActive ? "dashboard.simulator.title" : "dashboard.events.title")}
          </h2>
        </div>
        <div className="dashboard-event-panel__actions">
          {!simulatorActive ? (
            <span className="dashboard-live-indicator" data-active={live}>
              <span aria-hidden="true" />
              {t("dashboard.events.updatesActive")}
            </span>
          ) : null}
          <button
            className="dashboard-event-panel__switch"
            type="button"
            aria-controls="dashboard-event-panel-content"
            aria-pressed={simulatorActive}
            onClick={() => {
              if (simulatorActive) {
                setView("events");
                return;
              }
              clearSimulationConfirmation();
              setView("simulator");
            }}
          >
            <span aria-hidden="true">{simulatorActive ? "←" : "+"}</span>
            {t(simulatorActive ? "dashboard.simulator.back" : "dashboard.events.simulate")}
          </button>
        </div>
      </div>

      <div id="dashboard-event-panel-content">
        <div
          className="dashboard-event-panel__view"
          data-direction="back"
          hidden={simulatorActive}
        >
          {simulationConfirmation ? (
            <div
              className="dashboard-event-panel__confirmation"
              role="status"
              aria-live="polite"
              aria-atomic="true"
            >
              <span aria-hidden="true">✓</span>
              <strong>{simulationConfirmation}</strong>
            </div>
          ) : null}
          {recentEvents.length === 0 ? (
            <div className="dashboard-events-empty">
              <strong>{t("dashboard.events.emptyTitle")}</strong>
              <span>{t("dashboard.events.emptyDescription")}</span>
            </div>
          ) : (
            <ol className="dashboard-events-list">
              {recentEvents.map((streamEvent) => (
                <li key={streamEvent.id} data-platform={streamEvent.platform}>
                  <div className="dashboard-event__time">
                    <span>{formatDate(streamEvent.receivedAt)}</span>
                    <strong>{t(`dashboard.platforms.${streamEvent.platform}`)}</strong>
                  </div>
                  <div className="dashboard-event__content">
                    <div>
                      <strong>{streamEvent.user || t("dashboard.events.community")}</strong>
                      <span className="dashboard-event-type">
                        {t(`dashboard.eventTypes.${streamEvent.type}`, streamEvent.type)}
                      </span>
                    </div>
                    <p>{eventSummary(streamEvent)}</p>
                    {streamEvent.rule || streamEvent.action ? (
                      <small>
                        {streamEvent.rule ? `${t("dashboard.events.rule")}: ${streamEvent.rule}` : ""}
                        {streamEvent.rule && streamEvent.action ? " · " : ""}
                        {streamEvent.action ? `${t("dashboard.events.action")}: ${streamEvent.action}` : ""}
                      </small>
                    ) : null}
                  </div>
                  <span className="dashboard-event-status" data-status={streamEvent.status}>
                    {t(`dashboard.eventStatus.${streamEvent.status}`, streamEvent.status)}
                  </span>
                </li>
              ))}
            </ol>
          )}
        </div>

        <div
          className="dashboard-event-panel__view"
          data-direction="forward"
          hidden={!simulatorActive}
        >
          <DashboardSimulatorForm
            active={simulatorActive}
            onSubmitted={handleSimulationSubmitted}
          />
        </div>
      </div>
    </section>
  );
}
