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
  const currentYear = new Date().getFullYear();

  return (
    <div className="app-shell">
      <div className="locale-switch locale-switch--floating" aria-label={t("app.language")}>
        <button type="button" data-active={locale === "es"} aria-pressed={locale === "es"} onClick={() => setLocale("es")}>
          ES
        </button>
        <button type="button" data-active={locale === "en"} aria-pressed={locale === "en"} onClick={() => setLocale("en")}>
          EN
        </button>
      </div>

      <aside className="sidebar">
        <div className="sidebar__brand-block">
          <a className="brand" href="/config" aria-label={t("app.name")}>
            <img src="/assets/creator-tools/branding/modname.png" alt={t("app.name")} />
          </a>
          <StatusBadge status={status} />
        </div>

        <nav className="sidebar__nav" aria-label={t("nav.group")}>
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
          <button
            className={`sidebar__item${activeSection === "pesky" ? " sidebar__item--active" : ""}`}
            type="button"
            aria-current={activeSection === "pesky" ? "page" : undefined}
            onClick={() => onSectionChange("pesky")}
          >
            {t("nav.pesky")}
          </button>
        </nav>

        <div className="sidebar__footer">
          <p>
            {t("app.footer.madeWith")} <span aria-hidden="true">♡</span>{" "}
            <a href="https://gilo.mx" target="_blank" rel="noopener noreferrer">gilo.mx</a>{" "}
            © {currentYear}
          </p>
          <p>{t("app.footer.fanartDisclaimer")}</p>
        </div>
      </aside>
      <main className="main-content">{children}</main>
    </div>
  );
}
