import { Activity, HeartPulse, MousePointerClick, RotateCcw, Swords, Users } from "lucide-react";
import { useEffect, useRef, useState, type Dispatch } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItems } from "../interactions/interactionCatalog";
import type {
  OverlayComposerComponent,
  OverlayComposerProfile,
  PeskyBattlePreviewSnapshot,
  TapFarmingPreviewSnapshot,
} from "./model";
import { minimumComponentSize, proportionalComponentSize } from "./model";
import type { BattleSimulationAction, TapSimulationAction } from "./simulation";
import type { ChatChoosesState, ChatChoosesStage } from "../../model";
import type { ChatSimulationAction } from "./chatSimulation";

const battleAttacks = interactionItems.filter(item => item.category === "attack" || item.category === "mini_boss");

interface OverlayDesignerInspectorProps {
  profile: OverlayComposerProfile;
  component: OverlayComposerComponent;
  tapState: TapFarmingPreviewSnapshot;
  battleState: PeskyBattlePreviewSnapshot;
  chatState: ChatChoosesState;
  previewActive: boolean;
  previewPending: boolean;
  previewError: boolean;
  previewConflict: boolean;
  disabled?: boolean;
  onChange: (update: Partial<OverlayComposerComponent>) => void;
  onTogglePreview: () => void;
  dispatchTap: Dispatch<TapSimulationAction>;
  dispatchBattle: Dispatch<BattleSimulationAction>;
  dispatchChat: Dispatch<ChatSimulationAction>;
}

function numberValue(value: string, fallback: number) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.round(parsed) : fallback;
}

function colorBase(value: string) {
  return /^#[0-9a-f]{6}/i.test(value) ? value.slice(0, 7) : "#ffffff";
}

function colorOpacity(value: string) {
  if (!/^#[0-9a-f]{8}$/i.test(value)) return 100;
  return Math.round(Number.parseInt(value.slice(7, 9), 16) / 255 * 100);
}

function colorWithOpacity(value: string, opacity: number) {
  const alpha = Math.round(Math.min(100, Math.max(0, opacity)) * 255 / 100)
    .toString(16)
    .padStart(2, "0");
  return `${colorBase(value)}${alpha}`.toLowerCase();
}

function colorWithBase(value: string, base: string) {
  const alpha = /^#[0-9a-f]{8}$/i.test(value) ? value.slice(7, 9) : "ff";
  return `${base}${alpha}`.toLowerCase();
}

interface OverlayColorPickerProps {
  label: string;
  value: string;
  disabled: boolean;
  alphaLabel: string;
  hexLabel: string;
  openLabel: string;
  closeLabel: string;
  onChange: (value: string) => void;
}

function OverlayColorPicker({
  label,
  value,
  disabled,
  alphaLabel,
  hexLabel,
  openLabel,
  closeLabel,
  onChange,
}: OverlayColorPickerProps) {
  const [open, setOpen] = useState(false);
  const [hexDraft, setHexDraft] = useState(colorBase(value).toUpperCase());
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setHexDraft(colorBase(value).toUpperCase());
  }, [value]);

  useEffect(() => {
    if (!open) return undefined;
    const closeFromOutside = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const closeFromKeyboard = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("pointerdown", closeFromOutside);
    document.addEventListener("keydown", closeFromKeyboard);
    return () => {
      document.removeEventListener("pointerdown", closeFromOutside);
      document.removeEventListener("keydown", closeFromKeyboard);
    };
  }, [open]);

  const commitHex = () => {
    const normalized = hexDraft.trim().toUpperCase();
    if (/^#[0-9A-F]{6}$/.test(normalized)) {
      onChange(colorWithBase(value, normalized));
      setHexDraft(normalized);
      return;
    }
    setHexDraft(colorBase(value).toUpperCase());
  };

  const opacity = colorOpacity(value);

  return (
    <div
      className="overlay-designer-color-picker"
      data-open={open}
      ref={rootRef}
    >
      <span className="overlay-designer-color-picker__label">{label}</span>
      <button
        className="overlay-designer-color-picker__trigger"
        type="button"
        disabled={disabled}
        aria-expanded={open}
        aria-label={`${openLabel}: ${label}`}
        onClick={() => setOpen(current => !current)}
      >
        <i aria-hidden="true"><b style={{ backgroundColor: value }} /></i>
        <code>{colorBase(value).toUpperCase()}</code>
        <output>{opacity}%</output>
      </button>

      {open ? (
        <div className="overlay-designer-color-picker__panel" role="group" aria-label={label}>
          <div className="overlay-designer-color-picker__heading">
            <strong>{label}</strong>
            <button type="button" aria-label={closeLabel} onClick={() => setOpen(false)}>×</button>
          </div>
          <label className="overlay-designer-color-picker__native">
            <span>{label}</span>
            <input
              type="color"
              value={colorBase(value)}
              disabled={disabled}
              onInput={(event) => onChange(colorWithBase(
                value,
                (event.currentTarget as HTMLInputElement).value,
              ))}
              onChange={(event) => onChange(colorWithBase(value, event.target.value))}
            />
          </label>
          <label className="overlay-designer-color-picker__hex">
            <span>{hexLabel}</span>
            <input
              type="text"
              value={hexDraft}
              disabled={disabled}
              inputMode="text"
              maxLength={7}
              spellCheck={false}
              aria-invalid={!/^#[0-9A-F]{6}$/.test(hexDraft.trim().toUpperCase())}
              onChange={(event) => setHexDraft(event.target.value)}
              onBlur={commitHex}
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  commitHex();
                  event.currentTarget.select();
                }
              }}
            />
          </label>
          <label className="overlay-designer-color-picker__alpha">
            <span>{alphaLabel}</span>
            <span>
              <input
                type="range"
                min="0"
                max="100"
                step="1"
                value={opacity}
                disabled={disabled}
                onInput={(event) => onChange(colorWithOpacity(
                  value,
                  Number((event.currentTarget as HTMLInputElement).value),
                ))}
                onChange={(event) => onChange(colorWithOpacity(value, Number(event.target.value)))}
              />
              <output>{opacity}%</output>
            </span>
          </label>
        </div>
      ) : null}
    </div>
  );
}

