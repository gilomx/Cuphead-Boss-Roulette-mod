import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { InteractionPacingConfig } from "../../model";

const pacingFields = [
  { key: "minimumInterval", label: "minimum", min: 0.35, max: 300, step: "any" },
  { key: "maximumInterval", label: "maximum", min: 0.35, max: 300, step: "any" },
  { key: "miniBossCooldownSeconds", label: "cooldown", min: 0, max: 300, step: "any" },
  { key: "miniBossIntervalMultiplier", label: "multiplier", min: 1, max: 10, step: "any" },
  { key: "maximumCompanionsDuringMiniBoss", label: "companions", min: 0, max: 20, step: "1" },
] as const;

type PacingDraft = Record<(typeof pacingFields)[number]["key"], string> & { enabled: boolean };
function pacingDraftFor(value?: InteractionPacingConfig): PacingDraft {
  return {
    enabled: value?.enabled ?? false,
    minimumInterval: String(value?.minimumInterval ?? ""),
    maximumInterval: String(value?.maximumInterval ?? ""),
    miniBossCooldownSeconds: String(value?.miniBossCooldownSeconds ?? ""),
    miniBossIntervalMultiplier: String(value?.miniBossIntervalMultiplier ?? ""),
    maximumCompanionsDuringMiniBoss: String(value?.maximumCompanionsDuringMiniBoss ?? ""),
  };
}

