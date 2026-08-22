import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

const zeppelins = [
  {
    id: "hilda_green_zeppelin",
    key: "green",
    image: "/assets/creator-tools/interactions/green-zeppelin.png",
  },
  {
    id: "hilda_purple_zeppelin",
    key: "purple",
    image: "/assets/creator-tools/interactions/purple-zeppelin.png",
  },
] as const;

function zeppelinFor(item: string) {
  return zeppelins.find((zeppelin) => zeppelin.id === item);
}

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
  const [testingItem, setTestingItem] = useState<string | null>(null);
  const available = interaction?.available ?? false;
  const queue = [
    ...(interaction?.queue ?? []),
    ...optimisticInteractionQueue,
  ];
  const maxBatch = interaction?.maxBatch ?? 50;

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
          {zeppelins.map((zeppelin) => (
            <article className="interaction-card" key={zeppelin.id}>
              <div className="interaction-card__visual">
                <img
                  src={zeppelin.image}
                  alt={t(`interactions.zeppelin.${zeppelin.key}.imageAlt`)}
                />
              </div>
              <div className="interaction-card__content">
                <p className="interaction-card__eyebrow">{t("interactions.zeppelin.type")}</p>
                <h3>{t(`interactions.zeppelin.${zeppelin.key}.title`)}</h3>
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
                    const zeppelin = zeppelinFor(entry.item);
                    const displayStatus = entry.status === "queued" && !available
                      ? "waiting_game"
                      : entry.status;
                    return (
                      <tr key={entry.id}>
                        <td className="queue-table__position">{index + 1}</td>
                        <td>
                          <div className="interaction-item-label">
                            {zeppelin ? <img src={zeppelin.image} alt="" /> : null}
                            <span>
                              {zeppelin
                                ? t(`interactions.zeppelin.${zeppelin.key}.title`)
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
                {zeppelins.map((zeppelin) => {
                  const donor = donors[zeppelin.id] ?? "";
                  const quantity = quantities[zeppelin.id] ?? 1;
                  const canQueue = (interaction?.ready ?? false) && donor.trim().length > 0;
                  return (
                    <tr key={zeppelin.id}>
                      <td>
                        <div className="interaction-item-label interaction-item-label--test">
                          <img src={zeppelin.image} alt="" />
                          <span>{t(`interactions.zeppelin.${zeppelin.key}.title`)}</span>
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
                                [zeppelin.id]: event.target.value,
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
                                  [zeppelin.id]: Math.max(
                                    1,
                                    Math.min(maxBatch, Number(event.target.value) || 1),
                                  ),
                                }))}
                              />
                            </label>
                            <button
                              type="button"
                              disabled={!canQueue}
                              onClick={() => {
                                setTestingItem(zeppelin.id);
                                testInteraction(zeppelin.id, donor, quantity);
                              }}
                            >
                              {interactionTesting && testingItem === zeppelin.id
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
  );
}
