import { AppShell } from "./components/AppShell";
import { RouletteView } from "./features/roulette/RouletteView";

export default function App() {
  return (
    <AppShell>
      <RouletteView />
    </AppShell>
  );
}
