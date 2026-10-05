import { useEffect, useRef, useState, type FormEvent } from "react";
import { SearchableSelectField } from "../../components/SearchableSelectField";
import { useTikTokGiftCatalog } from "../../hooks/useTikTokGiftCatalog";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { StreamEventType, StreamPlatform, TikTokGift } from "../../model";

const PLATFORMS: StreamPlatform[] = ["tiktok", "twitch", "youtube"];
const EVENT_TYPES: StreamEventType[] = [
  "gift",
  "currency",
  "like",
  "follow",
  "subscription",
  "redemption",
  "chat",
];
const MAXIMUM_COUNT = 1_000;
const MAXIMUM_AMOUNT = 1_000_000_000;
const MAXIMUM_DELAY_SECONDS = 3_600;
const SIMULATION_PROFILES = [
  { id: "cuphead", name: "Cuphead" },
  { id: "cuphead-coins", name: "Cuphead Coins" },
  { id: "knight", name: "Hollow Knight" },
  { id: "cup-trio", name: "Cuphead Trio" },
  { id: "mugman", name: "Mugman" },
];

interface SimulationDraft {
  key: number;
  platform: StreamPlatform;
  type: StreamEventType;
  displayName: string;
  profileId: string;
  amount: number;
  count: number;
  selectedItemId: string;
  delaySeconds: number;
  chatText: string;
}

interface DashboardSimulatorFormProps {
  active: boolean;
  onSubmitted: (result: DashboardSimulationResult) => void | Promise<void>;
}

export interface DashboardSimulationResult {
  eventCount: number;
  delaySeconds: number;
}

function createSimulationDraft(
  key: number,
  profileIndex = 0,
  source?: SimulationDraft,
): SimulationDraft {
  const profile = SIMULATION_PROFILES[profileIndex % SIMULATION_PROFILES.length];
  return {
    key,
    platform: source?.platform ?? "tiktok",
    type: source?.type ?? "gift",
    displayName: profile.name,
    profileId: profile.id,
    amount: source?.amount ?? 1,
    count: source?.count ?? 1,
    selectedItemId: source?.selectedItemId ?? "",
    delaySeconds: source?.delaySeconds ?? 0,
    chatText: source?.chatText ?? "1",
  };
}

function boundedInteger(value: string, minimum: number, maximum: number) {
  return Math.max(minimum, Math.min(maximum, Math.floor(Number(value)) || minimum));
}

export function simulationUserId(displayName: string, profileId: string) {
  const normalizedName = displayName
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLocaleLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "") || "usuario";
  return `sim-${profileId}-${normalizedName}`.slice(0, 160);
}

function isCatalogGift(simulation: SimulationDraft) {
  return simulation.platform === "tiktok" && simulation.type === "gift";
}

