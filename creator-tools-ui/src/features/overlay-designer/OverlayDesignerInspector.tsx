import { Activity, HeartPulse, MousePointerClick, RotateCcw, Users } from "lucide-react";
import type { Dispatch } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type {
  OverlayComposerComponent,
  OverlayComposerProfile,
  PeskyBattlePreviewSnapshot,
  TapFarmingPreviewSnapshot,
} from "./model";
import { proportionalComponentSize } from "./model";
import type { BattleSimulationAction, TapSimulationAction } from "./simulation";

interface OverlayDesignerInspectorProps {
  profile: OverlayComposerProfile;
  component: OverlayComposerComponent;
  tapState: TapFarmingPreviewSnapshot;
  battleState: PeskyBattlePreviewSnapshot;
  previewActive: boolean;
  previewPending: boolean;
  previewError: boolean;
  previewConflict: boolean;
  disabled?: boolean;
  onChange: (update: Partial<OverlayComposerComponent>) => void;
  onTogglePreview: () => void;
  dispatchTap: Dispatch<TapSimulationAction>;
  dispatchBattle: Dispatch<BattleSimulationAction>;
}

function numberValue(value: string, fallback: number) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.round(parsed) : fallback;
}

export function OverlayDesignerInspector({
  profile,
  component,
  tapState,
  battleState,
  previewActive,
  previewPending,
  previewError,
  previewConflict,
  disabled = false,
  onChange,
  onTogglePreview,
  dispatchTap,
  dispatchBattle,
}: OverlayDesignerInspectorProps) {
  const { locale, t } = useLocalization();
  const numberLocale = locale === "es" ? "es-MX" : "en-US";
  const geometryDisabled = disabled || component.locked;
  const maximumSize = {
    width: profile.canvas.width - component.x,
    height: profile.canvas.height - component.y,
  };
  const minimumSize = proportionalComponentSize(component, 0, maximumSize);
  const maximumProportionalSize = proportionalComponentSize(
    component,
    Number.POSITIVE_INFINITY,
    maximumSize,
  );
  const geometry = [
    ["x", component.x, 0, Math.max(0, profile.canvas.width - component.width)],
    ["y", component.y, 0, Math.max(0, profile.canvas.height - component.height)],
    ["width", component.width, minimumSize.width, maximumProportionalSize.width],
    ["height", component.height, minimumSize.height, maximumProportionalSize.height],
  ] as const;

  const updateGeometry = (
    key: typeof geometry[number][0],
    value: number,
    minimum: number,
    maximum: number,
  ) => {
    const nextValue = Math.min(
      Math.max(minimum, maximum),
      Math.max(minimum, value),
    );
    if (key === "width" || key === "height") {
      const currentValue = key === "width" ? component.width : component.height;
      onChange(proportionalComponentSize(
        component,
        nextValue / Math.max(1, currentValue),
        maximumSize,
      ));
      return;
    }
    onChange({ [key]: nextValue });
  };

  return (
    <aside className="overlay-designer-inspector">
      <section className="overlay-designer-panel overlay-designer-properties" aria-labelledby="overlay-designer-properties-title">
        <header>
          <span>{t("overlayDesigner.inspector.eyebrow")}</span>
          <h2 id="overlay-designer-properties-title">
            {t(`overlayDesigner.components.${component.id}`)}
          </h2>
        </header>

        <div className="overlay-designer-properties__geometry">
          {geometry.map(([key, value, minimum, maximum]) => (
            <label key={key}>
              <span>{t(`overlayDesigner.inspector.${key}`)}</span>
              <input
                type="number"
                min={minimum}
                max={Math.max(minimum, maximum)}
                step="1"
                value={value}
                disabled={geometryDisabled}
                onChange={(event) => updateGeometry(
                  key,
                  numberValue(event.target.value, value),
                  minimum,
                  maximum,
                )}
              />
            </label>
          ))}
        </div>

        {component.id === "tap_farming" && (
          <div className="overlay-designer-properties__colors">
            <strong>{t("overlayDesigner.inspector.colors.title")}</strong>
            {(["liquidColor", "collectingColor", "textColor", "outlineColor"] as const).map((key) => (
              <label key={key}>
                <span>{t(`overlayDesigner.inspector.colors.${key}`)}</span>
                <span className="overlay-designer-properties__color-control">
                  <input
                    type="color"
                    value={component[key]}
                    disabled={disabled}
                    aria-label={t(`overlayDesigner.inspector.colors.${key}`)}
                    onInput={(event) => onChange({
                      [key]: (event.currentTarget as HTMLInputElement).value,
                    })}
                    onChange={(event) => onChange({ [key]: event.target.value })}
                  />
                  <code>{component[key]}</code>
                </span>
              </label>
            ))}
          </div>
        )}

        <div className="overlay-designer-properties__switches">
          {(["enabled", "locked", "showTitle", "showDetails", "motion"] as const).map((key) => (
            <button
              type="button"
              role="switch"
              aria-checked={component[key]}
              data-enabled={component[key]}
              disabled={disabled}
              key={key}
              onClick={() => onChange({ [key]: !component[key] })}
            >
              <span>
                <strong>{t(`overlayDesigner.inspector.options.${key}`)}</strong>
                <small>{t(`overlayDesigner.inspector.options.${key}Hint`)}</small>
              </span>
              <i aria-hidden="true"><b /></i>
            </button>
          ))}
        </div>
      </section>

      <section className="overlay-designer-panel overlay-designer-simulation" aria-labelledby="overlay-designer-simulation-title">
        <header>
          <span>{t("overlayDesigner.simulation.eyebrow")}</span>
          <h2 id="overlay-designer-simulation-title">{t("overlayDesigner.simulation.title")}</h2>
        </header>

        {component.id === "tap_farming" ? (
          <div className="overlay-designer-simulation__body">
            <label>
              <span>{t("overlayDesigner.simulation.scenario")}</span>
              <select
                value={tapState.phase}
                onChange={(event) => dispatchTap({
                  type: "scenario",
                  scenario: event.target.value as TapFarmingPreviewSnapshot["phase"],
                })}
              >
                {(["collecting", "active", "transition", "completed"] as const).map((phase) => (
                  <option value={phase} key={phase}>
                    {t(`overlayDesigner.simulation.tap.phases.${phase}`)}
                  </option>
                ))}
              </select>
            </label>
            <div className="overlay-designer-simulation__summary">
              <span><MousePointerClick aria-hidden="true" />{tapState.counters.totalTaps.toLocaleString(numberLocale)}</span>
              <span><HeartPulse aria-hidden="true" />+{tapState.counters.reserveHealth.toLocaleString(numberLocale)}</span>
              <span><Activity aria-hidden="true" />{Math.round(tapState.overallProgress * 100)}%</span>
            </div>
            <fieldset>
              <legend>{t("overlayDesigner.simulation.tap.addTaps")}</legend>
              <div>
                {[100, 500, 1000].map((amount) => (
                  <button type="button" key={amount} onClick={() => dispatchTap({ type: "add_taps", amount })}>
                    +{amount.toLocaleString(numberLocale)}
                  </button>
                ))}
              </div>
            </fieldset>
            <fieldset>
              <legend>{t("overlayDesigner.simulation.tap.damage")}</legend>
              <div>
                {[100, 500, 1000].map((amount) => (
                  <button type="button" key={amount} onClick={() => dispatchTap({ type: "damage", amount })}>
                    -{amount.toLocaleString(numberLocale)}
                  </button>
                ))}
              </div>
            </fieldset>
            <div className="overlay-designer-simulation__actions">
              <button type="button" onClick={() => dispatchTap({ type: "next_phase" })}>
                {t("overlayDesigner.simulation.tap.nextPhase")}
              </button>
              <button type="button" onClick={() => dispatchTap({ type: "retry" })}>
                {t("overlayDesigner.simulation.tap.retry")}
              </button>
              <button type="button" onClick={() => dispatchTap({ type: "reset" })}>
                <RotateCcw aria-hidden="true" />{t("overlayDesigner.simulation.reset")}
              </button>
            </div>
          </div>
        ) : (
          <div className="overlay-designer-simulation__body">
            <label>
              <span>{t("overlayDesigner.simulation.scenario")}</span>
              <select
                value={battleState.phase}
                onChange={(event) => dispatchBattle({
                  type: "scenario",
                  scenario: event.target.value as PeskyBattlePreviewSnapshot["phase"],
                })}
              >
                {(["recruiting", "ready", "waiting_level", "active", "won"] as const).map((phase) => (
                  <option value={phase} key={phase}>
                    {t(`overlayDesigner.simulation.battle.phases.${phase}`)}
                  </option>
                ))}
              </select>
            </label>
            <div className="overlay-designer-simulation__summary">
              <span><Users aria-hidden="true" />{battleState.participants.length}/{battleState.capacity}</span>
              <span>{t("overlayDesigner.simulation.battle.attempt")}: {battleState.attempt}</span>
            </div>
            <label>
              <span>{t("overlayDesigner.simulation.battle.participants")}</span>
              <input
                type="range"
                min="0"
                max={battleState.capacity}
                step="1"
                value={battleState.participants.length}
                onChange={(event) => dispatchBattle({
                  type: "participants",
                  count: Number(event.target.value),
                })}
              />
              <output>{battleState.participants.length}</output>
            </label>
            <label>
              <span>{t("overlayDesigner.simulation.battle.attempt")}</span>
              <input
                type="number"
                min="1"
                max="99"
                value={battleState.attempt}
                onChange={(event) => dispatchBattle({
                  type: "attempt",
                  attempt: Math.max(1, Number(event.target.value) || 1),
                })}
              />
            </label>
            <button
              className="overlay-designer-simulation__reset"
              type="button"
              onClick={() => dispatchBattle({ type: "reset" })}
            >
              <RotateCcw aria-hidden="true" />{t("overlayDesigner.simulation.reset")}
            </button>
          </div>
        )}

        <div className="overlay-designer-preview-control" data-active={previewActive}>
          <button
            type="button"
            role="switch"
            aria-checked={previewActive}
            aria-busy={previewPending}
            disabled={disabled || previewPending}
            onClick={onTogglePreview}
          >
            <span>
              <strong>{t("overlayDesigner.preview.title")}</strong>
              <small>{t(previewPending
                ? "overlayDesigner.preview.pending"
                : previewActive
                  ? "overlayDesigner.preview.active"
                  : "overlayDesigner.preview.inactive")}</small>
            </span>
            <i aria-hidden="true"><b /></i>
          </button>
          {previewError ? (
            <p role="status">{t(previewConflict
              ? "overlayDesigner.preview.conflict"
              : "overlayDesigner.preview.error")}</p>
          ) : null}
        </div>
      </section>
    </aside>
  );
}
