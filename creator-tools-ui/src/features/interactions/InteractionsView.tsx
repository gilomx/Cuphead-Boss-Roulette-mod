import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import {
  interactionItemFor,
  interactionItems,
  type InteractionCategoryFilter,
} from "./interactionCatalog";
import { InteractionCategorySelect } from "./InteractionCategorySelect";
import { InteractionPicker } from "./InteractionPicker";
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
  const [durations, setDurations] = useState<Record<string, number>>({});
  const [countdowns, setCountdowns] = useState<Record<string, number>>({});
  const [testingItem, setTestingItem] = useState<string | null>(null);
  const [selectedTestItemId, setSelectedTestItemId] = useState<string>(
    interactionItems[0]?.id ?? "",
  );
  const [category, setCategory] = useState<InteractionCategoryFilter>("all");
  const visibleItems = interactionItems.filter((item) =>
    category === "all" || item.group === category);
  const selectedTestItem = interactionItemFor(selectedTestItemId);
  const donor = donors[selectedTestItemId] ?? "";
  const quantity = quantities[selectedTestItemId] ?? 1;
  const delay = delays[selectedTestItemId] ?? 0;
  const duration = durations[selectedTestItemId] ?? 15;
  const countdown = countdowns[selectedTestItemId] ?? 3;
  const maxBatch = interaction?.maxBatch ?? 50;
  const maxDelay = interaction?.maxDelay ?? 3600;
  const canQueue = Boolean(
    selectedTestItem &&
    (interaction?.ready ?? false) &&
    (interaction?.interactionsEnabled ?? false) &&
    donor.trim().length > 0 &&
    Number.isInteger(duration) && duration >= 1 && duration <= 120 &&
    Number.isInteger(countdown) && countdown >= 0 && countdown <= 30,
  );
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

          <div className="interaction-test-form">
            <InteractionPicker
              id="interaction-test-item"
              label={t("interactions.test.item")}
              selectedKey={selectedTestItemId}
              onSelect={(item) => setSelectedTestItemId(item.id)}
            />

            {selectedTestItem ? (
              <div className="interaction-test-fields">
                {selectedTestItem.group === "challenge" ? (
                  <div className="interaction-test-fields__challenge">
                    <label>
                      <span>{t("interactions.challenges.countdown")}</span>
                      <input type="number" min={0} max={30} step={1} value={countdown}
                        onChange={(event) => setCountdowns((current) => ({
                          ...current,
                          [selectedTestItem.id]: Number(event.target.value),
                        }))} />
                    </label>
                    <label>
                      <span>{t("interactions.challenges.duration")}</span>
                      <input type="number" min={1} max={120} step={1} value={duration}
                        onChange={(event) => setDurations((current) => ({
                          ...current,
                          [selectedTestItem.id]: Number(event.target.value),
                        }))} />
                    </label>
                  </div>
                ) : null}
                <label>
                  <span>{t("interactions.test.donorLabel")}</span>
                  <input
                    type="text"
                    maxLength={32}
                    value={donor}
                    placeholder={t("interactions.test.donorPlaceholder")}
                    onChange={(event) => setDonors((current) => ({
                      ...current,
                      [selectedTestItem.id]: event.target.value,
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
                        [selectedTestItem.id]: Math.max(
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
                        [selectedTestItem.id]: Math.max(
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
                      setTestingItem(selectedTestItem.id);
                      testInteraction(
                        selectedTestItem.id,
                        donor,
                        quantity,
                        delay,
                        duration,
                        countdown,
                      );
                    }}
                  >
                    {interactionTesting && testingItem === selectedTestItem.id
                      ? t("interactions.test.testing")
                      : t("interactions.test.action")}
                  </button>
                </div>
              </div>
            ) : null}
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
