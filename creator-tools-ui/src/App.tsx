import { useEffect, useState } from "react";
import { AppShell } from "./components/AppShell";
import { InteractionsView } from "./features/interactions/InteractionsView";
import { RouletteView } from "./features/roulette/RouletteView";

export type ConfigSection = "roulette" | "interactions";

function sectionFromPath(): ConfigSection {
  return window.location.pathname.startsWith("/config/interactions")
    ? "interactions"
    : "roulette";
}

export default function App() {
  const [section, setSection] = useState<ConfigSection>(sectionFromPath);

  useEffect(() => {
    const handlePopState = () => setSection(sectionFromPath());
    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, []);

  const navigate = (next: ConfigSection) => {
    if (next === section) return;
    window.history.pushState(null, "", `/config/${next}`);
    setSection(next);
  };

  return (
    <AppShell activeSection={section} onSectionChange={navigate}>
      {section === "interactions" ? <InteractionsView /> : <RouletteView />}
    </AppShell>
  );
}
