import {
  ArrowDown,
  ArrowUp,
  Eye,
  EyeOff,
  Lock,
  Unlock,
} from "lucide-react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type {
  OverlayComposerComponent,
  OverlayComposerProfile,
  OverlayComponentId,
} from "./model";

interface OverlayLayersPanelProps {
  profile: OverlayComposerProfile;
  selectedComponentId: OverlayComponentId;
  disabled?: boolean;
  onSelect: (componentId: OverlayComponentId) => void;
  onChange: (
    componentId: OverlayComponentId,
    update: Partial<OverlayComposerComponent>,
  ) => void;
  onMoveLayer: (componentId: OverlayComponentId, direction: -1 | 1) => void;
}

export function OverlayLayersPanel({
  profile,
  selectedComponentId,
  disabled = false,
  onSelect,
  onChange,
  onMoveLayer,
}: OverlayLayersPanelProps) {
  const { t } = useLocalization();
  const layers = [...profile.components].sort((left, right) => right.layer - left.layer);
  return (
    <aside className="overlay-designer-panel overlay-designer-layers" aria-labelledby="overlay-designer-layers-title">
      <header>
        <span>{t("overlayDesigner.layers.eyebrow")}</span>
        <h2 id="overlay-designer-layers-title">{t("overlayDesigner.layers.title")}</h2>
      </header>
      <div className="overlay-designer-layers__list">
        {layers.map((component, index) => (
          <article
            className="overlay-designer-layer"
            data-selected={component.id === selectedComponentId}
            data-enabled={component.enabled}
            key={component.id}
          >
            <button
              className="overlay-designer-layer__select"
              type="button"
              aria-pressed={component.id === selectedComponentId}
              onClick={() => onSelect(component.id)}
            >
              <i aria-hidden="true">{component.layer}</i>
              <span>
                <strong>{t(`overlayDesigner.components.${component.id}`)}</strong>
              </span>
            </button>
            <div className="overlay-designer-layer__actions">
              <button
                type="button"
                disabled={disabled}
                aria-label={t(component.enabled
                  ? "overlayDesigner.layers.hide"
                  : "overlayDesigner.layers.show")}
                title={t(component.enabled
                  ? "overlayDesigner.layers.hide"
                  : "overlayDesigner.layers.show")}
                onClick={() => onChange(component.id, { enabled: !component.enabled })}
              >
                {component.enabled ? <Eye aria-hidden="true" /> : <EyeOff aria-hidden="true" />}
              </button>
              <button
                type="button"
                disabled={disabled}
                aria-label={t(component.locked
                  ? "overlayDesigner.layers.unlock"
                  : "overlayDesigner.layers.lock")}
                title={t(component.locked
                  ? "overlayDesigner.layers.unlock"
                  : "overlayDesigner.layers.lock")}
                onClick={() => onChange(component.id, { locked: !component.locked })}
              >
                {component.locked ? <Lock aria-hidden="true" /> : <Unlock aria-hidden="true" />}
              </button>
              <button
                type="button"
                disabled={disabled || index === 0}
                aria-label={t("overlayDesigner.layers.raise")}
                title={t("overlayDesigner.layers.raise")}
                onClick={() => onMoveLayer(component.id, 1)}
              >
                <ArrowUp aria-hidden="true" />
              </button>
              <button
                type="button"
                disabled={disabled || index === layers.length - 1}
                aria-label={t("overlayDesigner.layers.lower")}
                title={t("overlayDesigner.layers.lower")}
                onClick={() => onMoveLayer(component.id, -1)}
              >
                <ArrowDown aria-hidden="true" />
              </button>
            </div>
          </article>
        ))}
      </div>
      <p>{t("overlayDesigner.layers.hint")}</p>
    </aside>
  );
}
