export type ModifierKind = "plane" | "ground" | "both";

export interface CatalogOption {
  id: number;
  name: string;
}

export interface BossOption extends CatalogOption {
  plane: boolean;
}

export interface WeaponOption extends CatalogOption {
  empty: boolean;
}

export interface ModifierOption extends CatalogOption {
  kind: ModifierKind;
  none: boolean;
  enabled: boolean;
  canDisable: boolean;
}

export interface DisplayOption extends CatalogOption {
  key: string;
  image: string;
}

export interface DisplayBossOption extends BossOption, DisplayOption {}
export interface DisplayWeaponOption extends WeaponOption, DisplayOption {}
export interface DisplayModifierOption extends ModifierOption, DisplayOption {}

export interface RouletteSelection {
  boss: number;
  weapon1: number;
  weapon2: number;
  super: number;
  charm: number;
  modifier: number;
}

export interface ForceDraft extends RouletteSelection {
  enabled: boolean;
}

export interface RouletteConfigState {
  ready: boolean;
  enabled: boolean;
  selection: RouletteSelection;
  bosses: BossOption[];
  weapons: WeaponOption[];
  supers: CatalogOption[];
  charms: CatalogOption[];
  modifiers: ModifierOption[];
}

export type ConnectionStatus =
  | "connecting"
  | "saved"
  | "saving"
  | "pending"
  | "error";
