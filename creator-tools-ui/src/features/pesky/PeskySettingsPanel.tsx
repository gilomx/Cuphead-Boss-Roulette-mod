import { useEffect } from "react";
import { ArrowLeft } from "lucide-react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { PeskyChallengeTimingPanel } from "./PeskyChallengeTimingPanel";
import { PeskyIntervalPanel } from "./PeskyIntervalPanel";
import { PeskyNamesPanel } from "./PeskyNamesPanel";

interface PeskySettingsPanelProps {
  onBack: () => void;
}

export function PeskySettingsPanel({ onBack }: PeskySettingsPanelProps) {
  const { t } = useLocalization();

  useEffect(() => {
    const returnOnEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key !== "Escape" || event.defaultPrevented || event.isComposing) return;
      event.preventDefault();
      onBack();
    };
    window.addEventListener("keydown", returnOnEscape);
    return () => window.removeEventListener("keydown", returnOnEscape);
  }, [onBack]);

  return (
    <section className="interaction-panel interaction-settings-section pesky-settings"
      aria-labelledby="pesky-settings-title">
      <div className="interaction-panel__heading stream-rules-panel__heading interaction-settings-heading">
        <div>
          <span className="stream-rules-panel__eyebrow">{t("pesky.title")}</span>
          <h2 id="pesky-settings-title">{t("pesky.intervals.title")}</h2>
          <p>{t("pesky.settings.description")}</p>
        </div>
        <button type="button" className="stream-rule-back stream-rule-back--icon"
          onClick={onBack} aria-label={t("pesky.settings.back")}
          title={t("pesky.settings.back")}>
          <ArrowLeft aria-hidden="true" />
        </button>
      </div>
      <div className="pesky-settings__layout">
        <PeskyIntervalPanel />
        <PeskyNamesPanel />
        <PeskyChallengeTimingPanel />
      </div>
    </section>
  );
}
