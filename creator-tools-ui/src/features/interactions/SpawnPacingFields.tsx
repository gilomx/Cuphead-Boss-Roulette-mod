import { useLocalization } from "../../i18n/LocalizationContext";
import { pacingValuesFor, validPacingDraft, type PacingDraft, type PacingField } from "./pacingValues";

interface Props {
  draft: PacingDraft;
  onChange: (field: PacingField, value: string) => void;
  disabled: boolean;
  mode: "pesky" | "interactions";
  allowConcurrentStrongInteractions?: boolean;
  onAllowConcurrentStrongChange?: (value: boolean) => void;
}

export function SpawnPacingFields({ draft, onChange, disabled, mode,
  allowConcurrentStrongInteractions = true, onAllowConcurrentStrongChange }: Props) {
  const { t } = useLocalization();
  const values = pacingValuesFor(draft);
  const valid = validPacingDraft(draft);
  const text = (key: string) => t(`pesky.intervals.${key}`);
  const format = (number: number) => String(Number(number.toFixed(2)));
  const multiplier = values.miniBossIntervalMultiplier;
  const multiplierOptions = Array.from(new Set([
    1, 1.5, 2, 2.5, 3, 4, 5, 6, 7, 8, 9, 10,
    ...(Number.isFinite(multiplier) && multiplier >= 1 && multiplier <= 10 ? [multiplier] : []),
  ])).sort((a, b) => a - b);

  const range = (label: string, low: PacingField, high: PacingField, minimum: number, maximum: number, unit?: string) => (
    <div className="spawn-settings__range">
      <strong id={`${mode}-${low}-label`}>{label}</strong>
      <div className="spawn-settings__range-inputs" role="group" aria-labelledby={`${mode}-${low}-label`}>
        <label>
          <span>{text("rangeBetween")}</span>
          <input id={mode === "pesky" && low === "minimumInterval" ? "pesky-minimum-interval" : `${mode}-${low}`}
            type="number" min={minimum} max={maximum} step={unit ? "any" : "1"}
            inputMode={unit ? "decimal" : "numeric"} required value={draft[low]} disabled={disabled}
            aria-label={`${label}: ${text(unit ? "minimum" : "batchMinimum")}`} onChange={(event) => onChange(low, event.target.value)} />
        </label>
        <label>
          <span>{text("rangeAnd")}</span>
          <input id={`${mode}-${high}`} type="number" min={minimum} max={maximum} step={unit ? "any" : "1"}
            inputMode={unit ? "decimal" : "numeric"} required value={draft[high]} disabled={disabled}
            aria-label={`${label}: ${text(unit ? "maximum" : "batchMaximum")}`} onChange={(event) => onChange(high, event.target.value)} />
        </label>
        {unit ? <span>{unit}</span> : null}
      </div>
    </div>
  );

  return (
    <div className="spawn-settings">
      <fieldset className="spawn-settings__block" disabled={disabled}>
        <legend>{text("normalTitle")}</legend>
        {range(text("normalInterval"), "minimumInterval", "maximumInterval", 0.35, 300, text("seconds"))}
        <p className="pesky-interval-panel__hint">{text("hint")}</p>
        {mode === "pesky" ? <label className="interaction-settings__toggle">
          <span><strong>{text("allowStrong")}</strong><small>{text("allowStrongHint")}</small></span>
          <input type="checkbox" checked={allowConcurrentStrongInteractions} disabled={disabled}
            onChange={(event) => onAllowConcurrentStrongChange?.(event.target.checked)} />
        </label> : null}
        <details className="spawn-settings__details">
          <summary>{text("batchTitle")}</summary>
        <div className="spawn-settings__groups">
          {(["light", "strong"] as const).map((group) => (
            <div className="spawn-settings__group" key={group}>
              <h4>{t(`interactions.groups.${group}`)}</h4>
              {group === "strong" && !allowConcurrentStrongInteractions
                ? <p className="pesky-interval-panel__hint">{text("singleStrongHint")}</p>
                : range(text("batchTitle"), `${group}MinimumBatch`, `${group}MaximumBatch`, 1, 20)}
            </div>
          ))}
        </div>
        <p className="pesky-interval-panel__hint">{text(mode === "pesky" ? "batchHint" : "batchInteractionsHint")}</p>
        <p className="pesky-interval-panel__hint">{text("batchLimitHint")}</p>
        </details>
      </fieldset>

      <fieldset className="spawn-settings__block" disabled={disabled}>
        <legend>{t("interactions.groups.mini_boss")}</legend>
        {range(text("miniBossInterval"), "miniBossMinimumInterval", "miniBossMaximumInterval", 0, 300, text("seconds"))}
        <p className="pesky-interval-panel__hint">{text("firstMiniBossHint")}</p>
        <details className="spawn-settings__details">
          <summary>{text("duringMiniBoss")}</summary>
        <label className="interaction-settings__number spawn-settings__multiplier">
          <span><strong>{text("multiplier")}</strong></span>
          <select value={draft.miniBossIntervalMultiplier} disabled={disabled}
            onChange={(event) => onChange("miniBossIntervalMultiplier", event.target.value)}>
            {draft.miniBossIntervalMultiplier === "" ? <option value="" /> : null}
            {multiplierOptions.map((value) => (
              <option key={value} value={value}>
                {value === 1 ? text("multiplierOnce") : value === 2 ? text("multiplierTwice")
                  : text("multiplierTimes").replace("{value}", String(value))}
              </option>
            ))}
          </select>
        </label>
        <label className="interaction-settings__number">
          <span><strong>{text("companions")}</strong><small>{text("companionsHint")}</small></span>
          <input id={`${mode}-maximumCompanionsDuringMiniBoss`} type="number" inputMode="numeric"
            min={0} max={20} step={1} required value={draft.maximumCompanionsDuringMiniBoss} disabled={disabled}
            onChange={(event) => onChange("maximumCompanionsDuringMiniBoss", event.target.value)} />
        </label>
        </details>
      </fieldset>

      {valid ? (
        <details className="spawn-settings__summary spawn-settings__details">
          <summary>{text("summaryTitle")}</summary>
          <p>{values.minimumInterval === values.maximumInterval
            ? text("summaryNormalFixed").replace("{seconds}", format(values.minimumInterval))
            : text("summaryNormal").replace("{minimum}", format(values.minimumInterval)).replace("{maximum}", format(values.maximumInterval))}</p>
          <p>{values.maximumCompanionsDuringMiniBoss === 0 ? text("previewNone")
            : text(values.maximumCompanionsDuringMiniBoss === 1 ? "summaryMiniBossOne" : "summaryMiniBoss").replace("{minimum}", format(values.minimumInterval * multiplier))
              .replace("{maximum}", format(values.maximumInterval * multiplier))
              .replace("{count}", String(values.maximumCompanionsDuringMiniBoss))}</p>
          <p>{values.miniBossMinimumInterval === values.miniBossMaximumInterval
            ? text("summaryCooldown").replace("{seconds}", format(values.miniBossMinimumInterval))
            : text("summaryCooldownRange").replace("{minimum}", format(values.miniBossMinimumInterval))
            .replace("{maximum}", format(values.miniBossMaximumInterval))}</p>
          <small>{text("summaryHint")}</small>
        </details>
      ) : null}
    </div>
  );
}
