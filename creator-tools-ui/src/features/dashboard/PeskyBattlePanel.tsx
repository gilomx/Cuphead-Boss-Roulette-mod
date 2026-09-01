import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, Check, Copy, Swords, Users } from "lucide-react";
import { SearchableSelectField } from "../../components/SearchableSelectField";
import { useConfig } from "../../config/ConfigContext";
import { useTikTokGiftCatalog } from "../../hooks/useTikTokGiftCatalog";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { PeskyBattleParticipant } from "../../model";
import { interactionItems } from "../interactions/interactionCatalog";

const BATTLE_CAPACITY = 5;

function participantName(participant: PeskyBattleParticipant | null) {
  if (!participant) return "";
  return participant.displayName.trim() || participant.userName.trim();
}

function initials(value: string) {
  const parts = value.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  return parts.slice(0, 2).map((part) => part[0]).join("").toLocaleUpperCase();
}

function participantsBySlot(participants: PeskyBattleParticipant[]) {
  const slots: Array<PeskyBattleParticipant | null> = Array.from(
    { length: BATTLE_CAPACITY },
    () => null,
  );
  for (const participant of participants) {
    const preferred = participant.slot >= 1 && participant.slot <= BATTLE_CAPACITY
      ? participant.slot - 1
      : -1;
    const index = preferred >= 0 && slots[preferred] === null
      ? preferred
      : slots.findIndex((entry) => entry === null);
    if (index >= 0) slots[index] = participant;
  }
  return slots;
}

