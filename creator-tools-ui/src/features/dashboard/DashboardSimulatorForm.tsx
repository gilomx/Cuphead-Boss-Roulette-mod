import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { SearchableSelectField } from "../../components/SearchableSelectField";
import { useTikTokGiftCatalog } from "../../hooks/useTikTokGiftCatalog";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamEventType, StreamPlatform } from "../../model";

const PLATFORMS: StreamPlatform[] = ["tiktok", "twitch", "youtube"];
const EVENT_TYPES: StreamEventType[] = [
  "gift",
  "currency",
  "like",
  "follow",
  "subscription",
  "redemption",
];
const MAXIMUM_COUNT = 1_000;
const MAXIMUM_AMOUNT = 1_000_000_000;
const MAXIMUM_DELAY_SECONDS = 3_600;
const SIMULATION_PROFILES = [
  { id: "cuphead", name: "Cuphead", user: "cuphead_test" },
  { id: "cuphead-coins", name: "Cuphead Coins", user: "cuphead_coins" },
  { id: "knight", name: "Hollow Knight", user: "knight_test" },
  { id: "cup-trio", name: "Cuphead Trio", user: "cup_trio" },
  { id: "mugman", name: "Mugman", user: "mugman_test" },
];

interface SimulationDraft {
  platform: StreamPlatform;
  type: StreamEventType;
  user: string;
  userDisplayName: string;
  userId: string;
  profileId: string;
  amount: number;
  count: number;
  selectedItemId: string;
  delaySeconds: number;
}

interface DashboardSimulatorFormProps {
  active: boolean;
  onSubmitted: (result: DashboardSimulationResult) => void | Promise<void>;
}

export interface DashboardSimulationResult {
  delaySeconds: number;
}

const INITIAL_SIMULATION: SimulationDraft = {
  platform: "tiktok",
  type: "gift",
  user: "",
  userDisplayName: "",
  userId: "",
  profileId: "",
  amount: 1,
  count: 1,
  selectedItemId: "",
  delaySeconds: 0,
};

function boundedInteger(value: string, minimum: number, maximum: number) {
  return Math.max(minimum, Math.min(maximum, Math.floor(Number(value)) || minimum));
}

