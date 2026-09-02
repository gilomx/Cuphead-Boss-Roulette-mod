import {
  ArrowLeft,
  Check,
  Clipboard,
  Copy,
  Monitor,
  RotateCcw,
  Save,
  Smartphone,
} from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
  type KeyboardEvent,
} from "react";
import { useLocalization } from "../../i18n/LocalizationContext";
import { OverlayDesignerCanvas } from "./OverlayDesignerCanvas";
import { OverlayDesignerInspector } from "./OverlayDesignerInspector";
import { OverlayLayersPanel } from "./OverlayLayersPanel";
import {
  componentById,
  isOverlayComponentId,
  isOverlayProfileId,
  profileById,
  type OverlayComposerComponent,
  type OverlayComposerProfile,
  type OverlayComponentId,
  type OverlayProfileId,
} from "./model";
import {
  battleSimulationReducer,
  createBattleSimulation,
  createTapSimulation,
  previewCommand,
  tapSimulationReducer,
} from "./simulation";
import { useOverlayComposer } from "./useOverlayComposer";

interface OverlayDesignerViewProps {
  onBack: () => void;
}

type Confirmation = "reset" | "copy" | null;

function querySelection() {
  const query = new URLSearchParams(window.location.search);
  return {
    profileId: isOverlayProfileId(query.get("profile"))
      ? query.get("profile") as OverlayProfileId
      : "vertical" as const,
    componentId: isOverlayComponentId(query.get("component"))
      ? query.get("component") as OverlayComponentId
      : "tap_farming" as const,
  };
}

function createPreviewSessionId() {
  if (typeof crypto.randomUUID === "function") return crypto.randomUUID();
  const random = new Uint32Array(4);
  crypto.getRandomValues(random);
  return `preview-${Date.now().toString(36)}-${Array.from(random, (value) =>
    value.toString(36)).join("-")}`;
}