export function OverlayDesignerInspector({
  profile,
  component,
  tapState,
  battleState,
  chatState,
  previewActive,
  previewPending,
  previewError,
  previewConflict,
  disabled = false,
  onChange,
  onTogglePreview,
  dispatchTap,
  dispatchBattle,
  dispatchChat,
}: OverlayDesignerInspectorProps) {
  const { locale, t } = useLocalization();
  const [attackPlayer, setAttackPlayer] = useState(0);
  const [voteNumber, setVoteNumber] = useState(1);
  const colorGroup = component.id === "chat_chooses" ? "chatColors" : component.id === "pesky_battle" ? "battleColors" : "colors";
  const targetSlot = battleState.participants.some(player => player.slot === attackPlayer) ? attackPlayer : 0;
  const numberLocale = locale === "es" ? "es-MX" : "en-US";
  const geometryDisabled = disabled || component.locked;
  const colorLabel = (key: "liquidColor" | "collectingColor" | "textColor" | "outlineColor") =>
    t(`overlayDesigner.inspector.${colorGroup}.${key}`);
  const maximumSize = {
    width: profile.canvas.width - component.x,
    height: profile.canvas.height - component.y,
  };
  const independentSize = component.id === "chat_chooses";
  const minimumSize = independentSize ? minimumComponentSize(component.id) : proportionalComponentSize(component, 0, maximumSize);
  const maximumProportionalSize = independentSize ? maximumSize : proportionalComponentSize(
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
  const switchKeys: Array<
    "enabled" | "locked" | "showTitle" | "showDetails" | "motion"
  > = component.id === "tap_farming"
    ? ["enabled", "locked", "motion"]
    : component.id === "chat_chooses"
    ? ["enabled", "locked", "showTitle", "motion"]
    : ["enabled", "locked", "showTitle", "showDetails", "motion"];
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
    if (!independentSize && (key === "width" || key === "height")) {
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

        <div
          className="overlay-designer-properties__alignment"
          role="group"
          aria-label={t("overlayDesigner.inspector.alignment.title")}
        >
          <strong>{t("overlayDesigner.inspector.alignment.title")}</strong>
          <button
            type="button"
            disabled={geometryDisabled}
            onClick={() => onChange({
              x: Math.round((profile.canvas.width - component.width) / 2),
            })}
          >
            <span aria-hidden="true">↔</span>
            {t("overlayDesigner.inspector.alignment.horizontal")}
          </button>
          <button
            type="button"
            disabled={geometryDisabled}
            onClick={() => onChange({
              y: Math.round((profile.canvas.height - component.height) / 2),
            })}
          >
            <span aria-hidden="true">↕</span>
            {t("overlayDesigner.inspector.alignment.vertical")}
          </button>
        </div>

        <label className="overlay-designer-properties__opacity">
          <span>{t("overlayDesigner.inspector.opacity")}</span>
          <span className="overlay-designer-properties__opacity-control">
            <input
              type="range"
              min="0"
              max="100"
              step="1"
              value={component.opacity}
              disabled={disabled}
              onInput={(event) => onChange({
                opacity: Number((event.currentTarget as HTMLInputElement).value),
              })}
              onChange={(event) => onChange({ opacity: Number(event.target.value) })}
            />
            <output>{component.opacity}%</output>
          </span>
        </label>

        {(
          <div className="overlay-designer-properties__colors">
            <strong>{t(`overlayDesigner.inspector.${colorGroup}.title`)}</strong>
            {(["liquidColor", "collectingColor", "textColor", "outlineColor"] as const).map((key) => (
              <OverlayColorPicker
                key={key}
                label={colorLabel(key)}
                value={component[key]}
                disabled={disabled}
                alphaLabel={t("overlayDesigner.inspector.colors.alpha")}
                hexLabel={t("overlayDesigner.inspector.colors.hex")}
                openLabel={t("overlayDesigner.inspector.colors.open")}
                closeLabel={t("overlayDesigner.inspector.colors.close")}
                onChange={(value) => onChange({ [key]: value })}
              />
            ))}
          </div>
        )}

        <div className="overlay-designer-properties__switches">
          {switchKeys.map((key) => (
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
                <strong>{t(`overlayDesigner.inspector.${component.id === "chat_chooses" && (key === "showTitle" || key === "showDetails") ? "chatOptions" : "options"}.${key}`)}</strong>
                <small>{t(`overlayDesigner.inspector.${component.id === "chat_chooses" && (key === "showTitle" || key === "showDetails") ? "chatOptions" : "options"}.${key}Hint`)}</small>
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

        {component.id === "chat_chooses" ? (
          <div className="overlay-designer-simulation__body">
            <label><span>{t("overlayDesigner.simulation.scenario")}</span>
              <select value={chatState.phase} onChange={event => dispatchChat({ type: "scenario", phase: event.target.value as ChatChoosesState["phase"] })}>
                {(["off", "voting", "reveal", "result", "active"] as const).map(phase =>
                  <option key={phase} value={phase}>{t(`overlayDesigner.simulation.chat.phases.${phase}`)}</option>)}
              </select>
            </label>
            <label><span>{t("overlayDesigner.simulation.chat.stage")}</span>
              <select value={chatState.stage} onChange={event => dispatchChat({ type: "stage", stage: event.target.value as Exclude<ChatChoosesStage, "result"> })}>
                {(["boss", "weapon1", "weapon2", "super", "charm", "modifier"] as const).map(stage =>
                  <option key={stage} value={stage}>{t(`dashboard.chatChooses.stages.${stage}`)}</option>)}
              </select>
            </label>
            <label><span>{t("overlayDesigner.simulation.chat.count")}</span>
              <select value={chatState.options.length} disabled={chatState.stage === "super"} onChange={event => dispatchChat({ type: "count", count: Number(event.target.value) })}>
                {(chatState.stage === "super" ? [4] : [2, 3, 4, 5, 6]).map(count => <option key={count}>{count}</option>)}
              </select>
            </label>
            <label><span>{t("dashboard.simulator.voteNumber")}</span>
              <select value={Math.min(voteNumber, chatState.options.length)} onChange={event => setVoteNumber(Number(event.target.value))}>
                {chatState.options.map(option => <option key={option.number} value={option.number}>{option.number} · {option.name}</option>)}
              </select>
            </label>
            <button type="button" disabled={chatState.phase !== "voting"}
              onClick={() => dispatchChat({ type: "vote", number: Math.min(voteNumber, chatState.options.length) })}>
              {t("overlayDesigner.simulation.chat.vote")}
            </button>
            <small>{t("overlayDesigner.simulation.chat.hint")}</small>
            <button className="overlay-designer-simulation__reset" type="button" onClick={() => dispatchChat({ type: "reset" })}>
              <RotateCcw aria-hidden="true" />{t("overlayDesigner.simulation.reset")}
            </button>
          </div>
        ) : component.id === "tap_farming" ? (
          <div className="overlay-designer-simulation__body">
            <label>
              <span>{t("overlayDesigner.simulation.scenario")}</span>
              <select
                value={tapState.phase === "transition" ? "active" : tapState.phase}
                onChange={(event) => dispatchTap({
                  type: "scenario",
                  scenario: event.target.value as TapFarmingPreviewSnapshot["phase"],
                })}
              >
                {(["collecting", "active", "completed"] as const).map((phase) => (
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
                {(["off", "recruiting", "ready", "waiting_level", "active", "won"] as const).map((phase) => (
                  <option value={phase} key={phase}>
                    {t(`overlayDesigner.simulation.battle.phases.${phase}`)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>{t("overlayDesigner.simulation.battle.capacity")}</span>
              <select
                value={battleState.capacity}
                onChange={(event) => dispatchBattle({
                  type: "capacity",
                  capacity: Number(event.target.value),
                })}
              >
                {[2, 3, 4, 5].map((capacity) => (
                  <option value={capacity} key={capacity}>{capacity}</option>
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
                disabled={battleState.phase === "off"}
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
            <label>
              <span>{t("overlayDesigner.simulation.battle.attackPlayer")}</span>
              <select value={targetSlot} onChange={event => setAttackPlayer(Number(event.target.value))}>
                <option value="0">{t("overlayDesigner.simulation.battle.alternatePlayers")}</option>
                {battleState.participants.map(player => <option key={player.slot} value={player.slot}>{player.displayName}</option>)}
              </select>
            </label>
            <button
              type="button"
              disabled={battleState.phase !== "active" || battleState.participants.length === 0}
              onClick={() => {
                const item = battleAttacks[(battleState.attack?.sequence ?? 0) % battleAttacks.length];
                dispatchBattle({ type: "attack", name: t(item.titleKey), imagePath: item.image, startedAt: Date.now(), slot: targetSlot || undefined });
              }}
            >
              <Swords aria-hidden="true" />{t("overlayDesigner.simulation.battle.simulateAttack")}
            </button>
            <button type="button"
              disabled={battleState.phase !== "active" || battleState.participants.length === 0}
              onClick={() => {
                const startedAt = Date.now();
                for (let index = 0; index < 3; index++) {
                  const item = battleAttacks[((battleState.attack?.sequence ?? 0) + index) % battleAttacks.length];
                  dispatchBattle({ type: "attack", name: t(item.titleKey), imagePath: item.image, startedAt,
                    slot: targetSlot || battleState.participants[0].slot });
                }
              }}>
              <Swords aria-hidden="true" />{t("overlayDesigner.simulation.battle.simulateBurst")}
            </button>
            <button type="button"
              disabled={battleState.phase !== "active" || battleState.participants.length === 0}
              onClick={() => {
                if (battleState.challenges?.length) { dispatchBattle({ type: "clear_challenge" }); return; }
                const item = interactionItems.find(item => item.id === "challenge_no_dash")!;
                dispatchBattle({ type: "challenge", name: t(item.titleKey), imagePath: item.image,
                  startedAt: Date.now(), slot: targetSlot || battleState.participants[0].slot });
              }}>
              <Activity aria-hidden="true" />{t(battleState.challenges?.length
                ? "overlayDesigner.simulation.battle.stopChallenge" : "overlayDesigner.simulation.battle.simulateChallenge")}
            </button>
            <small>{t("overlayDesigner.simulation.battle.attackHint")}</small>
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
