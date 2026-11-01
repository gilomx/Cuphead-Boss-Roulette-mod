import { FlaskConical } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";

export function TapFarmingTestControls() {
  const { tapFarming, setTapFarmingTest } = useConfig();
  const { locale, t } = useLocalization();
  const sectionRef = useRef<HTMLElement>(null);
  const test = tapFarming?.testTaps;
  const quantity = test?.quantity ?? 10;
  const interval = test?.intervalSeconds ?? 1;
  const active = test?.active === true;
  const [quantityDraft, setQuantityDraft] = useState(String(quantity));
  const [intervalDraft, setIntervalDraft] = useState(String(interval));
  const [sending, setSending] = useState(false);
  const ready = tapFarming?.ready && Boolean(test);
  const canRun = ["collecting", "active", "transition"].includes(tapFarming?.phase ?? "off");

  useEffect(() => {
    if (sectionRef.current?.contains(document.activeElement)) return;
    setQuantityDraft(String(quantity));
    setIntervalDraft(String(interval));
  }, [quantity, interval]);

  function values() {
    const nextQuantity = Math.max(1, Math.min(100000,
      Math.floor(Number(quantityDraft) || quantity)));
    const nextInterval = Math.round(Math.max(0.1, Math.min(60,
      Number(intervalDraft) || interval)) * 10) / 10;
    setQuantityDraft(String(nextQuantity));
    setIntervalDraft(String(nextInterval));
    return { quantity: nextQuantity, interval: nextInterval };
  }

  function saveFields(nextTarget: EventTarget | null) {
    // A click on the switch submits the current fields together with its new state.
    if (nextTarget instanceof HTMLElement && nextTarget.closest('[role="switch"]')) return;
    const next = values();
    if (next.quantity !== quantity || next.interval !== interval) {
      void setTapFarmingTest(active, next.quantity, next.interval);
    }
  }

  async function toggle() {
    const next = values();
    setSending(true);
    try {
      await setTapFarmingTest(!active, next.quantity, next.interval);
    } finally {
      setSending(false);
    }
  }

  return (
    <section ref={sectionRef} className="dashboard-tap-farming__test" data-active={active}
      aria-labelledby="tap-farming-test-title">
      <div className="dashboard-tap-farming__test-heading">
        <h2 id="tap-farming-test-title"><FlaskConical aria-hidden="true" />{t("dashboard.tapFarming.test.title")}</h2>
        <button className="dashboard-master-switch" type="button" role="switch"
          aria-label={t("dashboard.tapFarming.test.title")} aria-checked={active}
          aria-busy={sending} data-enabled={active}
          disabled={!ready || sending || (!canRun && !active)} onClick={() => void toggle()}>
          <span className="dashboard-master-switch__track" aria-hidden="true"><i /></span>
          <span>{t(active ? "dashboard.tapFarming.test.on" : "dashboard.tapFarming.test.off")}</span>
        </button>
      </div>
      <p>{t("dashboard.tapFarming.test.description")}</p>
      <div className="dashboard-tap-farming__test-fields">
        <label>{t("dashboard.tapFarming.test.quantity")}
          <input type="number" min="1" max="100000" step="1" value={quantityDraft}
            disabled={!ready || sending} onChange={(event) => setQuantityDraft(event.target.value)}
            onBlur={(event) => saveFields(event.relatedTarget)}
            onKeyDown={(event) => { if (event.key === "Enter") event.currentTarget.blur(); }} />
        </label>
        <label>{t("dashboard.tapFarming.test.interval")}
          <input type="number" min="0.1" max="60" step="0.1" value={intervalDraft}
            disabled={!ready || sending} onChange={(event) => setIntervalDraft(event.target.value)}
            onBlur={(event) => saveFields(event.relatedTarget)}
            onKeyDown={(event) => { if (event.key === "Enter") event.currentTarget.blur(); }} />
        </label>
        <span role="status" aria-live="polite">{active
          ? t("dashboard.tapFarming.test.sent").replace("{count}", (test?.sent ?? 0).toLocaleString(locale === "es" ? "es-MX" : "en-US"))
          : t(canRun ? "dashboard.tapFarming.test.ready" : "dashboard.tapFarming.test.activateFirst")}</span>
      </div>
    </section>
  );
}
