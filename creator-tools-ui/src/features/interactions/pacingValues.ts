import type { InteractionPacingConfig, PeskyModeConfigState } from "../../model";

export type PacingValues = Omit<InteractionPacingConfig, "enabled">;
export const pacingFields = [
  "minimumInterval", "maximumInterval", "miniBossMinimumInterval", "miniBossMaximumInterval",
  "miniBossIntervalMultiplier", "maximumCompanionsDuringMiniBoss",
  "lightMinimumBatch", "lightMaximumBatch", "strongMinimumBatch", "strongMaximumBatch",
] as const;
export type PacingField = typeof pacingFields[number];
export type PacingDraft = Record<PacingField, string>;

export function pacingDraftFor(value?: PacingValues): PacingDraft {
  return Object.fromEntries(pacingFields.map((key) => [key, String(value?.[key] ?? "")])) as PacingDraft;
}

export function pacingValuesFor(draft: PacingDraft): PacingValues {
  return {
    ...Object.fromEntries(pacingFields.map((key) => [key, Number(draft[key])])),
    miniBossCooldownSeconds: Number(draft.miniBossMinimumInterval),
  } as PacingValues;
}

export function validPacing(value: PacingValues): boolean {
  const range = (minimum: number, maximum: number, lower: number, upper: number, integer = false) =>
    Number.isFinite(minimum) && Number.isFinite(maximum) && minimum >= lower && maximum <= upper &&
    minimum <= maximum && (!integer || (Number.isInteger(minimum) && Number.isInteger(maximum)));
  return range(value.minimumInterval, value.maximumInterval, 0.35, 300) &&
    range(value.miniBossMinimumInterval, value.miniBossMaximumInterval, 0, 300) &&
    range(value.lightMinimumBatch, value.lightMaximumBatch, 1, 20, true) &&
    range(value.strongMinimumBatch, value.strongMaximumBatch, 1, 20, true) &&
    range(value.miniBossIntervalMultiplier, value.miniBossIntervalMultiplier, 1, 10) &&
    range(value.maximumCompanionsDuringMiniBoss, value.maximumCompanionsDuringMiniBoss, 0, 20, true);
}

export function validPacingDraft(draft: PacingDraft): boolean {
  return pacingFields.every((key) => draft[key].trim() !== "") && validPacing(pacingValuesFor(draft));
}

export function samePacing(left: PacingValues, right?: PacingValues): boolean {
  return Boolean(right) && pacingFields.every((key) => left[key] === right?.[key]);
}

export function peskyDefaults(value: PeskyModeConfigState): PacingValues {
  return Object.fromEntries([
    ...pacingFields.map((key) => [key, value[("default" + key[0].toUpperCase() + key.slice(1)) as keyof PeskyModeConfigState]]),
    ["miniBossCooldownSeconds", value.defaultMiniBossMinimumInterval],
  ]) as unknown as PacingValues;
}
