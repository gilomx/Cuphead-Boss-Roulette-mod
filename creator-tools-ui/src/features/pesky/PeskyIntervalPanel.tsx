import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { SpawnPacingFields } from "../interactions/SpawnPacingFields";
import { pacingDraftFor, pacingValuesFor, peskyDefaults, samePacing, validPacingDraft } from "../interactions/pacingValues";

export function PeskyIntervalPanel() {
  const { pesky, interaction, status, interactionSettingsStatus, applyPeskyIntervals, applyPacingToBoth } = useConfig();
  const { t } = useLocalization();
  const [appliedBoth, setAppliedBoth] = useState(false);
  const [maxActiveDraft, setMaxActiveDraft] = useState(1);
  const [draft, setDraft] = useState(() => pacingDraftFor());
  const [dirty, setDirty] = useState(false);
  const [allowStrongDraft, setAllowStrongDraft] = useState(false);
  useEffect(() => {
    if (!dirty && pesky?.ready) {
      setDraft(pacingDraftFor(pesky));
      setMaxActiveDraft(pesky.maxActive);
      setAllowStrongDraft(pesky.allowConcurrentStrongInteractions ?? false);
    }
  }, [dirty, pesky]);
  const values = pacingValuesFor(draft);
  const valid = validPacingDraft(draft);
  const hasChanges = maxActiveDraft !== pesky?.maxActive ||
    !samePacing(values, pesky ?? undefined) ||
    allowStrongDraft !== (pesky?.allowConcurrentStrongInteractions ?? false);
  const saving = status === "saving" || status === "pending";
  const unavailable = status === "error" || status === "connecting";

  return (
    <section id="pesky-settings" className="interaction-panel pesky-interval-panel" aria-labelledby="pesky-interval-title">
      <div className="interaction-panel__heading"><div>
        <h2 id="pesky-interval-title">{t("pesky.intervals.title")}</h2>
        <p>{t("pesky.intervals.description")}</p>
      </div></div>
      <form className="interaction-settings pesky-interval-panel__form" onSubmit={(event) => {
        event.preventDefault();
        if (!valid || !pesky?.ready || saving) return;
        applyPeskyIntervals(maxActiveDraft, values, allowStrongDraft);
        setAppliedBoth(false);
        setDirty(false);
      }}>
        <label className="interaction-settings__number">
          <span><strong>{t("interactions.settings.maxActiveLabel")}</strong><small>{t("interactions.settings.maxActiveHint")}</small></span>
          <input type="number" min={1} max={pesky?.maxActiveLimit ?? 20} value={maxActiveDraft}
            onChange={(event) => { setMaxActiveDraft(Math.max(1, Math.min(pesky?.maxActiveLimit ?? 20,
              Number(event.target.value) || 1))); setDirty(true); }} />
        </label>
        <SpawnPacingFields draft={draft} disabled={!pesky?.ready || saving} mode="pesky"
          allowConcurrentStrongInteractions={allowStrongDraft}
          onAllowConcurrentStrongChange={(value) => {
            setAllowStrongDraft(value);
            // Do not hide an invalid number that would prevent saving the
            // concurrency as disabled. Keep the last saved pair in that case.
            const minimum = Number(draft.strongMinimumBatch);
            const maximum = Number(draft.strongMaximumBatch);
            if (!value && (!Number.isInteger(minimum) || !Number.isInteger(maximum) ||
                minimum < 1 || maximum > 20 || minimum > maximum)) {
              setDraft((current) => ({ ...current,
                strongMinimumBatch: String(pesky?.strongMinimumBatch ?? 1),
                strongMaximumBatch: String(pesky?.strongMaximumBatch ?? 1),
              }));
            }
            setDirty(true);
          }}
          onChange={(field, value) => { setDraft((current) => ({ ...current, [field]: value })); setDirty(true); }} />
        {dirty && !valid ? <p role="alert" className="interaction-settings__status" data-status="error">{t("interactions.settings.pacingInvalid")}</p> : null}
        <p className="interaction-settings__status" data-status={pesky?.error ? "error" : status} role="status" aria-live="polite">
          {unavailable ? t(`status.${status}`) : pesky?.error ? t(`pesky.feedback.${pesky.feedback}`)
            : hasChanges && !saving ? t("pesky.intervals.unsaved") : t(`status.${status}`)}
        </p>
        <div className="pesky-interval-panel__actions">
          <button type="button" disabled={!pesky?.ready || saving} onClick={() => {
            if (!pesky) return;
            setDraft(pacingDraftFor(peskyDefaults(pesky)));
            setMaxActiveDraft(pesky.defaultMaxActive ?? 1);
            setAllowStrongDraft(pesky.defaultAllowConcurrentStrongInteractions ?? false);
            setDirty(true);
          }}>{t("pesky.intervals.restore")}</button>
          <button type="button" disabled={!pesky?.ready || !interaction?.ready || !valid || saving} onClick={() => {
            applyPacingToBoth(maxActiveDraft, values, allowStrongDraft); setDirty(false); setAppliedBoth(true);
          }}>{t("pesky.intervals.applyBoth")}</button>
          <button type="submit" disabled={!pesky?.ready || !valid || !hasChanges || saving}>{t("pesky.intervals.save")}</button>
        </div>
        <p className="pesky-interval-panel__hint">{t("pesky.intervals.applyBothHint")}</p>
        {appliedBoth && interactionSettingsStatus !== "idle" ? <p role="status" className="interaction-settings__status" data-status={interactionSettingsStatus}>
          {t("interactions.title")}: {t(`interactions.settings.status.${interactionSettingsStatus}`)}
        </p> : null}
      </form>
    </section>
  );
}
