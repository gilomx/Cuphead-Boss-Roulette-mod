import { useCallback, useEffect, useRef, useState } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";

interface TwitchConnectionState {
  ready: boolean;
  status: string;
  authorized: boolean;
  account?: string;
  messageCode?: string;
  userCode?: string;
  verificationUri?: string;
  expiresAt?: string;
  commandPending?: boolean;
  controlToken?: string;
}

function verificationUrl(value?: string) {
  if (!value) return undefined;
  try {
    const url = new URL(value);
    return url.protocol === "https:" && url.hostname === "www.twitch.tv" &&
      (!url.port || url.port === "443") && url.pathname === "/activate" &&
      !url.username && !url.password ? url.href : undefined;
  } catch { return undefined; }
}

export function TwitchConnectionControls() {
  const { t } = useLocalization();
  const [state, setState] = useState<TwitchConnectionState>({ ready: false, status: "disconnected", authorized: false });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(false);
  const [confirmingDisconnect, setConfirmingDisconnect] = useState(false);
  const [now, setNow] = useState(Date.now());
  const latestRequest = useRef(0);
  const load = useCallback(async (signal?: AbortSignal) => {
    const request = ++latestRequest.current;
    try {
      const response = await fetch("/api/twitch", { cache: "no-store", signal });
      if (!response.ok) throw new Error("Unavailable");
      const data = await response.json() as TwitchConnectionState;
      if (typeof data.ready !== "boolean" || typeof data.authorized !== "boolean") throw new Error("Invalid state");
      if (request === latestRequest.current) { setNow(Date.now()); setState(data); }
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
    setBusy(true); setError(false); setConfirmingDisconnect(false);
    try {
      const response = await fetch(`/api/twitch/${action}`, {
        method: "POST", headers: { "X-Pichi-Twitch-Control": state.controlToken ?? "" }, body: "",
      });
      if (!response.ok) throw new Error("Command rejected");
      await load();
    } catch { setError(true); }
    finally { setBusy(false); }
  };

  const disabled = !state.ready || busy || state.commandPending;
  const url = verificationUrl(state.verificationUri);
  const authorizing = Boolean(state.userCode && url && Date.parse(state.expiresAt ?? "") > now);
  return (
    <div className="twitch-connection-controls" aria-label={t("dashboard.twitch.title")}>
      <p role="status">{t(`dashboard.twitch.messages.${state.messageCode ?? "companion_starting"}`,
        t("dashboard.twitch.messages.connection_error"))}</p>
      {state.account ? <strong className="twitch-connection-controls__account">@{state.account}</strong> : null}
      {authorizing ? (
        <div className="twitch-connection-controls__authorization">
          <p>{t("dashboard.twitch.authorizationInstructions")}</p>
          <code>{state.userCode}</code>
          <a href={url} target="_blank" rel="noopener noreferrer">{t("dashboard.twitch.openTwitch")}</a>
          <small>{t("dashboard.twitch.expires").replace("{minutes}", String(Math.max(1,
            Math.ceil((Date.parse(state.expiresAt ?? "") - now) / 60000))))}</small>
        </div>
      ) : null}
      <div className="twitch-connection-controls__actions">
        {authorizing || state.messageCode === "requesting_code" ? (
          <button type="button" disabled={disabled} onClick={() => void command("cancel")}>{t("dashboard.twitch.cancel")}</button>
        ) : state.authorized ? (
          <>
            <button type="button" disabled={disabled} onClick={() => void command("connect")}>{t("dashboard.twitch.changeAccount")}</button>
            <button type="button" disabled={disabled} onClick={() => setConfirmingDisconnect(true)}>{t("dashboard.twitch.disconnect")}</button>
          </>
        ) : (
          <button type="button" disabled={disabled} onClick={() => void command("connect")}>{t("dashboard.twitch.connect")}</button>
        )}
      </div>
      {confirmingDisconnect ? (
        <div className="twitch-connection-controls__confirmation" role="group" aria-label={t("dashboard.twitch.disconnect")}>
          <p>{t("dashboard.twitch.disconnectConfirmation")}</p>
          <button type="button" onClick={() => setConfirmingDisconnect(false)}>{t("dashboard.twitch.keepAccount")}</button>
          <button type="button" disabled={disabled} onClick={() => void command("disconnect")}>{t("dashboard.twitch.confirmDisconnect")}</button>
        </div>
      ) : null}
      {error ? <p role="alert">{t("dashboard.twitch.commandError")}</p> : null}
      <small>{t("dashboard.twitch.storageHint")}</small>
    </div>
  );
}
