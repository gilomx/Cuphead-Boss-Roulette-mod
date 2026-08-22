import type { ReactNode } from "react";
import { useConfig } from "../config/ConfigContext";
import { useLocalization } from "../i18n/LocalizationContext";
import { StatusBadge } from "./StatusBadge";

export function AppShell({ children }: { children: ReactNode }) {
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

        <nav className="sidebar__nav" aria-label={t("nav.interactions")}>
          <p className="sidebar__group">{t("nav.interactions")}</p>
          <button className="sidebar__item sidebar__item--active" type="button" aria-current="page">
            {t("nav.roulette")}
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
