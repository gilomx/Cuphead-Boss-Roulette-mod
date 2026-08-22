import type { ReactNode } from "react";
import type { ConfigSection } from "../App";
import { useConfig } from "../config/ConfigContext";
import { useLocalization } from "../i18n/LocalizationContext";
import { StatusBadge } from "./StatusBadge";

interface AppShellProps {
  activeSection: ConfigSection;
  children: ReactNode;
  onSectionChange: (section: ConfigSection) => void;
}

export function AppShell({ activeSection, children, onSectionChange }: AppShellProps) {
  const { locale, setLocale, t } = useLocalization();
  const { status } = useConfig();
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar__brand-block">
          <a className="brand" href="/config" aria-label={t("app.name")}>
            <img src="/assets/creator-tools/branding/modname.png" alt={t("app.name")} />
          </a>
          <StatusBadge status={status} />
        </div>

        <nav className="sidebar__nav" aria-label={t("nav.group")}>
          <p className="sidebar__group">{t("nav.group")}</p>
          <button
            className={`sidebar__item${activeSection === "roulette" ? " sidebar__item--active" : ""}`}
            type="button"
            aria-current={activeSection === "roulette" ? "page" : undefined}
            onClick={() => onSectionChange("roulette")}
          >
            {t("nav.roulette")}
          </button>
          <button
            className={`sidebar__item${activeSection === "interactions" ? " sidebar__item--active" : ""}`}
            type="button"
            aria-current={activeSection === "interactions" ? "page" : undefined}
            onClick={() => onSectionChange("interactions")}
          >
            {t("nav.interactions")}
          </button>
        </nav>

        <div className="sidebar__footer">
          <a className="sidebar__overlay-link" href="/" target="_blank" rel="noreferrer">
            {t("app.openOverlay")}
          </a>
          <div className="locale-switch" aria-label={t("app.language")}>
            <button type="button" data-active={locale === "es"} aria-pressed={locale === "es"} onClick={() => setLocale("es")}>
              ES
            </button>
            <button type="button" data-active={locale === "en"} aria-pressed={locale === "en"} onClick={() => setLocale("en")}>
              EN
            </button>
          </div>
        </div>
      </aside>
      <main className="main-content">{children}</main>
    </div>
  );
}
