import { ArrowLeft } from "lucide-react";
import { useEffect, useMemo, useRef } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { LiveOverlayPreview, type OverlayPreviewPreset } from "./LiveOverlayPreview";
import { PeskyBattlePanel } from "./PeskyBattlePanel";

interface PeskyBattleDetailViewProps {
  onBack: () => void;
}

export function PeskyBattleDetailView({ onBack }: PeskyBattleDetailViewProps) {
  const { t } = useLocalization();
  const backButtonRef = useRef<HTMLButtonElement>(null);

  const previewPresets = useMemo<OverlayPreviewPreset[]>(() => {
    const participants = [
      "La Pichi",
      "Don Taza",
      "Srita. Cáliz",
      "Mugman MX",
      "CupFan",
    ].map((displayName, index) => ({
      slot: index + 1,
      userId: `preview-${index + 1}`,
      userName: `preview${index + 1}`,
      displayName,
      avatarUrl: "",
    }));
    const common = {
      revision: 1,
      capacity: 5,
      attempt: 1,
      trigger: {
        giftId: "preview-gift",
        giftName: t("dashboard.overlayPreview.pesky.gift"),
        giftImagePath: "",
      },
    };
    return [
      {
        id: "recruiting",
        label: t("dashboard.overlayPreview.pesky.recruiting"),
        state: { ...common, phase: "recruiting", participants: participants.slice(0, 3) },
      },
      {
        id: "ready",
        label: t("dashboard.overlayPreview.pesky.ready"),
        state: { ...common, phase: "ready", participants },
      },
      {
        id: "active",
        label: t("dashboard.overlayPreview.pesky.active"),
        state: { ...common, phase: "active", participants },
      },
      {
        id: "won",
        label: t("dashboard.overlayPreview.pesky.won"),
        state: { ...common, phase: "won", participants },
      },
    ];
  }, [t]);

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
      <LiveOverlayPreview
        overlay="pesky-battle"
        src="/pesky-battle-overlay"
        presets={previewPresets}
      />
    </div>
  );
}
