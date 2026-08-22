import type {
  BossOption,
  CatalogOption,
  DisplayBossOption,
  DisplayModifierOption,
  DisplayOption,
  DisplayWeaponOption,
  ModifierOption,
  WeaponOption,
} from "./model";

interface OptionMeta {
  key: string;
  image: string;
}

const bossMeta: OptionMeta[] = [
  ["Frogs", "bosses/hoscoytosco.png"],
  ["Veggies", "bosses/pandillaraiz.png"],
  ["Slime", "bosses/goopylegrande.png"],
  ["FlyingBlimp", "bosses/hilda.png"],
  ["Flower", "bosses/claveldecagney.png"],
  ["Baroness", "bosses/baronesa.png"],
  ["FlyingGenie", "bosses/djimmi.png"],
  ["Clown", "bosses/beppi.png"],
  ["FlyingBird", "bosses/titi.png"],
  ["Dragon", "bosses/fosforo.png"],
  ["Bee", "bosses/abejita.png"],
  ["Mouse", "bosses/werner.png"],
  ["Pirate", "bosses/capitan.png"],
  ["FlyingMermaid", "bosses/calamaria.png"],
  ["SallyStagePlay", "bosses/sally.png"],
  ["Robot", "bosses/robot.png"],
  ["Train", "bosses/expreso.png"],
  ["DicePalaceMain", "bosses/dado.png"],
  ["Devil", "bosses/diablo.png"],
  ["RumRunners", "bosses/alimanas.png"],
  ["FlyingCowboy", "bosses/vaca.png"],
  ["Airplane", "bosses/perritos.png"],
  ["Graveyard", "bosses/angelydemonio.png"],
  ["SnowCult", "bosses/genovevo.png"],
  ["OldMan", "bosses/granito.png"],
  ["Saltbaker", "bosses/salero.png"],
].map(([key, image]) => ({ key, image }));

const weaponMeta: OptionMeta[] = [
  ["level_weapon_peashot", "weapons/lanzaguisantes.png"],
  ["level_weapon_spreadshot", "weapons/expansion.png"],
  ["level_weapon_homing", "weapons/rastreador.png"],
  ["level_weapon_bouncer", "weapons/globero.png"],
  ["level_weapon_charge", "weapons/carga.png"],
  ["level_weapon_boomerang", "weapons/rodeo.png"],
  ["level_weapon_crackshot", "weapons/tirocertero.png"],
  ["level_weapon_wide_shot", "weapons/convergencia.png"],
  ["level_weapon_upshot", "weapons/ciclonica.png"],
  ["None", "creator-tools/empty.png"],
].map(([key, image]) => ({ key, image }));

const superMeta: OptionMeta[] = [
  ["level_super_beam", "supers/super1.png"],
  ["level_super_invincible", "supers/super2.png"],
  ["level_super_ghost", "supers/super3.png"],
  ["None", "creator-tools/empty.png"],
].map(([key, image]) => ({ key, image }));

const charmMeta: OptionMeta[] = [
  ["charm_health_up_1", "charms/corazon.png"],
  ["charm_super_builder", "charms/cafe.png"],
  ["charm_smoke_dash", "charms/bombadehumo.png"],
  ["charm_parry_plus", "charms/desviodulce.png"],
  ["charm_health_up_2", "charms/corazondoble.png"],
  ["charm_parry_attack", "charms/afiladora.png"],
  ["charm_chalice", "charms/galletitaastral.png"],
  ["charm_curse_0", "charms/reliquiadivina.png"],
  ["charm_curse_4", "charms/reliquiadivina.png"],
  ["charm_healer", "charms/anillocorazon.png"],
  ["None", "creator-tools/empty.png"],
].map(([key, image]) => ({ key, image }));

const modifierMeta: OptionMeta[] = [
  ["NoDash", "creator-tools/modifiers/nodash_01.png"],
  ["NoMiniPlane", "creator-tools/modifiers/nomini_01.png"],
  ["MiniPlaneOnly", "creator-tools/modifiers/mini_01.png"],
  ["NoBombs", "creator-tools/modifiers/nobombs_01.png"],
  ["NoPeashooter", "creator-tools/modifiers/nopeashooter_01.png"],
  ["NoEx", "creator-tools/modifiers/noex_01.png"],
  ["BlackAndWhite", "creator-tools/modifiers/blacknwhite_01.png"],
  ["RgbShift", "creator-tools/modifiers/rgb_01.png"],
  ["UpsideDown", "creator-tools/modifiers/upside_down_01.png"],
  ["HpOne", "creator-tools/modifiers/hp1_01.png"],
  ["InkRain", "creator-tools/modifiers/inkrain_01.png"],
  ["HalfDamage", "creator-tools/modifiers/halfdamage_01.png"],
  ["StiffMode", "creator-tools/modifiers/locked_01.png"],
  ["None", "creator-tools/empty.png"],
].map(([key, image]) => ({ key, image }));

function decorate<T extends CatalogOption>(options: T[], metadata: OptionMeta[]) {
  return options.map((option) => {
    const meta = metadata[option.id] ?? { key: String(option.id), image: "weapons/vacio.png" };
    return { ...option, key: meta.key, image: `/assets/${meta.image}` };
  });
}

export const displayBosses = (options: BossOption[]) =>
  decorate(options, bossMeta) as DisplayBossOption[];
export const displayWeapons = (options: WeaponOption[]) =>
  decorate(options, weaponMeta) as DisplayWeaponOption[];
export const displaySupers = (options: CatalogOption[]) =>
  decorate(options, superMeta) as DisplayOption[];
export const displayCharms = (options: CatalogOption[]) =>
  decorate(options, charmMeta) as DisplayOption[];
export const displayModifiers = (options: ModifierOption[]) =>
  decorate(options, modifierMeta) as DisplayModifierOption[];
