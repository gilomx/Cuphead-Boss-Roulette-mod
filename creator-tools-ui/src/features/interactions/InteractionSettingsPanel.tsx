import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { InteractionTestSection } from "./InteractionTestSection";
import { SpawnPacingFields } from "./SpawnPacingFields";
import { pacingDraftFor, pacingValuesFor, samePacing, validPacingDraft } from "./pacingValues";

interface InteractionSettingsPanelProps {
  onBack: () => void;
  onTestSent: () => void;
}

export function InteractionSettingsPanel({ onBack, onTestSent }: InteractionSettingsPanelProps) {
  const { interaction, interactionSettingsStatus, applyInteractionSettings, applyPacingToBoth, pesky, streamRules, status } = useConfig();
  const { t } = useLocalization();
  const [maxActiveDraft, setMaxActiveDraft] = useState(6);
  const [maxActiveUnlimitedDraft, setMaxActiveUnlimitedDraft] = useState(false);
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
  useEffect(() => { if (interaction) setMaxActiveUnlimitedDraft(interaction.maxActiveUnlimited ?? false); }, [interaction?.maxActiveUnlimited]);
  useEffect(() => { if (interaction) setShowGiftImageDraft(interaction.showGiftImage !== false); }, [interaction?.showGiftImage]);
  const values = pacingValuesFor(draft);
  const valid = validPacingDraft(draft);
  const hasChanges = Boolean(interaction) && (maxActiveDraft !== interaction?.maxActive ||
    maxActiveUnlimitedDraft !== interaction?.maxActiveUnlimited ||
    showGiftImageDraft !== (interaction?.showGiftImage !== false) || enabledDraft !== interaction?.pacing?.enabled ||
    !samePacing(values, interaction?.pacing));
  const hasRuleCooldown = streamRules?.rules.some((rule) => rule.enabled &&
    (rule.userCooldownSeconds > 0 || rule.globalCooldownSeconds > 0)) ?? false;
  const visibleStatus = hasChanges ? "dirty" : interactionSettingsStatus;

  return (
    <section className="interaction-panel interaction-settings-section" aria-labelledby="interaction-settings-title">
      <div className="interaction-panel__heading stream-rules-panel__heading interaction-settings-heading">
        <div>
          <span className="stream-rules-panel__eyebrow">
            {t("interactions.workspace.eyebrow")}
          </span>
          <h2 id="interaction-settings-title">{t("interactions.settings.title")}</h2>
          <p>{t("interactions.settings.description")}</p>
        </div>
        <button type="button" className="stream-rule-back" onClick={onBack}>
          <span aria-hidden="true">&larr;</span>
          {t("interactions.workspace.back")}
        </button>
      </div>
      <form className="interaction-settings" onSubmit={(event) => {
        event.preventDefault();
        if (!valid || !interaction?.ready || saving) return;
        applyInteractionSettings(maxActiveDraft, maxActiveUnlimitedDraft,
          showGiftImageDraft, { ...values, enabled: enabledDraft });
        setAppliedBoth(false); setDirty(false);
      }}>
        <div className="interaction-settings__number">
          <span><strong>{t("interactions.settings.maxActiveLabel")}</strong><small>{t("interactions.settings.maxActiveHint")}</small></span>
          <div className="interaction-settings__limit-controls">
            <input type="number" min={1} max={interaction?.maxActiveLimit ?? 20}
              value={maxActiveDraft} disabled={maxActiveUnlimitedDraft}
              aria-label={t("interactions.settings.maxActiveLabel")}
              onChange={(event) => setMaxActiveDraft(Math.max(1, Math.min(interaction?.maxActiveLimit ?? 20, Number(event.target.value) || 1)))} />
            <label className="interaction-settings__unlimited">
              <input type="checkbox" checked={maxActiveUnlimitedDraft}
                onChange={(event) => setMaxActiveUnlimitedDraft(event.target.checked)} />
              <span>{t("interactions.settings.maxActiveUnlimited")}</span>
            </label>
          </div>
        </div>
        {maxActiveUnlimitedDraft && !enabledDraft && !hasRuleCooldown ? (
          <p className="interaction-settings__warning" role="status">
            {t("interactions.settings.unlimitedWarning")}
          </p>
        ) : null}
        <div className="interaction-settings__number"><span>
          <strong>{t("interactions.settings.maxMiniBossesLabel")}</strong><small>{t("interactions.settings.maxMiniBossesHint")}</small>
        </span></div>
        <label className="interaction-settings__toggle">
          <span><strong>{t("interactions.settings.showGiftImage")}</strong><small>{t("interactions.settings.showGiftImageHint")}</small></span>
          <input type="checkbox" checked={showGiftImageDraft} onChange={(event) => setShowGiftImageDraft(event.target.checked)} />
        </label>
        <details className="interaction-settings__advanced">
          <summary>{t("interactions.settings.advancedTitle")}</summary>
          <div className="interaction-settings__advanced-content">
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
                setDraft(pacingDraftFor(interaction?.defaultPacing)); setEnabledDraft(interaction?.defaultPacing.enabled ?? false);
                setMaxActiveDraft(interaction?.defaultMaxActive ?? 6); setMaxActiveUnlimitedDraft(false); setDirty(true);
              }}>{t("interactions.settings.pacingRestore")}</button>
              <button type="button" disabled={!interaction?.ready || !pesky?.ready || !valid || saving} onClick={() => {
                applyPacingToBoth(maxActiveDraft, values, undefined, maxActiveUnlimitedDraft); setAppliedBoth(true);
              }}>{t("pesky.intervals.applyBoth")}</button>
            </div>
            <p className="pesky-interval-panel__hint">{t(maxActiveUnlimitedDraft
              ? "interactions.settings.applyBothUnlimitedHint"
              : "pesky.intervals.applyBothHint")}</p>
            {appliedBoth ? <p role="status" className="interaction-settings__status" data-status={pesky?.error ? "error" : status}>
              {t("pesky.title")}: {pesky?.error ? t(`pesky.feedback.${pesky.feedback}`) : t(`status.${status}`)}
            </p> : null}
          </div>
        </details>
        {visibleStatus !== "idle" ? <p className="interaction-settings__status" data-status={visibleStatus} role="status" aria-live="polite">
          {t(`interactions.settings.status.${visibleStatus}`)}
        </p> : null}
        <button type="submit" disabled={!interaction?.ready || !valid || saving}>{t("interactions.settings.save")}</button>
      </form>
      <InteractionTestSection onSent={onTestSent} />
    </section>
  );
}
