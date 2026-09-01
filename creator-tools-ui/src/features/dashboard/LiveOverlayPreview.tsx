import { MonitorPlay, RotateCcw, Sparkles } from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
} from "react";
import { useLocalization } from "../../i18n/LocalizationContext";

export type OverlayPreviewId = "pesky-battle" | "tap-farming";
type PreviewBackground = "alpha" | "light" | "dark";

export interface OverlayPreviewPreset {
  id: string;
  label: string;
  state: Record<string, unknown>;
}

interface LiveOverlayPreviewProps {
  overlay: OverlayPreviewId;
  src: string;
  presets: OverlayPreviewPreset[];
  simulateTaps?: (
    state: Record<string, unknown>,
    amount: number,
  ) => Record<string, unknown>;
}

interface PreviewStyle extends CSSProperties {
  "--overlay-preview-scale": number;
}

const TAP_BATCHES = [100, 500, 1000] as const;

export function LiveOverlayPreview({
  overlay,
  src,
  presets,
  simulateTaps,
}: LiveOverlayPreviewProps) {
  const { locale, t } = useLocalization();
  const stageRef = useRef<HTMLDivElement>(null);
  const iframeRef = useRef<HTMLIFrameElement>(null);
  const [scale, setScale] = useState(1);
  const [background, setBackground] = useState<PreviewBackground>("alpha");
  const [presetId, setPresetId] = useState(presets[0]?.id ?? "");
  const [simulatedState, setSimulatedState] = useState<Record<string, unknown>>(
    presets[0]?.state ?? {},
  );
  const [iframeKey, setIframeKey] = useState(0);

  const selectedPreset = useMemo(
    () => presets.find((preset) => preset.id === presetId) ?? presets[0],
    [presetId, presets],
  );

  useEffect(() => {
    if (!selectedPreset) return;
    setSimulatedState(selectedPreset.state);
  }, [selectedPreset]);

  useEffect(() => {
    const stage = stageRef.current;
    if (!stage) return;
    const resize = () => setScale(Math.max(0.01, stage.clientWidth / 1920));
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(stage);
    return () => observer.disconnect();
  }, []);

  const postState = useCallback(() => {
    iframeRef.current?.contentWindow?.postMessage({
      type: "creator-tools-overlay-preview",
      version: 1,
      overlay,
      state: { ...simulatedState, locale },
    }, window.location.origin);
  }, [locale, overlay, simulatedState]);

  useEffect(() => {
    postState();
  }, [postState]);

  useEffect(() => {
    const handleMessage = (event: MessageEvent<unknown>) => {
      if (event.origin !== window.location.origin ||
          event.source !== iframeRef.current?.contentWindow ||
          !event.data || typeof event.data !== "object") return;
      const message = event.data as {
        type?: string;
        version?: number;
        overlay?: string;
      };
      if (message.type === "creator-tools-overlay-preview-ready" &&
          message.version === 1 && message.overlay === overlay) {
        postState();
      }
    };
    window.addEventListener("message", handleMessage);
    return () => window.removeEventListener("message", handleMessage);
  }, [overlay, postState]);

  const previewUrl = useMemo(() => {
    const query = new URLSearchParams({ preview: "1", locale });
    return `${src}?${query}`;
  }, [locale, src]);

  const style: PreviewStyle = { "--overlay-preview-scale": scale };

  return (
    <section className="live-overlay-preview" aria-labelledby={`${overlay}-preview-title`}>
      <header className="live-overlay-preview__heading">
        <div>
          <p className="dashboard-eyebrow">{t("dashboard.overlayPreview.eyebrow")}</p>
          <h2 id={`${overlay}-preview-title`}>
            <MonitorPlay aria-hidden="true" />
            {t("dashboard.overlayPreview.title")}
          </h2>
          <p>{t("dashboard.overlayPreview.description")}</p>
        </div>
        <button
          className="live-overlay-preview__reload"
          type="button"
          onClick={() => setIframeKey((value) => value + 1)}
        >
          <RotateCcw aria-hidden="true" />
          {t("dashboard.overlayPreview.reload")}
        </button>
      </header>

      <div className="live-overlay-preview__toolbar">
        <div className="live-overlay-preview__control">
          <span>{t("dashboard.overlayPreview.state")}</span>
          <div className="live-overlay-preview__segments">
            {presets.map((preset) => (
              <button
                type="button"
                data-active={preset.id === selectedPreset?.id}
                aria-pressed={preset.id === selectedPreset?.id}
                key={preset.id}
                onClick={() => setPresetId(preset.id)}
              >
                {preset.label}
              </button>
            ))}
          </div>
        </div>

        <div className="live-overlay-preview__control">
          <span>{t("dashboard.overlayPreview.background")}</span>
          <div className="live-overlay-preview__segments">
            {(["alpha", "light", "dark"] as const).map((option) => (
              <button
                type="button"
                data-active={background === option}
                aria-pressed={background === option}
                key={option}
                onClick={() => setBackground(option)}
              >
                {t(`dashboard.overlayPreview.backgrounds.${option}`)}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div
        ref={stageRef}
        className="live-overlay-preview__stage"
        data-background={background}
        style={style}
      >
        <iframe
          key={iframeKey}
          ref={iframeRef}
          title={t("dashboard.overlayPreview.iframeTitle")}
          src={previewUrl}
          width="1920"
          height="1080"
          tabIndex={-1}
          onLoad={postState}
        />
      </div>

      {simulateTaps ? (
        <div className="live-overlay-preview__simulation">
          <span><Sparkles aria-hidden="true" />{t("dashboard.overlayPreview.simulate")}</span>
          <div>
            {TAP_BATCHES.map((amount) => (
              <button
                type="button"
                key={amount}
                onClick={() => setSimulatedState((current) =>
                  simulateTaps(current, amount))}
              >
                +{amount.toLocaleString(locale === "es" ? "es-MX" : "en-US")} taps
              </button>
            ))}
          </div>
          <button
            className="live-overlay-preview__reset"
            type="button"
            onClick={() => selectedPreset && setSimulatedState(selectedPreset.state)}
          >
            {t("dashboard.overlayPreview.resetSimulation")}
          </button>
        </div>
      ) : null}
    </section>
  );
}
