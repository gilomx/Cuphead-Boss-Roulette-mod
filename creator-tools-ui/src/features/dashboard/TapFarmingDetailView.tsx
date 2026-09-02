import { ArrowLeft } from "lucide-react";
import { useEffect, useRef } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { OverlayDesignerCallout } from "./OverlayDesignerCallout";
import { TapFarmingPanel } from "./TapFarmingPanel";

interface TapFarmingDetailViewProps {
  onBack: () => void;
  onOpenOverlayDesigner: () => void;
}

export function TapFarmingDetailView({
  onBack,
  onOpenOverlayDesigner,
}: TapFarmingDetailViewProps) {
  const { t } = useLocalization();
  const backButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const frame = window.requestAnimationFrame(() => {
      backButtonRef.current?.focus({ preventScroll: true });
    });
    return () => window.cancelAnimationFrame(frame);
  }, []);

  return (
    <div className="page page--dashboard dashboard-live-event-detail">
      <nav
        className="dashboard-live-event-detail__navigation"
        aria-label={t("dashboard.liveEvents.title")}
      >
        <button
          ref={backButtonRef}
          className="dashboard-live-event-back"
          type="button"
          onClick={onBack}
        >
          <ArrowLeft aria-hidden="true" />
          {t("dashboard.liveEvents.back")}
        </button>
      </nav>
      <TapFarmingPanel />
      <OverlayDesignerCallout componentId="tap_farming" onOpen={onOpenOverlayDesigner} />
    </div>
  );
}
