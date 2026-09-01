import {
  Activity,
  AlertTriangle,
  Copy,
  HeartPulse,
  Layers3,
  MousePointerClick,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

const RATE_PRESETS = [1, 2, 5] as const;

function normalized(value: number | undefined) {
  return Number.isFinite(value) ? Math.max(0, value ?? 0) : 0;
}

export function TapFarmingPanel() {
  const {
    liveEvents,
    tapFarming,
    applyTapFarmingRate,
    activateTapFarming,
    deactivateTapFarming,
    resetTapFarming,
  } = useConfig();
  const { locale, t } = useLocalization();
  const configuredRate = Math.max(
    1,
    Math.floor(tapFarming?.conversion?.tapsPerHealthPoint ?? 2),
  );
  const [rateDraft, setRateDraft] = useState(configuredRate);
  const [copyStatus, setCopyStatus] = useState<"idle" | "copied" | "error">("idle");
  const phase = tapFarming?.phase ?? "off";
  const locked = phase !== "off";
  const blockedByPeskyBattle = liveEvents?.activeEvent === "pesky_battle";
  const numberLocale = locale === "es" ? "es-MX" : "en-US";

  useEffect(() => setRateDraft(configuredRate), [configuredRate]);
  useEffect(() => {
    if (locked) setRateDraft(configuredRate);
  }, [configuredRate, locked]);

  const counters = tapFarming?.counters;
  const totalTaps = normalized(counters?.totalTaps);
  const bankedTaps = normalized(counters?.bankedTaps);
  const reserveHealth = normalized(counters?.reserveHealth);
  const convertedHealth = normalized(counters?.convertedHealth);
  const spentHealth = normalized(counters?.spentHealth);
  const phaseIndex = Math.max(0, Math.floor(tapFarming?.phaseIndex ?? 0));
  const phaseCount = Math.max(0, Math.floor(tapFarming?.phaseCount ?? 0));
  const overallProgress = Math.min(1, Math.max(0, tapFarming?.overallProgress ?? 0));
  const bossTotalHealth = normalized(tapFarming?.boss?.totalHealth);
  const reserveEquivalent = bossTotalHealth > 0 ? reserveHealth / bossTotalHealth : 0;
  const reserveDescription = reserveEquivalent >= 1
    ? t("dashboard.tapFarming.progress.reserveEquivalent")
      .replace("{value}", reserveEquivalent.toLocaleString(numberLocale, {
        maximumSignificantDigits: 3,
      }))
    : reserveEquivalent > 0
      ? t("dashboard.tapFarming.progress.reservePercent")
        .replace("{value}", reserveEquivalent.toLocaleString(numberLocale, {
          style: "percent",
          maximumSignificantDigits: 3,
        }))
      : t("dashboard.tapFarming.progress.converted")
        .replace("{value}", convertedHealth.toLocaleString(numberLocale));

  const phaseDescription = useMemo(() => {
    if (phase === "active" && tapFarming?.bossName) {
      return t("dashboard.tapFarming.phaseDescription.activeBoss")
        .replace("{boss}", tapFarming.bossName)
        .replace("{phase}", String(Math.max(1, phaseIndex)))
        .replace("{count}", String(Math.max(phaseIndex, phaseCount)));
    }
    return t(`dashboard.tapFarming.phaseDescription.${phase}`);
  }, [phase, phaseCount, phaseIndex, t, tapFarming?.bossName]);

  const copyOverlayUrl = async () => {
    try {
      if (!navigator.clipboard?.writeText) throw new Error("Clipboard unavailable");
      const localeQuery = locale === "en" ? "?locale=en" : "";
      await navigator.clipboard.writeText(
        `${window.location.origin}/tap-farming-overlay${localeQuery}`,
      );
      setCopyStatus("copied");
    } catch {
      setCopyStatus("error");
    }
    window.setTimeout(() => setCopyStatus("idle"), 2600);
  };

  const saveRate = (value: number) => {
    const next = Math.max(1, Math.min(100000, Math.floor(value) || 1));
    setRateDraft(next);
    applyTapFarmingRate(next);
  };

  return (
    <section
      className="dashboard-tap-farming"
      data-phase={phase}
      aria-labelledby="dashboard-tap-farming-title"
    >
      <header className="dashboard-tap-farming__heading">
        <div>
          <p className="dashboard-eyebrow">{t("dashboard.tapFarming.eyebrow")}</p>
          <h1 id="dashboard-tap-farming-title">
            <MousePointerClick aria-hidden="true" />
            {t("dashboard.tapFarming.title")}
          </h1>
          <p>{t("dashboard.tapFarming.description")}</p>
        </div>
        <div className="dashboard-pesky-battle__heading-actions">
          <span className="dashboard-pesky-battle__status" data-phase={phase}>
            {t(`dashboard.tapFarming.phase.${phase}`)}
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
      </header>

      <div className="dashboard-pesky-battle__state" role="status" aria-live="polite">
        <strong>{t(`dashboard.tapFarming.phase.${phase}`)}</strong>
        <span>{phaseDescription}</span>
      </div>

      {blockedByPeskyBattle ? (
        <div className="dashboard-live-event-conflict" role="status">
          <AlertTriangle aria-hidden="true" />
          <div>
            <strong>{t("dashboard.liveEvents.conflict.title")}</strong>
            <span>{t("dashboard.liveEvents.conflict.peskyBattleActive")}</span>
          </div>
        </div>
      ) : null}

      <div className="dashboard-tap-farming__layout">
        <section className="dashboard-tap-farming__configuration">
          <div>
            <strong>{t("dashboard.tapFarming.conversion.title")}</strong>
            <p>{t("dashboard.tapFarming.conversion.description")}</p>
          </div>
          <div className="dashboard-tap-farming__rate-presets">
            {RATE_PRESETS.map((rate) => (
              <button
                type="button"
                data-active={rateDraft === rate}
                aria-pressed={rateDraft === rate}
                disabled={locked || !tapFarming?.ready}
                key={rate}
                onClick={() => saveRate(rate)}
              >
                <strong>{rate}</strong>
                <span>{t(rate === 1
                  ? "dashboard.tapFarming.conversion.presetSingular"
                  : "dashboard.tapFarming.conversion.preset")}</span>
              </button>
            ))}
          </div>
          <label className="dashboard-tap-farming__custom-rate">
            <span>{t("dashboard.tapFarming.conversion.custom")}</span>
            <input
              type="number"
              min="1"
              max="100000"
              step="1"
              value={rateDraft}
              disabled={locked || !tapFarming?.ready}
              onChange={(event) => setRateDraft(Math.max(1, Number(event.target.value) || 1))}
              onBlur={() => saveRate(rateDraft)}
              onKeyDown={(event) => {
                if (event.key === "Enter") event.currentTarget.blur();
              }}
            />
          </label>
          <div className="dashboard-tap-farming__equation">
            <MousePointerClick aria-hidden="true" />
            <span>{t("dashboard.tapFarming.conversion.equation")
              .replace("{taps}", rateDraft.toLocaleString(numberLocale))}</span>
            <HeartPulse aria-hidden="true" />
          </div>
          <small>{t(locked
            ? "dashboard.tapFarming.conversion.locked"
            : "dashboard.tapFarming.conversion.hint")}</small>
        </section>

        <section className="dashboard-tap-farming__progress-panel">
          <div className="dashboard-tap-farming__boss-heading">
            <div>
              <strong><Activity aria-hidden="true" />{tapFarming?.bossName ||
                t("dashboard.tapFarming.progress.waitingBoss")}</strong>
              <small>{phaseCount > 0
                ? t("dashboard.tapFarming.progress.phase")
                  .replace("{phase}", String(Math.max(1, phaseIndex)))
                  .replace("{count}", String(phaseCount))
                : t("dashboard.tapFarming.progress.noBoss")}</small>
            </div>
            <span>{Math.round(overallProgress * 100)}%</span>
          </div>

          <div
            className="dashboard-tap-farming__battle-progress"
            role="progressbar"
            aria-label={t("dashboard.tapFarming.progress.battle")}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={Math.round(overallProgress * 100)}
          >
            <i style={{ width: `${overallProgress * 100}%` }} />
            {Array.from({ length: Math.max(0, phaseCount - 1) }, (_, index) => (
              <b key={index} style={{ left: `${(index + 1) / phaseCount * 100}%` }} />
            ))}
          </div>

          <div className="dashboard-tap-farming__counter-grid">
            <article>
              <MousePointerClick aria-hidden="true" />
              <span>{t("dashboard.tapFarming.counters.totalTaps")}</span>
              <strong>{totalTaps.toLocaleString(numberLocale)}</strong>
            </article>
            <article>
              <Layers3 aria-hidden="true" />
              <span>{t("dashboard.tapFarming.counters.bankedTaps")}</span>
              <strong>{bankedTaps.toLocaleString(numberLocale)}</strong>
            </article>
            <article>
              <HeartPulse aria-hidden="true" />
              <span>{t("dashboard.tapFarming.counters.reserve")}</span>
              <strong>+{reserveHealth.toLocaleString(numberLocale)}</strong>
            </article>
            <article>
              <Activity aria-hidden="true" />
              <span>{t("dashboard.tapFarming.counters.absorbed")}</span>
              <strong>{spentHealth.toLocaleString(numberLocale)}</strong>
            </article>
          </div>

          <p className="dashboard-tap-farming__reserve-note">
            {reserveDescription}
          </p>
        </section>
      </div>

      <footer className="dashboard-pesky-battle__footer">
        <div>
          <p data-error={tapFarming?.error ?? false} role="status" aria-live="polite">
            {t(
              `dashboard.tapFarming.feedback.${tapFarming?.feedback || "ready"}`,
              t("dashboard.tapFarming.feedback.generic"),
            )}
          </p>
          <small>{t("dashboard.tapFarming.footer")}</small>
        </div>
        <div className="dashboard-pesky-battle__actions">
          {phase === "off" ? (
            <button
              className="dashboard-pesky-battle__primary"
              type="button"
              disabled={!tapFarming?.ready || blockedByPeskyBattle}
              onClick={() => activateTapFarming(rateDraft)}
            >
              {t("dashboard.tapFarming.actions.activate")}
            </button>
          ) : null}
          {phase !== "off" && phase !== "completed" ? (
            <button
              className="dashboard-pesky-battle__danger"
              type="button"
              disabled={phase === "stopping"}
              onClick={deactivateTapFarming}
            >
              {t(phase === "stopping"
                ? "dashboard.tapFarming.actions.stopping"
                : "dashboard.tapFarming.actions.deactivate")}
            </button>
          ) : null}
          {phase === "completed" ? (
            <button
              className="dashboard-pesky-battle__primary"
              type="button"
              onClick={resetTapFarming}
            >
              {t("dashboard.tapFarming.actions.finish")}
            </button>
          ) : null}
        </div>
      </footer>
    </section>
  );
}
