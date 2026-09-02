import { useEffect, useState } from "react";
import { AppShell } from "./components/AppShell";
import { DashboardView } from "./features/dashboard/DashboardView";
import { PeskyBattleDetailView } from "./features/dashboard/PeskyBattleDetailView";
import { TapFarmingDetailView } from "./features/dashboard/TapFarmingDetailView";
import { OverlayDesignerView } from "./features/overlay-designer/OverlayDesignerView";
import type { OverlayComponentId } from "./features/overlay-designer/model";
import { InteractionsView } from "./features/interactions/InteractionsView";
import { PeskyModeView } from "./features/pesky/PeskyModeView";
import { RouletteView } from "./features/roulette/RouletteView";
import { useLocalization } from "./i18n/LocalizationContext";

export type ConfigSection = "dashboard" | "roulette" | "interactions" | "pesky";
type AppView = ConfigSection | "peskyBattle" | "tapFarming" | "overlayDesigner";

function viewFromPath(): AppView {
  if (window.location.pathname.startsWith("/config/overlay-designer")) {
    return "overlayDesigner";
  }
  if (window.location.pathname.startsWith("/config/tap-farming")) {
    return "tapFarming";
  }
  if (window.location.pathname.startsWith("/config/pesky-battle")) {
    return "peskyBattle";
  }
  if (window.location.pathname.startsWith("/dashboard")) return "dashboard";
  if (window.location.pathname.startsWith("/config/interactions")) return "interactions";
  if (window.location.pathname.startsWith("/config/pesky")) return "pesky";
  return "roulette";
}

export default function App() {
  const [view, setView] = useState<AppView>(viewFromPath);
  const { t } = useLocalization();

  useEffect(() => {
    const handlePopState = () => setView(viewFromPath());
    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, []);

  useEffect(() => {
    document.title = view === "overlayDesigner"
      ? `${t("overlayDesigner.title")} — La Pichi Ruleta`
      : view === "peskyBattle" || view === "tapFarming"
      ? `${t(view === "peskyBattle"
        ? "dashboard.peskyBattle.title"
        : "dashboard.tapFarming.title")} — La Pichi Ruleta`
      : t(view === "dashboard" ? "dashboard.documentTitle" : "app.documentTitle");
  }, [t, view]);

  const navigate = (next: AppView, state: object | null = null) => {
    if (next === view) return;
    const path = next === "dashboard"
      ? "/dashboard"
      : next === "peskyBattle"
        ? "/config/pesky-battle"
        : next === "tapFarming"
          ? "/config/tap-farming"
        : next === "overlayDesigner"
          ? "/config/overlay-designer"
        : `/config/${next}`;
    window.history.pushState(state, "", path);
    setView(next);
    window.scrollTo({ top: 0 });
  };

  const openPeskyBattle = () => {
    const currentState = window.history.state &&
      typeof window.history.state === "object"
      ? { ...window.history.state }
      : {};
    window.history.replaceState(
      { ...currentState, focusLiveEvent: "peskyBattle" },
      "",
      window.location.href,
    );
    navigate("peskyBattle", { fromLiveEvents: true });
  };

  const openTapFarming = () => {
    const currentState = window.history.state &&
      typeof window.history.state === "object"
      ? { ...window.history.state }
      : {};
    window.history.replaceState(
      { ...currentState, focusLiveEvent: "tapFarming" },
      "",
      window.location.href,
    );
    navigate("tapFarming", { fromLiveEvents: true });
  };

  const returnToLiveEvents = (focusLiveEvent: "peskyBattle" | "tapFarming") => {
    const currentState = window.history.state as {
      fromLiveEvents?: boolean;
    } | null;
    if (currentState?.fromLiveEvents && window.history.length > 1) {
      window.history.back();
      return;
    }
    window.history.pushState(
      { focusLiveEvent },
      "",
      "/dashboard",
    );
    setView("dashboard");
    window.scrollTo({ top: 0 });
  };

  const openOverlayDesigner = (component: OverlayComponentId) => {
    window.history.pushState(
      { overlayDesignerFrom: component },
      "",
      `/config/overlay-designer?component=${component}&profile=vertical`,
    );
    setView("overlayDesigner");
    window.scrollTo({ top: 0 });
  };

  const closeOverlayDesigner = () => {
    const state = window.history.state as { overlayDesignerFrom?: OverlayComponentId } | null;
    if (state?.overlayDesignerFrom && window.history.length > 1) {
      window.history.back();
      return;
    }
    const component = new URLSearchParams(window.location.search).get("component");
    const next: AppView = component === "pesky_battle" ? "peskyBattle" : "tapFarming";
    const path = next === "peskyBattle" ? "/config/pesky-battle" : "/config/tap-farming";
    window.history.pushState(null, "", path);
    setView(next);
    window.scrollTo({ top: 0 });
  };

  const activeSection: ConfigSection = view === "peskyBattle" || view === "tapFarming" ||
    view === "overlayDesigner"
    ? "dashboard"
    : view;

  return (
    <AppShell
      activeSection={activeSection}
      currentSection={view === "peskyBattle" || view === "tapFarming" ||
        view === "overlayDesigner" ? null : activeSection}
      workspace={view === "overlayDesigner"}
      onOpenOverlays={() => openOverlayDesigner("tap_farming")}
      onSectionChange={(section) => navigate(section)}
    >
      {view === "dashboard"
        ? <DashboardView
            onOpenInteractions={() => navigate("interactions")}
            onOpenPeskyBattle={openPeskyBattle}
            onOpenTapFarming={openTapFarming}
          />
        : view === "peskyBattle"
          ? <PeskyBattleDetailView
              onBack={() => returnToLiveEvents("peskyBattle")}
              onOpenOverlayDesigner={() => openOverlayDesigner("pesky_battle")}
            />
        : view === "tapFarming"
          ? <TapFarmingDetailView
              onBack={() => returnToLiveEvents("tapFarming")}
              onOpenOverlayDesigner={() => openOverlayDesigner("tap_farming")}
            />
        : view === "overlayDesigner"
          ? <OverlayDesignerView onBack={closeOverlayDesigner} />
        : view === "interactions"
        ? <InteractionsView />
        : view === "pesky"
          ? <PeskyModeView />
          : <RouletteView />}
    </AppShell>
  );
}
