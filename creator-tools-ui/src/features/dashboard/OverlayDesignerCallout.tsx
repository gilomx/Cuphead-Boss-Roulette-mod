import { LayoutDashboard, MoveDiagonal2 } from "lucide-react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { OverlayComponentId } from "../overlay-designer/model";

interface OverlayDesignerCalloutProps {
  componentId: OverlayComponentId;
  onOpen: () => void;
}

export function OverlayDesignerCallout({
  componentId,
  onOpen,
}: OverlayDesignerCalloutProps) {
  const { t } = useLocalization();
  return (
    <section className="overlay-designer-callout" aria-labelledby={`${componentId}-overlay-designer-callout-title`}>
      <span className="overlay-designer-callout__icon" aria-hidden="true">
        <LayoutDashboard />
      </span>
      <div>
        <p className="dashboard-eyebrow">{t("overlayDesigner.callout.eyebrow")}</p>
        <h2 id={`${componentId}-overlay-designer-callout-title`}>
          {t("overlayDesigner.callout.title")}
        </h2>
        <p>{t("overlayDesigner.callout.description")}</p>
      </div>
      <button type="button" onClick={onOpen}>
        <MoveDiagonal2 aria-hidden="true" />
        {t("overlayDesigner.callout.action")}
      </button>
    </section>
  );
}
