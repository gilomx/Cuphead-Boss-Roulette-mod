import { useEffect, useMemo, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
import { interactionItemFor, interactionItems } from "../interactions/interactionCatalog";
import { useLocalization } from "../../i18n/LocalizationContext";

function validNames(value: string) {
  const seen = new Set<string>();
  return value
    .split(/\r?\n|\r/)
    .map((name) => name.trim().slice(0, 32))
    .filter((name) => {
      const key = name.toLocaleLowerCase();
      if (!name || seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .slice(0, 200);
}

export function PeskyModeView() {
  const {
    interaction,
    pesky,
    applyPeskyEnabled,
    applyPeskyNames,
    applyPeskyItem,
  } = useConfig();
  const { t } = useLocalization();
  const [namesDraft, setNamesDraft] = useState("");
  const [namesDirty, setNamesDirty] = useState(false);
  const [confirmingPesky, setConfirmingPesky] = useState(false);

  useEffect(() => {
    if (!namesDirty && pesky?.names) {
      setNamesDraft(pesky.names.join("\n"));
    }
  }, [namesDirty, pesky?.names]);

  useEffect(() => {
    if (confirmingPesky && !interaction?.randomTestEnabled) {
      setConfirmingPesky(false);
    }
  }, [confirmingPesky, interaction?.randomTestEnabled]);

  const normalizedNames = useMemo(() => validNames(namesDraft), [namesDraft]);
  const disabledItems = new Set(pesky?.disabledItems ?? []);
  const enabledItemCount = interactionItems.filter(
    (item) => !disabledItems.has(item.id),
  ).length;
  const canEnable = (pesky?.ready ?? false) &&
    normalizedNames.length > 0 && enabledItemCount > 0;
  const queue = pesky?.queue ?? [];
  const statusKey = pesky?.waitingForInteractions
    ? "waitingInteractions"
    : pesky?.running
      ? "running"
      : pesky?.enabled
        ? "waitingGame"
        : "disabled";

  return (
    <div className="page page--pesky">
      <header className="page-header pesky-page-header">
        <div>
          <h1>{t("pesky.title")}</h1>
          <p>{t("pesky.description")}</p>
        </div>
      </header>

      <section
        className="pesky-hero mode-switch-shell"
        data-active={pesky?.enabled ?? false}
        data-step={confirmingPesky ? "confirm" : "control"}
      >
        <div className="mode-switch-stage">
          <div
            className="mode-switch-pane mode-switch-pane--primary pesky-hero__pane"
            aria-hidden={confirmingPesky}
          >
            <div className="pesky-hero__copy">
              <span className="pesky-status" data-status={statusKey}>
                {t(`pesky.status.${statusKey}`)}
              </span>
              <h2>{t("pesky.control.title")}</h2>
              <p className="pesky-feedback" data-error={pesky?.error ?? false} role="status" aria-live="polite">
                {t(`pesky.feedback.${pesky?.feedback ?? "ready"}`)}
              </p>
            </div>
            <button
              className="pesky-toggle"
              type="button"
              tabIndex={confirmingPesky ? -1 : 0}
              aria-pressed={pesky?.enabled ?? false}
              data-active={pesky?.enabled ?? false}
              disabled={!pesky?.ready || (!(pesky?.enabled ?? false) && !canEnable)}
              onClick={() => {
                if (pesky?.enabled) {
                  applyPeskyEnabled(false);
                } else if (interaction?.randomTestEnabled) {
                  setConfirmingPesky(true);
                } else {
                  applyPeskyEnabled(true);
                }
              }}
            >
              {t(`pesky.control.${pesky?.enabled ? "disable" : "enable"}`)}
            </button>
          </div>
          <div
            className="mode-switch-pane mode-switch-pane--confirmation pesky-hero__pane"
            aria-hidden={!confirmingPesky}
          >
            <div className="mode-switch-confirmation__copy">
              <strong>{t("pesky.control.switch.title")}</strong>
              <p>{t("pesky.control.switch.description")}</p>
            </div>
            <div className="mode-switch-confirmation__actions">
              <button
                type="button"
                className="mode-switch-action mode-switch-action--cancel"
                tabIndex={confirmingPesky ? 0 : -1}
                onClick={() => setConfirmingPesky(false)}
              >
                {t("pesky.control.switch.cancel")}
              </button>
              <button
                type="button"
                className="mode-switch-action mode-switch-action--confirm"
                tabIndex={confirmingPesky ? 0 : -1}
                onClick={() => {
                  setConfirmingPesky(false);
                  applyPeskyEnabled(true);
                }}
              >
                {t("pesky.control.switch.confirm")}
              </button>
            </div>
          </div>
        </div>
      </section>

      <div className="interaction-workspace pesky-workspace">
        <section className="interaction-panel interaction-queue" aria-labelledby="pesky-queue-title">
          <div className="interaction-panel__heading">
            <div>
              <h2 id="pesky-queue-title">{t("pesky.queue.title")}</h2>
              <p>{t("pesky.queue.description")}</p>
            </div>
            <span className="interaction-count" aria-label={t("pesky.queue.countLabel")}>
              {queue.length}
            </span>
          </div>

          {queue.length === 0 ? (
            <div className="interaction-queue__empty">
              <strong>{t("pesky.queue.emptyTitle")}</strong>
              <span>{t("pesky.queue.emptyDescription")}</span>
            </div>
          ) : (
            <div className="interaction-table-wrap">
              <table className="interaction-table queue-table">
                <thead>
                  <tr>
                    <th scope="col">{t("pesky.queue.position")}</th>
                    <th scope="col">{t("pesky.queue.item")}</th>
                    <th scope="col">{t("pesky.queue.name")}</th>
                    <th scope="col">{t("pesky.queue.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {queue.map((entry, index) => {
                    const item = interactionItemFor(entry.item);
                    const displayStatus = entry.status === "queued" && !pesky?.available
                      ? "waiting_game"
                      : entry.status;
                    return (
                      <tr key={entry.id}>
                        <td className="queue-table__position">{index + 1}</td>
                        <td>
                          <div className="interaction-item-label">
                            {item ? <img src={item.image} alt="" /> : null}
                            <span>{item ? t(item.titleKey) : entry.item}</span>
                          </div>
                        </td>
                        <td className="queue-table__donor">{entry.donor}</td>
                        <td>
                          <span className="queue-status" data-status={displayStatus}>
                            {t(`pesky.queue.${displayStatus}`)}
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

        <div className="interaction-workspace__tools">
        <section className="interaction-panel pesky-names" aria-labelledby="pesky-names-title">
          <div className="interaction-panel__heading">
            <div>
              <h2 id="pesky-names-title">{t("pesky.names.title")}</h2>
              <p>{t("pesky.names.description")}</p>
            </div>
            <span className="interaction-count">{normalizedNames.length}</span>
          </div>
          <div className="pesky-names__body">
            <textarea
              value={namesDraft}
              rows={9}
              maxLength={7000}
              placeholder={t("pesky.names.placeholder")}
              onChange={(event) => {
                const nextDraft = event.target.value;
                const savedNames = (pesky?.names ?? []).join("\n");
                setNamesDraft(nextDraft);
                setNamesDirty(validNames(nextDraft).join("\n") !== savedNames);
              }}
            />
            <div className="pesky-names__footer">
              <span data-error={normalizedNames.length === 0}>
                {t(normalizedNames.length === 0
                  ? "pesky.names.required"
                  : "pesky.names.hint")}
              </span>
              <div
                className="pesky-names__save-slot"
                data-visible={namesDirty}
                aria-hidden={!namesDirty}
              >
                <button
                  type="button"
                  tabIndex={namesDirty ? 0 : -1}
                  disabled={!pesky?.ready}
                  onClick={() => {
                    applyPeskyNames(normalizedNames.join("\n"));
                    setNamesDirty(false);
                  }}
                >
                  {t("pesky.names.save")}
                </button>
              </div>
            </div>
          </div>
        </section>

        <section className="interaction-panel pesky-attacks" aria-labelledby="pesky-attacks-title">
          <div className="interaction-panel__heading">
            <div>
              <h2 id="pesky-attacks-title">{t("pesky.attacks.title")}</h2>
              <p>{t("pesky.attacks.description")}</p>
            </div>
            <span className="interaction-count">{enabledItemCount}</span>
          </div>
          <div className="pesky-attack-list">
            {interactionItems.map((item) => {
              const enabled = !disabledItems.has(item.id);
              return (
                <label className="pesky-attack" data-enabled={enabled} key={item.id}>
                  <img src={item.image} alt="" />
                  <span>
                    <strong>{t(item.titleKey)}</strong>
                    <small>{t(item.typeKey)}</small>
                  </span>
                  <input
                    type="checkbox"
                    checked={enabled}
                    disabled={!pesky?.ready}
                    onChange={(event) => applyPeskyItem(item.id, event.target.checked)}
                  />
                </label>
              );
            })}
          </div>
          {enabledItemCount === 0 ? (
            <p className="pesky-attacks__required">{t("pesky.attacks.required")}</p>
          ) : null}
        </section>
        </div>
      </div>
    </div>
  );
}
