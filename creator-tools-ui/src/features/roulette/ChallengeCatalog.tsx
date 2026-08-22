import { useLocalization } from "../../i18n/LocalizationContext";
import type { DisplayModifierOption, ModifierKind } from "../../model";

const groupOrder: ModifierKind[] = ["plane", "ground", "both"];

interface ChallengeCatalogProps {
  modifiers: DisplayModifierOption[];
  onToggle: (id: number, enabled: boolean) => void;
}

export function ChallengeCatalog({ modifiers, onToggle }: ChallengeCatalogProps) {
  const { t } = useLocalization();
  const labelFor = (modifier: DisplayModifierOption) =>
    t(`catalog.modifier.${modifier.key}`, modifier.name);

  return (
    <section className="section challenge-section" aria-labelledby="challenge-title">
      <div className="section__heading">
        <h2 id="challenge-title">{t("roulette.challenges.title")}</h2>
        <p>{t("roulette.challenges.description")}</p>
      </div>
      <div className="challenge-groups">
        {groupOrder.map((kind) => {
          const items = modifiers.filter((modifier) => !modifier.none && modifier.kind === kind);
          if (items.length === 0) return null;
          return (
            <div className="challenge-group" key={kind}>
              <h3>{t(`roulette.challenges.${kind}`)}</h3>
              <div className="challenge-list">
                {items.map((modifier) => {
                  const label = labelFor(modifier);
                  const required = modifier.enabled && !modifier.canDisable;
                  return (
                    <button
                      className="challenge-icon"
                      type="button"
                      data-enabled={modifier.enabled}
                      aria-pressed={modifier.enabled}
                      aria-label={required
                        ? `${label}. ${t("roulette.challenges.required")}`
                        : `${t(modifier.enabled ? "roulette.challenges.disable" : "roulette.challenges.enable")} ${label}`}
                      disabled={required}
                      key={modifier.id}
                      title={required
                        ? `${label} — ${t("roulette.challenges.required")}`
                        : label}
                      onClick={() => onToggle(modifier.id, !modifier.enabled)}
                    >
                      <img src={modifier.image} alt={label} />
                    </button>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}
