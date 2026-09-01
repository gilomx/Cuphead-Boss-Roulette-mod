import { ChevronRight, MousePointerClick, Swords, Users } from "lucide-react";
import type { Ref } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

interface LiveEventsSectionProps {
  onOpenPeskyBattle: () => void;
  onOpenTapFarming: () => void;
  peskyBattleCardRef?: Ref<HTMLButtonElement>;
  tapFarmingCardRef?: Ref<HTMLButtonElement>;
}

export function LiveEventsSection({
  onOpenPeskyBattle,
  onOpenTapFarming,
  peskyBattleCardRef,
  tapFarmingCardRef,
}: LiveEventsSectionProps) {
  const { liveEvents, peskyBattle, tapFarming } = useConfig();
  const { locale, t } = useLocalization();
  const phase = peskyBattle?.phase ?? "off";
  const tapPhase = tapFarming?.phase ?? "off";
  const capacity = Math.max(1, peskyBattle?.capacity ?? 5);
  const participantCount = Math.min(
    capacity,
    Math.max(0, peskyBattle?.participants.length ?? 0),
  );
  const totalTaps = Math.max(0, tapFarming?.counters?.totalTaps ?? 0);
  const activeEvent = liveEvents?.activeEvent ?? null;

  return (
    <section
      className="dashboard-live-events"
      aria-labelledby="dashboard-live-events-title"
    >
      <div className="dashboard-section-heading dashboard-live-events__heading">
        <div>
          <p className="dashboard-eyebrow">{t("dashboard.liveEvents.eyebrow")}</p>
          <h2 id="dashboard-live-events-title">{t("dashboard.liveEvents.title")}</h2>
          <p>{t("dashboard.liveEvents.description")}</p>
        </div>
      </div>

      <div className="dashboard-live-event-grid">
        <button
          ref={peskyBattleCardRef}
          className="dashboard-live-event-card"
          type="button"
          data-phase={phase}
          data-active={activeEvent === "pesky_battle"}
          aria-labelledby="dashboard-live-event-pesky-title"
          aria-describedby="dashboard-live-event-pesky-description dashboard-live-event-pesky-phase dashboard-live-event-pesky-participants"
          onClick={onOpenPeskyBattle}
        >
          <span className="dashboard-live-event-card__icon" aria-hidden="true">
            <Swords />
          </span>
          <span className="dashboard-live-event-card__copy">
            <strong id="dashboard-live-event-pesky-title">
              {t("dashboard.peskyBattle.title")}
            </strong>
            <small id="dashboard-live-event-pesky-description">
              {t("dashboard.liveEvents.peskyBattleDescription")}
            </small>
          </span>
          <span className="dashboard-live-event-card__meta">
            <span id="dashboard-live-event-pesky-phase" data-phase={phase}>
              {t(`dashboard.peskyBattle.phase.${phase}`)}
            </span>
            <span id="dashboard-live-event-pesky-participants">
              <Users aria-hidden="true" />
              {t("dashboard.liveEvents.participants")
                .replace("{count}", String(participantCount))
                .replace("{capacity}", String(capacity))}
            </span>
          </span>
          <ChevronRight
            className="dashboard-live-event-card__arrow"
            aria-hidden="true"
          />
        </button>

        <button
          ref={tapFarmingCardRef}
          className="dashboard-live-event-card dashboard-live-event-card--taps"
          type="button"
          data-phase={tapPhase}
          data-active={activeEvent === "tap_farming"}
          aria-labelledby="dashboard-live-event-taps-title"
          aria-describedby="dashboard-live-event-taps-description dashboard-live-event-taps-phase dashboard-live-event-taps-count"
          onClick={onOpenTapFarming}
        >
          <span className="dashboard-live-event-card__icon" aria-hidden="true">
            <MousePointerClick />
          </span>
          <span className="dashboard-live-event-card__copy">
            <strong id="dashboard-live-event-taps-title">
              {t("dashboard.tapFarming.title")}
            </strong>
            <small id="dashboard-live-event-taps-description">
              {t("dashboard.liveEvents.tapFarmingDescription")}
            </small>
          </span>
          <span className="dashboard-live-event-card__meta">
            <span id="dashboard-live-event-taps-phase" data-phase={tapPhase}>
              {t(`dashboard.tapFarming.phase.${tapPhase}`)}
            </span>
            <span id="dashboard-live-event-taps-count">
              <MousePointerClick aria-hidden="true" />
              {t("dashboard.liveEvents.taps")
                .replace("{count}", totalTaps.toLocaleString(
                  locale === "es" ? "es-MX" : "en-US",
                ))}
            </span>
          </span>
          <ChevronRight
            className="dashboard-live-event-card__arrow"
            aria-hidden="true"
          />
        </button>
      </div>
    </section>
  );
}
