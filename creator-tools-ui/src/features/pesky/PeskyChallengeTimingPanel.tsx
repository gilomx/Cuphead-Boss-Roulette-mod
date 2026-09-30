import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

export function PeskyChallengeTimingPanel() {
  const { pesky, applyPeskyChallengeDuration } = useConfig();
  const { t } = useLocalization();
  const savedDuration = pesky?.challengeDurationSeconds ?? 15;
  const savedCountdown = pesky?.challengeCountdownSeconds ?? 3;
  const savedWait = pesky?.challengeWaitSeconds ?? 5;
  const [duration, setDuration] = useState(String(savedDuration));
  const [countdown, setCountdown] = useState(String(savedCountdown));
  const [wait, setWait] = useState(String(savedWait));

  useEffect(() => setWait(String(savedWait)), [savedWait]);
  useEffect(() => setCountdown(String(savedCountdown)), [savedCountdown]);
  useEffect(() => setDuration(String(savedDuration)), [savedDuration]);

  const seconds = Number(duration);
  const countdownSeconds = Number(countdown);
  const waitSeconds = Number(wait);
  const valid = duration.trim() !== "" && countdown.trim() !== "" && wait.trim() !== "" &&
    Number.isInteger(waitSeconds) && waitSeconds >= 0 && waitSeconds <= 300 &&
    Number.isInteger(seconds) && seconds >= 1 && seconds <= 120 &&
    Number.isInteger(countdownSeconds) && countdownSeconds >= 0 && countdownSeconds <= 30;
  const changed = seconds !== savedDuration || countdownSeconds !== savedCountdown ||
    waitSeconds !== savedWait;

  return (
    <section className="pesky-settings-card pesky-settings-card--challenges"
      aria-labelledby="pesky-challenge-settings-title">
      <div className="pesky-settings-card__heading">
        <div>
          <h3 id="pesky-challenge-settings-title">{t("pesky.challengeSettings.title")}</h3>
          <p>{t("pesky.challengeSettings.description")}</p>
        </div>
      </div>
      <form className="interaction-settings pesky-challenge-settings" onSubmit={(event) => {
        event.preventDefault();
        if (valid && changed) {
          applyPeskyChallengeDuration(seconds, countdownSeconds, waitSeconds);
        }
      }}>
        <label className="interaction-settings__number">
          <span><strong>{t("interactions.challenges.wait")}</strong>
            <small>{t("interactions.challenges.waitHint")}</small></span>
          <input type="number" min={0} max={300} step={1} value={wait}
            onChange={(event) => setWait(event.target.value)} />
        </label>
        <label className="interaction-settings__number">
          <span><strong>{t("interactions.challenges.countdown")}</strong>
            <small>{t("interactions.challenges.countdownHint")}</small></span>
          <input type="number" min={0} max={30} step={1} value={countdown}
            onChange={(event) => setCountdown(event.target.value)} />
        </label>
        <label className="interaction-settings__number">
          <span><strong>{t("interactions.challenges.duration")}</strong>
            <small>{t("interactions.challenges.durationHint")}</small></span>
          <input type="number" min={1} max={120} step={1} value={duration}
            onChange={(event) => setDuration(event.target.value)} />
        </label>
        {!valid ? <p className="interaction-settings__status" data-status="error" role="alert">
          {t("pesky.challengeSettings.invalid")}
        </p> : null}
        <button type="submit" disabled={!pesky?.ready || !valid || !changed}>
          {t("interactions.challenges.save")}
        </button>
      </form>
    </section>
  );
}
