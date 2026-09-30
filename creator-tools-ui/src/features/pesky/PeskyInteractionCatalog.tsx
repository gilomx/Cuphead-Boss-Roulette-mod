import { Check } from "lucide-react";
import { useMemo, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { InteractionCategorySelect } from "../interactions/InteractionCategorySelect";
import {
  interactionItems,
  type InteractionCategoryFilter,
} from "../interactions/interactionCatalog";

export function PeskyInteractionCatalog() {
  const { pesky, applyPeskyItem } = useConfig();
  const { t } = useLocalization();
  const [category, setCategory] = useState<InteractionCategoryFilter>("all");
  const supportedItems = useMemo(
    () => new Set(pesky?.items ?? []),
    [pesky?.items],
  );
  const disabledItems = useMemo(
    () => new Set(pesky?.disabledItems ?? []),
    [pesky?.disabledItems],
  );
  const visibleItems = interactionItems.filter(
    (item) => category === "all" || item.group === category,
  );
  const supportedCatalogItems = interactionItems.filter((item) => supportedItems.has(item.id));
  const enabledItems = supportedCatalogItems.filter((item) => !disabledItems.has(item.id));
  const allEnabled = supportedCatalogItems.length > 0 &&
    enabledItems.length === supportedCatalogItems.length;

  const toggleAll = () => {
    const enable = !allEnabled;
    supportedCatalogItems.forEach((item) => {
      if ((!disabledItems.has(item.id)) !== enable) {
        applyPeskyItem(item.id, enable);
      }
    });
  };

  return (
    <section className="section interaction-catalog-section pesky-catalog-section"
      aria-labelledby="pesky-catalog-title">
      <div className="section__heading interaction-section-heading">
        <div>
          <h2 id="pesky-catalog-title">{t("pesky.catalog.title")}</h2>
          <p>{t("pesky.catalog.description")}</p>
        </div>
        <div className="pesky-catalog__tools">
          <span className="interaction-count" aria-label={t("pesky.catalog.enabledCount")
            .replace("{count}", String(enabledItems.length))}>
            {enabledItems.length}
          </span>
          <button className="pesky-bulk-toggle" type="button"
            disabled={!pesky?.ready || supportedCatalogItems.length === 0}
            onClick={toggleAll}>
            {t(`pesky.items.${allEnabled ? "disableAll" : "enableAll"}`)}
          </button>
          <InteractionCategorySelect value={category} onChange={setCategory} />
        </div>
      </div>

      <div className="interaction-catalog pesky-catalog">
        {visibleItems.map((item) => {
          const supported = supportedItems.has(item.id);
          const enabled = supported && !disabledItems.has(item.id);
          const label = t(item.titleKey);
          return (
            <button
              className="interaction-card pesky-catalog-card"
              type="button"
              data-enabled={enabled}
              data-supported={supported}
              aria-pressed={enabled}
              aria-label={`${t(enabled ? "pesky.catalog.disable" : "pesky.catalog.enable")} ${label}`}
              disabled={!pesky?.ready || !supported}
              title={supported ? label : t("pesky.catalog.unavailable")}
              key={item.id}
              onClick={() => applyPeskyItem(item.id, !enabled)}
            >
              <div className="interaction-card__visual">
                <img src={item.image} alt="" />
                <span className="pesky-catalog-card__check" aria-hidden="true">
                  <Check />
                </span>
              </div>
              <div className="interaction-card__content">
                <p className="interaction-card__eyebrow">
                  {t(`interactions.groups.${item.group}`)}
                </p>
                <h3>{label}</h3>
              </div>
            </button>
          );
        })}
      </div>

      {enabledItems.length === 0 ? (
        <p className="pesky-attacks__required">{t("pesky.attacks.required")}</p>
      ) : null}
    </section>
  );
}
