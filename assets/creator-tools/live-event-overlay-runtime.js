(() => {
  "use strict";

  const PREVIEW_MESSAGE = "creator-tools-overlay-preview";
  const PREVIEW_READY_MESSAGE = "creator-tools-overlay-preview-ready";
  const MESSAGE_VERSION = 1;

  const previewRequested = () => {
    const query = new URLSearchParams(window.location.search);
    const preview = String(query.get("preview") || "").trim().toLowerCase();
    const mode = String(query.get("mode") || "").trim().toLowerCase();
    return preview === "1" || preview === "true" || preview === "on" ||
      mode === "preview";
  };

  const embeddedRequested = () => {
    const query = new URLSearchParams(window.location.search);
    const embedded = String(query.get("embedded") || "").trim().toLowerCase();
    const mode = String(query.get("mode") || "").trim().toLowerCase();
    return embedded === "1" || embedded === "true" || embedded === "on" ||
      mode === "embedded";
  };

  const sameParentOrigin = (event) => {
    if (event.source !== window.parent) return false;
    const ownOrigin = window.location.origin;
    return event.origin === ownOrigin ||
      (ownOrigin === "null" && event.origin === "null");
  };

  const create = ({
    overlay,
    endpoint,
    interval = 500,
    initialLiveState,
    initialPreviewState,
    render,
  }) => {
    if (!overlay || typeof render !== "function") {
      throw new Error("Invalid live-event overlay configuration.");
    }

    const preview = previewRequested();
    // Preview retains its original semantics when both flags are present.
    // Embedded is the compositor bridge: the parent owns transport but the
    // widget must still render as live output (no preview-only badges).
    const embedded = !preview && embeddedRequested();
    const mode = preview ? "preview" : embedded ? "embedded" : "live";
    let requestPending = false;
    let lastRevision = null;
    let timer = 0;
    let disposed = false;

    document.documentElement.dataset.overlayMode = mode;
    document.body.dataset.overlayMode = mode;

    const draw = (state) => {
      if (disposed || !state || typeof state !== "object") return;
      render(state, { preview, embedded, mode });
    };

    const refresh = async () => {
      if (mode !== "live" || requestPending || disposed || !endpoint) return;
      requestPending = true;
      try {
        const response = await fetch(endpoint, { cache: "no-store" });
        if (!response.ok) return;
        const state = await response.json();
        const revision = Number(state?.revision);
        if (!Number.isFinite(revision) || revision !== lastRevision) {
          lastRevision = Number.isFinite(revision) ? revision : lastRevision;
          draw(state);
        }
      } catch {
        // Keep the last complete snapshot visible through a short reconnect.
      } finally {
        requestPending = false;
      }
    };

    const receivePreview = (event) => {
      if ((mode !== "preview" && mode !== "embedded") ||
          !sameParentOrigin(event)) return;
      const message = event.data;
      if (!message || typeof message !== "object" ||
          message.type !== PREVIEW_MESSAGE ||
          Number(message.version || MESSAGE_VERSION) !== MESSAGE_VERSION ||
          message.overlay !== overlay ||
          !message.state || typeof message.state !== "object") return;
      draw(message.state);
    };

    const announceReady = () => {
      if ((mode !== "preview" && mode !== "embedded") ||
          window.parent === window) return;
      const ownOrigin = window.location.origin;
      window.parent.postMessage({
        type: PREVIEW_READY_MESSAGE,
        version: MESSAGE_VERSION,
        overlay,
        mode,
      }, ownOrigin === "null" ? "*" : ownOrigin);
    };

    const dispose = () => {
      if (disposed) return;
      disposed = true;
      if (timer) window.clearInterval(timer);
      window.removeEventListener("message", receivePreview);
    };

    window.addEventListener("pagehide", dispose, { once: true });
    if (mode === "preview" || mode === "embedded") {
      draw(mode === "preview" ? initialPreviewState : initialLiveState);
      window.addEventListener("message", receivePreview);
      announceReady();
    } else {
      draw(initialLiveState);
      void refresh();
      timer = window.setInterval(refresh, Math.max(250, Number(interval) || 500));
    }

    return Object.freeze({ preview, embedded, mode, refresh, dispose, draw });
  };

  window.LiveEventOverlayRuntime = Object.freeze({
    create,
    previewMessageType: PREVIEW_MESSAGE,
    previewReadyMessageType: PREVIEW_READY_MESSAGE,
    messageVersion: MESSAGE_VERSION,
  });
})();
