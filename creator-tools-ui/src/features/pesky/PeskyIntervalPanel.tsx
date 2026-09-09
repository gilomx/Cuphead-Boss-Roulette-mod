import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

export function PeskyIntervalPanel() {
  const { pesky, interaction, status, interactionSettingsStatus, applyPeskyIntervals, applyPacingToBoth } = useConfig();
  const { t } = useLocalization();
  const [appliedBoth, setAppliedBoth] = useState(false);
  const [minimumDraft, setMinimumDraft] = useState("");
  const [maximumDraft, setMaximumDraft] = useState("");
  const [cooldownDraft, setCooldownDraft] = useState("");
  const [multiplierDraft, setMultiplierDraft] = useState("");
  const [companionsDraft, setCompanionsDraft] = useState("");
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    if (!dirty && pesky?.ready) {
      setMinimumDraft(String(pesky.minimumInterval));
      setMaximumDraft(String(pesky.maximumInterval));
      setCooldownDraft(String(pesky.miniBossCooldownSeconds));
      setMultiplierDraft(String(pesky.miniBossIntervalMultiplier));
      setCompanionsDraft(String(pesky.maximumCompanionsDuringMiniBoss));
    }
  }, [dirty, pesky?.ready, pesky?.minimumInterval, pesky?.maximumInterval,
    pesky?.miniBossCooldownSeconds, pesky?.miniBossIntervalMultiplier, pesky?.maximumCompanionsDuringMiniBoss]);

  const minimum = Number(minimumDraft);
  const maximum = Number(maximumDraft);
  const cooldown = Number(cooldownDraft);
  const multiplier = Number(multiplierDraft);
  const companions = Number(companionsDraft);
  const lowerLimit = pesky?.intervalLowerLimit;
  const upperLimit = pesky?.intervalUpperLimit;
  const validIntervals = minimumDraft.trim() !== "" && maximumDraft.trim() !== "" &&
    Number.isFinite(minimum) && Number.isFinite(maximum) &&
    typeof lowerLimit === "number" && typeof upperLimit === "number" &&
    minimum >= lowerLimit && maximum <= upperLimit && minimum <= maximum;
  const validBalance = cooldownDraft.trim() !== "" && multiplierDraft.trim() !== "" && companionsDraft.trim() !== "" &&
    Number.isFinite(cooldown) && cooldown >= 0 && cooldown <= 300 &&
    Number.isFinite(multiplier) && multiplier >= 1 && multiplier <= 10 &&
    Number.isInteger(companions) && companions >= 0 && companions <= 20;
  const valid = validIntervals && validBalance;
  const hasChanges = minimum !== pesky?.minimumInterval ||
    maximum !== pesky?.maximumInterval || cooldown !== pesky?.miniBossCooldownSeconds ||
    multiplier !== pesky?.miniBossIntervalMultiplier || companions !== pesky?.maximumCompanionsDuringMiniBoss;
  const validationMessage = t("pesky.intervals.invalid")
    .replace("{minimum}", String(lowerLimit ?? ""))
    .replace("{maximum}", String(upperLimit ?? ""));
  const saving = status === "saving" || status === "pending";
  const connectionUnavailable = status === "error" || status === "connecting";

  return (
    <section
      id="pesky-settings"
      className="interaction-panel pesky-interval-panel"
      aria-labelledby="pesky-interval-title"
      aria-describedby="pesky-interval-description"
    >
      <div className="interaction-panel__heading">
        <div>
          <h2 id="pesky-interval-title">{t("pesky.intervals.title")}</h2>
          <p id="pesky-interval-description">{t("pesky.intervals.description")}</p>
        </div>
      </div>
      <form
        className="interaction-settings pesky-interval-panel__form"
        onSubmit={(event) => {
          event.preventDefault();
          if (!valid || !pesky?.ready || saving) return;
          applyPeskyIntervals(minimum, maximum, cooldown, multiplier, companions);
          setAppliedBoth(false);
          setDirty(false);
        }}
      >
        <label className="interaction-settings__number">
          <span><strong>{t("pesky.intervals.minimum")}</strong></span>
          <input
            id="pesky-minimum-interval"
            type="number"
            inputMode="decimal"
            min={lowerLimit}
            max={upperLimit}
            step="any"
            required
            value={minimumDraft}
            disabled={!pesky?.ready || saving}
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
            disabled={!pesky?.ready || saving}
            aria-describedby={dirty && !valid ? "pesky-interval-validation" : undefined}
            aria-invalid={dirty && !valid}
            onChange={(event) => {
              setMaximumDraft(event.target.value);
              setDirty(true);
            }}
          />
        </label>
        <p className="pesky-interval-panel__hint">{t("pesky.intervals.hint")}</p>
        <h3 className="pesky-interval-panel__section">{t("pesky.intervals.miniBossTitle")}</h3>
        {[
          { key: "cooldown", value: cooldownDraft, set: setCooldownDraft, min: 0, max: 300, step: "any" },
          { key: "multiplier", value: multiplierDraft, set: setMultiplierDraft, min: 1, max: 10, step: "any" },
          { key: "companions", value: companionsDraft, set: setCompanionsDraft, min: 0, max: 20, step: "1" },
        ].map((field) => (
          <label className="interaction-settings__number" key={field.key}>
            <span>
              <strong>{t(`pesky.intervals.${field.key}`)}</strong>
              <small id={`pesky-${field.key}-hint`}>{t(`pesky.intervals.${field.key}Hint`)}</small>
            </span>
            <input
              type="number"
              inputMode={field.key === "companions" ? "numeric" : "decimal"}
              min={field.min}
              max={field.max}
              step={field.step}
              required
              value={field.value}
              disabled={!pesky?.ready || saving}
              aria-describedby={`pesky-${field.key}-hint${dirty && !validBalance ? " pesky-interval-validation" : ""}`}
              aria-invalid={dirty && !validBalance}
              onChange={(event) => {
                field.set(event.target.value);
                setDirty(true);
              }}
            />
          </label>
        ))}
        {valid ? (
          <p className="pesky-interval-panel__hint pesky-interval-panel__preview">
            {companions === 0 ? t("pesky.intervals.previewNone") : t("pesky.intervals.preview")
              .replace("{minimum}", String(Number((minimum * multiplier).toFixed(2))))
              .replace("{maximum}", String(Number((maximum * multiplier).toFixed(2))))}
          </p>
        ) : null}
        {dirty && !valid ? (
          <p id="pesky-interval-validation" className="interaction-settings__status" data-status="error" role="alert">
            {!validIntervals ? validationMessage : t("pesky.intervals.invalidBalance")}
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
              setCooldownDraft(String(pesky.defaultMiniBossCooldownSeconds));
              setMultiplierDraft(String(pesky.defaultMiniBossIntervalMultiplier));
              setCompanionsDraft(String(pesky.defaultMaximumCompanionsDuringMiniBoss));
              setDirty(true);
            }}
          >
            {t("pesky.intervals.restore")}
          </button>
          <button type="button" disabled={!pesky?.ready || !interaction?.ready || !valid || saving}
            onClick={() => {
              applyPacingToBoth({ minimumInterval: minimum, maximumInterval: maximum,
                miniBossCooldownSeconds: cooldown, miniBossIntervalMultiplier: multiplier,
                maximumCompanionsDuringMiniBoss: companions });
              setDirty(false);
              setAppliedBoth(true);
            }}>
            {t("pesky.intervals.applyBoth")}
          </button>
          <button type="submit" disabled={!pesky?.ready || !valid || !hasChanges || saving}>
            {t("pesky.intervals.save")}
          </button>
        </div>
        <p className="pesky-interval-panel__hint">{t("pesky.intervals.applyBothHint")}</p>
        {appliedBoth && interactionSettingsStatus !== "idle" ? (
          <p role="status" className="interaction-settings__status" data-status={interactionSettingsStatus}>
            {t("interactions.title")}: {t(`interactions.settings.status.${interactionSettingsStatus}`)}
          </p>
        ) : null}
      </form>
    </section>
  );
}
