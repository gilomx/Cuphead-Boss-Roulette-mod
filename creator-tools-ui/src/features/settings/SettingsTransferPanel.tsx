import { useEffect, useRef, useState } from "react";
import { Download, Upload } from "lucide-react";
import { useLocalization, type Locale } from "../../i18n/LocalizationContext";

interface TransferState { ready: boolean; pendingImport: boolean; controlToken?: string }
interface SelectedConfiguration { name: string; body: string; locale: Locale }
const MAXIMUM_BYTES = 1024 * 1024;

export function SettingsTransferPanel() {
  const { t, locale, setLocale } = useLocalization();
  const [state, setState] = useState<TransferState>({ ready: false, pendingImport: false });
  const [selected, setSelected] = useState<SelectedConfiguration | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const selectionVersion = useRef(0);

  useEffect(() => {
    const controller = new AbortController();
    let latest = 0;
    const load = async () => {
      const request = ++latest;
      try {
        const response = await fetch("/api/settings", { cache: "no-store", signal: controller.signal });
        if (!response.ok) throw new Error();
        const data = await response.json() as TransferState;
        if (request === latest) setState(data);
      } catch { if (!controller.signal.aborted && request === latest) setState((current) => ({ ...current, ready: false })); }
    };
    void load();
    const timer = window.setInterval(() => void load(), 1500);
    return () => { controller.abort(); window.clearInterval(timer); };
  }, []);

  const chooseFile = async (file?: File) => {
    const version = ++selectionVersion.current;
    setSelected(null); setMessage(""); setError(false);
    if (!file) return;
    try {
      if (file.size > MAXIMUM_BYTES) throw new Error();
      const body = await file.text();
      const document = JSON.parse(body);
      if (document.format !== "la-pichi-ruleta-settings" || document.schemaVersion !== 1 ||
        !document.files || typeof document.files !== "object" || !["es", "en"].includes(document.panelLocale)) throw new Error();
      if (version === selectionVersion.current) setSelected({ name: file.name, body, locale: document.panelLocale });
    } catch { if (version === selectionVersion.current) { setError(true); setMessage("invalidFile"); } }
  };

  const action = async (operation: "export" | "import" | "cancel-import") => {
    setBusy(true); setMessage(""); setError(false);
    const controller = new AbortController();
    const timer = window.setTimeout(() => controller.abort(), 15000);
    try {
      const response = await fetch(`/api/settings/${operation}`, {
        method: "POST", signal: controller.signal,
        headers: { "Content-Type": "application/json", "X-Pichi-Settings-Control": state.controlToken ?? "" },
        body: operation === "import" ? selected?.body ?? "" : "",
      });
      if (!response.ok) {
        const failure = await response.json().catch(() => ({}));
        throw new Error(failure.error === "invalid_configuration" ? "invalidFile" : "requestError");
      }
      if (operation === "export") {
        const document = await response.json();
        document.panelLocale = locale;
        const url = URL.createObjectURL(new Blob([JSON.stringify(document, null, 2)], { type: "application/json" }));
        const anchor = window.document.createElement("a");
        anchor.href = url; anchor.download = `la-pichi-ruleta-config-${new Date().toISOString().slice(0, 10)}.json`;
        window.document.body.append(anchor); anchor.click(); anchor.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        setMessage("exported");
      } else {
        const data = await response.json() as TransferState;
        setState((current) => ({ ...current, ...data }));
        if (operation === "import" && selected) setLocale(selected.locale);
        setSelected(null);
        if (fileInput.current) fileInput.current.value = "";
        setMessage(operation === "import" ? "imported" : "cancelled");
      }
    } catch (failure) {
      setError(true); setMessage(failure instanceof Error && failure.message === "invalidFile" ? "invalidFile" : "requestError");
    } finally { window.clearTimeout(timer); setBusy(false); }
  };

  const disabled = busy || !state.ready;
  return (
    <section className="interaction-panel settings-transfer" aria-labelledby="settings-transfer-title" aria-busy={busy}>
      <header className="interaction-panel__heading"><div><h2 id="settings-transfer-title">{t("settings.transfer.title")}</h2><p>{t("settings.transfer.description")}</p></div></header>
      <div className="settings-transfer__content">
        <p>{t("settings.transfer.includes")}</p>
        <p className="settings-transfer__hint">{t("settings.transfer.connectionsExcluded")}</p>
        <div className="settings-transfer__actions">
          <button type="button" disabled={disabled} onClick={() => void action("export")}><Download size={18} aria-hidden="true" />{t("settings.transfer.export")}</button>
          <button type="button" disabled={disabled} onClick={() => fileInput.current?.click()}><Upload size={18} aria-hidden="true" />{t("settings.transfer.selectFile")}</button>
          <input ref={fileInput} type="file" accept=".json,application/json" aria-label={t("settings.transfer.selectFile")} hidden onChange={(event) => void chooseFile(event.currentTarget.files?.[0])} />
        </div>
        {selected ? <div className="settings-transfer__confirmation">
          <strong>{selected.name}</strong><p>{t("settings.transfer.confirmImport")}</p>
          <div className="settings-transfer__actions">
            <button type="button" disabled={disabled} onClick={() => void action("import")}>{t("settings.transfer.import")}</button>
            <button type="button" disabled={busy} onClick={() => { selectionVersion.current++; setSelected(null); if (fileInput.current) fileInput.current.value = ""; }}>{t("settings.transfer.cancel")}</button>
          </div>
        </div> : null}
        {state.pendingImport ? <div className="settings-transfer__pending" role="status"><p>{t("settings.transfer.restartRequired")}</p><button type="button" disabled={disabled} onClick={() => void action("cancel-import")}>{t("settings.transfer.cancelPending")}</button></div> : null}
        {!state.ready ? <p role="status">{t("settings.transfer.unavailable")}</p> : null}
        {message ? <p role={error ? "alert" : "status"} className="settings-transfer__feedback" data-error={error}>{t(`settings.transfer.${message}`)}</p> : null}
      </div>
    </section>
  );
}