export function DashboardSimulatorForm({ active, onSubmitted }: DashboardSimulatorFormProps) {
  const { locale, t } = useLocalization();
  const { catalog, error: catalogError } = useTikTokGiftCatalog();
  const [simulations, setSimulations] = useState<SimulationDraft[]>([
    createSimulationDraft(1),
  ]);
  const [simulationStatus, setSimulationStatus] = useState<"idle" | "sending" | "error">("idle");
  const activeRef = useRef(active);
  const nextSimulationKey = useRef(2);
  const gifts = catalog?.gifts ?? [];
  const selectedGift = (simulation: SimulationDraft): TikTokGift | undefined =>
    gifts.find((gift) => gift.giftId === simulation.selectedItemId);
  const canSubmit = simulationStatus !== "sending" && simulations.every((simulation) =>
    Boolean(simulation.displayName.trim()) &&
    (simulation.type !== "chat" || /^[1-6]$/.test(simulation.chatText.trim())) &&
    (!isCatalogGift(simulation) || Boolean(selectedGift(simulation))));
  const multiple = simulations.length > 1;

  useEffect(() => {
    activeRef.current = active;
    if (active) return;
    setSimulationStatus((current) => current === "sending" ? current : "idle");
  }, [active]);

  useEffect(() => {
    setSimulations((current) => {
      let changed = false;
      const next = current.map((simulation) => {
        if (!simulation.selectedItemId) return simulation;
        const valid = isCatalogGift(simulation) &&
          (!catalog || catalog.gifts.some((gift) => gift.giftId === simulation.selectedItemId));
        if (valid) return simulation;
        changed = true;
        return { ...simulation, selectedItemId: "" };
      });
      return changed ? next : current;
    });
  }, [catalog]);

  const updateSimulation = (
    key: number,
    update: (current: SimulationDraft) => SimulationDraft,
  ) => {
    setSimulations((current) => current.map((simulation) =>
      simulation.key === key ? update(simulation) : simulation));
    setSimulationStatus((current) => current === "error" ? "idle" : current);
  };

  const selectProfile = (simulationKey: number, profileId: string) => {
    const profile = SIMULATION_PROFILES.find((candidate) => candidate.id === profileId);
    if (!profile) return;
    updateSimulation(simulationKey, (current) => {
      const previousProfile = SIMULATION_PROFILES.find((candidate) => candidate.id === current.profileId);
      const replaceDefaultName = !current.displayName.trim() || current.displayName === previousProfile?.name;
      return {
        ...current,
        profileId,
        displayName: replaceDefaultName ? profile.name : current.displayName,
      };
    });
  };

  const addSimulation = () => {
    const key = nextSimulationKey.current;
    nextSimulationKey.current += 1;
    setSimulations((current) => {
      const usedProfiles = new Set(current.map((simulation) => simulation.profileId));
      const availableProfile = SIMULATION_PROFILES.findIndex((profile) => !usedProfiles.has(profile.id));
      const profileIndex = availableProfile >= 0 ? availableProfile : current.length;
      return [
        ...current,
        createSimulationDraft(key, profileIndex, current[current.length - 1]),
      ];
    });
    setSimulationStatus("idle");
  };

  const removeSimulation = (key: number) => {
    setSimulations((current) => current.length <= 1
      ? current
      : current.filter((simulation) => simulation.key !== key));
    setSimulationStatus("idle");
  };

  const queryForSimulation = (simulation: SimulationDraft) => {
    const count = simulation.type === "chat" ? 1 : boundedInteger(String(simulation.count), 1, MAXIMUM_COUNT);
    const delaySeconds = boundedInteger(
      String(simulation.delaySeconds),
      0,
      MAXIMUM_DELAY_SECONDS,
    );
    const displayName = simulation.displayName.trim();
    const gift = selectedGift(simulation);
    const query = new URLSearchParams({
      platform: simulation.platform,
      type: simulation.type,
      user: displayName,
      userDisplayName: displayName,
      userId: simulationUserId(displayName, simulation.profileId),
      userAvatarUrl: `/assets/creator-tools/simulator/avatars/${simulation.profileId}.jpg`,
      count: String(count),
      delaySeconds: String(delaySeconds),
      chatText: simulation.type === "chat" ? simulation.chatText.trim() : "",
    });
    if (isCatalogGift(simulation) && gift) {
      query.set("giftId", gift.giftId);
    } else if (simulation.type !== "chat") {
      query.set(
        "amount",
        String(Math.max(0, Math.min(MAXIMUM_AMOUNT, simulation.amount || 0))),
      );
    }
    return { query, delaySeconds };
  };

  const submitSimulations = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit) return;

    const requests = simulations.map(queryForSimulation);
    setSimulationStatus("sending");
    try {
      for (const request of requests) {
        const response = await fetch(`/api/dashboard/simulate?${request.query}`, { cache: "no-store" });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
      }
    } catch {
      setSimulationStatus(activeRef.current ? "error" : "idle");
      return;
    }

    setSimulationStatus("idle");
    try {
      await onSubmitted({
        eventCount: requests.length,
        delaySeconds: Math.max(...requests.map((request) => request.delaySeconds)),
      });
    } catch {
      // The simulations were already accepted even if refreshing the feed fails.
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
      <form className="dashboard-simulator-form" onSubmit={(event) => void submitSimulations(event)}>
        <div className="dashboard-simulator-form__events">
          {simulations.map((simulation, index) => {
            const catalogGift = isCatalogGift(simulation);
            return (
              <section
                className="dashboard-simulator-event"
                key={simulation.key}
                aria-label={t("dashboard.simulator.eventNumber").replace("{number}", String(index + 1))}
              >
                {multiple ? (
                  <div className="dashboard-simulator-event__heading">
                    <strong>{t("dashboard.simulator.eventNumber").replace("{number}", String(index + 1))}</strong>
                    <button
                      type="button"
                      onClick={() => removeSimulation(simulation.key)}
                      disabled={simulationStatus === "sending"}
                    >
                      {t("dashboard.simulator.removeEvent")}
                    </button>
                  </div>
                ) : null}

                <div className="dashboard-simulator-form__row">
                  <label>
                    <span>{t("dashboard.simulator.platform")}</span>
                    <select
                      value={simulation.platform}
                      disabled={simulationStatus === "sending"}
                      onChange={(event) => {
                        const platform = event.target.value as StreamPlatform;
                        updateSimulation(simulation.key, (current) => ({
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
                      disabled={simulationStatus === "sending"}
                      onChange={(event) => {
                        const type = event.target.value as StreamEventType;
                        updateSimulation(simulation.key, (current) => ({
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
                        aria-label={profile.name}
                        title={profile.name}
                        aria-pressed={simulation.profileId === profile.id}
                        onClick={() => selectProfile(simulation.key, profile.id)}
                      >
                        <img
                          src={`/assets/creator-tools/simulator/avatars/${profile.id}.jpg`}
                          alt=""
                          width={42}
                          height={42}
                        />
                      </button>
                    ))}
                  </div>
                </fieldset>

                <label>
                  <span>{t("dashboard.simulator.displayName")}</span>
                  <input
                    type="text"
                    maxLength={80}
                    value={simulation.displayName}
                    placeholder={t("dashboard.simulator.userPlaceholder")}
                    disabled={simulationStatus === "sending"}
                    onChange={(event) => updateSimulation(simulation.key, (current) => ({
                      ...current,
                      displayName: event.target.value,
                    }))}
                  />
                </label>

                {simulation.type === "chat" ? <label>
                  <span>{t("dashboard.simulator.voteNumber")}</span>
                  <select value={simulation.chatText} disabled={simulationStatus === "sending"}
                    onChange={(event) => updateSimulation(simulation.key, (current) => ({ ...current, chatText: event.target.value }))}>
                    {[1, 2, 3, 4, 5, 6].map((number) => <option key={number}>{number}</option>)}
                  </select>
                  <small className="dashboard-simulator-form__hint">{t("dashboard.simulator.voteHint")}</small>
                </label> : catalogGift ? (
                  <div>
                    <SearchableSelectField
                      id={`dashboard-simulator-item-${simulation.key}`}
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
                      onSelect={(gift) => updateSimulation(simulation.key, (current) => ({
                        ...current,
                        selectedItemId: gift.giftId,
                      }))}
                    />
                    {catalogError ? (
                      <small className="dashboard-simulator-form__hint" role="alert">
                        {t("dashboard.simulator.itemCatalogError")}
                      </small>
                    ) : null}
                  </div>
                ) : (
                  <label>
                    <span>{t("dashboard.simulator.amount")}</span>
                    <input
                      type="number"
                      min={0}
                      max={MAXIMUM_AMOUNT}
                      step="any"
                      value={simulation.amount}
                      disabled={simulationStatus === "sending"}
                      onChange={(event) => updateSimulation(simulation.key, (current) => ({
                        ...current,
                        amount: Math.max(0, Math.min(MAXIMUM_AMOUNT, Number(event.target.value) || 0)),
                      }))}
                    />
                  </label>
                )}

                <div className="dashboard-simulator-form__row dashboard-simulator-form__row--metrics" data-chat={simulation.type === "chat"}>
                  {simulation.type !== "chat" ? <label>
                    <span>{t("dashboard.simulator.count")}</span>
                    <input
                      type="number"
                      min={1}
                      max={MAXIMUM_COUNT}
                      value={simulation.count}
                      disabled={simulationStatus === "sending"}
                      onChange={(event) => updateSimulation(simulation.key, (current) => ({
                        ...current,
                        count: boundedInteger(event.target.value, 1, MAXIMUM_COUNT),
                      }))}
                    />
                  </label> : null}
                  <label>
                    <span>{t("dashboard.simulator.delay")}</span>
                    <input
                      type="number"
                      min={0}
                      max={MAXIMUM_DELAY_SECONDS}
                      step={1}
                      value={simulation.delaySeconds}
                      disabled={simulationStatus === "sending"}
                      onChange={(event) => updateSimulation(simulation.key, (current) => ({
                        ...current,
                        delaySeconds: boundedInteger(event.target.value, 0, MAXIMUM_DELAY_SECONDS),
                      }))}
                    />
                  </label>
                </div>
              </section>
            );
          })}
        </div>

        <div className="dashboard-simulator-form__actions">
          <button
            type="submit"
            disabled={!canSubmit}
            data-sending={simulationStatus === "sending"}
          >
            {t(`dashboard.simulator.${simulationStatus === "sending"
              ? multiple ? "sendingMultiple" : "sending"
              : multiple ? "submitMultiple" : "submit"}`)}
          </button>
          <button
            type="button"
            className="dashboard-simulator-form__add"
            onClick={addSimulation}
            disabled={simulationStatus === "sending"}
          >
            <span aria-hidden="true">+</span>
            {t("dashboard.simulator.addEvent")}
          </button>
        </div>
        <p data-status={simulationStatus} role="status" aria-live="polite">
          {simulationStatus === "error"
            ? t("dashboard.simulator.error")
            : t("dashboard.simulator.hint")}
        </p>
      </form>
    </>
  );
}
