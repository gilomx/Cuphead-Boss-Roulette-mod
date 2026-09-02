import { useCallback, useEffect, useRef, useState } from "react";
import type {
  OverlayComposerCommand,
  OverlayComposerComponent,
  OverlayComposerConfigState,
  OverlayComposerProfile,
  OverlayComponentId,
  OverlayPreviewCommand,
  OverlayPreviewState,
  OverlayProfileId,
} from "./model";
import { cloneProfiles, componentById, profileById } from "./model";

type ComposerStatus = "loading" | "ready" | "saving" | "saved" | "error";
export type PreviewSendResult = "ok" | "conflict" | "expired" | "error";

const EDITABLE_KEYS = [
  "x",
  "y",
  "width",
  "height",
  "enabled",
  "locked",
  "layer",
  "opacity",
  "variant",
  "showTitle",
  "showDetails",
  "motion",
  "liquidColor",
  "collectingColor",
  "textColor",
  "outlineColor",
] as const;

function sameComponent(
  left: OverlayComposerComponent | null,
  right: OverlayComposerComponent,
) {
  return Boolean(left && EDITABLE_KEYS.every((key) => left[key] === right[key]));
}

function updateCommand(
  profileId: OverlayProfileId,
  component: OverlayComposerComponent,
): Omit<OverlayComposerCommand, "expectedRevision"> {
  return {
    schemaVersion: 1,
    operation: "update",
    profileId,
    componentId: component.id,
    x: component.x,
    y: component.y,
    width: component.width,
    height: component.height,
    enabled: component.enabled,
    locked: component.locked,
    layer: component.layer,
    opacity: component.opacity,
    variant: component.variant,
    showTitle: component.showTitle,
    showDetails: component.showDetails,
    motion: component.motion,
    liquidColor: component.liquidColor,
    collectingColor: component.collectingColor,
    textColor: component.textColor,
    outlineColor: component.outlineColor,
  };
}

async function readConfig() {
  const response = await fetch("/api/overlay-composer/config", { cache: "no-store" });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return await response.json() as OverlayComposerConfigState;
}