export function OverlayDesignerView({ onBack }: OverlayDesignerViewProps) {
  const { locale, t } = useLocalization();
  const initialSelection = useMemo(querySelection, []);
  const [profileId, setProfileId] = useState<OverlayProfileId>(initialSelection.profileId);
  const [selectedComponentId, setSelectedComponentId] = useState<OverlayComponentId>(
    initialSelection.componentId,
  );
  const [tapState, dispatchTap] = useReducer(tapSimulationReducer, createTapSimulation());
  const [battleState, dispatchBattle] = useReducer(
    battleSimulationReducer,
    createBattleSimulation(),
  );
  const [previewActive, setPreviewActive] = useState(false);
  const [previewPending, setPreviewPending] = useState(false);
  const [copyStatus, setCopyStatus] = useState<OverlayProfileId | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation>(null);
  const [sessionEpoch, setSessionEpoch] = useState(0);
  const previewRef = useRef<{
    profileId: OverlayProfileId;
    sessionId: string;
  } | null>(null);
  const profileRef = useRef<OverlayComposerProfile | null>(null);
  const selectedComponentIdRef = useRef(selectedComponentId);
  const previewActiveRef = useRef(previewActive);
  const previewPendingRef = useRef(previewPending);
  const previewUpdateInFlightRef = useRef(false);
  const previewUpdateQueuedRef = useRef(false);
  const publishLatestRef = useRef<() => void>(() => undefined);
  const previewRetryTimerRef = useRef<number | null>(null);
  const previewRetryAttemptsRef = useRef(0);
  const tapStateRef = useRef(tapState);
  const battleStateRef = useRef(battleState);
  const viewMountedRef = useRef(true);
  tapStateRef.current = tapState;
  battleStateRef.current = battleState;
  const {
    profiles,
    status,
    feedback,
    previewError,
    previewConflict,
    ready,
    updateComponent,
    saveProfile,
    resetProfile,
    copyProfile,
    sendPreview,
    stopPreviewImmediately,
    isDirty,
    reload,
  } = useOverlayComposer();

  const profile = profileById(profiles, profileId);
  const component = componentById(profile, selectedComponentId);
  profileRef.current = profile;
  selectedComponentIdRef.current = selectedComponentId;
  previewActiveRef.current = previewActive;
  previewPendingRef.current = previewPending;
  const busy = status === "loading" || status === "saving";
  const dirty = isDirty(profileId);

  const updateQuery = useCallback((
    nextProfileId: OverlayProfileId,
    nextComponentId: OverlayComponentId,
  ) => {
    const query = new URLSearchParams(window.location.search);
    query.set("profile", nextProfileId);
    query.set("component", nextComponentId);
    window.history.replaceState(window.history.state, "", `${window.location.pathname}?${query}`);
  }, []);

  const schedulePreviewRetry = useCallback(() => {
    if (previewRetryTimerRef.current !== null ||
        previewRetryAttemptsRef.current >= 3) return;
    const delay = 1_000 * (2 ** previewRetryAttemptsRef.current);
    previewRetryAttemptsRef.current += 1;
    previewRetryTimerRef.current = window.setTimeout(() => {
      previewRetryTimerRef.current = null;
      if (viewMountedRef.current && !previewRef.current) {
        setSessionEpoch((current) => current + 1);
      }
    }, delay);
  }, []);

  const stopPreview = useCallback(() => {
    const active = previewRef.current;
    const currentProfile = profileRef.current;
    if (!active || !currentProfile) {
      previewActiveRef.current = false;
      previewPendingRef.current = false;
      setPreviewActive(false);
      setPreviewPending(false);
      return Promise.resolve("ok" as const);
    }
    previewPendingRef.current = true;
    setPreviewPending(true);
    return sendPreview(previewCommand(
      "stop",
      active.profileId,
      selectedComponentIdRef.current,
      active.sessionId,
      tapStateRef.current,
      battleStateRef.current,
      currentProfile,
      false,
    )).then((result) => {
      if (!viewMountedRef.current) return result;
      if (previewRef.current === active && result !== "error") {
        previewRef.current = null;
        previewActiveRef.current = false;
        setPreviewActive(false);
      }
      previewPendingRef.current = false;
      setPreviewPending(false);
      return result;
    });
  }, [sendPreview]);

  useEffect(() => {
    viewMountedRef.current = true;
    const stopOwnedPreview = (synchronizeView: boolean) => {
      if (previewRetryTimerRef.current !== null) {
        window.clearTimeout(previewRetryTimerRef.current);
        previewRetryTimerRef.current = null;
      }
      const active = previewRef.current;
      previewRef.current = null;
      if (synchronizeView) {
        previewActiveRef.current = false;
        previewPendingRef.current = false;
        setPreviewActive(false);
        setPreviewPending(false);
      }
      const currentProfile = profileRef.current;
      if (!active || !currentProfile) return;
      stopPreviewImmediately(previewCommand(
        "stop",
        active.profileId,
        selectedComponentIdRef.current,
        active.sessionId,
        tapStateRef.current,
        battleStateRef.current,
        currentProfile,
        false,
      ));
    };
    const handlePageHide = () => stopOwnedPreview(true);
    const handlePageShow = (event: PageTransitionEvent) => {
      if (event.persisted && !previewRef.current) {
        previewActiveRef.current = false;
        previewPendingRef.current = false;
        setPreviewActive(false);
        setPreviewPending(false);
        setSessionEpoch((current) => current + 1);
      }
    };
    window.addEventListener("pagehide", handlePageHide);
    window.addEventListener("pageshow", handlePageShow);
    return () => {
      window.removeEventListener("pagehide", handlePageHide);
      window.removeEventListener("pageshow", handlePageShow);
      viewMountedRef.current = false;
      stopOwnedPreview(false);
    };
  }, [stopPreviewImmediately]);

  const publishLatest = useCallback(() => {
    const active = previewRef.current;
    const currentProfile = profileRef.current;
    if (!active || !currentProfile ||
        active.profileId !== currentProfile.id) return;
    if (previewPendingRef.current || previewUpdateInFlightRef.current) {
      previewUpdateQueuedRef.current = true;
      return;
    }

    previewUpdateInFlightRef.current = true;
    previewUpdateQueuedRef.current = false;
    void sendPreview(previewCommand(
      "update",
      active.profileId,
      selectedComponentIdRef.current,
      active.sessionId,
      tapStateRef.current,
      battleStateRef.current,
      currentProfile,
      previewActiveRef.current,
    )).then((result) => {
      if (viewMountedRef.current &&
          (result === "conflict" || result === "expired") &&
          previewRef.current === active) {
        previewRef.current = null;
        previewActiveRef.current = false;
        setPreviewActive(false);
        if (result === "expired") schedulePreviewRetry();
      }
    }).finally(() => {
      previewUpdateInFlightRef.current = false;
      if (previewUpdateQueuedRef.current && previewRef.current === active) {
        previewUpdateQueuedRef.current = false;
        window.setTimeout(() => publishLatestRef.current(), 0);
      }
    });
  }, [schedulePreviewRetry, sendPreview]);
  publishLatestRef.current = publishLatest;

  useEffect(() => {
    if (!ready || !profile || previewRef.current) return;
    const active = {
      profileId,
      sessionId: createPreviewSessionId(),
    };
    previewRef.current = active;
    previewPendingRef.current = true;
    previewActiveRef.current = false;
    setPreviewPending(true);
    setPreviewActive(false);
    void sendPreview(previewCommand(
      "start",
      profileId,
      selectedComponentIdRef.current,
      active.sessionId,
      tapStateRef.current,
      battleStateRef.current,
      profile,
      false,
    )).then((result) => {
      if (!viewMountedRef.current || previewRef.current !== active) return;
      previewPendingRef.current = false;
      setPreviewPending(false);
      if (result === "ok") {
        previewRetryAttemptsRef.current = 0;
        publishLatestRef.current();
        return;
      }
      stopPreviewImmediately(previewCommand(
        "stop",
        active.profileId,
        selectedComponentIdRef.current,
        active.sessionId,
        tapStateRef.current,
        battleStateRef.current,
        profileRef.current ?? profile,
        false,
      ));
      previewRef.current = null;
      if (result !== "conflict") schedulePreviewRetry();
    });
  }, [profile?.id, profileId, ready, schedulePreviewRetry, sendPreview,
    sessionEpoch, stopPreviewImmediately]);

  useEffect(() => {
    if (!profile || previewPending || !previewRef.current) return;
    const timer = window.setTimeout(() => publishLatest(), 80);
    return () => window.clearTimeout(timer);
  }, [battleState, previewActive, previewPending, profile, publishLatest,
    selectedComponentId, tapState]);

  useEffect(() => {
    if (previewPending || !previewRef.current) return;
    const timer = window.setInterval(() => publishLatest(), 30_000);
    return () => window.clearInterval(timer);
  }, [previewPending, profileId, publishLatest]);

  const selectProfile = async (next: OverlayProfileId) => {
    if (next === profileId || previewPending) return false;
    if (previewRef.current && await stopPreview() === "error") return false;
    if (previewRetryTimerRef.current !== null) {
      window.clearTimeout(previewRetryTimerRef.current);
      previewRetryTimerRef.current = null;
    }
    previewRetryAttemptsRef.current = 0;
    setConfirmation(null);
    setProfileId(next);
    updateQuery(next, selectedComponentId);
    return true;
  };

  const selectComponent = async (next: OverlayComponentId) => {
    if (next === selectedComponentId || previewPending) return false;
    setSelectedComponentId(next);
    selectedComponentIdRef.current = next;
    updateQuery(profileId, next);
    return true;
  };

  const changeComponent = (
    componentId: OverlayComponentId,
    update: Partial<OverlayComposerComponent>,
  ) => updateComponent(profileId, componentId, update);

  const moveLayer = (componentId: OverlayComponentId, direction: -1 | 1) => {
    if (!profile) return;
    const ordered = [...profile.components].sort((left, right) => left.layer - right.layer);
    const index = ordered.findIndex((entry) => entry.id === componentId);
    const target = ordered[index + direction];
    const current = ordered[index];
    if (!current || !target) return;
    updateComponent(profileId, current.id, { layer: target.layer });
    updateComponent(profileId, target.id, { layer: current.layer });
  };

  const togglePreview = async () => {
    const active = previewRef.current;
    const currentProfile = profileRef.current;
    if (previewPending || !active || !currentProfile) return;
    const nextPreviewActive = !previewActive;
    previewPendingRef.current = true;
    setPreviewPending(true);
    const result = await sendPreview(previewCommand(
      "update",
      active.profileId,
      selectedComponentIdRef.current,
      active.sessionId,
      tapStateRef.current,
      battleStateRef.current,
      currentProfile,
      nextPreviewActive,
    ));
    if (!viewMountedRef.current || previewRef.current !== active) return;
    previewPendingRef.current = false;
    setPreviewPending(false);
    if (result === "ok") {
      previewActiveRef.current = nextPreviewActive;
      setPreviewActive(nextPreviewActive);
      return;
    }
    if (result === "conflict") {
      previewRef.current = null;
      previewActiveRef.current = false;
      setPreviewActive(false);
    } else if (result === "expired") {
      previewRef.current = null;
      previewActiveRef.current = false;
      setPreviewActive(false);
      schedulePreviewRetry();
    }
  };

  const handleProfileTabKeyDown = (
    event: KeyboardEvent<HTMLButtonElement>,
    current: OverlayProfileId,
  ) => {
    const ids: OverlayProfileId[] = ["vertical", "horizontal"];
    const index = ids.indexOf(current);
    const next = event.key === "ArrowRight" || event.key === "ArrowDown"
      ? ids[(index + 1) % ids.length]
      : event.key === "ArrowLeft" || event.key === "ArrowUp"
        ? ids[(index - 1 + ids.length) % ids.length]
        : event.key === "Home"
          ? ids[0]
          : event.key === "End"
            ? ids[ids.length - 1]
            : null;
    if (!next) return;
    event.preventDefault();
    void selectProfile(next).then((changed) => {
      if (!changed) return;
      window.requestAnimationFrame(() => {
        document.querySelector<HTMLButtonElement>(
          `[data-profile-tab="${next}"]`,
        )?.focus();
      });
    });
  };

  const copyObsUrl = async (targetProfileId: OverlayProfileId) => {
    const query = locale === "en" ? "?locale=en" : "";
    try {
      if (!navigator.clipboard?.writeText) throw new Error("Clipboard unavailable");
      await navigator.clipboard.writeText(
        `${window.location.origin}/overlay/${targetProfileId}${query}`,
      );
      setCopyStatus(targetProfileId);
    } catch {
      setCopyStatus(null);
    }
    window.setTimeout(() => setCopyStatus(null), 2200);
  };

  const otherProfileId: OverlayProfileId = profileId === "vertical"
    ? "horizontal"
    : "vertical";

  return (
    <div className="page page--overlay-designer">
      <header className="overlay-designer-header">
        <button className="overlay-designer-header__back" type="button" onClick={() => {
          void stopPreview().then((result) => {
            if (result !== "error") onBack();
          });
        }}>
          <ArrowLeft aria-hidden="true" />{t("overlayDesigner.actions.back")}
        </button>
        <div>
          <p className="dashboard-eyebrow">{t("overlayDesigner.eyebrow")}</p>
          <h1>{t("overlayDesigner.title")}</h1>
          <p>{t("overlayDesigner.description")}</p>
        </div>
        <div className="overlay-designer-header__status" data-status={status}>
          <span>{t(`overlayDesigner.status.${status}`)}</span>
          {status === "error" ? (
            <button type="button" onClick={() => void reload()}>{t("overlayDesigner.actions.retry")}</button>
          ) : null}
        </div>
      </header>

      <div className="overlay-designer-toolbar">
        <div className="overlay-designer-tabs" role="tablist" aria-label={t("overlayDesigner.profiles.title")}>
          {(["vertical", "horizontal"] as OverlayProfileId[]).map((id) => (
            <button
              type="button"
              role="tab"
              aria-selected={profileId === id}
              data-active={profileId === id}
              data-profile-tab={id}
              tabIndex={profileId === id ? 0 : -1}
              key={id}
              onClick={() => { void selectProfile(id); }}
              onKeyDown={(event) => handleProfileTabKeyDown(event, id)}
            >
              {id === "vertical" ? <Smartphone aria-hidden="true" /> : <Monitor aria-hidden="true" />}
              <span>
                <strong>{t(`overlayDesigner.profiles.${id}`)}</strong>
                <small>{id === "vertical" ? "1080 × 1920" : "1920 × 1080"}</small>
              </span>
            </button>
          ))}
        </div>

        <div className="overlay-designer-toolbar__actions">
          <button type="button" disabled={!ready} onClick={() => void copyObsUrl(profileId)}>
            {copyStatus === profileId ? <Check aria-hidden="true" /> : <Clipboard aria-hidden="true" />}
            {t(copyStatus === profileId
              ? "overlayDesigner.actions.urlCopied"
              : "overlayDesigner.actions.copyUrl")}
          </button>
          <button type="button" disabled={busy} onClick={() => setConfirmation("copy")}>
            <Copy aria-hidden="true" />{t("overlayDesigner.actions.copyProfile")}
          </button>
          <button type="button" disabled={busy} onClick={() => setConfirmation("reset")}>
            <RotateCcw aria-hidden="true" />{t("overlayDesigner.actions.resetProfile")}
          </button>
          <button
            className="overlay-designer-toolbar__save"
            type="button"
            disabled={busy || !ready || !dirty}
            onClick={() => void saveProfile(profileId)}
          >
            <Save aria-hidden="true" />{t("overlayDesigner.actions.save")}
          </button>
        </div>
      </div>

      {confirmation ? (
        <div className="overlay-designer-confirmation" role="alert">
          <div>
            <strong>{t(`overlayDesigner.confirmation.${confirmation}.title`)}</strong>
            <span>{t(`overlayDesigner.confirmation.${confirmation}.description`)
              .replace("{source}", t(`overlayDesigner.profiles.${otherProfileId}`))
              .replace("{target}", t(`overlayDesigner.profiles.${profileId}`))}</span>
          </div>
          <div>
            <button type="button" onClick={() => setConfirmation(null)}>
              {t("overlayDesigner.actions.cancel")}
            </button>
            <button
              type="button"
              onClick={() => {
                const pending = confirmation;
                setConfirmation(null);
                if (pending === "reset") void resetProfile(profileId);
                else void copyProfile(otherProfileId, profileId);
              }}
            >
              {t("overlayDesigner.actions.confirm")}
            </button>
          </div>
        </div>
      ) : null}

      {profile && component ? (
        <div className="overlay-designer-workspace">
          <OverlayLayersPanel
            profile={profile}
            selectedComponentId={selectedComponentId}
            disabled={busy}
            onSelect={selectComponent}
            onChange={(componentId, update) => changeComponent(componentId, update)}
            onMoveLayer={moveLayer}
          />
          <OverlayDesignerCanvas
            profile={profile}
            selectedComponentId={selectedComponentId}
            tapState={tapState}
            battleState={battleState}
            disabled={busy}
            onSelect={selectComponent}
            onChange={(componentId, update) => changeComponent(componentId, update)}
          />
          <OverlayDesignerInspector
            profile={profile}
            component={component}
            tapState={tapState}
            battleState={battleState}
            previewActive={previewActive}
            previewPending={previewPending}
            previewError={previewError}
            previewConflict={previewConflict}
            disabled={busy}
            onChange={(update) => changeComponent(component.id, update)}
            onTogglePreview={togglePreview}
            dispatchTap={dispatchTap}
            dispatchBattle={dispatchBattle}
          />
        </div>
      ) : (
        <div className="overlay-designer-empty" data-error={status === "error"}>
          <strong>{t(status === "error"
            ? "overlayDesigner.empty.error"
            : "overlayDesigner.empty.loading")}</strong>
          <p>{t(`overlayDesigner.feedback.${feedback}`, t("overlayDesigner.feedback.generic"))}</p>
        </div>
      )}
    </div>
  );
}
