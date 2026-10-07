import { ChevronRight, MessageCircleCheck, MousePointerClick, Swords } from "lucide-react";
import type { Ref } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

interface LiveEventsSectionProps {
  onOpenPeskyBattle: () => void;
  onOpenTapFarming: () => void;
  onOpenChatChooses: () => void;
  chatChoosesCardRef?: Ref<HTMLButtonElement>;
  peskyBattleCardRef?: Ref<HTMLButtonElement>;
  tapFarmingCardRef?: Ref<HTMLButtonElement>;
}

export function LiveEventsSection({
  onOpenPeskyBattle,
  onOpenTapFarming,
  onOpenChatChooses,
  chatChoosesCardRef,
  peskyBattleCardRef,
  tapFarmingCardRef,
}: LiveEventsSectionProps) {
  const { liveEvents } = useConfig();
  const { t } = useLocalization();
  const activeEvent = liveEvents?.activeEvent ?? null;
  const cards = [
    { id: "chat_chooses", Icon: MessageCircleCheck, title: "dashboard.chatChooses.title",
      description: "dashboard.chatChooses.description", ref: chatChoosesCardRef, onOpen: onOpenChatChooses },
    { id: "pesky_battle", Icon: Swords, title: "dashboard.peskyBattle.title",
      description: "dashboard.liveEvents.peskyBattleDescription", ref: peskyBattleCardRef, onOpen: onOpenPeskyBattle },
    { id: "tap_farming", Icon: MousePointerClick, title: "dashboard.tapFarming.title",
      description: "dashboard.liveEvents.tapFarmingDescription", ref: tapFarmingCardRef, onOpen: onOpenTapFarming },
  ];

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
        {cards.map(({ id, Icon, title, description, ref, onOpen }) => (
          <button key={id} ref={ref} className="dashboard-live-event-card" type="button"
            data-active={activeEvent === id}
            aria-labelledby={`dashboard-live-event-${id}-title`}
            aria-describedby={`dashboard-live-event-${id}-description dashboard-live-event-${id}-status`}
            onClick={onOpen}>
            <span className="dashboard-live-event-card__icon" aria-hidden="true"><Icon /></span>
            <span className="dashboard-live-event-card__copy">
              <strong id={`dashboard-live-event-${id}-title`}>{t(title)}</strong>
              <small id={`dashboard-live-event-${id}-description`}>{t(description)}</small>
            </span>
            <span className="dashboard-live-event-card__meta">
              <span id={`dashboard-live-event-${id}-status`}>
                {t(`dashboard.liveEvents.status.${activeEvent === id ? "active" : "inactive"}`)}
              </span>
            </span>
            <ChevronRight className="dashboard-live-event-card__arrow" aria-hidden="true" />
          </button>
        ))}
      </div>
    </section>
  );
}
