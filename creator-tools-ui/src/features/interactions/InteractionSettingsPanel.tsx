import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { SpawnPacingFields } from "./SpawnPacingFields";
import { pacingDraftFor, pacingValuesFor, samePacing, validPacingDraft } from "./pacingValues";

export function InteractionSettingsPanel() {
  const { interaction, interactionSettingsStatus, applyInteractionSettings, applyPacingToBoth, pesky, status } = useConfig();
  const { t } = useLocalization();
  const [maxActiveDraft, setMaxActiveDraft] = useState(1);
  const [showGiftImageDraft, setShowGiftImageDraft] = useState(true);
  const [enabledDraft, setEnabledDraft] = useState(false);
  const [draft, setDraft] = useState(() => pacingDraftFor());
  const [dirty, setDirty] = useState(false);
  const [appliedBoth, setAppliedBoth] = useState(false);
  const saving = status === "saving" || status === "pending";

  useEffect(() => {
    if (!dirty && interaction?.pacing) {
      setDraft(pacingDraftFor(interaction.pacing));
      setEnabledDraft(interaction.pacing.enabled);
    }
  }, [dirty, interaction?.pacing]);
  useEffect(() => { if (interaction) setMaxActiveDraft(interaction.maxActive); }, [interaction?.maxActive]);
  useEffect(() => { if (interaction) setShowGiftImageDraft(interaction.showGiftImage !== false); }, [interaction?.showGiftImage]);
  const values = pacingValuesFor(draft);
  const valid = validPacingDraft(draft);
  const hasChanges = Boolean(interaction) && (maxActiveDraft !== interaction?.maxActive ||
    showGiftImageDraft !== (interaction?.showGiftImage !== false) || enabledDraft !== interaction?.pacing?.enabled ||
    !samePacing(values, interaction?.pacing));
  const visibleStatus = hasChanges ? "dirty" : interactionSettingsStatus;

  return (
    <section className="interaction-panel interaction-settings-section" aria-labelledby="interaction-settings-title">
      <div className="interaction-panel__heading interaction-settings-heading">
        <h2 id="interaction-settings-title">{t("interactions.settings.title")}</h2>
        <p>{t("interactions.settings.description")}</p>
      </div>
      <form className="interaction-settings" onSubmit={(event) => {
        event.preventDefault();
        if (!valid || !interaction?.ready || saving) return;
        applyInteractionSettings(maxActiveDraft, showGiftImageDraft, { ...values, enabled: enabledDraft });
        setAppliedBoth(false); setDirty(false);
      }}>
        <label className="interaction-settings__number">
          <span><strong>{t("interactions.settings.maxActiveLabel")}</strong><small>{t("interactions.settings.maxActiveHint")}</small></span>
          <input type="number" min={1} max={interaction?.maxActiveLimit ?? 20} value={maxActiveDraft}
            onChange={(event) => setMaxActiveDraft(Math.max(1, Math.min(interaction?.maxActiveLimit ?? 20, Number(event.target.value) || 1)))} />
        </label>
        <div className="interaction-settings__number"><span>
          <strong>{t("interactions.settings.maxMiniBossesLabel")}</strong><small>{t("interactions.settings.maxMiniBossesHint")}</small>
        </span></div>
        <label className="interaction-settings__toggle">
          <span><strong>{t("interactions.settings.showGiftImage")}</strong><small>{t("interactions.settings.showGiftImageHint")}</small></span>
          <input type="checkbox" checked={showGiftImageDraft} onChange={(event) => setShowGiftImageDraft(event.target.checked)} />
        </label>
        <h3 className="pesky-interval-panel__section">{t("interactions.settings.pacingTitle")}</h3>
        <p className="pesky-interval-panel__hint">{t("interactions.settings.pacingIndependent")}</p>
        <label className="interaction-settings__toggle">
          <span><strong>{t("interactions.settings.pacingEnable")}</strong><small>{t("interactions.settings.pacingEnableHint")}</small></span>
          <input type="checkbox" checked={enabledDraft} disabled={!interaction?.pacing || saving}
            onChange={(event) => { setEnabledDraft(event.target.checked); setDirty(true); }} />
        </label>
        {!enabledDraft ? <p className="pesky-interval-panel__hint">{t("interactions.settings.pacingOffHint")}</p> : null}
        <SpawnPacingFields draft={draft} disabled={!interaction?.pacing || saving} mode="interactions"
          onChange={(field, value) => { setDraft((current) => ({ ...current, [field]: value })); setDirty(true); }} />
        {dirty && !valid ? <p role="alert" className="interaction-settings__status" data-status="error">{t("interactions.settings.pacingInvalid")}</p> : null}
        <div className="pesky-interval-panel__actions">
          <button type="button" disabled={!interaction?.defaultPacing || saving} onClick={() => {
            setDraft(pacingDraftFor(interaction?.defaultPacing)); setEnabledDraft(interaction?.defaultPacing.enabled ?? false); setDirty(true);
          }}>{t("interactions.settings.pacingRestore")}</button>
          <button type="button" disabled={!interaction?.ready || !pesky?.ready || !valid || saving} onClick={() => {
            applyPacingToBoth(values); setAppliedBoth(true);
          }}>{t("pesky.intervals.applyBoth")}</button>
        </div>
        <p className="pesky-interval-panel__hint">{t("pesky.intervals.applyBothHint")}</p>
        {appliedBoth ? <p role="status" className="interaction-settings__status" data-status={pesky?.error ? "error" : status}>
          {t("pesky.title")}: {pesky?.error ? t(`pesky.feedback.${pesky.feedback}`) : t(`status.${status}`)}
        </p> : null}
        {visibleStatus !== "idle" ? <p className="interaction-settings__status" data-status={visibleStatus} role="status" aria-live="polite">
          {t(`interactions.settings.status.${visibleStatus}`)}
        </p> : null}
        <button type="submit" disabled={!interaction?.ready || !valid || saving}>{t("interactions.settings.save")}</button>
      </form>
    </section>
  );
}
