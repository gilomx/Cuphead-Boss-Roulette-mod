import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type {
  DashboardConnection,
  DashboardCounters,
  DashboardEvent,
  DashboardState,
  StreamEventType,
  StreamPlatform,
} from "../../model";

const EMPTY_COUNTERS: DashboardCounters = {
  received: 0,
  matched: 0,
  queued: 0,
  ignored: 0,
  gifts: 0,
  valued: 0,
  likes: 0,
  follows: 0,
  subscriptions: 0,
};

const EMPTY_STATE: DashboardState = {
  ready: false,
  revision: 0,
  engineStatus: "connecting",
  connections: [],
  counters: EMPTY_COUNTERS,
  events: [],
};

const DEFAULT_CONNECTIONS: DashboardConnection[] = [
  {
    id: "tikfinity",
    platform: "tiktok",
    connector: "tikfinity",
    label: "TikFinity",
    status: "pending",
  },
  {
    id: "twitch",
    platform: "twitch",
    connector: "twitch-eventsub",
    label: "Twitch",
    status: "pending",
  },
  {
    id: "youtube",
    platform: "youtube",
    connector: "youtube-live-chat",
    label: "YouTube",
    status: "pending",
  },
];

const PLATFORMS: StreamPlatform[] = ["tiktok", "twitch", "youtube"];
const EVENT_TYPES: StreamEventType[] = [
  "gift",
  "currency",
  "like",
  "follow",
  "subscription",
  "redemption",
];

interface SimulationDraft {
  platform: StreamPlatform;
  type: StreamEventType;
  user: string;
  amount: number;
  count: number;
  itemName: string;
}

const INITIAL_SIMULATION: SimulationDraft = {
  platform: "tiktok",
  type: "gift",
  user: "",
  amount: 1,
  count: 1,
  itemName: "",
};

function isDashboardState(value: unknown): value is DashboardState {
  if (!value || typeof value !== "object") return false;
  const candidate = value as Partial<DashboardState>;
  return typeof candidate.ready === "boolean" &&
    typeof candidate.revision === "number" &&
    typeof candidate.engineStatus === "string" &&
    Array.isArray(candidate.connections) &&
    Array.isArray(candidate.events) &&
    Boolean(candidate.counters && typeof candidate.counters === "object");
}

function connectionsWithPlaceholders(connections: DashboardConnection[]) {
  const result = [...connections];
  for (const placeholder of DEFAULT_CONNECTIONS) {
    if (!connections.some((connection) => connection.platform === placeholder.platform)) {
      result.push(placeholder);
    }
  }
  return result;
}

function normalizedCounter(value: number | undefined) {
  return Number.isFinite(value) ? Math.max(0, value ?? 0) : 0;
}

