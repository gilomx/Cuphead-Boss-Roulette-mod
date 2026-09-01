import { ArrowLeft } from "lucide-react";
import { useEffect, useMemo, useRef } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import {
  LiveOverlayPreview,
  type OverlayPreviewPreset,
} from "./LiveOverlayPreview";
import { TapFarmingPanel } from "./TapFarmingPanel";

interface TapFarmingDetailViewProps {
  onBack: () => void;
}

function phases(activeIndex: number, progress: number) {
  return Array.from({ length: 4 }, (_, index) => ({
    index: index + 1,
    status: index + 1 < activeIndex
      ? "complete"
      : index + 1 === activeIndex
        ? "active"
        : "pending",
    progress: index + 1 < activeIndex
      ? 1
      : index + 1 === activeIndex
        ? progress
        : 0,
  }));
}

function simulateTaps(state: Record<string, unknown>, amount: number) {
  const conversion = state.conversion && typeof state.conversion === "object"
    ? state.conversion as Record<string, unknown>
    : {};
  const counters = state.counters && typeof state.counters === "object"
    ? state.counters as Record<string, unknown>
    : {};
  const tapsPerHealthPoint = Math.max(
    1,
    Math.floor(Number(conversion.tapsPerHealthPoint) || 2),
  );
  const remainder = Math.max(0, Number(counters.unconvertedTaps) || 0) + amount;
  const healthAdded = Math.floor(remainder / tapsPerHealthPoint);
  const nextRemainder = remainder % tapsPerHealthPoint;
  const nextReserve = Math.max(0, Number(counters.reserveHealth) || 0) + healthAdded;
  return {
    ...state,
    revision: Math.max(1, Number(state.revision) || 1) + 1,
    counters: {
      ...counters,
      totalTaps: Math.max(0, Number(counters.totalTaps) || 0) + amount,
      bankedTaps: Math.round(nextReserve * tapsPerHealthPoint + nextRemainder),
      unconvertedTaps: nextRemainder,
      convertedHealth:
        Math.max(0, Number(counters.convertedHealth) || 0) + healthAdded,
      reserveHealth: nextReserve,
    },
  };
}

export function TapFarmingDetailView({ onBack }: TapFarmingDetailViewProps) {
  const { t } = useLocalization();
  const backButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const frame = window.requestAnimationFrame(() => {
      backButtonRef.current?.focus({ preventScroll: true });
    });
    return () => window.cancelAnimationFrame(frame);
  }, []);

  const previewPresets = useMemo<OverlayPreviewPreset[]>(() => {
    const common = {
      revision: 1,
      bossName: t("dashboard.overlayPreview.tapFarming.boss"),
      levelId: "preview-level",
      attempt: 1,
      conversion: { tapsPerHealthPoint: 2 },
      boss: { currentHealth: 1810, totalHealth: 3000, progress: 0.4 },
      phaseCount: 4,
    };
    return [
      {
        id: "collecting",
        label: t("dashboard.overlayPreview.tapFarming.collecting"),
        state: {
          ...common,
          phase: "collecting",
          bossName: "",
          phaseIndex: 0,
          overallProgress: 0,
          phases: phases(1, 0),
          counters: {
            totalTaps: 4320,
            bankedTaps: 4320,
            unconvertedTaps: 0,
            convertedHealth: 2160,
            reserveHealth: 2160,
            spentHealth: 0,
          },
        },
      },
      {
        id: "active",
        label: t("dashboard.overlayPreview.tapFarming.active"),
        state: {
          ...common,
          phase: "active",
          phaseIndex: 2,
          overallProgress: 0.43,
          phases: phases(2, 0.72),
          counters: {
            totalTaps: 25680,
            bankedTaps: 7680,
            unconvertedTaps: 0,
            convertedHealth: 12840,
            reserveHealth: 3840,
            spentHealth: 9000,
          },
        },
      },
      {
        id: "transition",
        label: t("dashboard.overlayPreview.tapFarming.transition"),
        state: {
          ...common,
          phase: "transition",
          phaseIndex: 2,
          overallProgress: 0.5,
          phases: phases(2, 1),
          counters: {
            totalTaps: 27930,
            bankedTaps: 9930,
            unconvertedTaps: 0,
            convertedHealth: 13965,
            reserveHealth: 4965,
            spentHealth: 9000,
          },
        },
      },
      {
        id: "completed",
        label: t("dashboard.overlayPreview.tapFarming.completed"),
        state: {
          ...common,
          phase: "completed",
          phaseIndex: 4,
          overallProgress: 1,
          phases: phases(5, 1),
          counters: {
            totalTaps: 48720,
            bankedTaps: 0,
            unconvertedTaps: 0,
            convertedHealth: 24360,
            reserveHealth: 0,
            spentHealth: 24360,
          },
        },
      },
    ];
  }, [t]);

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
      <LiveOverlayPreview
        overlay="tap-farming"
        src="/tap-farming-overlay"
        presets={previewPresets}
        simulateTaps={simulateTaps}
      />
    </div>
  );
}
