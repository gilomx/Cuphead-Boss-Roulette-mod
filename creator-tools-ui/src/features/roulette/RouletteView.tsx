import {
  displayBosses,
  displayCharms,
  displayModifiers,
  displaySupers,
  displayWeapons,
} from "../../catalogMetadata";
import { SelectField } from "../../components/SelectField";
import { useConfig } from "../../config/ConfigContext";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { DisplayOption, ForceDraft } from "../../model";
import { ChallengeCatalog } from "./ChallengeCatalog";

export function RouletteView() {
  const { config, draft, applyDraft, applyChallenge } = useConfig();
  const { t } = useLocalization();

  const bosses = config ? displayBosses(config.bosses) : [];
  const weapons = config ? displayWeapons(config.weapons) : [];
  const supers = config ? displaySupers(config.supers) : [];
  const charms = config ? displayCharms(config.charms) : [];
  const modifiers = config ? displayModifiers(config.modifiers) : [];
  const optionLabel = (group: string) => (option: DisplayOption) =>
    t(`catalog.${group}.${option.key}`, option.name);

  const update = (key: keyof ForceDraft, value: number | boolean) => {
    if (!config || !draft) return;
    const next = { ...draft, [key]: value } as ForceDraft;

    if (key === "weapon1" && next.weapon2 === next.weapon1) {
      next.weapon2 = config.weapons.find((weapon) => weapon.empty)?.id ?? next.weapon2;
    }
    if (key === "boss") {
      const boss = config.bosses.find((item) => item.id === next.boss);
      const modifier = config.modifiers.find((item) => item.id === next.modifier);
      const kind = boss?.plane ? "plane" : "ground";
      if (modifier && !modifier.none && modifier.kind !== "both" && modifier.kind !== kind) {
        next.modifier = config.modifiers.find((item) => item.none)?.id ?? next.modifier;
      }
    }
    applyDraft(next);
  };

  const weapon1Options = weapons.filter((weapon) => !weapon.empty);
  const weapon2Options = weapons.filter((weapon) => weapon.empty || weapon.id !== draft?.weapon1);
  const selectedBoss = bosses.find((boss) => boss.id === draft?.boss);
  const bossKind = selectedBoss?.plane ? "plane" : "ground";
  const modifierOptions = modifiers.filter(
    (modifier) => modifier.none || modifier.kind === "both" || modifier.kind === bossKind,
  );

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <h1>{t("roulette.title")}</h1>
          <p>{t("roulette.description")}</p>
        </div>
      </header>

      {config && <ChallengeCatalog modifiers={modifiers} onToggle={applyChallenge} />}

      <section className="section force-section" aria-labelledby="force-title">
        <div className="section__heading">
          <h2 id="force-title">{t("roulette.force.title")}</h2>
          <p>{t("roulette.force.description")}</p>
        </div>

        {config && draft ? (
          <>
            <div className="form-grid">
              <SelectField id="boss" label={t("roulette.force.boss")} value={draft.boss} options={bosses} getLabel={optionLabel("boss")} onChange={(value) => update("boss", value)} />
              <SelectField id="weapon1" label={t("roulette.force.weapon1")} value={draft.weapon1} options={weapon1Options} getLabel={optionLabel("weapon")} onChange={(value) => update("weapon1", value)} />
              <SelectField id="weapon2" label={t("roulette.force.weapon2")} value={draft.weapon2} options={weapon2Options} getLabel={optionLabel("weapon")} onChange={(value) => update("weapon2", value)} />
              <SelectField id="super" label={t("roulette.force.super")} value={draft.super} options={supers} getLabel={optionLabel("super")} onChange={(value) => update("super", value)} />
              <SelectField id="charm" label={t("roulette.force.charm")} value={draft.charm} options={charms} getLabel={optionLabel("charm")} onChange={(value) => update("charm", value)} />
              <SelectField id="modifier" label={t("roulette.force.modifier")} value={draft.modifier} options={modifierOptions} getLabel={optionLabel("modifier")} onChange={(value) => update("modifier", value)} />
            </div>

            <label className="force-check">
              <input type="checkbox" checked={draft.enabled} onChange={(event) => update("enabled", event.target.checked)} />
              <span className="force-check__box" aria-hidden="true"><span /></span>
              <span>{t("roulette.force.enabled")}</span>
            </label>
          </>
        ) : (
          <div className="form-skeleton" aria-hidden="true">
            {Array.from({ length: 6 }, (_, index) => <span key={index} />)}
          </div>
        )}
      </section>
    </div>
  );
}