export function PeskyBattlePanel() {
  const {
    interaction,
    liveEvents,
    peskyBattle,
    applyPeskyBattleGift,
    applyPeskyBattleStreamAttacks,
    applyPeskyBattleItem,
    armPeskyBattle,
    startPeskyBattle,
    cancelPeskyBattle,
    disablePeskyBattle,
    resetPeskyBattle,
  } = useConfig();
  const { locale, t } = useLocalization();
  const { catalog, error: catalogError } = useTikTokGiftCatalog();
  const [giftDraft, setGiftDraft] = useState("");
  const [copyStatus, setCopyStatus] = useState<"idle" | "copied" | "error">("idle");

  const phase = peskyBattle?.phase ?? "off";
  const configurationLocked = phase !== "off";
  const gifts = catalog?.gifts ?? [];

  useEffect(() => {
    const configuredGift = peskyBattle?.trigger.giftId ?? "";
    if (configurationLocked || !giftDraft || configuredGift) {
      setGiftDraft(configuredGift);
    }
  }, [configurationLocked, giftDraft, peskyBattle?.trigger.giftId]);

  const selectedGift = useMemo(
    () => gifts.find((gift) => gift.giftId === giftDraft),
    [giftDraft, gifts],
  );
  const disabledItems = new Set(peskyBattle?.disabledItems ?? []);
  const enabledItemCount = interactionItems.filter(
    (item) => !disabledItems.has(item.id),
  ).length;
  const slots = participantsBySlot(peskyBattle?.participants ?? []);
  const participantCount = slots.filter(Boolean).length;
  const rosterReady = participantCount === BATTLE_CAPACITY;
  const blockedByTapFarming = liveEvents?.activeEvent === "tap_farming";
  const canArm = Boolean(
    peskyBattle?.ready && phase === "off" && giftDraft && selectedGift &&
    enabledItemCount > 0 && !blockedByTapFarming,
  );
  const giftPlaceholder = catalogError
    ? t("dashboard.peskyBattle.trigger.catalogError")
    : catalog
      ? t("dashboard.peskyBattle.trigger.placeholder")
      : t("dashboard.peskyBattle.trigger.loading");
  const targetLevel = peskyBattle?.targetLevel?.trim();
  const phaseDescription = phase === "waiting_level" && targetLevel
    ? t("dashboard.peskyBattle.phaseDescription.waitingTarget")
      .replace("{level}", targetLevel)
    : phase === "active"
      ? t("dashboard.peskyBattle.phaseDescription.active")
        .replace("{attempt}", String(Math.max(1, peskyBattle?.attempt ?? 1)))
      : t(`dashboard.peskyBattle.phaseDescription.${phase}`);

  const copyOverlayUrl = async () => {
    const localeQuery = locale === "en" ? "?locale=en" : "";
    const overlayUrl = `${window.location.origin}/pesky-battle-overlay${localeQuery}`;
    try {
      if (!navigator.clipboard?.writeText) throw new Error("Clipboard unavailable");
      await navigator.clipboard.writeText(overlayUrl);
      setCopyStatus("copied");
    } catch {
      setCopyStatus("error");
    }
    window.setTimeout(() => setCopyStatus("idle"), 2600);
  };

  return (
    <section
      className="dashboard-pesky-battle"
      data-phase={phase}
      aria-labelledby="dashboard-pesky-battle-title"
    >
      <div className="dashboard-pesky-battle__heading">
        <div>
          <p className="dashboard-eyebrow">{t("dashboard.peskyBattle.eyebrow")}</p>
          <h1 id="dashboard-pesky-battle-title">
            <Swords aria-hidden="true" />
            {t("dashboard.peskyBattle.title")}
          </h1>
          <p>{t("dashboard.peskyBattle.description")}</p>
        </div>
        <div className="dashboard-pesky-battle__heading-actions">
          <span className="dashboard-pesky-battle__status" data-phase={phase}>
            {t(`dashboard.peskyBattle.phase.${phase}`)}
          </span>
          <button
            className="dashboard-pesky-battle__copy"
            type="button"
            onClick={() => void copyOverlayUrl()}
          >
            <Copy aria-hidden="true" />
            {t(`dashboard.peskyBattle.overlay.${copyStatus}`)}
          </button>
        </div>
      </div>

      <div className="dashboard-pesky-battle__state" role="status" aria-live="polite">
        <strong>{t(`dashboard.peskyBattle.phase.${phase}`)}</strong>
        <span>{phaseDescription}</span>
      </div>

      {blockedByTapFarming ? (
        <div className="dashboard-live-event-conflict" role="status">
          <AlertTriangle aria-hidden="true" />
          <div>
            <strong>{t("dashboard.liveEvents.conflict.title")}</strong>
            <span>{t("dashboard.liveEvents.conflict.tapFarmingActive")}</span>
          </div>
        </div>
      ) : null}

      <div className="dashboard-pesky-battle__layout">
        <div className="dashboard-pesky-battle__configuration">
          <div className="dashboard-pesky-battle__trigger">
            <SearchableSelectField
              id="dashboard-pesky-battle-gift"
              label={t("dashboard.peskyBattle.trigger.label")}
              options={gifts}
              selectedKey={giftDraft || null}
              placeholder={giftPlaceholder}
              noResults={t("dashboard.peskyBattle.trigger.noResults")}
              disabled={configurationLocked || !peskyBattle?.ready ||
                !catalog || catalogError}
              getKey={(gift) => gift.giftId}
              getLabel={(gift) => gift.name}
              getImage={(gift) => gift.imagePath}
              getMeta={(gift) => t("dashboard.peskyBattle.trigger.coins")
                .replace("{coins}", gift.coinsPerUnit.toLocaleString(
                  locale === "es" ? "es-MX" : "en-US",
                ))}
              getSearchTerms={(gift) => gift.aliases}
              onSelect={(gift) => {
                setGiftDraft(gift.giftId);
                applyPeskyBattleGift(gift.giftId);
              }}
            />
            <small>
              {configurationLocked
                ? t("dashboard.peskyBattle.trigger.locked")
                : t("dashboard.peskyBattle.trigger.hint")}
            </small>
          </div>

          <button
            className="dashboard-pesky-battle__stream-toggle"
            type="button"
            role="switch"
            aria-checked={peskyBattle?.allowStreamAttacks ?? true}
            data-enabled={peskyBattle?.allowStreamAttacks ?? true}
            disabled={!peskyBattle?.ready}
            onClick={() => applyPeskyBattleStreamAttacks(
              !(peskyBattle?.allowStreamAttacks ?? true),
            )}
          >
            <span>
              <strong>{t("dashboard.peskyBattle.streamAttacks.title")}</strong>
              <small>{t(peskyBattle?.allowStreamAttacks
                ? "dashboard.peskyBattle.streamAttacks.enabled"
                : "dashboard.peskyBattle.streamAttacks.disabled")}</small>
            </span>
            <i aria-hidden="true"><b /></i>
          </button>

          <div className="dashboard-pesky-battle__attacks">
            <div className="dashboard-pesky-battle__attacks-heading">
              <div>
                <strong>{t("dashboard.peskyBattle.attacks.title")}</strong>
                <small>{t(configurationLocked
                  ? "dashboard.peskyBattle.attacks.locked"
                  : "dashboard.peskyBattle.attacks.description")}</small>
              </div>
              <span>{enabledItemCount}/{interactionItems.length}</span>
            </div>
            <div className="dashboard-pesky-battle__attack-grid">
              {interactionItems.map((item) => {
                const itemEnabled = !disabledItems.has(item.id);
                const lastEnabled = itemEnabled && enabledItemCount === 1;
                return (
                  <label
                    className="dashboard-pesky-battle__attack"
                    data-enabled={itemEnabled}
                    key={item.id}
                  >
                    <img src={item.image} alt="" />
                    <span>{t(item.titleKey)}</span>
                    <input
                      type="checkbox"
                      checked={itemEnabled}
                      disabled={configurationLocked || !peskyBattle?.ready || lastEnabled}
                      onChange={(event) => applyPeskyBattleItem(
                        item.id,
                        event.target.checked,
                      )}
                    />
                    <Check aria-hidden="true" />
                  </label>
                );
              })}
            </div>
          </div>
        </div>

        <div className="dashboard-pesky-battle__roster">
          <div className="dashboard-pesky-battle__roster-heading">
            <div>
              <strong><Users aria-hidden="true" />{t("dashboard.peskyBattle.roster.title")}</strong>
              <small>{t("dashboard.peskyBattle.roster.description")}</small>
            </div>
            <span>{participantCount}/{BATTLE_CAPACITY}</span>
          </div>

          <div
            className="dashboard-pesky-battle__progress"
            role="progressbar"
            aria-label={t("dashboard.peskyBattle.roster.progress")}
            aria-valuemin={0}
            aria-valuemax={BATTLE_CAPACITY}
            aria-valuenow={participantCount}
          >
            <i style={{ width: `${participantCount / BATTLE_CAPACITY * 100}%` }} />
          </div>

          <div className="dashboard-pesky-battle__slots">
            {slots.map((participant, index) => {
              const name = participantName(participant) ||
                t("dashboard.peskyBattle.roster.participantFallback")
                  .replace("{slot}", String(index + 1));
              return (
                <article
                  className="dashboard-pesky-battle__slot"
                  data-filled={Boolean(participant)}
                  key={participant?.userId || participant?.userName || index}
                >
                  <span className="dashboard-pesky-battle__slot-number">
                    {index + 1}
                  </span>
                  <div className="dashboard-pesky-battle__avatar">
                    <span aria-hidden="true">{participant ? initials(name) : "?"}</span>
                    {participant?.avatarUrl ? (
                      <img
                        src={participant.avatarUrl}
                        alt=""
                        referrerPolicy="no-referrer"
                        onError={(event) => {
                          event.currentTarget.hidden = true;
                        }}
                      />
                    ) : null}
                  </div>
                  <strong>{participant
                    ? name
                    : t("dashboard.peskyBattle.roster.emptySlot")
                      .replace("{slot}", String(index + 1))}</strong>
                </article>
              );
            })}
          </div>

          <p className="dashboard-pesky-battle__simulation-hint">
            {t("dashboard.peskyBattle.roster.simulationHint")}
          </p>
        </div>
      </div>

      <div className="dashboard-pesky-battle__footer">
        <div>
          <p data-error={peskyBattle?.error ?? false} role="status" aria-live="polite">
            {t(
              `dashboard.peskyBattle.feedback.${peskyBattle?.feedback || "ready"}`,
              t("dashboard.peskyBattle.feedback.generic"),
            )}
          </p>
          <small>{t(!interaction?.interactionsEnabled
            ? "dashboard.peskyBattle.master.off"
            : peskyBattle?.allowStreamAttacks
              ? "dashboard.peskyBattle.master.streamAllowed"
              : "dashboard.peskyBattle.master.streamBlocked")}</small>
        </div>
        <div className="dashboard-pesky-battle__actions">
          {phase === "off" ? (
            <button
              className="dashboard-pesky-battle__primary"
              type="button"
              disabled={!canArm}
              onClick={() => armPeskyBattle(giftDraft)}
            >
              {t("dashboard.peskyBattle.actions.arm")}
            </button>
          ) : null}
          {phase === "recruiting" || phase === "ready" ? (
            <button
              className="dashboard-pesky-battle__secondary"
              type="button"
              onClick={cancelPeskyBattle}
            >
              {t("dashboard.peskyBattle.actions.cancel")}
            </button>
          ) : null}
          {phase === "ready" ? (
            <button
              className="dashboard-pesky-battle__primary"
              type="button"
              disabled={!rosterReady}
              onClick={startPeskyBattle}
            >
              {t("dashboard.peskyBattle.actions.start")}
            </button>
          ) : null}
          {phase === "waiting_level" || phase === "active" ? (
            <button
              className="dashboard-pesky-battle__danger"
              type="button"
              onClick={disablePeskyBattle}
            >
              {t("dashboard.peskyBattle.actions.disable")}
            </button>
          ) : null}
          {phase === "won" ? (
            <button
              className="dashboard-pesky-battle__primary"
              type="button"
              onClick={resetPeskyBattle}
            >
              {t("dashboard.peskyBattle.actions.reset")}
            </button>
          ) : null}
        </div>
      </div>
    </section>
  );
}
