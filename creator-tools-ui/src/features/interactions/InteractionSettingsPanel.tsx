import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

export function InteractionSettingsPanel() {
  const {
    interaction,
    interactionSettingsStatus,
    applyInteractionSettings,
  } = useConfig();
  const { t } = useLocalization();
  const [maxActiveDraft, setMaxActiveDraft] = useState(1);
  const [showGiftImageDraft, setShowGiftImageDraft] = useState(true);

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
    showGiftImageDraft !== (interaction?.showGiftImage !== false)
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
          applyInteractionSettings(maxActiveDraft, showGiftImageDraft);
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
        <button type="submit" disabled={!interaction?.ready}>
          {t("interactions.settings.save")}
        </button>
      </form>
    </section>
  );
}
