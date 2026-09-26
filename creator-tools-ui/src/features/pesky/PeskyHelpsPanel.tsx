import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItems } from "../interactions/interactionCatalog";

export function PeskyHelpsPanel() {
  const { pesky, applyPeskyItem } = useConfig();
  const { t } = useLocalization();
  const helpItems = interactionItems.filter((item) => item.group === "help");
  const supportedHelpItems = helpItems.filter((item) => pesky?.items.includes(item.id));
  const enabledHelpItems = supportedHelpItems.filter(
    (item) => !pesky?.disabledItems.includes(item.id),
  );
  const allHelpsEnabled = supportedHelpItems.length > 0 &&
    enabledHelpItems.length === supportedHelpItems.length;

  const toggleAllHelps = () => {
    const enable = !allHelpsEnabled;
    supportedHelpItems.forEach((item) => {
      const enabled = !pesky?.disabledItems.includes(item.id);
      if (enabled !== enable) applyPeskyItem(item.id, enable);
    });
  };

  return (
    <section className="interaction-panel pesky-attacks" aria-labelledby="pesky-helps-title">
      <div className="interaction-panel__heading">
        <div>
          <h2 id="pesky-helps-title">{t("interactions.groups.help")}</h2>
          <p>{t("pesky.helps.description")}</p>
        </div>
        <div className="pesky-attacks__heading-actions">
          <span className="interaction-count">{enabledHelpItems.length}</span>
          <button
            className="pesky-bulk-toggle"
            type="button"
            disabled={!pesky?.ready || supportedHelpItems.length === 0}
            onClick={toggleAllHelps}
          >
            {t(`pesky.items.${allHelpsEnabled ? "disableAll" : "enableAll"}`)}
          </button>
        </div>
      </div>
      <div className="pesky-attack-list">
        {helpItems.map((item) => {
          const supported = Boolean(pesky?.items.includes(item.id));
          const enabled = supported && !pesky?.disabledItems.includes(item.id);
          return (
            <label className="pesky-attack" data-enabled={enabled} key={item.id}>
              <img src={item.image} alt="" />
              <span>
                <strong>{t(item.titleKey)}</strong>
                <small>{t(item.descriptionKey)}</small>
              </span>
              <input
                type="checkbox"
                checked={enabled}
                disabled={!pesky?.ready || !supported}
                onChange={(event) => applyPeskyItem(item.id, event.target.checked)}
              />
            </label>
          );
        })}
      </div>
    </section>
  );
}