export function DashboardView() {
  const { locale, t } = useLocalization();
  const [dashboard, setDashboard] = useState<DashboardState>(EMPTY_STATE);
  const [reachable, setReachable] = useState(true);
  const [simulation, setSimulation] = useState<SimulationDraft>(INITIAL_SIMULATION);
  const [simulationStatus, setSimulationStatus] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const latestDashboardRequest = useRef(0);

  const loadDashboard = useCallback(async (signal?: AbortSignal) => {
    const requestId = latestDashboardRequest.current + 1;
    latestDashboardRequest.current = requestId;
    try {
      const response = await fetch("/api/dashboard", { cache: "no-store", signal });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const payload: unknown = await response.json();
      if (!isDashboardState(payload)) throw new Error("Invalid dashboard payload");
      if (requestId !== latestDashboardRequest.current) return;
      setDashboard((current) => (
        current !== EMPTY_STATE && current.revision === payload.revision ? current : payload
      ));
      setReachable(true);
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") return;
      if (requestId === latestDashboardRequest.current) setReachable(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void loadDashboard(controller.signal);
    const interval = window.setInterval(() => void loadDashboard(controller.signal), 1250);
    return () => {
      controller.abort();
      window.clearInterval(interval);
    };
  }, [loadDashboard]);

  const connections = useMemo(
    () => connectionsWithPlaceholders(dashboard.connections),
    [dashboard.connections],
  );

  const counters = dashboard.counters ?? EMPTY_COUNTERS;
  const counterCards = [
    ["received", counters.received],
    ["matched", counters.matched],
    ["queued", counters.queued],
    ["ignored", counters.ignored],
    ["gifts", counters.gifts],
    ["valued", counters.valued],
    ["likes", counters.likes],
    ["follows", counters.follows],
    ["subscriptions", counters.subscriptions],
  ] as const;

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
    if ((event.type === "currency" || event.type === "gift") &&
        typeof event.amount === "number" && event.amount > 0) {
      const amount = event.amount.toLocaleString(locale === "es" ? "es-MX" : "en-US");
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

  const submitSimulation = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSimulationStatus("sending");
    const query = new URLSearchParams({
      platform: simulation.platform,
      type: simulation.type,
      user: simulation.user.trim(),
      amount: String(Math.max(0, simulation.amount || 0)),
      count: String(Math.max(1, Math.floor(simulation.count) || 1)),
      itemName: simulation.itemName.trim(),
    });

    try {
      const response = await fetch(`/api/dashboard/simulate?${query}`, { cache: "no-store" });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      setSimulationStatus("sent");
      await loadDashboard();
      window.setTimeout(() => setSimulationStatus((current) => current === "sent" ? "idle" : current), 2200);
    } catch {
      setSimulationStatus("error");
    }
  };

  return (
    <div className="page page--dashboard">
      <header className="page-header dashboard-page-header">
        <div>
          <h1>{t("dashboard.title")}</h1>
          <p>{t("dashboard.description")}</p>
        </div>
        <span
          className="dashboard-engine-status"
          data-status={reachable ? dashboard.engineStatus : "error"}
        >
          <span aria-hidden="true" />
          {reachable
            ? t(`dashboard.engine.${dashboard.engineStatus}`, dashboard.engineStatus)
            : t("dashboard.engine.unreachable")}
        </span>
      </header>

      <section className="dashboard-summary" aria-labelledby="dashboard-summary-title">
        <div className="dashboard-section-heading">
          <div>
            <p className="dashboard-eyebrow">{t("dashboard.summary.eyebrow")}</p>
            <h2 id="dashboard-summary-title">{t("dashboard.summary.title")}</h2>
          </div>
          <span>{t("dashboard.summary.session")}</span>
        </div>
        <div className="dashboard-counter-grid">
          {counterCards.map(([key, value]) => (
            <article className="dashboard-counter" key={key} data-counter={key}>
              <strong>{normalizedCounter(value).toLocaleString(locale === "es" ? "es-MX" : "en-US")}</strong>
              <span>{t(`dashboard.counters.${key}`)}</span>
            </article>
          ))}
        </div>
      </section>

      <section className="dashboard-connections" aria-labelledby="dashboard-connections-title">
        <div className="dashboard-section-heading">
          <div>
            <p className="dashboard-eyebrow">{t("dashboard.connections.eyebrow")}</p>
            <h2 id="dashboard-connections-title">{t("dashboard.connections.title")}</h2>
          </div>
        </div>
        <div className="dashboard-connection-grid">
          {connections.map((connection) => (
            <article
              className="dashboard-connection-card"
              key={connection.id}
              data-platform={connection.platform}
            >
              <div className="dashboard-connection-card__header">
                <span className="dashboard-platform-mark" aria-hidden="true">
                  {connection.platform === "tiktok" ? "TK" : connection.platform === "twitch" ? "TW" : "YT"}
                </span>
                <span className="dashboard-connection-state" data-status={connection.status}>
                  <span aria-hidden="true" />
                  {t(`dashboard.connectionStatus.${connection.status}`, connection.status)}
                </span>
              </div>
              <div className="dashboard-connection-card__copy">
                <p>{t(`dashboard.platforms.${connection.platform}`)}</p>
                <h3>{connection.label}</h3>
                <span>{connection.account || t("dashboard.connections.notConfigured")}</span>
              </div>
              <div className="dashboard-connection-card__footer">
                <span>{t("dashboard.connections.lastEvent")}</span>
                <strong>{formatDate(connection.lastEventAt)}</strong>
              </div>
            </article>
          ))}
        </div>
      </section>

      <div className="dashboard-workspace">
        <section className="dashboard-panel dashboard-event-feed" aria-labelledby="dashboard-events-title">
          <div className="dashboard-panel__heading">
            <div>
              <p className="dashboard-eyebrow">{t("dashboard.events.eyebrow")}</p>
              <h2 id="dashboard-events-title">{t("dashboard.events.title")}</h2>
            </div>
            <span className="dashboard-live-indicator" data-active={reachable && dashboard.ready}>
              <span aria-hidden="true" />
              {t("dashboard.events.updatesActive")}
            </span>
          </div>

          {dashboard.events.length === 0 ? (
            <div className="dashboard-events-empty">
              <strong>{t("dashboard.events.emptyTitle")}</strong>
              <span>{t("dashboard.events.emptyDescription")}</span>
            </div>
          ) : (
            <ol className="dashboard-events-list">
              {dashboard.events.map((streamEvent) => (
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
        </section>

        <section className="dashboard-panel dashboard-simulator" aria-labelledby="dashboard-simulator-title">
          <div className="dashboard-panel__heading">
            <div>
              <p className="dashboard-eyebrow">{t("dashboard.simulator.eyebrow")}</p>
              <h2 id="dashboard-simulator-title">{t("dashboard.simulator.title")}</h2>
            </div>
          </div>
          <p className="dashboard-simulator__description">{t("dashboard.simulator.description")}</p>
          <form className="dashboard-simulator-form" onSubmit={(event) => void submitSimulation(event)}>
            <div className="dashboard-simulator-form__row">
              <label>
                <span>{t("dashboard.simulator.platform")}</span>
                <select
                  value={simulation.platform}
                  onChange={(event) => setSimulation((current) => ({
                    ...current,
                    platform: event.target.value as StreamPlatform,
                  }))}
                >
                  {PLATFORMS.map((platform) => (
                    <option key={platform} value={platform}>{t(`dashboard.platforms.${platform}`)}</option>
                  ))}
                </select>
              </label>
              <label>
                <span>{t("dashboard.simulator.type")}</span>
                <select
                  value={simulation.type}
                  onChange={(event) => setSimulation((current) => ({
                    ...current,
                    type: event.target.value as StreamEventType,
                  }))}
                >
                  {EVENT_TYPES.map((type) => (
                    <option key={type} value={type}>{t(`dashboard.eventTypes.${type}`)}</option>
                  ))}
                </select>
              </label>
            </div>
            <label>
              <span>{t("dashboard.simulator.user")}</span>
              <input
                type="text"
                maxLength={64}
                value={simulation.user}
                placeholder={t("dashboard.simulator.userPlaceholder")}
                onChange={(event) => setSimulation((current) => ({ ...current, user: event.target.value }))}
              />
            </label>
            <label>
              <span>{t("dashboard.simulator.itemName")}</span>
              <input
                type="text"
                maxLength={80}
                value={simulation.itemName}
                placeholder={t("dashboard.simulator.itemNamePlaceholder")}
                onChange={(event) => setSimulation((current) => ({ ...current, itemName: event.target.value }))}
              />
            </label>
            <div className="dashboard-simulator-form__row">
              <label>
                <span>{t("dashboard.simulator.amount")}</span>
                <input
                  type="number"
                  min={0}
                  step="any"
                  value={simulation.amount}
                  onChange={(event) => setSimulation((current) => ({
                    ...current,
                    amount: Math.max(0, Number(event.target.value) || 0),
                  }))}
                />
              </label>
              <label>
                <span>{t("dashboard.simulator.count")}</span>
                <input
                  type="number"
                  min={1}
                  max={1000000}
                  value={simulation.count}
                  onChange={(event) => setSimulation((current) => ({
                    ...current,
                    count: Math.max(1, Math.min(1000000, Math.floor(Number(event.target.value)) || 1)),
                  }))}
                />
              </label>
            </div>
            <button type="submit" disabled={simulationStatus === "sending"}>
              {t(`dashboard.simulator.${simulationStatus === "sending" ? "sending" : "submit"}`)}
            </button>
            <p data-status={simulationStatus} role="status" aria-live="polite">
              {simulationStatus === "sent"
                ? t("dashboard.simulator.sent")
                : simulationStatus === "error"
                  ? t("dashboard.simulator.error")
                  : t("dashboard.simulator.hint")}
            </p>
          </form>
        </section>
      </div>
    </div>
  );
}
