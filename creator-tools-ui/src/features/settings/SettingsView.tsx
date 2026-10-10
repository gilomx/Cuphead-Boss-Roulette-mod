import { useEffect, useState } from "react";
import { Monitor } from "lucide-react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { DashboardConnection, DashboardState, StreamPlatform } from "../../model";
import { TwitchConnectionControls } from "./TwitchConnectionControls";
import { YouTubeConnectionControls } from "./YouTubeConnectionControls";
import { SettingsTransferPanel } from "./SettingsTransferPanel";

const PLATFORMS: StreamPlatform[] = ["tiktok", "twitch", "youtube"];

export function SettingsView() {
  const { t } = useLocalization();
  const [connections, setConnections] = useState<DashboardConnection[]>([]);
  useEffect(() => {
    const controller = new AbortController();
    let latest = 0;
    const load = async () => {
      const request = ++latest;
      try {
        const response = await fetch("/api/dashboard", { cache: "no-store", signal: controller.signal });
        if (!response.ok) return;
        const data = await response.json() as Partial<DashboardState>;
        if (request === latest && Array.isArray(data.connections)) setConnections(data.connections);
      } catch { /* Each connection retains its last known status. */ }
    };
    void load();
    const timer = window.setInterval(() => void load(), 1250);
    return () => { controller.abort(); window.clearInterval(timer); };
  }, []);

  useEffect(() => {
    const platform = new URLSearchParams(window.location.search).get("connection");
    if (!PLATFORMS.includes(platform as StreamPlatform)) return;
    const frame = window.requestAnimationFrame(() => document.getElementById(`settings-${platform}`)?.focus());
    return () => window.cancelAnimationFrame(frame);
  }, []);

  return (
    <div className="page page--settings">
      <header className="page-header">
        <div><h1>{t("settings.title")}</h1><p>{t("settings.description")}</p></div>
      </header>
      <div className="settings-panels">
      <section className="interaction-panel settings-connections" aria-labelledby="settings-connections-title">
        <header className="interaction-panel__heading"><div><h2 id="settings-connections-title">{t("dashboard.connections.title")}</h2><p>{t("settings.connectionsDescription")}</p></div></header>
        <div className="settings-connection-list">
          {PLATFORMS.map((platform) => {
            const connection = connections.find((entry) => entry.platform === platform);
            const status = connection?.status ?? "pending";
            return (
              <article className="settings-connection" key={platform} data-platform={platform}>
                <header className="settings-connection__header">
                  <h3 id={`settings-${platform}`} tabIndex={-1}>{t(`settings.platforms.${platform}`)}</h3>
                  <span className="dashboard-connection-state" data-status={status}>
                    <span aria-hidden="true" />{t(`dashboard.connectionStatus.${status}`, status)}
                  </span>
                </header>
                {platform === "twitch" ? <TwitchConnectionControls /> : platform === "youtube" ? <YouTubeConnectionControls /> : (
                  <p>{t(`dashboard.connectionDescriptions.tikfinity.${status}`, t("settings.tikfinity"))}</p>
                )}
                {platform === "tiktok" ? (
                  <div className="settings-tikfinity-desktop">
                    <Monitor aria-hidden="true" />
                    <div>
                      <strong>{t("settings.tikfinityDesktopTitle")}</strong>
                      <p>{t("settings.tikfinity")}</p>
                    </div>
                  </div>
                ) : null}
              </article>
            );
          })}
        </div>
      </section>
      <SettingsTransferPanel />
      </div>
    </div>
  );
}
