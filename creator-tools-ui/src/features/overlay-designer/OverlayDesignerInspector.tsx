import { Activity, HeartPulse, MousePointerClick, RotateCcw, Swords, Users } from "lucide-react";
import { useEffect, useRef, useState, type Dispatch } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItems } from "../interactions/interactionCatalog";
import type {
  OverlayComposerComponent,
  OverlayComposerProfile,
  OverlayTextFont,
  InteractionsPreviewSnapshot,
  PeskyBattlePreviewSnapshot,
  TapFarmingPreviewSnapshot,
} from "./model";
import { minimumComponentSize, proportionalComponentSize } from "./model";
import type { BattleSimulationAction, TapSimulationAction } from "./simulation";
import type { ChatChoosesState, ChatChoosesStage } from "../../model";
import type { ChatSimulationAction } from "./chatSimulation";
import type { RouletteScenario } from "./model";

const battleAttacks = interactionItems.filter(item => item.category === "attack" || item.category === "mini_boss");

interface OverlayDesignerInspectorProps {
  profile: OverlayComposerProfile;
  component: OverlayComposerComponent;
  tapState: TapFarmingPreviewSnapshot;
  battleState: PeskyBattlePreviewSnapshot;
  chatState: ChatChoosesState;
  rouletteScenario: RouletteScenario;
  interactionsState: InteractionsPreviewSnapshot;
  interactionsBattleOnly: boolean;
  onInteractionsBattleOnlyChange: (value: boolean) => void;
  onRouletteScenarioChange: (scenario: RouletteScenario) => void;
  onRouletteReplay: () => void;
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

function interactionsTextColor(component: OverlayComposerComponent) {
  const color = component.textColor ?? "#ffffff";
  if (component.interactionsTemplate !== "cards" || !["#ffffff", "#fff4dc"].includes(color.toLowerCase())) return color;
  // Match the legacy paper ink in the renderer. Color picker edits include
  // alpha, so explicitly choosing white remains white on the card.
  const paper = (component.interactionsCardColor ?? "#f8e5be").slice(1, 7);
  const brightness = .299 * parseInt(paper.slice(0, 2), 16) + .587 * parseInt(paper.slice(2, 4), 16) + .114 * parseInt(paper.slice(4, 6), 16);
  return brightness > 160 ? "#321c10" : color;
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
  rouletteScenario,
  interactionsState,
  interactionsBattleOnly,
  onInteractionsBattleOnlyChange,
  onRouletteScenarioChange,
  onRouletteReplay,
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
  const typographyColorKeys = component.id === "interactions" ? ["textShadowColor"] as const : ["textColor", "textShadowColor"] as const;
  const targetSlot = battleState.participants.some(player => player.slot === attackPlayer) ? attackPlayer : 0;
  const numberLocale = locale === "es" ? "es-MX" : "en-US";
  const geometryDisabled = disabled || component.locked;
  const colorKeys = ["liquidColor", "collectingColor",
    "outlineColor", ...(component.id === "chat_chooses" ? ["voteOutlineColor" as const] : [])] as const;
  const colorLabel = (key: typeof colorKeys[number]) =>
    t(`overlayDesigner.inspector.${colorGroup}.${key}`);
  const maximumSize = {
    width: profile.canvas.width - component.x,
    height: profile.canvas.height - component.y,
  };
  const independentSize = component.id === "chat_chooses" || component.id === "interactions";
  const interactionsLayout = component.interactionsLayout ?? (profile.id === "vertical" ? "pages" : "strip");
  const interactionsSize = component.interactionsSize ?? 0;
  const interactionsPlatforms = [
    ["interactionsTikTok", "TikTok", component.interactionsTikTok ?? true],
    ["interactionsTwitch", "Twitch", component.interactionsTwitch ?? profile.id === "horizontal"],
    ["interactionsYouTube", "YouTube", component.interactionsYouTube ?? profile.id === "horizontal"],
  ] as const;
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
  > = component.id === "roulette"
    ? ["enabled", "locked"]
    : component.id === "interactions"
    ? ["enabled", "locked", "motion"]
    : component.id === "tap_farming"
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

        {component.id === "interactions" ? (
          <div className="overlay-designer-simulation__body" role="group" aria-label={t("overlayDesigner.interactions.title")}>
            <strong>{t("overlayDesigner.interactions.title")}</strong>
            <label><span>{t("overlayDesigner.interactions.template")}</span>
              <select disabled={disabled} value={component.interactionsTemplate ?? "floating"}
                onChange={event => {
                  const template = event.target.value as "cards" | "floating";
                  const previousPaper = component.interactionsCardColor?.toLowerCase();
                  onChange({ interactionsTemplate: template, showTitle: false, showDetails: false,
                    ...(template === "cards" && ["#23262bee", "#662effa6"].includes(previousPaper ?? "")
                      ? { interactionsCardColor: "#f8e5be" } : {}),
                  });
                }}>
                <option value="cards">{t("overlayDesigner.interactions.cards")}</option>
                <option value="floating">{t("overlayDesigner.interactions.floating")}</option>
              </select>
            </label>
            <label><span>{t("overlayDesigner.interactions.layout")}</span>
              <select disabled={disabled} value={interactionsLayout}
                onChange={event => {
                  const layout = event.target.value as "strip" | "pages";
                  const width = Math.min(profile.canvas.width, 960);
                  const height = Math.min(profile.canvas.height, layout === "pages" ? 620 : 280);
                  onChange({ interactionsLayout: layout, width, height,
                    x: Math.min(component.x, profile.canvas.width - width),
                    y: Math.min(component.y, profile.canvas.height - height) });
                }}>
                <option value="strip">{t("overlayDesigner.interactions.strip")}</option>
                <option value="pages">{t("overlayDesigner.interactions.pages")}</option>
              </select>
            </label>
            <OverlayColorPicker
              label={t("overlayDesigner.inspector.typography.textColor")}
              value={interactionsTextColor(component)}
              disabled={disabled}
              alphaLabel={t("overlayDesigner.inspector.colors.alpha")}
              hexLabel={t("overlayDesigner.inspector.colors.hex")}
              openLabel={t("overlayDesigner.inspector.colors.open")}
              closeLabel={t("overlayDesigner.inspector.colors.close")}
              onChange={value => onChange({ textColor: value })}
            />
            {component.interactionsTemplate === "cards" ? <>
              <OverlayColorPicker
                label={t("overlayDesigner.interactions.cardColor")}
                value={component.interactionsCardColor ?? "#f8e5be"}
                disabled={disabled}
                alphaLabel={t("overlayDesigner.inspector.colors.alpha")}
                hexLabel={t("overlayDesigner.inspector.colors.hex")}
                openLabel={t("overlayDesigner.inspector.colors.open")}
                closeLabel={t("overlayDesigner.inspector.colors.close")}
                onChange={value => onChange({ interactionsCardColor: value })}
              />
              <label><span>{t("overlayDesigner.interactions.cardNoise")}</span>
                <input type="range" min="0" max="100" step="1" disabled={disabled}
                  value={component.interactionsCardNoise ?? 18}
                  onChange={event => onChange({ interactionsCardNoise: Number(event.target.value) })} />
                <output>{component.interactionsCardNoise ?? 18}%</output>
              </label>
              <small>{t("overlayDesigner.interactions.cardNoiseHint")}</small>
            </> : null}
            <strong>{t("overlayDesigner.interactions.itemOutlines")}</strong>
            <small>{t("overlayDesigner.interactions.itemOutlinesHint")}</small>
            {([
              ["interactionsImageOutlineColor", "interactionsImageOutlineWidth", "imageOutlineColor", "imageOutlineWidth"],
              ["interactionsTextOutlineColor", "interactionsTextOutlineWidth", "textOutlineColor", "textOutlineWidth"],
            ] as const).map(([colorKey, widthKey, colorLabelKey, widthLabelKey]) => <div key={colorKey} className="overlay-designer-simulation__body">
              <label><span>{t(`overlayDesigner.interactions.${widthLabelKey}`)}</span>
                <input type="range" min="0" max="8" step="1" disabled={disabled}
                  value={component[widthKey] ?? 0}
                  onChange={event => onChange({ [widthKey]: Number(event.target.value) })} />
                <output>{component[widthKey] ?? 0} px</output>
              </label>
              <OverlayColorPicker
                label={t(`overlayDesigner.interactions.${colorLabelKey}`)}
                value={component[colorKey] ?? "#000000"}
                disabled={disabled}
                alphaLabel={t("overlayDesigner.inspector.colors.alpha")}
                hexLabel={t("overlayDesigner.inspector.colors.hex")}
                openLabel={t("overlayDesigner.inspector.colors.open")}
                closeLabel={t("overlayDesigner.inspector.colors.close")}
                onChange={value => onChange({ [colorKey]: value })}
              />
            </div>)}
            <strong>{t("overlayDesigner.interactions.platforms")}</strong>
            {interactionsPlatforms.map(([key, label, checked]) => (
              <label className="overlay-designer-properties__checkbox" key={key}>
                <input type="checkbox" disabled={disabled} checked={checked}
                  onChange={event => onChange({ [key]: event.target.checked })} />
                <span>{label}</span>
              </label>
            ))}
            {!interactionsPlatforms.some(([, , checked]) => checked) ? <small role="status">{t("overlayDesigner.interactions.noPlatforms")}</small> : null}
            {interactionsLayout === "pages" ? <>
              <label><span>{t("overlayDesigner.interactions.pageSeconds")}</span>
                <input type="number" min="3" max="60" step="1" disabled={disabled}
                  value={component.interactionsPageSeconds ?? 8}
                  onChange={event => onChange({ interactionsPageSeconds: Math.max(3, Math.min(60, numberValue(event.target.value, 8))) })} />
              </label>
              <small>{t("overlayDesigner.interactions.pagesHint")}</small>
            </> : <><label><span>{t("overlayDesigner.interactions.speed")}</span>
              <input type="range" min="40" max="140" step="1" disabled={disabled}
                value={component.interactionsSpeed ?? 40}
                onChange={event => onChange({ interactionsSpeed: Number(event.target.value) })} />
              <output>{component.interactionsSpeed ?? 40} px/s</output>
            </label><small>{t("overlayDesigner.interactions.stripHint")}</small></>}
            <label className="overlay-designer-properties__checkbox">
              <input type="checkbox" disabled={disabled} checked={component.interactionsAdaptive ?? true}
                onChange={event => onChange({ interactionsAdaptive: event.target.checked })} />
              <span>{t("overlayDesigner.interactions.adaptive")}</span>
            </label>
            <small>{t("overlayDesigner.interactions.adaptiveHint")}</small>
            <label className="overlay-designer-properties__checkbox">
              <input type="checkbox" disabled={disabled} checked={interactionsSize === 0}
                onChange={event => onChange({ interactionsSize: event.target.checked ? 0 : 75 })} />
              <span>{t("overlayDesigner.interactions.automaticSize")}</span>
            </label>
            {interactionsSize > 0 ? <label><span>{t("overlayDesigner.interactions.itemSize")}</span>
              <input type="range" min="25" max="100" step="1" disabled={disabled}
                value={interactionsSize}
                onChange={event => onChange({ interactionsSize: Number(event.target.value) })} />
              <output>{interactionsSize}%</output>
            </label> : null}
            <small>{t("overlayDesigner.interactions.itemSizeHint")}</small>
            <label className="overlay-designer-properties__checkbox">
              <input type="checkbox" disabled={disabled} checked={interactionsBattleOnly}
                onChange={event => onInteractionsBattleOnlyChange(event.target.checked)} />
              <span>{t("overlayDesigner.interactions.battleOnly")}</span>
            </label>
            <small>{t("overlayDesigner.interactions.battleOnlyHint")}</small>
          </div>
        ) : null}

        <div className="overlay-designer-simulation__body" role="group" aria-label={t("overlayDesigner.inspector.typography.title")}>
          <strong>{t("overlayDesigner.inspector.typography.title")}</strong>
          {component.id === "roulette" ? <small>{t("overlayDesigner.inspector.typography.fixedRouletteFont")}</small> : <>
          <label><span>{t("overlayDesigner.inspector.typography.font")}</span>
            <select disabled={disabled} value={component.textFont ?? "clean"} onChange={event => onChange({ textFont: event.target.value as OverlayTextFont })}>
              {["clean", "system", "rounded", "serif"].map(font =>
                <option key={font} value={font}>{t(`overlayDesigner.inspector.typography.fonts.${font}`)}</option>)}
            </select>
          </label>
          <label><span>{t("overlayDesigner.inspector.typography.weight")}</span>
            <select disabled={disabled} value={component.textWeight ?? 700} onChange={event => onChange({ textWeight: Number(event.target.value) })}>
              {[400, 700, 900].map(weight => <option key={weight} value={weight}>{t(`overlayDesigner.inspector.typography.weights.${weight}`)}</option>)}
            </select>
          </label>
          </>}
          {typographyColorKeys.map(key => <OverlayColorPicker
            key={key}
            label={t(`overlayDesigner.inspector.typography.${key}`)}
            value={component[key] ?? (key === "textColor" ? "#ffffff" : "#00000000")}
            disabled={disabled}
            alphaLabel={t("overlayDesigner.inspector.colors.alpha")}
            hexLabel={t("overlayDesigner.inspector.colors.hex")}
            openLabel={t("overlayDesigner.inspector.colors.open")}
            closeLabel={t("overlayDesigner.inspector.colors.close")}
            onChange={value => onChange({ [key]: value })}
          />)}
          <small>{t("overlayDesigner.inspector.typography.shadowHint")}</small>
        </div>

        {component.id !== "roulette" && component.id !== "interactions" ? (
          <div className="overlay-designer-properties__colors">
            <strong>{t(`overlayDesigner.inspector.${colorGroup}.title`)}</strong>
            {colorKeys.map((key) => (
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
        ) : component.id === "roulette" ? (
          <div className="overlay-designer-simulation__body">
            <label><span>{t("overlayDesigner.roulette.order")}</span>
              <select disabled={disabled} value={String(component.rouletteTextFirst ?? false)} onChange={event => onChange({ rouletteTextFirst: event.target.value === "true" })}>
                <option value="false">{t("overlayDesigner.roulette.iconsAbove")}</option>
                <option value="true">{t("overlayDesigner.roulette.textAbove")}</option>
              </select>
            </label>
            <label><span>{t("overlayDesigner.roulette.alignment")}</span>
              <select disabled={disabled} value={component.rouletteAlignment ?? "center"} onChange={event => onChange({ rouletteAlignment: event.target.value as "left" | "center" | "right" })}>
                {["left", "center", "right"].map(value => <option key={value} value={value}>{t(`overlayDesigner.roulette.${value}`)}</option>)}
              </select>
            </label>
            <label><span>{t("overlayDesigner.roulette.retry")}</span>
              <select disabled={disabled} value={component.rouletteRetry ?? "reappear"} onChange={event => onChange({ rouletteRetry: event.target.value as "keep" | "reappear" })}>
                {["keep", "reappear"].map(value => <option key={value} value={value}>{t(`overlayDesigner.roulette.${value}`)}</option>)}
              </select>
            </label>
            <label className="overlay-designer-properties__checkbox">
              <input type="checkbox" disabled={disabled} checked={component.rouletteLogo ?? false} onChange={event => onChange({ rouletteLogo: event.target.checked })} />
              <span>{t("overlayDesigner.roulette.logo")}</span>
            </label>
          </div>
        ) : null}

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
          <h2 id="overlay-designer-simulation-title">{t(component.id === "interactions" ? "overlayDesigner.interactions.previewTitle" : "overlayDesigner.simulation.title")}</h2>
        </header>

        {component.id === "interactions" ? (
          <div className="overlay-designer-simulation__body">
            <strong>{t(interactionsState.examples ? "overlayDesigner.interactions.examples" : "overlayDesigner.interactions.configuredRules")}</strong>
            <small>{t(interactionsState.examples ? "overlayDesigner.interactions.examplesHint"
              : interactionsState.rules.length ? "overlayDesigner.interactions.rulesHint" : "overlayDesigner.interactions.noRules")}</small>
            <small>{t("overlayDesigner.interactions.obsHint")}</small>
          </div>
        ) : component.id === "roulette" ? (
          <div className="overlay-designer-simulation__body">
            <label><span>{t("overlayDesigner.simulation.scenario")}</span>
              <select value={rouletteScenario} onChange={event => onRouletteScenarioChange(event.target.value as RouletteScenario)}>
                {["hud", "logo", "hidden", "retry"].map(value => <option key={value} value={value}>{t(`overlayDesigner.roulette.scenarios.${value}`)}</option>)}
              </select>
            </label>
            <button className="overlay-designer-simulation__reset" type="button" onClick={onRouletteReplay}>
              <RotateCcw aria-hidden="true" />{t("overlayDesigner.roulette.replay")}
            </button>
            <small>{t(!component.enabled ? "overlayDesigner.roulette.disabledPreviewHint"
              : rouletteScenario === "logo" && !component.rouletteLogo ? "overlayDesigner.roulette.logoPreviewHint"
              : "overlayDesigner.roulette.previewHint")}</small>
          </div>
        ) : component.id === "chat_chooses" ? (
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
