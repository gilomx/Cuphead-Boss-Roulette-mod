import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItemFor, interactionItems } from "./interactionCatalog";
import { InteractionPicker } from "./InteractionPicker";

interface InteractionTestSectionProps {
  onSent: () => void;
}

export function InteractionTestSection({ onSent }: InteractionTestSectionProps) {
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
  const [selectedItemId, setSelectedItemId] = useState<string>(
    interactionItems[0]?.id ?? "",
  );
  const selectedItem = interactionItemFor(selectedItemId);
  const donor = donors[selectedItemId] ?? "";
  const testDonor = donor.trim() || t("interactions.test.defaultDonor");
  const quantity = quantities[selectedItemId] ?? 1;
  const delay = delays[selectedItemId] ?? 0;
  const duration = durations[selectedItemId] ?? 15;
  const countdown = countdowns[selectedItemId] ?? 3;
  const maxBatch = interaction?.maxBatch ?? 50;
  const maxDelay = interaction?.maxDelay ?? 3600;
  const canSend = Boolean(
    selectedItem &&
    interaction?.ready &&
    Number.isInteger(duration) && duration >= 1 && duration <= 120 &&
    Number.isInteger(countdown) && countdown >= 0 && countdown <= 30,
  );
  const feedback = optimisticInteractionQueue.length > 0
    ? "waiting_game"
    : interaction?.feedback ?? "ready";
  const showFeedback = feedback !== "ready" && feedback !== "settings_saved";

  useEffect(() => {
    if (!interactionTesting) setTestingItem(null);
  }, [interactionTesting]);

  return (
    <section
      className="interaction-settings-tests interaction-tests"
      aria-labelledby="interaction-tests-title"
    >
      <div className="interaction-settings-tests__heading">
        <h3 id="interaction-tests-title">{t("interactions.test.title")}</h3>
        <p>{t("interactions.test.description")}</p>
      </div>

      <div className="interaction-test-form">
        <InteractionPicker
          id="interaction-test-item"
          label={t("interactions.test.item")}
          selectedKey={selectedItemId}
          onSelect={(item) => setSelectedItemId(item.id)}
        />

        {selectedItem ? (
          <div className="interaction-test-fields">
            {selectedItem.group === "challenge" ? (
              <div className="interaction-test-fields__challenge">
                <label>
                  <span>{t("interactions.challenges.countdown")}</span>
                  <input
                    type="number"
                    min={0}
                    max={30}
                    step={1}
                    value={countdown}
                    onChange={(event) => setCountdowns((current) => ({
                      ...current,
                      [selectedItem.id]: Number(event.target.value),
                    }))}
                  />
                </label>
                <label>
                  <span>{t("interactions.challenges.duration")}</span>
                  <input
                    type="number"
                    min={1}
                    max={120}
                    step={1}
                    value={duration}
                    onChange={(event) => setDurations((current) => ({
                      ...current,
                      [selectedItem.id]: Number(event.target.value),
                    }))}
                  />
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
                  [selectedItem.id]: event.target.value,
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
                    [selectedItem.id]: Math.max(
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
                    [selectedItem.id]: Math.max(
                      0,
                      Math.min(maxDelay, Number(event.target.value) || 0),
                    ),
                  }))}
                />
              </label>
              <button
                type="button"
                disabled={!canSend}
                onClick={() => {
                  setTestingItem(selectedItem.id);
                  testInteraction(
                    selectedItem.id,
                    testDonor,
                    quantity,
                    delay,
                    duration,
                    countdown,
                  );
                  onSent();
                }}
              >
                {interactionTesting && testingItem === selectedItem.id
                  ? t("interactions.test.testing")
                  : t("interactions.test.action")}
              </button>
            </div>
          </div>
        ) : null}
      </div>

      {showFeedback ? (
        <p
          className="interaction-tests__feedback"
          data-error={interaction?.error ?? false}
          role="status"
          aria-live="polite"
        >
          {t(`interactions.feedback.${feedback}`)}
        </p>
      ) : null}
    </section>
  );
}
