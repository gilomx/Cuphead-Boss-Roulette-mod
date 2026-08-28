import { useEffect, useState } from "react";
import { AppShell } from "./components/AppShell";
import { DashboardView } from "./features/dashboard/DashboardView";
import { InteractionsView } from "./features/interactions/InteractionsView";
import { PeskyModeView } from "./features/pesky/PeskyModeView";
import { RouletteView } from "./features/roulette/RouletteView";
import { useLocalization } from "./i18n/LocalizationContext";

export type ConfigSection = "dashboard" | "roulette" | "interactions" | "pesky";

function sectionFromPath(): ConfigSection {
  if (window.location.pathname.startsWith("/dashboard")) return "dashboard";
  if (window.location.pathname.startsWith("/config/interactions")) return "interactions";
  if (window.location.pathname.startsWith("/config/pesky")) return "pesky";
  return "roulette";
}

export default function App() {
  const [section, setSection] = useState<ConfigSection>(sectionFromPath);
  const { t } = useLocalization();

  useEffect(() => {
    const handlePopState = () => setSection(sectionFromPath());
    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, []);

  useEffect(() => {
    document.title = t(section === "dashboard" ? "dashboard.documentTitle" : "app.documentTitle");
  }, [section, t]);

  const navigate = (next: ConfigSection) => {
    if (next === section) return;
    window.history.pushState(null, "", next === "dashboard" ? "/dashboard" : `/config/${next}`);
    setSection(next);
    window.scrollTo({ top: 0 });
  };

  return (
    <AppShell activeSection={section} onSectionChange={navigate}>
      {section === "dashboard"
        ? <DashboardView onOpenInteractions={() => navigate("interactions")} />
        : section === "interactions"
        ? <InteractionsView />
        : section === "pesky"
          ? <PeskyModeView />
          : <RouletteView />}
    </AppShell>
  );
}
