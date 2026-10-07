import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { InteractionQueuePanel } from "../interactions/InteractionQueuePanel";
import { DashboardEventsPanel } from "./DashboardEventsPanel";
import { LiveEventsSection } from "./LiveEventsSection";
import { TwitchConnectionControls } from "./TwitchConnectionControls";
import type {
  DashboardConnection,
  DashboardCounters,
  DashboardState,
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
  coins: 0,
  bits: 0,
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
    id: "tikfinity-local",
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

interface DashboardViewProps {
  onOpenInteractions: () => void;
  onOpenPeskyBattle: () => void;
  onOpenTapFarming: () => void;
  onOpenChatChooses: () => void;
}

export function DashboardView({
  onOpenInteractions,
  onOpenPeskyBattle,
  onOpenTapFarming,
  onOpenChatChooses,
}: DashboardViewProps) {
  const { locale, t } = useLocalization();
  const [dashboard, setDashboard] = useState<DashboardState>(EMPTY_STATE);
  const [reachable, setReachable] = useState(true);
  const latestDashboardRequest = useRef(0);
  const peskyBattleCardRef = useRef<HTMLButtonElement>(null);
  const tapFarmingCardRef = useRef<HTMLButtonElement>(null);
  const chatChoosesCardRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const navigationState = window.history.state as {
      focusLiveEvent?: string;
      [key: string]: unknown;
    } | null;
    if (navigationState?.focusLiveEvent !== "peskyBattle" &&
        navigationState?.focusLiveEvent !== "tapFarming" &&
        navigationState?.focusLiveEvent !== "chatChooses") return;

    const frame = window.requestAnimationFrame(() => {
      if (navigationState.focusLiveEvent === "chatChooses") {
        chatChoosesCardRef.current?.focus();
      } else if (navigationState.focusLiveEvent === "tapFarming") {
        tapFarmingCardRef.current?.focus();
      } else {
        peskyBattleCardRef.current?.focus();
      }
    });
    return () => window.cancelAnimationFrame(frame);
  }, []);

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
    ["likes", counters.likes],
    ["follows", counters.follows],
    ["subscriptions", counters.subscriptions],
    ["coins", counters.coins ?? 0],
    ["bits", counters.bits ?? 0],
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

  const connectionDescription = (connection: DashboardConnection) => {
    const fallback = connection.message || connection.account || t("dashboard.connections.notConfigured");
    return connection.connector === "tikfinity"
      ? t(`dashboard.connectionDescriptions.tikfinity.${connection.status}`, fallback)
      : fallback;
  };

  return (
    <div className="page page--dashboard">
      <header className="page-header dashboard-page-header">
        <div>
          <h1>{t("dashboard.title")}</h1>
          <p>{t("dashboard.description")}</p>
        </div>
      </header>

      <section className="dashboard-summary" aria-labelledby="dashboard-summary-title">
        <div className="dashboard-section-heading">
          <div>
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

      <LiveEventsSection
        onOpenPeskyBattle={onOpenPeskyBattle}
        onOpenTapFarming={onOpenTapFarming}
        onOpenChatChooses={onOpenChatChooses}
        chatChoosesCardRef={chatChoosesCardRef}
        peskyBattleCardRef={peskyBattleCardRef}
        tapFarmingCardRef={tapFarmingCardRef}
      />

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
                <h3>{connection.label}</h3>
                <span className="dashboard-connection-state" data-status={connection.status}>
                  <span aria-hidden="true" />
                  {t(`dashboard.connectionStatus.${connection.status}`, connection.status)}
                </span>
              </div>
              {connection.platform === "twitch" ? <TwitchConnectionControls /> : <div className="dashboard-connection-card__copy">
                <span>{connectionDescription(connection)}</span>
              </div>}
              <div className="dashboard-connection-card__footer">
                <span>{t("dashboard.connections.lastEvent")}</span>
                <strong>{formatDate(connection.lastEventAt)}</strong>
              </div>
            </article>
          ))}
        </div>
      </section>

      <div className="dashboard-activity-grid">
        <InteractionQueuePanel
          className="dashboard-interaction-queue"
          onConfigure={onOpenInteractions}
        />
        <DashboardEventsPanel
          events={dashboard.events}
          live={reachable && dashboard.ready}
          onSimulationSubmitted={loadDashboard}
        />
      </div>
    </div>
  );
}
