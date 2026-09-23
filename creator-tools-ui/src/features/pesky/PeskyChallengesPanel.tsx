import { useEffect, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItems } from "../interactions/interactionCatalog";

export function PeskyChallengesPanel() {
  const { pesky, applyPeskyItem, applyPeskyChallengeDuration } = useConfig();
  const { t } = useLocalization();
  const savedDuration = pesky?.challengeDurationSeconds ?? 15;
  const [duration, setDuration] = useState(String(savedDuration));
  const savedCountdown = pesky?.challengeCountdownSeconds ?? 3;
  const [countdown, setCountdown] = useState(String(savedCountdown));
  const savedWait = pesky?.challengeWaitSeconds ?? 5;
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
  return (
    <section className="interaction-panel pesky-attacks" aria-labelledby="pesky-challenges-title">
      <div className="interaction-panel__heading">
        <div>
          <h2 id="pesky-challenges-title">{t("interactions.groups.challenge")}</h2>
          <p>{t("interactions.challenges.description")}</p>
        </div>
      </div>
      <div className="pesky-attack-list">
        {interactionItems.filter((item) => item.group === "challenge").map((item) => {
          const enabled = Boolean(pesky?.items.includes(item.id)) && !pesky?.disabledItems.includes(item.id);
          return (
            <label className="pesky-attack" data-enabled={enabled} key={item.id}>
              <img src={item.image} alt="" />
              <span><strong>{t(item.titleKey)}</strong><small>{t(item.descriptionKey)}</small></span>
              <input type="checkbox" checked={enabled} disabled={!pesky?.ready || !pesky.items.includes(item.id)}
                onChange={(event) => applyPeskyItem(item.id, event.target.checked)} />
            </label>
          );
        })}
        <form className="interaction-test-fields pesky-challenge-duration" onSubmit={(event) => {
          event.preventDefault();
          if (valid) applyPeskyChallengeDuration(seconds, countdownSeconds, waitSeconds);
        }}>
          <label>
            <span>{t("interactions.challenges.wait")}</span>
            <input type="number" min={0} max={300} step={1} value={wait}
              onChange={(event) => setWait(event.target.value)} />
            <small>{t("interactions.challenges.waitHint")}</small>
          </label>
          <label>
            <span>{t("interactions.challenges.countdown")}</span>
            <input type="number" min={0} max={30} step={1} value={countdown}
              onChange={(event) => setCountdown(event.target.value)} />
            <small>{t("interactions.challenges.countdownHint")}</small>
          </label>
          <label>
            <span>{t("interactions.challenges.duration")}</span>
            <input type="number" min={1} max={120} step={1} value={duration}
              onChange={(event) => setDuration(event.target.value)} />
            <small>{t("interactions.challenges.durationHint")}</small>
          </label>
          <button type="submit" disabled={!pesky?.ready || !valid || (seconds === savedDuration && countdownSeconds === savedCountdown && waitSeconds === savedWait)}>
            {t("interactions.challenges.save")}
          </button>
        </form>
      </div>
    </section>
  );
}
