import { ArrowLeft } from "lucide-react";
import { useEffect, useRef } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { OverlayDesignerCallout } from "./OverlayDesignerCallout";
import { PeskyBattlePanel } from "./PeskyBattlePanel";

interface PeskyBattleDetailViewProps {
  onBack: () => void;
  onOpenOverlayDesigner: () => void;
}

export function PeskyBattleDetailView({
  onBack,
  onOpenOverlayDesigner,
}: PeskyBattleDetailViewProps) {
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
      <PeskyBattlePanel />
      <OverlayDesignerCallout componentId="pesky_battle" onOpen={onOpenOverlayDesigner} />
    </div>
  );
}
