import { useCallback, useState } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import {
  interactionItems,
  type InteractionCategoryFilter,
} from "./interactionCatalog";
import { InteractionCategorySelect } from "./InteractionCategorySelect";
import { InteractionSettingsPanel } from "./InteractionSettingsPanel";
import { StreamRulesView } from "./StreamRulesView";

type InteractionWorkspaceView = "rules" | "settings";

export function InteractionsView() {
  const { t } = useLocalization();
  const [workspaceView, setWorkspaceView] = useState<InteractionWorkspaceView>("rules");
  const [testSentNotice, setTestSentNotice] = useState(false);
  const [category, setCategory] = useState<InteractionCategoryFilter>("all");
  const visibleItems = interactionItems.filter((item) =>
    category === "all" || item.group === category);
  const dismissTestSentNotice = useCallback(() => setTestSentNotice(false), []);

  return (
    <div className="page page--interactions">
      <header className="page-header interaction-page-header">
        <div>
          <h1>{t("interactions.title")}</h1>
          <p>{t("interactions.description")}</p>
        </div>
      </header>

      <section className="section interaction-catalog-section" aria-labelledby="interaction-catalog-title">
        <div className="section__heading interaction-section-heading">
          <h2 id="interaction-catalog-title">{t("interactions.catalog.title")}</h2>
          <InteractionCategorySelect value={category} onChange={setCategory} />
        </div>

        <p className="interaction-catalog-note">{t("interactions.groups.description")}</p>
        {category === "all" || category === "mini_boss" ? (
          <p className="interaction-catalog-note">
            {t("interactions.miniBoss.description")}{" "}
            {t("interactions.miniBoss.compatibility")}
          </p>
        ) : null}

        <div className="interaction-catalog">
          {visibleItems.map((item) => (
            <article className="interaction-card" key={item.id}>
              <div className="interaction-card__visual">
                <img
                  src={item.image}
                  alt={t(item.imageAltKey)}
                />
              </div>
              <div className="interaction-card__content">
                <p className="interaction-card__eyebrow">{t(`interactions.groups.${item.group}`)}</p>
                <h3>{t(item.titleKey)}</h3>
              </div>
            </article>
          ))}
        </div>
      </section>

      <div className="interaction-workspace">
        <div
          className="interaction-workspace__view"
          data-view={workspaceView}
          key={workspaceView}
        >
          {workspaceView === "rules" ? (
            <StreamRulesView
              onOpenSettings={() => setWorkspaceView("settings")}
              testSentNotice={testSentNotice}
              onTestSentNoticeDismissed={dismissTestSentNotice}
            />
          ) : (
            <InteractionSettingsPanel
              onBack={() => setWorkspaceView("rules")}
              onTestSent={() => {
                setTestSentNotice(true);
                setWorkspaceView("rules");
              }}
            />
          )}
        </div>
      </div>
    </div>
  );
}