async function readPreview(profileId: OverlayProfileId) {
  const query = new URLSearchParams({ profile: profileId });
  const response = await fetch(`/api/overlay-composer/preview?${query}`, {
    cache: "no-store",
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const state = await response.json() as OverlayPreviewState;
  if (!Number.isFinite(state.revision)) throw new Error("Invalid preview state");
  return state;
}

async function postConfig(command: OverlayComposerCommand) {
  const response = await fetch("/api/overlay-composer/config/set", {
    method: "POST",
    cache: "no-store",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(command),
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return await response.json() as OverlayComposerConfigState;
}

async function postPreview(
  command: OverlayPreviewCommand,
  keepalive = false,
) {
  const response = await fetch("/api/overlay-composer/preview/set", {
    method: "POST",
    cache: "no-store",
    keepalive,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(command),
  });
  let state: OverlayPreviewState | null = null;
  try {
    state = await response.json() as OverlayPreviewState;
  } catch {
    // The status still communicates the failure when a proxy drops the body.
  }
  if (!response.ok) {
    return { ok: false as const, status: response.status, state };
  }
  if (!state || !Number.isFinite(state.revision)) {
    throw new Error("Invalid preview response");
  }
  return { ok: true as const, status: response.status, state };
}

function replaceProfile(
  profiles: OverlayComposerProfile[],
  replacement: OverlayComposerProfile,
) {
  return profiles.map((profile) => profile.id === replacement.id
    ? {
        ...replacement,
        canvas: { ...replacement.canvas },
        components: replacement.components.map((component) => ({ ...component })),
      }
    : profile);
}

export function useOverlayComposer() {
  const [confirmed, setConfirmed] = useState<OverlayComposerConfigState | null>(null);
  const [profiles, setProfiles] = useState<OverlayComposerProfile[]>([]);
  const [status, setStatus] = useState<ComposerStatus>("loading");
  const [feedback, setFeedback] = useState("loading");
  const [previewError, setPreviewError] = useState(false);
  const [previewConflict, setPreviewConflict] = useState(false);
  const confirmedRef = useRef<OverlayComposerConfigState | null>(null);
  const mountedRef = useRef(true);
  const configWriteChainRef = useRef<Promise<void>>(Promise.resolve());
  const previewWriteChainRef = useRef<Promise<void>>(Promise.resolve());
  const previewRevisionRef = useRef<Record<OverlayProfileId, number | null>>({
    vertical: null,
    horizontal: null,
  });

  const load = useCallback(async () => {
    setStatus("loading");
    try {
      const next = await readConfig();
      if (!mountedRef.current) return;
      confirmedRef.current = next;
      setConfirmed(next);
      setProfiles(cloneProfiles(next.profiles));
      setFeedback(next.feedback || "ready");
      setStatus(next.ready && !next.error ? "ready" : "error");
    } catch {
      if (!mountedRef.current) return;
      setFeedback("connection_error");
      setStatus("error");
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    void load();
    return () => {
      mountedRef.current = false;
    };
  }, [load]);

  const updateComponent = useCallback((
    profileId: OverlayProfileId,
    componentId: OverlayComponentId,
    update: Partial<OverlayComposerComponent> |
      ((current: OverlayComposerComponent) => OverlayComposerComponent),
  ) => {
    setProfiles((current) => current.map((profile) => profile.id !== profileId
      ? profile
      : {
          ...profile,
          components: profile.components.map((component) => {
            if (component.id !== componentId) return component;
            return typeof update === "function"
              ? update(component)
              : { ...component, ...update, id: component.id };
          }),
        }));
    setStatus((current) => current === "loading" || current === "error"
      ? current
      : "ready");
  }, []);

  const enqueueConfigWrite = useCallback((
    work: () => Promise<void>,
  ) => {
    const next = configWriteChainRef.current.then(work, work);
    configWriteChainRef.current = next.catch(() => undefined);
    return next;
  }, []);

  const runCommand = useCallback(async (
    command: Omit<OverlayComposerCommand, "expectedRevision">,
  ) => {
    const current = confirmedRef.current;
    if (!current) throw new Error("Composer state is unavailable");
    const next = await postConfig({ ...command, expectedRevision: current.revision });
    confirmedRef.current = next;
    if (mountedRef.current) {
      setConfirmed(next);
      setFeedback(next.feedback || "saved");
    }
    return next;
  }, []);

  const saveProfile = useCallback((profileId: OverlayProfileId) => {
    const draft = profileById(profiles, profileId);
    if (!draft || status === "saving" || !confirmedRef.current) return Promise.resolve();
    const snapshot: OverlayComposerProfile = {
      ...draft,
      canvas: { ...draft.canvas },
      components: draft.components.map((component) => ({ ...component })),
    };
    setStatus("saving");
    setFeedback("saving");
    return enqueueConfigWrite(async () => {
      try {
        let next = confirmedRef.current;
        for (const component of snapshot.components) {
          const serverProfile = profileById(next?.profiles ?? [], profileId);
          if (sameComponent(componentById(serverProfile, component.id), component)) continue;
          next = await runCommand(updateCommand(profileId, component));
        }
        const savedProfile = profileById(next?.profiles ?? [], profileId);
        if (mountedRef.current && savedProfile) {
          setProfiles((current) => replaceProfile(current, savedProfile));
          setStatus("saved");
          window.setTimeout(() => {
            if (mountedRef.current) setStatus((current) => current === "saved" ? "ready" : current);
          }, 2200);
        }
      } catch {
        if (mountedRef.current) {
          setStatus("error");
          setFeedback("save_error");
        }
        try {
          const next = await readConfig();
          confirmedRef.current = next;
          if (mountedRef.current) setConfirmed(next);
        } catch {
          // Preserve the local draft so the user can retry.
        }
      }
    });
  }, [enqueueConfigWrite, profiles, runCommand, status]);

  const resetProfile = useCallback((profileId: OverlayProfileId) => {
    if (status === "saving") return Promise.resolve();
    setStatus("saving");
    setFeedback("resetting");
    return enqueueConfigWrite(async () => {
      try {
        const next = await runCommand({
          schemaVersion: 1,
          operation: "reset",
          profileId,
        });
        const replacement = profileById(next.profiles, profileId);
        if (mountedRef.current && replacement) {
          setProfiles((current) => replaceProfile(current, replacement));
          setStatus("ready");
        }
      } catch {
        if (mountedRef.current) {
          setStatus("error");
          setFeedback("reset_error");
        }
      }
    });
  }, [enqueueConfigWrite, runCommand, status]);

  const copyProfile = useCallback((
    sourceProfileId: OverlayProfileId,
    profileId: OverlayProfileId,
  ) => {
    if (status === "saving") return Promise.resolve();
    setStatus("saving");
    setFeedback("copying");
    return enqueueConfigWrite(async () => {
      try {
        const next = await runCommand({
          schemaVersion: 1,
          operation: "copy",
          profileId,
          sourceProfileId,
        });
        const replacement = profileById(next.profiles, profileId);
        if (mountedRef.current && replacement) {
          setProfiles((current) => replaceProfile(current, replacement));
          setStatus("ready");
        }
      } catch {
        if (mountedRef.current) {
          setStatus("error");
          setFeedback("copy_error");
        }
      }
    });
  }, [enqueueConfigWrite, runCommand, status]);

  const sendPreview = useCallback((
    command: OverlayPreviewCommand,
    options: { keepalive?: boolean } = {},
  ) => {
    const work = async () => {
      const publishResult = (result: PreviewSendResult) => {
        if (mountedRef.current) {
          setPreviewError(result !== "ok");
          setPreviewConflict(result === "conflict");
        }
        return result;
      };
      const rememberState = (state: OverlayPreviewState | null) => {
        if (state && Number.isFinite(state.revision)) {
          previewRevisionRef.current[command.profileId] = state.revision;
        }
      };
      const knownRevision = previewRevisionRef.current[command.profileId];
      try {
        const guardedCommand = command.operation !== "start" && knownRevision !== null
          ? { ...command, expectedRevision: knownRevision }
          : command;
        const response = await postPreview(
          guardedCommand,
          options.keepalive === true,
        );
        if (!response.ok) {
          rememberState(response.state);
          if (response.status === 409 && response.state &&
              !response.state.active) {
            return publishResult("expired");
          }
          if (response.status === 409 &&
              response.state?.sessionId === command.sessionId &&
              response.state.active) {
            return publishResult("error");
          }
          return publishResult(response.status === 409 ? "conflict" : "error");
        }
        rememberState(response.state);
        return publishResult("ok");
      } catch {
        try {
          const state = await readPreview(command.profileId);
          rememberState(state);
          const ownsSlot = state.sessionId === command.sessionId;
          if (command.operation === "start") {
            return publishResult(ownsSlot && state.active
              ? "ok"
              : state.active ? "conflict" : "error");
          }
          if (command.operation === "stop") {
            return publishResult(!state.active
              ? "ok"
              : ownsSlot ? "error" : "conflict");
          }
          if (!state.active) return publishResult("expired");
          if (!ownsSlot) return publishResult("conflict");
          return publishResult(knownRevision !== null &&
            state.revision !== knownRevision ? "ok" : "error");
        } catch {
          return publishResult("error");
        }
      }
    };
    const next = previewWriteChainRef.current.then(work, work);
    previewWriteChainRef.current = next.then(() => undefined, () => undefined);
    return next;
  }, []);

  const stopPreviewImmediately = useCallback((command: OverlayPreviewCommand) => {
    const endpoint = "/api/overlay-composer/preview/set";
    const payload = JSON.stringify({ ...command, expectedRevision: undefined });
    try {
      if (typeof navigator.sendBeacon === "function" && navigator.sendBeacon(
        endpoint,
        new Blob([payload], { type: "application/json" }),
      )) return;
    } catch {
      // Fall through to a keepalive request when Beacon is unavailable.
    }
    void fetch(endpoint, {
      method: "POST",
      cache: "no-store",
      keepalive: true,
      headers: { "Content-Type": "application/json" },
      body: payload,
    }).catch(() => undefined);
  }, []);

  const isDirty = useCallback((profileId: OverlayProfileId) => {
    const draft = profileById(profiles, profileId);
    const server = profileById(confirmed?.profiles ?? [], profileId);
    if (!draft || !server || draft.components.length !== server.components.length) return false;
    return draft.components.some((component) =>
      !sameComponent(componentById(server, component.id), component));
  }, [confirmed?.profiles, profiles]);

  return {
    profiles,
    status,
    feedback,
    previewError,
    previewConflict,
    ready: Boolean(confirmed?.ready),
    updateComponent,
    saveProfile,
    resetProfile,
    copyProfile,
    sendPreview,
    stopPreviewImmediately,
    isDirty,
    reload: load,
  };
}