export function DashboardSimulatorForm({ active, onSubmitted }: DashboardSimulatorFormProps) {
  const { locale, t } = useLocalization();
  const { catalog, error: catalogError } = useTikTokGiftCatalog();
  const [simulation, setSimulation] = useState<SimulationDraft>(INITIAL_SIMULATION);
  const [simulationStatus, setSimulationStatus] = useState<"idle" | "sending" | "error">("idle");
  const activeRef = useRef(active);
  const gifts = catalog?.gifts ?? [];
  const isCatalogGift = simulation.platform === "tiktok" && simulation.type === "gift";
  const selectedGift = useMemo(
    () => gifts.find((gift) => gift.giftId === simulation.selectedItemId),
    [gifts, simulation.selectedItemId],
  );
  const canSubmit = simulationStatus !== "sending" && (!isCatalogGift || Boolean(selectedGift));

  useEffect(() => {
    activeRef.current = active;
    if (active) return;
    setSimulationStatus((current) => current === "sending" ? current : "idle");
  }, [active]);

  useEffect(() => {
    if (!simulation.selectedItemId) return;
    if (!isCatalogGift || (catalog && !selectedGift)) {
      setSimulation((current) => ({ ...current, selectedItemId: "" }));
    }
  }, [catalog, isCatalogGift, selectedGift, simulation.selectedItemId]);

  const submitSimulation = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit) return;

    const count = boundedInteger(String(simulation.count), 1, MAXIMUM_COUNT);
    const delaySeconds = boundedInteger(
      String(simulation.delaySeconds),
      0,
      MAXIMUM_DELAY_SECONDS,
    );
    const query = new URLSearchParams({
      platform: simulation.platform,
      type: simulation.type,
      user: simulation.user.trim(),
      userDisplayName: simulation.userDisplayName.trim(),
      userId: simulation.userId.trim(),
      userAvatarUrl: simulation.profileId
        ? `/assets/creator-tools/simulator/avatars/${simulation.profileId}.jpg`
        : "",
      count: String(count),
      delaySeconds: String(delaySeconds),
    });
    if (isCatalogGift && selectedGift) {
      query.set("giftId", selectedGift.giftId);
    } else {
      query.set(
        "amount",
        String(Math.max(0, Math.min(MAXIMUM_AMOUNT, simulation.amount || 0))),
      );
    }

    setSimulationStatus("sending");
    try {
      const response = await fetch(`/api/dashboard/simulate?${query}`, { cache: "no-store" });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
    } catch {
      setSimulationStatus(activeRef.current ? "error" : "idle");
      return;
    }

    setSimulationStatus("idle");
    try {
      await onSubmitted({ delaySeconds });
    } catch {
      // The simulation was already accepted even if refreshing the feed fails.
    }
  };

  const giftPickerPlaceholder = catalogError
    ? t("dashboard.simulator.itemCatalogError")
    : catalog
      ? t("dashboard.simulator.itemPlaceholder")
      : t("dashboard.simulator.itemLoading");
  return (
    <>
      <p className="dashboard-simulator__description">{t("dashboard.simulator.description")}</p>
      <form className="dashboard-simulator-form" onSubmit={(event) => void submitSimulation(event)}>
        <div className="dashboard-simulator-form__row">
          <label>
            <span>{t("dashboard.simulator.platform")}</span>
            <select
              value={simulation.platform}
              onChange={(event) => {
                const platform = event.target.value as StreamPlatform;
                setSimulation((current) => ({
                  ...current,
                  platform,
                  selectedItemId: platform === "tiktok" && current.type === "gift"
                    ? current.selectedItemId
                    : "",
                }));
              }}
            >
              {PLATFORMS.map((platform) => (
                <option key={platform} value={platform}>{t(`dashboard.platforms.${platform}`)}</option>
              ))}
            </select>
          </label>
          <label>
            <span>{t("dashboard.simulator.type")}</span>
            <select
              value={simulation.type}
              onChange={(event) => {
                const type = event.target.value as StreamEventType;
                setSimulation((current) => ({
                  ...current,
                  type,
                  selectedItemId: current.platform === "tiktok" && type === "gift"
                    ? current.selectedItemId
                    : "",
                }));
              }}
            >
              {EVENT_TYPES.map((type) => (
                <option key={type} value={type}>{t(`dashboard.eventTypes.${type}`)}</option>
              ))}
            </select>
          </label>
        </div>

        <fieldset className="dashboard-simulator-form__profiles" disabled={simulationStatus === "sending"}>
          <legend>{t("dashboard.simulator.profile")}</legend>
          <div role="group" aria-label={t("dashboard.simulator.profile")}>
            {SIMULATION_PROFILES.map((profile) => (
              <button
                key={profile.id}
                type="button"
                aria-pressed={simulation.profileId === profile.id}
                onClick={() => setSimulation((current) => ({
                  ...current,
                  profileId: profile.id,
                  user: profile.user,
                  userDisplayName: profile.name,
                  userId: `sim-${profile.id}`,
                }))}
              >
                <img src={`/assets/creator-tools/simulator/avatars/${profile.id}.jpg`} alt="" width={48} height={48} />
                <span>{profile.name}</span>
              </button>
            ))}
            <button
              type="button"
              aria-pressed={!simulation.profileId}
              onClick={() => setSimulation((current) => ({ ...current, profileId: "" }))}
            >
              <span className="dashboard-simulator-form__no-photo" aria-hidden="true">?</span>
              <span>{t("dashboard.simulator.noPhoto")}</span>
            </button>
          </div>
          <small className="dashboard-simulator-form__hint">{t("dashboard.simulator.profileHint")}</small>
        </fieldset>

        <label>
          <span>{t("dashboard.simulator.user")}</span>
          <input
            type="text"
            maxLength={80}
            value={simulation.user}
            placeholder={t("dashboard.simulator.userPlaceholder")}
            onChange={(event) => setSimulation((current) => ({ ...current, user: event.target.value }))}
          />
        </label>

        <div className="dashboard-simulator-form__row">
          <label>
            <span>{t("dashboard.simulator.displayName")}</span>
            <input
              type="text"
              maxLength={80}
              value={simulation.userDisplayName}
              placeholder={simulation.user || t("dashboard.simulator.userPlaceholder")}
              onChange={(event) => setSimulation((current) => ({ ...current, userDisplayName: event.target.value }))}
            />
          </label>
          <label>
            <span>{t("dashboard.simulator.userId")}</span>
            <input
              type="text"
              maxLength={160}
              value={simulation.userId}
              onChange={(event) => setSimulation((current) => ({ ...current, userId: event.target.value }))}
            />
          </label>
        </div>

        {isCatalogGift ? (
          <div>
            <SearchableSelectField
              id="dashboard-simulator-item"
              label={t("dashboard.simulator.item")}
              options={gifts}
              selectedKey={simulation.selectedItemId || null}
              placeholder={giftPickerPlaceholder}
              noResults={t("dashboard.simulator.itemNoResults")}
              disabled={!active || !catalog || catalogError || simulationStatus === "sending"}
              getKey={(gift) => gift.giftId}
              getLabel={(gift) => gift.name}
              getImage={(gift) => gift.imagePath}
              getMeta={(gift) => t("dashboard.simulator.itemCoins")
                .replace("{coins}", gift.coinsPerUnit.toLocaleString(locale === "es" ? "es-MX" : "en-US"))}
              getSearchTerms={(gift) => gift.aliases}
              onSelect={(gift) => setSimulation((current) => ({
                ...current,
                selectedItemId: gift.giftId,
              }))}
            />
            {selectedGift ? (
              <small className="dashboard-simulator-form__hint">
                {t("dashboard.simulator.itemValueHint")
                  .replace("{coins}", selectedGift.coinsPerUnit.toLocaleString(locale === "es" ? "es-MX" : "en-US"))}
              </small>
            ) : null}
            {catalogError ? (
              <small className="dashboard-simulator-form__hint" role="alert">
                {t("dashboard.simulator.itemCatalogError")}
              </small>
            ) : null}
          </div>
        ) : null}

        {!isCatalogGift ? (
          <div className="dashboard-simulator-form__row">
            <label>
              <span>{t("dashboard.simulator.amount")}</span>
              <input
                type="number"
                min={0}
                max={MAXIMUM_AMOUNT}
                step="any"
                value={simulation.amount}
                onChange={(event) => setSimulation((current) => ({
                  ...current,
                  amount: Math.max(0, Math.min(MAXIMUM_AMOUNT, Number(event.target.value) || 0)),
                }))}
              />
            </label>
            <label>
              <span>{t("dashboard.simulator.count")}</span>
              <input
                type="number"
                min={1}
                max={MAXIMUM_COUNT}
                value={simulation.count}
                onChange={(event) => setSimulation((current) => ({
                  ...current,
                  count: boundedInteger(event.target.value, 1, MAXIMUM_COUNT),
                }))}
              />
            </label>
          </div>
        ) : (
          <label>
            <span>{t("dashboard.simulator.count")}</span>
            <input
              type="number"
              min={1}
              max={MAXIMUM_COUNT}
              value={simulation.count}
              onChange={(event) => setSimulation((current) => ({
                ...current,
                count: boundedInteger(event.target.value, 1, MAXIMUM_COUNT),
              }))}
            />
          </label>
        )}

        <label>
          <span>{t("dashboard.simulator.delay")}</span>
          <input
            type="number"
            min={0}
            max={MAXIMUM_DELAY_SECONDS}
            step={1}
            value={simulation.delaySeconds}
            onChange={(event) => setSimulation((current) => ({
              ...current,
              delaySeconds: boundedInteger(event.target.value, 0, MAXIMUM_DELAY_SECONDS),
            }))}
          />
          <small className="dashboard-simulator-form__hint">{t("dashboard.simulator.delayHint")}</small>
        </label>

        <button
          type="submit"
          disabled={!canSubmit}
          data-sending={simulationStatus === "sending"}
        >
          {t(`dashboard.simulator.${simulationStatus === "sending" ? "sending" : "submit"}`)}
        </button>
        <p data-status={simulationStatus} role="status" aria-live="polite">
          {simulationStatus === "error"
            ? t("dashboard.simulator.error")
            : t("dashboard.simulator.hint")}
        </p>
      </form>
    </>
  );
}
