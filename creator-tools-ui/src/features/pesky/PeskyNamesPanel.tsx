import { useEffect, useMemo, useState } from "react";
import { useConfig } from "../../config/ConfigContext";
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

export function PeskyNamesPanel() {
  const { pesky, applyPeskyNames } = useConfig();
  const { t } = useLocalization();
  const [namesDraft, setNamesDraft] = useState("");
  const [namesDirty, setNamesDirty] = useState(false);
  const normalizedNames = useMemo(() => validNames(namesDraft), [namesDraft]);

  useEffect(() => {
    if (!namesDirty && pesky?.names) setNamesDraft(pesky.names.join("\n"));
  }, [namesDirty, pesky?.names]);

  return (
    <section className="pesky-settings-card pesky-settings-card--names pesky-names"
      aria-labelledby="pesky-names-title">
      <div className="pesky-settings-card__heading">
        <div>
          <h3 id="pesky-names-title">{t("pesky.names.title")}</h3>
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
          <span>{t(normalizedNames.length === 0
            ? "pesky.names.emptyHint"
            : "pesky.names.hint")}</span>
          <div className="pesky-names__save-slot" data-visible={namesDirty}
            aria-hidden={!namesDirty}>
            <button type="button" tabIndex={namesDirty ? 0 : -1}
              disabled={!pesky?.ready}
              onClick={() => {
                applyPeskyNames(normalizedNames.join("\n"));
                setNamesDirty(false);
              }}>
              {t("pesky.names.save")}
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