export function InteractionSettingsPanel() {
  const {
    interaction,
    interactionSettingsStatus,
    applyInteractionSettings,
    applyPacingToBoth,
    pesky,
    status,
  } = useConfig();
  const { t } = useLocalization();
  const [maxActiveDraft, setMaxActiveDraft] = useState(1);
  const [showGiftImageDraft, setShowGiftImageDraft] = useState(true);
  const [pacingDraft, setPacingDraft] = useState(() => pacingDraftFor());
  const [pacingDirty, setPacingDirty] = useState(false);
  const [appliedBoth, setAppliedBoth] = useState(false);
  const saving = status === "saving" || status === "pending";

  useEffect(() => {
    if (!pacingDirty && interaction?.pacing) setPacingDraft(pacingDraftFor(interaction.pacing));
  }, [pacingDirty, interaction?.pacing]);

  const pacing: InteractionPacingConfig = {
    enabled: pacingDraft.enabled,
    minimumInterval: Number(pacingDraft.minimumInterval),
    maximumInterval: Number(pacingDraft.maximumInterval),
    miniBossCooldownSeconds: Number(pacingDraft.miniBossCooldownSeconds),
    miniBossIntervalMultiplier: Number(pacingDraft.miniBossIntervalMultiplier),
    maximumCompanionsDuringMiniBoss: Number(pacingDraft.maximumCompanionsDuringMiniBoss),
  };
  const pacingValid = pacingFields.every((field) => pacingDraft[field.key].trim() !== "" &&
    Number.isFinite(pacing[field.key]) && pacing[field.key] >= field.min && pacing[field.key] <= field.max) &&
    pacing.minimumInterval <= pacing.maximumInterval && Number.isInteger(pacing.maximumCompanionsDuringMiniBoss);
  const pacingChanged = pacing.enabled !== interaction?.pacing?.enabled ||
    pacingFields.some((field) => pacing[field.key] !== interaction?.pacing?.[field.key]);

  useEffect(() => {
    if (typeof interaction?.maxActive === "number") {
      setMaxActiveDraft(interaction.maxActive);
    }
  }, [interaction?.maxActive]);

  useEffect(() => {
    if (typeof interaction?.showGiftImage === "boolean") {
      setShowGiftImageDraft(interaction.showGiftImage);
    }
  }, [interaction?.showGiftImage]);

  const hasChanges = Boolean(interaction) && (
    maxActiveDraft !== interaction?.maxActive ||
    showGiftImageDraft !== (interaction?.showGiftImage !== false) || pacingChanged
  );
  const visibleStatus = hasChanges ? "dirty" : interactionSettingsStatus;

  return (
    <section
      className="interaction-panel interaction-settings-section"
      aria-labelledby="interaction-settings-title"
    >
      <div className="interaction-panel__heading interaction-settings-heading">
        <h2 id="interaction-settings-title">{t("interactions.settings.title")}</h2>
        <p>{t("interactions.settings.description")}</p>
      </div>
      <form
        className="interaction-settings"
        onSubmit={(event) => {
          event.preventDefault();
          if (!pacingValid) return;
          applyInteractionSettings(maxActiveDraft, showGiftImageDraft, pacing);
          setAppliedBoth(false);
          setPacingDirty(false);
        }}
      >
        <label className="interaction-settings__number">
          <span>
            <strong>{t("interactions.settings.maxActiveLabel")}</strong>
            <small>{t("interactions.settings.maxActiveHint")}</small>
          </span>
          <input
            type="number"
            min={1}
            max={interaction?.maxActiveLimit ?? 20}
            value={maxActiveDraft}
            onChange={(event) => setMaxActiveDraft(Math.max(
              1,
              Math.min(
                interaction?.maxActiveLimit ?? 20,
                Number(event.target.value) || 1,
              ),
            ))}
          />
        </label>
        <div className="interaction-settings__number">
          <span>
            <strong>{t("interactions.settings.maxMiniBossesLabel")}</strong>
            <small>{t("interactions.settings.maxMiniBossesHint")}</small>
          </span>
        </div>
        <label className="interaction-settings__toggle">
          <span>
            <strong>{t("interactions.settings.showGiftImage")}</strong>
            <small>{t("interactions.settings.showGiftImageHint")}</small>
          </span>
          <input
            type="checkbox"
            checked={showGiftImageDraft}
            onChange={(event) => setShowGiftImageDraft(event.target.checked)}
          />
        </label>
        <h3 className="pesky-interval-panel__section">{t("interactions.settings.pacingTitle")}</h3>
        <p className="pesky-interval-panel__hint">{t("interactions.settings.pacingIndependent")}</p>
        <label className="interaction-settings__toggle">
          <span>
            <strong>{t("interactions.settings.pacingEnable")}</strong>
            <small>{t("interactions.settings.pacingEnableHint")}</small>
          </span>
          <input type="checkbox" checked={pacingDraft.enabled} disabled={!interaction?.pacing}
            onChange={(event) => {
              setPacingDraft((current) => ({ ...current, enabled: event.target.checked }));
              setPacingDirty(true);
            }} />
        </label>
        {pacingFields.map((field) => (
          <label className="interaction-settings__number" key={field.key}>
            <span><strong>{t(`pesky.intervals.${field.label}`)}</strong></span>
            <input type="number" min={field.min} max={field.max} step={field.step} required
              value={pacingDraft[field.key]} disabled={!interaction?.pacing}
              aria-invalid={pacingDirty && !pacingValid}
              aria-describedby="interaction-pacing-hint"
              onChange={(event) => {
                setPacingDraft((current) => ({ ...current, [field.key]: event.target.value }));
                setPacingDirty(true);
              }} />
          </label>
        ))}
        <p id="interaction-pacing-hint" className="pesky-interval-panel__hint">{t("interactions.settings.pacingHint")}</p>
        {pacingDirty && !pacingValid ? (
          <p role="alert" className="interaction-settings__status" data-status="error">{t("interactions.settings.pacingInvalid")}</p>
        ) : null}
        <div className="pesky-interval-panel__actions">
        <button type="button" disabled={!interaction?.defaultPacing || saving}
          onClick={() => {
            setPacingDraft(pacingDraftFor(interaction?.defaultPacing));
            setPacingDirty(true);
          }}>
          {t("interactions.settings.pacingRestore")}
        </button>
        <button type="button" disabled={!interaction?.ready || !pesky?.ready || !pacingValid || saving}
          onClick={() => {
            applyPacingToBoth(pacing);
            setAppliedBoth(true);
          }}>
          {t("pesky.intervals.applyBoth")}
        </button>
        </div>
        <p className="pesky-interval-panel__hint">{t("pesky.intervals.applyBothHint")}</p>
        {appliedBoth ? (
          <p role="status" className="interaction-settings__status" data-status={pesky?.error ? "error" : status}>
            {t("pesky.title")}: {pesky?.error ? t(`pesky.feedback.${pesky.feedback}`) : t(`status.${status}`)}
          </p>
        ) : null}
        {visibleStatus !== "idle" ? (
          <p
            className="interaction-settings__status"
            data-status={visibleStatus}
            role="status"
            aria-live="polite"
          >
            {t(`interactions.settings.status.${visibleStatus}`)}
          </p>
        ) : null}
        <button type="submit" disabled={!interaction?.ready || !pacingValid || saving}>
          {t("interactions.settings.save")}
        </button>
      </form>
    </section>
  );
}
