import { Maximize2 } from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
  type PointerEvent,
} from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type {
  OverlayComposerComponent,
  OverlayComposerDesignMessage,
  OverlayComposerProfile,
  OverlayComponentId,
  PeskyBattlePreviewSnapshot,
  TapFarmingPreviewSnapshot,
} from "./model";
import { proportionalComponentSize } from "./model";

type PreviewBackground = "alpha" | "light" | "dark";
type ZoomOption = "fit" | 0.25 | 0.5 | 1;
type ResizeHandle = "nw" | "ne" | "sw" | "se";

interface OverlayDesignerCanvasProps {
  profile: OverlayComposerProfile;
  selectedComponentId: OverlayComponentId;
  tapState: TapFarmingPreviewSnapshot;
  battleState: PeskyBattlePreviewSnapshot;
  disabled?: boolean;
  onChange: (
    componentId: OverlayComponentId,
    update: Partial<OverlayComposerComponent>,
  ) => void;
}

interface CanvasStyle extends CSSProperties {
  "--designer-canvas-scale": number;
  "--designer-canvas-inverse-scale": number;
}

interface DragState {
  pointerId: number;
  componentId: OverlayComponentId;
  mode: "move" | "resize";
  handle?: ResizeHandle;
  clientX: number;
  clientY: number;
  start: OverlayComposerComponent;
}

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value));
}

function resizedRect(
  start: OverlayComposerComponent,
  handle: ResizeHandle,
  dx: number,
  dy: number,
  canvasWidth: number,
  canvasHeight: number,
) {
  const fromWest = handle.includes("w");
  const fromNorth = handle.includes("n");
  const horizontalScale = (
    start.width + (fromWest ? -dx : dx)
  ) / Math.max(1, start.width);
  const verticalScale = (
    start.height + (fromNorth ? -dy : dy)
  ) / Math.max(1, start.height);
  const requestedScale = Math.abs(horizontalScale - 1) >= Math.abs(verticalScale - 1)
    ? horizontalScale
    : verticalScale;
  const maximum = {
    width: fromWest ? start.x + start.width : canvasWidth - start.x,
    height: fromNorth ? start.y + start.height : canvasHeight - start.y,
  };
  const size = proportionalComponentSize(start, requestedScale, maximum);
  const right = start.x + start.width;
  const bottom = start.y + start.height;

  return {
    x: fromWest ? right - size.width : start.x,
    y: fromNorth ? bottom - size.height : start.y,
    width: size.width,
    height: size.height,
  };
}

