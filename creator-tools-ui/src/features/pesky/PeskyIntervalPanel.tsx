import { useEffect, useRef, useState } from "react";
import { X } from "lucide-react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

export function PeskyIntervalPanel({ onClose }: { onClose: () => void }) {
  const { pesky, status, applyPeskyIntervals } = useConfig();
  const { t } = useLocalization();
  const dialogRef = useRef<HTMLDialogElement>(null);
  const minimumRef = useRef<HTMLInputElement>(null);
  const [minimumDraft, setMinimumDraft] = useState("");
  const [maximumDraft, setMaximumDraft] = useState("");
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    const dialog = dialogRef.current;
    const previousFocus = document.activeElement;
    dialog?.showModal();
    minimumRef.current?.focus();
    return () => {
      dialog?.close();
      if (previousFocus instanceof HTMLElement && previousFocus.isConnected) {
        previousFocus.focus();
      }
    };
  }, []);

  useEffect(() => {
    if (!dirty && pesky?.ready) {
      setMinimumDraft(String(pesky.minimumInterval));
      setMaximumDraft(String(pesky.maximumInterval));
    }
  }, [dirty, pesky?.ready, pesky?.minimumInterval, pesky?.maximumInterval]);

  const minimum = Number(minimumDraft);
  const maximum = Number(maximumDraft);
  const lowerLimit = pesky?.intervalLowerLimit;
  const upperLimit = pesky?.intervalUpperLimit;
  const valid = minimumDraft.trim() !== "" && maximumDraft.trim() !== "" &&
    Number.isFinite(minimum) && Number.isFinite(maximum) &&
    typeof lowerLimit === "number" && typeof upperLimit === "number" &&
    minimum >= lowerLimit && maximum <= upperLimit && minimum <= maximum;
  const hasChanges = minimum !== pesky?.minimumInterval ||
    maximum !== pesky?.maximumInterval;
  const validationMessage = t("pesky.intervals.invalid")
    .replace("{minimum}", String(lowerLimit ?? ""))
    .replace("{maximum}", String(upperLimit ?? ""));
  const saving = status === "saving" || status === "pending";
  const connectionUnavailable = status === "error" || status === "connecting";

  return (
    <dialog
      ref={dialogRef}
      className="pesky-interval-panel"
      aria-labelledby="pesky-interval-title"
      aria-describedby="pesky-interval-description"
      onClose={onClose}
    >
      <div className="interaction-panel__heading">
        <div>
          <h2 id="pesky-interval-title">{t("pesky.intervals.title")}</h2>
          <p id="pesky-interval-description">{t("pesky.intervals.description")}</p>
        </div>
        <button
          className="pesky-interval-panel__close"
          type="button"
          aria-label={t("pesky.intervals.close")}
          onClick={onClose}
        >
          <X aria-hidden="true" size={20} />
        </button>
      </div>
      <form
        className="interaction-settings pesky-interval-panel__form"
        onSubmit={(event) => {
          event.preventDefault();
          if (!valid || !pesky?.ready || saving) return;
          applyPeskyIntervals(minimum, maximum);
          setDirty(false);
        }}
      >
        <label className="interaction-settings__number">
          <span><strong>{t("pesky.intervals.minimum")}</strong></span>
          <input
            ref={minimumRef}
            type="number"
            inputMode="decimal"
            min={lowerLimit}
            max={upperLimit}
            step="any"
            required
            value={minimumDraft}
            disabled={!pesky?.ready}
            aria-describedby={dirty && !valid ? "pesky-interval-validation" : undefined}
            aria-invalid={dirty && !valid}
            onChange={(event) => {
              setMinimumDraft(event.target.value);
              setDirty(true);
            }}
          />
        </label>
        <label className="interaction-settings__number">
          <span><strong>{t("pesky.intervals.maximum")}</strong></span>
          <input
            type="number"
            inputMode="decimal"
            min={lowerLimit}
            max={upperLimit}
            step="any"
            required
            value={maximumDraft}
            disabled={!pesky?.ready}
            aria-describedby={dirty && !valid ? "pesky-interval-validation" : undefined}
            aria-invalid={dirty && !valid}
            onChange={(event) => {
              setMaximumDraft(event.target.value);
              setDirty(true);
            }}
          />
        </label>
        <p className="pesky-interval-panel__hint">{t("pesky.intervals.hint")}</p>
        {dirty && !valid ? (
          <p id="pesky-interval-validation" className="interaction-settings__status" data-status="error" role="alert">
            {validationMessage}
          </p>
        ) : null}
        <p className="interaction-settings__status" data-status={pesky?.error ? "error" : status} role="status" aria-live="polite">
          {connectionUnavailable ? t(`status.${status}`)
            : pesky?.error ? t(`pesky.feedback.${pesky.feedback}`)
            : hasChanges && !saving ? t("pesky.intervals.unsaved")
            : t(`status.${status}`)}
        </p>
        <div className="pesky-interval-panel__actions">
          <button
            type="button"
            disabled={!pesky?.ready || saving}
            onClick={() => {
              if (!pesky) return;
              setMinimumDraft(String(pesky.defaultMinimumInterval));
              setMaximumDraft(String(pesky.defaultMaximumInterval));
              setDirty(true);
            }}
          >
            {t("pesky.intervals.restore")}
          </button>
          <button type="submit" disabled={!pesky?.ready || !valid || !hasChanges || saving}>
            {t("pesky.intervals.save")}
          </button>
        </div>
      </form>
    </dialog>
  );
}
