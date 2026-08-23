import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

const interactionItems = [
  {
    id: "hilda_green_zeppelin",
    titleKey: "interactions.zeppelin.green.title",
    imageAltKey: "interactions.zeppelin.green.imageAlt",
    typeKey: "interactions.zeppelin.type",
    image: "/assets/creator-tools/interactions/green-zeppelin.png",
  },
  {
    id: "hilda_purple_zeppelin",
    titleKey: "interactions.zeppelin.purple.title",
    imageAltKey: "interactions.zeppelin.purple.imageAlt",
    typeKey: "interactions.zeppelin.type",
    image: "/assets/creator-tools/interactions/purple-zeppelin.png",
  },
  {
    id: "rootpack_homing_carrot",
    titleKey: "interactions.rootpack.homingCarrot.title",
    imageAltKey: "interactions.rootpack.homingCarrot.imageAlt",
    typeKey: "interactions.rootpack.type",
    image: "/assets/creator-tools/interactions/homing-carrot.png",
  },
  {
    id: "cagney_homing_plant",
    titleKey: "interactions.cagney.homingPlant.title",
    imageAltKey: "interactions.cagney.homingPlant.imageAlt",
    typeKey: "interactions.cagney.type",
    image: "/assets/creator-tools/interactions/cagney-homing-plant.png",
  },
] as const;

function interactionItemFor(item: string) {
  return interactionItems.find((catalogItem) => catalogItem.id === item);
}

export function InteractionsView() {
  const {
    interaction,
    optimisticInteractionQueue,
    interactionTesting,
    applyInteractionMaxActive,
    applyInteractionRandomTest,
    testInteraction,
  } = useConfig();
  const { t } = useLocalization();
  const [donors, setDonors] = useState<Record<string, string>>({});
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [delays, setDelays] = useState<Record<string, number>>({});
  const [maxActiveDraft, setMaxActiveDraft] = useState(1);
  const [testingItem, setTestingItem] = useState<string | null>(null);
  const available = interaction?.available ?? false;
  const randomTestEnabled = interaction?.randomTestEnabled ?? false;
  const queue = [
    ...(interaction?.queue ?? []),
    ...optimisticInteractionQueue,
  ];
  const maxBatch = interaction?.maxBatch ?? 50;
  const maxDelay = interaction?.maxDelay ?? 3600;

  useEffect(() => {
    if (!interactionTesting) setTestingItem(null);
  }, [interactionTesting]);

  useEffect(() => {
    if (typeof interaction?.maxActive === "number") {
      setMaxActiveDraft(interaction.maxActive);
    }
  }, [interaction?.maxActive]);

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
        <section className="interaction-panel interaction-queue" aria-labelledby="interaction-queue-title">
          <div className="interaction-panel__heading">
            <div>
              <h2 id="interaction-queue-title">{t("interactions.queue.title")}</h2>
              <p>{t("interactions.queue.description")}</p>
            </div>
            <span className="interaction-count" aria-label={t("interactions.queue.countLabel")}>
              {queue.length}
            </span>
          </div>

          {queue.length === 0 ? (
            <div className="interaction-queue__empty">
              <strong>{t("interactions.queue.emptyTitle")}</strong>
              <span>{t("interactions.queue.emptyDescription")}</span>
            </div>
          ) : (
            <div className="interaction-table-wrap">
              <table className="interaction-table queue-table">
                <thead>
                  <tr>
                    <th scope="col">{t("interactions.queue.position")}</th>
                    <th scope="col">{t("interactions.queue.item")}</th>
                    <th scope="col">{t("interactions.queue.donor")}</th>
                    <th scope="col">{t("interactions.queue.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {queue.map((entry, index) => {
                    const item = interactionItemFor(entry.item);
                    const displayStatus = entry.status === "queued" && !available
                      ? "waiting_game"
                      : entry.status;
                    return (
                      <tr key={entry.id}>
                        <td className="queue-table__position">{index + 1}</td>
                        <td>
                          <div className="interaction-item-label">
                            {item ? <img src={item.image} alt="" /> : null}
                            <span>
                              {item
                                ? t(item.titleKey)
                                : entry.item}
                            </span>
                          </div>
                        </td>
                        <td className="queue-table__donor">{entry.donor}</td>
                        <td>
                          <span className="queue-status" data-status={displayStatus}>
                            {t(`interactions.queue.${displayStatus}`)}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <div className="interaction-workspace__tools">
          <section
            className="interaction-panel interaction-settings-section"
            aria-labelledby="interaction-settings-title"
          >
            <div className="interaction-panel__heading interaction-settings-heading">
              <h2 id="interaction-settings-title">{t("interactions.settings.title")}</h2>
              <p>{t("interactions.settings.description")}</p>
            </div>
            <form
              className="interaction-settings"
              onSubmit={(event) => {
                event.preventDefault();
                applyInteractionMaxActive(maxActiveDraft);
              }}
            >
              <label>
                <span>{t("interactions.settings.maxActiveLabel")}</span>
                <input
                  type="number"
                  min={1}
                  max={interaction?.maxActiveLimit ?? 20}
                  value={maxActiveDraft}
                  onChange={(event) => setMaxActiveDraft(Math.max(
                    1,
                    Math.min(
                      interaction?.maxActiveLimit ?? 20,
                      Number(event.target.value) || 1,
                    ),
                  ))}
                />
              </label>
              <button type="submit" disabled={!interaction?.ready}>
                {t("interactions.settings.save")}
              </button>
            </form>
          </section>

        <section className="interaction-panel interaction-tests" aria-labelledby="interaction-tests-title">
          <div className="interaction-panel__heading">
            <div>
              <h2 id="interaction-tests-title">{t("interactions.test.title")}</h2>
              <p>{t("interactions.test.description")}</p>
            </div>
          </div>

          <div className="interaction-random-test" data-active={randomTestEnabled}>
            <div className="interaction-random-test__copy">
              <div className="interaction-random-test__title">
                <strong>{t("interactions.test.random.title")}</strong>
                <span data-active={randomTestEnabled}>
                  {t(`interactions.test.random.${randomTestEnabled ? "active" : "inactive"}`)}
                </span>
              </div>
              <p>{t("interactions.test.random.description")}</p>
            </div>
            <button
              type="button"
              aria-pressed={randomTestEnabled}
              data-active={randomTestEnabled}
              disabled={!interaction?.ready}
              onClick={() => applyInteractionRandomTest(!randomTestEnabled)}
            >
              {t(`interactions.test.random.${randomTestEnabled ? "disable" : "enable"}`)}
            </button>
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
                  const canQueue = (interaction?.ready ?? false) && donor.trim().length > 0;
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

          <p
            className="interaction-tests__feedback"
            data-error={interaction?.error ?? false}
            role="status"
            aria-live="polite"
          >
            {t(`interactions.feedback.${
              optimisticInteractionQueue.length > 0
                ? "waiting_game"
                : interaction?.feedback ?? "ready"
            }`)}
          </p>
        </section>
        </div>
      </div>
    </div>
  );
}