export function OverlayDesignerCanvas({
  profile,
  selectedComponentId,
  tapState,
  battleState,
  disabled = false,
  onChange,
}: OverlayDesignerCanvasProps) {
  const { locale, t } = useLocalization();
  const viewportRef = useRef<HTMLDivElement>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const dragRef = useRef<DragState | null>(null);
  const [available, setAvailable] = useState({ width: 1, height: 1 });
  const [zoom, setZoom] = useState<ZoomOption>("fit");
  const [background, setBackground] = useState<PreviewBackground>("alpha");

  useEffect(() => {
    const viewport = viewportRef.current;
    if (!viewport) return;
    const measure = () => setAvailable({
      width: Math.max(1, viewport.clientWidth - 40),
      height: Math.max(1, viewport.clientHeight - 40),
    });
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(viewport);
    return () => observer.disconnect();
  }, []);

  const fitScale = Math.min(
    available.width / Math.max(1, profile.canvas.width),
    available.height / Math.max(1, profile.canvas.height),
    1,
  );
  const scale = zoom === "fit" ? fitScale : zoom;
  const canvasStyle: CanvasStyle = {
    width: profile.canvas.width,
    height: profile.canvas.height,
    "--designer-canvas-scale": scale,
    "--designer-canvas-inverse-scale": 1 / Math.max(0.01, scale),
  };

  const message = useMemo<OverlayComposerDesignMessage>(() => ({
    type: "creator-tools-overlay-composer-design",
    version: 1,
    profileId: profile.id,
    profile,
    selectedComponentId,
    states: {
      tap_farming: tapState,
      pesky_battle: battleState,
    },
    locale,
    background,
  }), [background, battleState, locale, profile, selectedComponentId, tapState]);

  const latestMessageRef = useRef(message);
  latestMessageRef.current = message;

  const postDesign = useCallback(() => {
    iframeRef.current?.contentWindow?.postMessage(
      latestMessageRef.current,
      window.location.origin,
    );
  }, []);

  useEffect(() => {
    const receiveReady = (event: MessageEvent<unknown>) => {
      if (event.source !== iframeRef.current?.contentWindow ||
          event.origin !== window.location.origin ||
          !event.data || typeof event.data !== "object") return;
      const ready = event.data as Record<string, unknown>;
      if (ready.type !== "creator-tools-overlay-composer-ready" ||
          Number(ready.version) !== 1 ||
          ready.profileId !== latestMessageRef.current.profileId) return;
      postDesign();
    };

    window.addEventListener("message", receiveReady);
    return () => window.removeEventListener("message", receiveReady);
  }, [postDesign]);

  useEffect(() => postDesign(), [message, postDesign]);

  const source = useMemo(() => {
    const query = new URLSearchParams({ designer: "1", locale, background });
    return `/overlay/${profile.id}?${query}`;
  }, [background, locale, profile.id]);

  const beginInteraction = (
    event: PointerEvent<HTMLDivElement | HTMLButtonElement>,
    component: OverlayComposerComponent,
    mode: DragState["mode"],
    handle?: ResizeHandle,
  ) => {
    event.preventDefault();
    event.stopPropagation();
    if (component.id !== selectedComponentId) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    if (disabled || component.locked) return;
    dragRef.current = {
      pointerId: event.pointerId,
      componentId: component.id,
      mode,
      handle,
      clientX: event.clientX,
      clientY: event.clientY,
      start: { ...component },
    };
  };

  const moveInteraction = (event: PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId || disabled) return;
    event.preventDefault();
    const dx = (event.clientX - drag.clientX) / Math.max(0.01, scale);
    const dy = (event.clientY - drag.clientY) / Math.max(0.01, scale);
    if (drag.mode === "move") {
      onChange(drag.componentId, {
        x: Math.round(clamp(
          drag.start.x + dx,
          0,
          Math.max(0, profile.canvas.width - drag.start.width),
        )),
        y: Math.round(clamp(
          drag.start.y + dy,
          0,
          Math.max(0, profile.canvas.height - drag.start.height),
        )),
      });
      return;
    }
    onChange(drag.componentId, resizedRect(
      drag.start,
      drag.handle ?? "se",
      dx,
      dy,
      profile.canvas.width,
      profile.canvas.height,
    ));
  };

  const endInteraction = (event: PointerEvent<HTMLDivElement>) => {
    if (dragRef.current?.pointerId === event.pointerId) dragRef.current = null;
  };

  const sortedComponents = [...profile.components].sort((left, right) =>
    left.layer - right.layer);

  return (
    <section className="overlay-designer-canvas" aria-label={t("overlayDesigner.canvas.title")}>
      <div className="overlay-designer-canvas__toolbar">
        <div className="overlay-designer-canvas__zoom" role="group" aria-label={t("overlayDesigner.canvas.zoom")}>
          {(["fit", 0.25, 0.5, 1] as ZoomOption[]).map((option) => (
            <button
              type="button"
              data-active={zoom === option}
              aria-pressed={zoom === option}
              key={String(option)}
              onClick={() => setZoom(option)}
            >
              {option === "fit" ? <><Maximize2 aria-hidden="true" />{t("overlayDesigner.canvas.fit")}</> : `${option * 100}%`}
            </button>
          ))}
        </div>
        <div className="overlay-designer-canvas__background" role="group" aria-label={t("overlayDesigner.canvas.background")}>
          {(["alpha", "light", "dark"] as PreviewBackground[]).map((option) => (
            <button
              type="button"
              data-active={background === option}
              aria-pressed={background === option}
              key={option}
              onClick={() => setBackground(option)}
            >
              {t(`overlayDesigner.canvas.backgrounds.${option}`)}
            </button>
          ))}
        </div>
        <span>{profile.canvas.width} × {profile.canvas.height}</span>
      </div>

      <div
        ref={viewportRef}
        className="overlay-designer-canvas__viewport"
        data-background={background}
      >
        <div
          className="overlay-designer-canvas__scaled-space"
          style={{
            width: profile.canvas.width * scale,
            height: profile.canvas.height * scale,
          }}
        >
          <div
            className="overlay-designer-canvas__surface"
            style={canvasStyle}
            onPointerMove={moveInteraction}
            onPointerUp={endInteraction}
            onPointerCancel={endInteraction}
          >
            <iframe
              key={source}
              ref={iframeRef}
              title={t("overlayDesigner.canvas.iframeTitle")}
              src={source}
              width={profile.canvas.width}
              height={profile.canvas.height}
              tabIndex={-1}
              onLoad={postDesign}
            />
            <div className="overlay-designer-canvas__hit-plane">
              {sortedComponents.map((component) => {
                const selected = component.id === selectedComponentId;
                return (
                  <div
                    className="overlay-designer-selection"
                    data-selected={selected}
                    data-enabled={component.enabled}
                    data-locked={component.locked}
                    role="presentation"
                    aria-label={t(`overlayDesigner.components.${component.id}`)}
                    key={component.id}
                    style={{
                      left: component.x,
                      top: component.y,
                      width: component.width,
                      height: component.height,
                      zIndex: component.layer + 1,
                    }}
                    onPointerDown={(event) => beginInteraction(event, component, "move")}
                  >
                    <span className="overlay-designer-selection__label">
                      {t(`overlayDesigner.components.${component.id}`)}
                    </span>
                    {selected && !component.locked ? (["nw", "ne", "sw", "se"] as ResizeHandle[]).map((handle) => (
                      <button
                        type="button"
                        className="overlay-designer-selection__handle"
                        data-handle={handle}
                        aria-label={t(`overlayDesigner.canvas.resize.${handle}`)}
                        key={handle}
                        onPointerDown={(event) => beginInteraction(
                          event,
                          component,
                          "resize",
                          handle,
                        )}
                      />
                    )) : null}
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
