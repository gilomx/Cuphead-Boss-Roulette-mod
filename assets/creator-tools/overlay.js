(() => {
  "use strict";

  const stage = document.getElementById("stage");
  const iconsRoot = document.getElementById("icons");
  const challenge = document.getElementById("challenge");
  const challengeImage = document.getElementById("challenge-image");
  const challengeFallback = document.getElementById("challenge-fallback");

  let socket = null;
  let reconnectDelay = 250;
  let currentSession = null;
  let revealed = 0;
  let textVisible = false;
  let receivedState = false;
  let previewActive = false;
  let exiting = false;
  let entryTimers = [];
  let exitTimers = [];

  const iconEntryStep = 280;
  const challengeEntryDelay = 210;
  const iconExitDuration = 380;
  const challengeExitDuration = 280;
  const challengeExitDelay = challengeEntryDelay;
  const iconExitStep = iconEntryStep;

  function connect() {
    const protocol = location.protocol === "https:" ? "wss:" : "ws:";
    socket = new WebSocket(`${protocol}//${location.host}/ws`);

    socket.addEventListener("open", () => {
      reconnectDelay = 250;
      receivedState = false;
    });

    socket.addEventListener("message", event => {
      try {
        applyState(JSON.parse(event.data));
      } catch (_) {
        hideOverlayImmediately();
      }
    });

    socket.addEventListener("close", reconnect);
    socket.addEventListener("error", () => socket.close());
  }

  function reconnect() {
    hideOverlayImmediately();
    window.setTimeout(connect, reconnectDelay);
    reconnectDelay = Math.min(5000, reconnectDelay * 1.7);
  }

  function applyState(state) {
    if (!state || state.type !== "state") {
      return;
    }

    applySettings(state.settings || {});
    const visible = Boolean(state.active && state.visible);
    if (!visible) {
      previewActive = false;
      cancelEntryAnimation();
      if (receivedState) {
        beginExitAnimation();
      } else {
        hideOverlayImmediately();
      }
      receivedState = true;
      return;
    }

    cancelExitAnimation();
    const isPreview = Boolean(state.preview);
    const previewStarting = isPreview && !previewActive;
    const sessionChanged = currentSession !== state.session;
    const initial = !receivedState || sessionChanged;
    if (sessionChanged) {
      currentSession = state.session;
      revealed = 0;
      textVisible = false;
      rebuildIcons(state.icons || []);
    } else if (iconsRoot.children.length !== (state.icons || []).length) {
      rebuildIcons(state.icons || []);
      revealed = 0;
    }

    setChallenge(state.challengeText || "", state.labelRevision || 0);
    stage.classList.remove("hidden");
    if (previewStarting) {
      animatePreviewEntry(
        Math.max(0, state.revealed || 0),
        Boolean(state.textVisible));
    } else if (!(isPreview && entryTimers.length > 0)) {
      revealIcons(Math.max(0, state.revealed || 0), initial);
      revealChallenge(Boolean(state.textVisible), initial);
    }
    previewActive = isPreview;
    receivedState = true;
  }

  function applySettings(settings) {
    const alignment = ["left", "center", "right"].includes(settings.alignment)
      ? settings.alignment
      : "center";
    stage.classList.remove("align-left", "align-center", "align-right");
    stage.classList.add(`align-${alignment}`);

    const scale = Number(settings.scale) || 1;
    stage.classList.remove("scale-1", "scale-15", "scale-2");
    stage.classList.add(scale >= 1.75
      ? "scale-2"
      : scale >= 1.25 ? "scale-15" : "scale-1");
    stage.classList.toggle("text-first", Boolean(settings.textFirst));
    stage.classList.toggle("icons-first", !settings.textFirst);

    const opacity = Math.max(0.25, Math.min(1, Number(settings.opacity) || 1));
    stage.style.setProperty("--overlay-opacity", String(opacity));
  }

  function rebuildIcons(iconPaths) {
    iconsRoot.replaceChildren();
    iconPaths.forEach(path => {
      const image = document.createElement("img");
      image.className = "result-icon";
      const normalizedPath = String(path).replace(/\\/g, "/").toLowerCase();
      if (normalizedPath.endsWith("weapons/vacio.png")) {
        image.classList.add("empty-icon");
        image.src = "/assets/creator-tools/empty.png";
      } else if (normalizedPath.startsWith("weapons/")) {
        image.src = `/assets/creator-tools/${encodeURI(normalizedPath)}`;
      } else {
        image.src = `/assets/${encodeURI(path)}`;
      }
      image.alt = "";
      iconsRoot.appendChild(image);
    });
  }

  function setChallenge(text, labelRevision) {
    challengeFallback.textContent = text;
    if (labelRevision > 0 && text) {
      challengeImage.onload = () => {
        challengeImage.style.display = "block";
        challengeFallback.style.display = "none";
      };
      challengeImage.onerror = () => {
        challengeImage.style.display = "none";
        challengeFallback.style.display = "block";
      };
      challengeImage.src = `/generated/challenge.png?v=${labelRevision}`;
    } else {
      challengeImage.removeAttribute("src");
      challengeImage.style.display = "none";
      challengeFallback.style.display = text ? "block" : "none";
    }
  }

  function revealIcons(nextCount, initial) {
    const children = Array.from(iconsRoot.children);
    children.forEach((icon, index) => {
      if (index >= nextCount) {
        icon.classList.remove("settled", "reveal", "exit");
        return;
      }
      if (initial || index < revealed) {
        icon.classList.remove("reveal", "exit");
        icon.classList.add("settled");
        return;
      }
      icon.classList.remove("settled", "reveal", "exit");
      void icon.offsetWidth;
      icon.classList.add("reveal");
    });
    revealed = nextCount;
  }

  function revealChallenge(nextVisible, initial) {
    if (!nextVisible) {
      challenge.classList.remove("settled", "reveal", "exit");
      textVisible = false;
      return;
    }
    if (initial || textVisible) {
      challenge.classList.remove("reveal", "exit");
      challenge.classList.add("settled");
    } else {
      challenge.classList.remove("settled", "reveal", "exit");
      void challenge.offsetWidth;
      challenge.classList.add("reveal");
    }
    textVisible = true;
  }

  function animatePreviewEntry(nextCount, nextTextVisible) {
    cancelEntryAnimation();
    const icons = Array.from(iconsRoot.children);
    revealed = 0;
    textVisible = false;

    icons.forEach(icon => {
      icon.classList.remove("settled", "reveal", "exit");
    });
    challenge.classList.remove("settled", "reveal", "exit");

    icons.slice(0, nextCount).forEach((icon, index) => {
      entryTimers.push(window.setTimeout(() => {
        if (!previewActive || exiting) {
          return;
        }
        icon.classList.remove("settled", "reveal", "exit");
        void icon.offsetWidth;
        icon.classList.add("reveal");
        revealed = Math.max(revealed, index + 1);
      }, index * iconEntryStep));
    });

    const challengeDelay = nextCount > 0
      ? (nextCount - 1) * iconEntryStep + challengeEntryDelay
      : 0;
    if (nextTextVisible) {
      entryTimers.push(window.setTimeout(() => {
        if (!previewActive || exiting) {
          return;
        }
        challenge.classList.remove("settled", "reveal", "exit");
        void challenge.offsetWidth;
        challenge.classList.add("reveal");
        textVisible = true;
      }, challengeDelay));
    }

    const finishDelay = challengeDelay +
      (nextTextVisible ? challengeExitDuration : iconExitDuration);
    entryTimers.push(window.setTimeout(() => {
      entryTimers = [];
    }, finishDelay));
  }

  function beginExitAnimation() {
    if (exiting || stage.classList.contains("hidden")) {
      return;
    }

    exiting = true;
    const icons = Array.from(iconsRoot.children)
      .slice(0, revealed);

    icons.forEach((icon, index) => {
      const timer = window.setTimeout(() => {
        icon.classList.remove("settled", "reveal", "exit");
        void icon.offsetWidth;
        icon.classList.add("exit");
      }, index * iconExitStep);
      exitTimers.push(timer);
    });

    const challengeStart = icons.length > 0
      ? (icons.length - 1) * iconExitStep + challengeExitDelay
      : 0;
    if (textVisible) {
      exitTimers.push(window.setTimeout(() => {
        challenge.classList.remove("settled", "reveal", "exit");
        void challenge.offsetWidth;
        challenge.classList.add("exit");
      }, challengeStart));
    }

    const lastIconDelay = icons.length > 0
      ? (icons.length - 1) * iconExitStep + iconExitDuration
      : 0;
    const challengeEnd = textVisible
      ? challengeStart + challengeExitDuration
      : 0;
    exitTimers.push(window.setTimeout(
      finishExitAnimation,
      Math.max(lastIconDelay, challengeEnd)));
  }

  function finishExitAnimation() {
    stage.classList.add("hidden");
    Array.from(iconsRoot.children).forEach(icon => {
      icon.classList.remove("settled", "reveal", "exit");
    });
    challenge.classList.remove("settled", "reveal", "exit");
    exiting = false;
    exitTimers = [];
  }

  function cancelExitAnimation() {
    exitTimers.forEach(timer => window.clearTimeout(timer));
    exitTimers = [];
    exiting = false;
    Array.from(iconsRoot.children).forEach(icon => {
      icon.classList.remove("exit");
    });
    challenge.classList.remove("exit");
  }

  function cancelEntryAnimation() {
    entryTimers.forEach(timer => window.clearTimeout(timer));
    entryTimers = [];
  }

  function hideOverlayImmediately() {
    cancelEntryAnimation();
    cancelExitAnimation();
    previewActive = false;
    stage.classList.add("hidden");
  }

  connect();
})();
