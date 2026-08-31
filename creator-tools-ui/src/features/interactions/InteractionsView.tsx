import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItems } from "./interactionCatalog";
import { InteractionSettingsPanel } from "./InteractionSettingsPanel";
import { StreamRulesView } from "./StreamRulesView";

export function InteractionsView() {
  const {
    interaction,
    optimisticInteractionQueue,
    interactionTesting,
    testInteraction,
  } = useConfig();
  const { t } = useLocalization();
  const [donors, setDonors] = useState<Record<string, string>>({});
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [delays, setDelays] = useState<Record<string, number>>({});
  const [testingItem, setTestingItem] = useState<string | null>(null);
  const maxBatch = interaction?.maxBatch ?? 50;
  const maxDelay = interaction?.maxDelay ?? 3600;
  const testFeedback = optimisticInteractionQueue.length > 0
    ? "waiting_game"
    : interaction?.feedback ?? "ready";
  const showTestFeedback = testFeedback !== "ready" &&
    testFeedback !== "settings_saved";

  useEffect(() => {
    if (!interactionTesting) setTestingItem(null);
  }, [interactionTesting]);

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
        </div>

        <div className="interaction-catalog">
          {interactionItems.map((item) => (
            <article className="interaction-card" key={item.id}>
              <div className="interaction-card__visual">
                <img
                  src={item.image}
                  alt={t(item.imageAltKey)}
                />
              </div>
              <div className="interaction-card__content">
                <p className="interaction-card__eyebrow">{t(item.typeKey)}</p>
                <h3>{t(item.titleKey)}</h3>
              </div>
            </article>
          ))}
        </div>
      </section>

      <div className="interaction-workspace">
        <StreamRulesView />

        <div className="interaction-workspace__tools">
          <InteractionSettingsPanel />

        <section className="interaction-panel interaction-tests" aria-labelledby="interaction-tests-title">
          <div className="interaction-panel__heading">
            <div>
              <h2 id="interaction-tests-title">{t("interactions.test.title")}</h2>
              <p>{t("interactions.test.description")}</p>
            </div>
          </div>

          <div className="interaction-table-wrap">
            <table className="interaction-table test-table">
              <thead>
                <tr>
                  <th scope="col">{t("interactions.test.item")}</th>
                  <th scope="col">{t("interactions.test.configuration")}</th>
                </tr>
              </thead>
              <tbody>
                {interactionItems.map((item) => {
                  const donor = donors[item.id] ?? "";
                  const quantity = quantities[item.id] ?? 1;
                  const delay = delays[item.id] ?? 0;
                  const canQueue = (interaction?.ready ?? false) &&
                    (interaction?.interactionsEnabled ?? false) &&
                    donor.trim().length > 0;
                  return (
                    <tr key={item.id}>
                      <td>
                        <div className="interaction-item-label interaction-item-label--test">
                          <img src={item.image} alt="" />
                          <span>{t(item.titleKey)}</span>
                        </div>
                      </td>
                      <td>
                        <div className="interaction-test-fields">
                          <label>
                            <span>{t("interactions.test.donorLabel")}</span>
                            <input
                              type="text"
                              maxLength={32}
                              value={donor}
                              placeholder={t("interactions.test.donorPlaceholder")}
                              onChange={(event) => setDonors((current) => ({
                                ...current,
                                [item.id]: event.target.value,
                              }))}
                            />
                          </label>
                          <div className="interaction-test-fields__action">
                            <label className="interaction-quantity">
                              <span>{t("interactions.test.quantityLabel")}</span>
                              <input
                                type="number"
                                min={1}
                                max={maxBatch}
                                value={quantity}
                                onChange={(event) => setQuantities((current) => ({
                                  ...current,
                                  [item.id]: Math.max(
                                    1,
                                    Math.min(maxBatch, Number(event.target.value) || 1),
                                  ),
                                }))}
                              />
                            </label>
                            <label className="interaction-delay">
                              <span>{t("interactions.test.delayLabel")}</span>
                              <input
                                type="number"
                                min={0}
                                max={maxDelay}
                                step={0.5}
                                value={delay}
                                onChange={(event) => setDelays((current) => ({
                                  ...current,
                                  [item.id]: Math.max(
                                    0,
                                    Math.min(maxDelay, Number(event.target.value) || 0),
                                  ),
                                }))}
                              />
                            </label>
                            <button
                              type="button"
                              disabled={!canQueue}
                              onClick={() => {
                                setTestingItem(item.id);
                                testInteraction(item.id, donor, quantity, delay);
                              }}
                            >
                              {interactionTesting && testingItem === item.id
                                ? t("interactions.test.testing")
                                : t("interactions.test.action")}
                            </button>
                          </div>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {showTestFeedback ? (
            <p
              className="interaction-tests__feedback"
              data-error={interaction?.error ?? false}
              role="status"
              aria-live="polite"
            >
              {t(`interactions.feedback.${testFeedback}`)}
            </p>
          ) : null}
        </section>
        </div>
      </div>
    </div>
  );
}
