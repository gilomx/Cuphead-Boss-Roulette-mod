import { useCallback, useState } from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { InteractionSettingsPanel } from "./InteractionSettingsPanel";
import { StreamRulesView } from "./StreamRulesView";

type InteractionWorkspaceView = "rules" | "settings";

export function InteractionsView() {
  const { t } = useLocalization();
  const [workspaceView, setWorkspaceView] = useState<InteractionWorkspaceView>("rules");
  const [focusAdvancedSettings, setFocusAdvancedSettings] = useState(false);
  const [testSentNotice, setTestSentNotice] = useState(false);
  const dismissTestSentNotice = useCallback(() => setTestSentNotice(false), []);

  return (
    <div className="page page--interactions">
      <header className="page-header interaction-page-header">
        <div>
          <h1>{t("interactions.title")}</h1>
          <p>{t("interactions.description")}</p>
        </div>
      </header>

      <div className="interaction-workspace">
        <div
          className="interaction-workspace__view"
          data-view={workspaceView}
          key={workspaceView}
        >
          {workspaceView === "rules" ? (
            <StreamRulesView
              onOpenSettings={() => {
                setFocusAdvancedSettings(false);
                setWorkspaceView("settings");
              }}
              onOpenAdvancedSettings={() => {
                setFocusAdvancedSettings(true);
                setWorkspaceView("settings");
              }}
              testSentNotice={testSentNotice}
              onTestSentNoticeDismissed={dismissTestSentNotice}
            />
          ) : (
            <InteractionSettingsPanel
              focusAdvanced={focusAdvancedSettings}
              onBack={() => setWorkspaceView("rules")}
              onTestSent={() => {
                setTestSentNotice(true);
                setWorkspaceView("rules");
              }}
            />
          )}
        </div>
      </div>
    </div>
  );
}
