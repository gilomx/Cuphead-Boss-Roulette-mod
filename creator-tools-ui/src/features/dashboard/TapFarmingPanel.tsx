import {
  Activity,
  AlertTriangle,
  HeartPulse,
  Layers3,
  MousePointerClick,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

const CONVERSION_PRESETS = [
  { taps: 1, healthPoints: 1 },
  { taps: 2, healthPoints: 1 },
  { taps: 5, healthPoints: 1 },
  { taps: 1, healthPoints: 2 },
] as const;

function normalizeConversionValue(value: number | undefined, fallback = 1) {
  const candidate = Number.isFinite(value) ? Math.floor(value ?? fallback) : fallback;
  return Math.max(1, Math.min(100000, candidate));
}

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
  const configuredTapsPerConversion = normalizeConversionValue(
    tapFarming?.conversion?.tapsPerConversion ??
      tapFarming?.conversion?.tapsPerHealthPoint,
    2,
  );
  const configuredHealthPointsPerConversion = normalizeConversionValue(
    tapFarming?.conversion?.healthPointsPerConversion,
    1,
  );
  const [tapsDraft, setTapsDraft] = useState(configuredTapsPerConversion);
  const [healthPointsDraft, setHealthPointsDraft] = useState(
    configuredHealthPointsPerConversion,
  );
  const phase = tapFarming?.phase ?? "off";
  const locked = phase !== "off";
  const blockedByPeskyBattle = liveEvents?.activeEvent === "pesky_battle";
  const numberLocale = locale === "es" ? "es-MX" : "en-US";

  useEffect(() => {
    setTapsDraft(configuredTapsPerConversion);
    setHealthPointsDraft(configuredHealthPointsPerConversion);
  }, [configuredHealthPointsPerConversion, configuredTapsPerConversion]);
  useEffect(() => {
    if (!locked) return;
    setTapsDraft(configuredTapsPerConversion);
    setHealthPointsDraft(configuredHealthPointsPerConversion);
  }, [configuredHealthPointsPerConversion, configuredTapsPerConversion, locked]);

  const counters = tapFarming?.counters;
  const totalTaps = normalized(counters?.totalTaps);
  const bankedTaps = normalized(counters?.bankedTaps);
  const reserveHealth = normalized(counters?.reserveHealth);
  const convertedHealth = normalized(counters?.convertedHealth);
  const spentHealth = normalized(counters?.spentHealth);
  const phaseIndex = Math.max(0, Math.floor(tapFarming?.phaseIndex ?? 0));
  const phaseCount = Math.max(0, Math.floor(tapFarming?.phaseCount ?? 0));
  const bossTotalHealth = normalized(tapFarming?.boss?.totalHealth);
  const effectiveHealth = tapFarming?.effectiveHealth;
  const healthAvailable = effectiveHealth?.available ?? (
    (phase === "active" || phase === "transition" || phase === "completed") &&
    bossTotalHealth > 0
  );
  const effectiveCurrent = healthAvailable
    ? normalized(effectiveHealth?.current ?? tapFarming?.boss?.currentHealth)
    : reserveHealth;
  const effectiveTotal = healthAvailable
    ? normalized(effectiveHealth?.total ?? bossTotalHealth)
    : 0;
  const healthRatio = healthAvailable && effectiveTotal > 0
    ? Math.min(1, Math.max(0, effectiveHealth?.ratio ?? effectiveCurrent / effectiveTotal))
    : 0;
  const healthPercent = Math.round(healthRatio * 100);
  const healthMetric = healthAvailable
    ? `${healthPercent}%`
    : `+${reserveHealth.toLocaleString(numberLocale)} ${t("dashboard.tapFarming.progress.healthPointsShort")}`;
  const reserveEquivalent = bossTotalHealth > 0 ? reserveHealth / bossTotalHealth : 0;
  const reserveDescription = !healthAvailable
    ? t("dashboard.tapFarming.progress.collected")
      .replace("{value}", reserveHealth.toLocaleString(numberLocale))
    : reserveEquivalent >= 1
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

  const saveConversion = (tapsValue: number, healthPointsValue: number) => {
    const nextTaps = normalizeConversionValue(tapsValue);
    const nextHealthPoints = normalizeConversionValue(healthPointsValue);
    setTapsDraft(nextTaps);
    setHealthPointsDraft(nextHealthPoints);
    applyTapFarmingRate(nextTaps, nextHealthPoints);
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
            {CONVERSION_PRESETS.map((preset) => {
              const active = tapsDraft === preset.taps &&
                healthPointsDraft === preset.healthPoints;
              const label = t("dashboard.tapFarming.conversion.equation")
                .replace("{taps}", String(preset.taps))
                .replace("{health}", String(preset.healthPoints));
              return (
                <button
                  type="button"
                  data-active={active}
                  aria-label={label}
                  aria-pressed={active}
                  disabled={locked || !tapFarming?.ready}
                  key={`${preset.taps}-${preset.healthPoints}`}
                  onClick={() => saveConversion(preset.taps, preset.healthPoints)}
                >
                  <strong>{preset.taps} → +{preset.healthPoints}</strong>
                  <span>{t("dashboard.tapFarming.conversion.preset")}</span>
                </button>
              );
            })}
          </div>
          <div
            className="dashboard-tap-farming__equation"
            role="group"
            aria-label={t("dashboard.tapFarming.conversion.custom")}
            onBlur={(event) => {
              const nextTarget = event.relatedTarget;
              if (nextTarget instanceof Node && event.currentTarget.contains(nextTarget)) {
                return;
              }
              saveConversion(tapsDraft, healthPointsDraft);
            }}
          >
            <MousePointerClick aria-hidden="true" />
            <div className="dashboard-tap-farming__equation-fields">
              <span>{t("dashboard.tapFarming.conversion.every")}</span>
              <input
                type="number"
                min="1"
                max="100000"
                step="1"
                value={tapsDraft}
                aria-label={t("dashboard.tapFarming.conversion.tapsInput")}
                disabled={locked || !tapFarming?.ready}
                onChange={(event) => setTapsDraft(
                  normalizeConversionValue(Number(event.target.value)),
                )}
                onKeyDown={(event) => {
                  if (event.key === "Enter") event.currentTarget.blur();
                }}
              />
              <span>{t("dashboard.tapFarming.conversion.tapsAdd")}</span>
              <input
                type="number"
                min="1"
                max="100000"
                step="1"
                value={healthPointsDraft}
                aria-label={t("dashboard.tapFarming.conversion.healthInput")}
                disabled={locked || !tapFarming?.ready}
                onChange={(event) => setHealthPointsDraft(
                  normalizeConversionValue(Number(event.target.value)),
                )}
                onKeyDown={(event) => {
                  if (event.key === "Enter") event.currentTarget.blur();
                }}
              />
              <span>{t("dashboard.tapFarming.conversion.healthPoints")}</span>
            </div>
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
            <span data-mode={healthAvailable ? "percent" : "points"}>{healthMetric}</span>
          </div>

          <div
            className="dashboard-tap-farming__battle-progress"
            data-mode={healthAvailable ? "percent" : "points"}
            role={healthAvailable ? "progressbar" : "status"}
            aria-label={healthAvailable
              ? t("dashboard.tapFarming.progress.battle")
              : t("dashboard.tapFarming.progress.collected")
                .replace("{value}", reserveHealth.toLocaleString(numberLocale))}
            aria-valuemin={healthAvailable ? 0 : undefined}
            aria-valuemax={healthAvailable ? 100 : undefined}
            aria-valuenow={healthAvailable ? healthPercent : undefined}
          >
            <i style={{ width: `${healthRatio * 100}%` }} />
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
              onClick={() => activateTapFarming(tapsDraft, healthPointsDraft)}
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
