import { useCallback, useEffect, useRef, useState } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";

interface YouTubeConnectionState {
  ready: boolean;
  authorized: boolean;
  account?: string;
  messageCode?: string;
  verificationUri?: string;
  expiresAt?: string;
  commandPending?: boolean;
  controlToken?: string;
}

function authorizationUrl(value?: string) {
  try {
    const url = new URL(value ?? "");
    return url.protocol === "https:" && url.hostname === "accounts.google.com" &&
      (!url.port || url.port === "443") && url.pathname === "/o/oauth2/v2/auth" &&
      !url.username && !url.password && !url.hash ? url.href : undefined;
  } catch { return undefined; }
}

export function YouTubeConnectionControls() {
  const { t } = useLocalization();
  const [state, setState] = useState<YouTubeConnectionState>({ ready: false, authorized: false });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  const [confirmingDisconnect, setConfirmingDisconnect] = useState(false);
  const [now, setNow] = useState(Date.now());
  const latestRequest = useRef(0);
  const pendingSince = useRef(0);
  const load = useCallback(async (signal?: AbortSignal) => {
    const request = ++latestRequest.current;
    try {
      const response = await fetch("/api/youtube", { cache: "no-store", signal });
      if (!response.ok) throw new Error("Unavailable");
      const data = await response.json() as YouTubeConnectionState;
      if (typeof data.ready !== "boolean" || typeof data.authorized !== "boolean") throw new Error("Invalid state");
      if (request === latestRequest.current) {
        if (data.commandPending && !pendingSince.current) pendingSince.current = Date.now();
        if (!data.commandPending) pendingSince.current = 0;
        setNow(Date.now()); setState(data);
      }
    } catch (failure) {
      if (failure instanceof DOMException && failure.name === "AbortError") return;
      if (request === latestRequest.current) setState((current) => ({ ...current, ready: false }));
    }
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    const timer = window.setInterval(() => { setNow(Date.now()); void load(controller.signal); }, 1250);
    return () => { controller.abort(); window.clearInterval(timer); };
  }, [load]);

  const command = async (action: "connect" | "disconnect" | "cancel") => {
    pendingSince.current = Date.now(); setBusy(true); setError(false); setConfirmingDisconnect(false);
    const deadline = new AbortController();
    const timer = window.setTimeout(() => deadline.abort(), 10000);
    try {
      const response = await fetch(`/api/youtube/${action}`, {
        method: "POST", headers: { "X-Pichi-YouTube-Control": state.controlToken ?? "" }, body: "", signal: deadline.signal,
      });
      if (!response.ok) throw new Error("Command rejected");
      await load(deadline.signal);
    } catch { setError(true); }
    finally { window.clearTimeout(timer); setBusy(false); }
  };

  const timedOut = Boolean(state.commandPending && pendingSince.current && now - pendingSince.current >= 10000);
  const disabled = !state.ready || busy || Boolean(state.commandPending && !timedOut);
  const url = authorizationUrl(state.verificationUri);
  const authorizing = Boolean(state.ready && url && Date.parse(state.expiresAt ?? "") > now);
  const preparing = state.messageCode === "requesting_authorization" || state.messageCode === "mock_authorization_pending";
  return (
    <div className="twitch-connection-controls" aria-label={t("settings.youtube.title")}>
      <p role="status">{busy || (state.commandPending && !timedOut)
        ? t("dashboard.twitch.waitingCommand")
        : t(`settings.youtube.messages.${state.ready ? state.messageCode ?? "companion_starting" : "companion_starting"}`,
          t("settings.youtube.messages.connection_error"))}</p>
      {timedOut ? <p role="alert">{t("dashboard.twitch.commandTimeout")}</p> : null}
      {state.account ? <strong className="twitch-connection-controls__account">{state.account}</strong> : null}
      {authorizing ? (
        <div className="twitch-connection-controls__authorization">
          <p>{t("settings.youtube.authorizationInstructions")}</p>
          <a href={url} target="_blank" rel="noopener noreferrer">{t("settings.youtube.openGoogle")}</a>
          <small>{t("settings.youtube.expires").replace("{minutes}", String(Math.max(1,
            Math.ceil((Date.parse(state.expiresAt ?? "") - now) / 60000))))}</small>
        </div>
      ) : null}
      <div className="twitch-connection-controls__actions">
        {authorizing || preparing ? (
          <button type="button" disabled={disabled} onClick={() => void command("cancel")}>{t("dashboard.twitch.cancel")}</button>
        ) : state.authorized ? (
          <button type="button" disabled={disabled} onClick={() => setConfirmingDisconnect(true)}>{t("dashboard.twitch.disconnect")}</button>
        ) : (
          <button type="button" disabled={disabled || state.messageCode === "application_not_configured"}
            onClick={() => void command("connect")}>{t("settings.youtube.connect")}</button>
        )}
      </div>
      {confirmingDisconnect ? (
        <div className="twitch-connection-controls__confirmation" role="group" aria-label={t("dashboard.twitch.disconnect")}>
          <p>{t("settings.youtube.disconnectConfirmation")}</p>
          <button type="button" onClick={() => setConfirmingDisconnect(false)}>{t("dashboard.twitch.keepAccount")}</button>
          <button type="button" disabled={disabled} onClick={() => void command("disconnect")}>{t("dashboard.twitch.confirmDisconnect")}</button>
        </div>
      ) : null}
      {error ? <p role="alert">{t("dashboard.twitch.commandError")}</p> : null}
      <small>{t("dashboard.twitch.storageHint")}</small>
    </div>
  );
}
