(() => {
  "use strict";

  const stage = document.getElementById("stage");
  const result = document.getElementById("result");
  const iconsRoot = document.getElementById("icons");
  const challenge = document.getElementById("challenge");
  const challengeImage = document.getElementById("challenge-image");
  const challengeFallback = document.getElementById("challenge-fallback");
  const brand = document.getElementById("brand");

  let socket = null;
  let reconnectDelay = 250;
  let currentSession = null;
  let revealed = 0;
  let textVisible = false;
  let receivedState = false;
  let previewActive = false;
  let exiting = false;
  let exitMustComplete = false;
  let entryTimers = [];
  let exitTimers = [];
  let currentView = "hidden";
  let targetView = "hidden";
  let pendingState = null;
  let logoExitTimer = null;
  let hudToLogoTimer = null;

  const iconEntryStep = 280;
  const challengeEntryDelay = 210;
  const iconExitDuration = 380;
  const challengeExitDuration = 280;
  const challengeExitDelay = challengeEntryDelay;
  const iconExitStep = iconEntryStep;
  const groundRetryIconExitDuration = 260;
  const groundRetryChallengeExitDuration = 200;
  const groundRetryChallengeExitDelay = 130;
  const groundRetryIconExitStep = 180;
  const logoExitDuration = 620;
  const hudToLogoGapDuration = 80;

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

    const settings = state.settings || {};
    const battleActive = Boolean(state.battleActive);
    applySettings(settings);
    pendingState = state;
    if (!state.active) {
      hideOverlayImmediately();
      receivedState = true;
      return;
    }
    targetView = state.visible
      ? "hud"
      : !battleActive && settings.logo ? "logo" : "hidden";
    transitionToTarget();
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
    window.requestAnimationFrame(fitChallengeFallback);
  }

  function transitionToTarget() {
    if (hudToLogoTimer !== null) {
      if (targetView === "logo") {
        return;
      }
      window.clearTimeout(hudToLogoTimer);
      hudToLogoTimer = null;
    }

    if (currentView === targetView) {
      if (currentView === "hud") {
        if (exiting) {
          if (exitMustComplete) {
            return;
          }
          cancelExitAnimation();
        }
        renderHudState(pendingState, false);
      } else if (currentView === "logo" &&
                 brand.classList.contains("brand-exit")) {
        enterLogo();
      }
      return;
    }

    if (currentView === "logo") {
      beginLogoExit();
      return;
    }
    if (currentView === "hud") {
      cancelEntryAnimation();
      beginExitAnimation();
      return;
    }
    if (targetView === "logo") {
      enterLogo();
      return;
    }
    if (targetView === "hud") {
      enterHud();
      return;
    }
    updateStageVisibility();
  }

  function enterLogo() {
    if (logoExitTimer !== null) {
      window.clearTimeout(logoExitTimer);
      logoExitTimer = null;
    }
    currentView = "logo";
    result.hidden = true;
    brand.hidden = false;
    brand.setAttribute("aria-hidden", "false");
    brand.classList.remove("brand-enter", "brand-exit");
    void brand.offsetWidth;
    brand.classList.add("brand-enter");
    stage.classList.remove("hidden");
  }

  function beginLogoExit() {
    if (currentView !== "logo" ||
        brand.classList.contains("brand-exit")) {
      return;
    }
    brand.classList.remove("brand-enter");
    void brand.offsetWidth;
    brand.classList.add("brand-exit");
    logoExitTimer = window.setTimeout(
      finishLogoExit, logoExitDuration);
  }

  function finishLogoExit() {
    logoExitTimer = null;
    brand.hidden = true;
    brand.setAttribute("aria-hidden", "true");
    brand.classList.remove("brand-enter", "brand-exit");
    currentView = "hidden";
    updateStageVisibility();
    transitionToTarget();
  }

  function enterHud() {
    if (!pendingState) {
      return;
    }
    currentView = "hud";
    brand.hidden = true;
    brand.setAttribute("aria-hidden", "true");
    result.hidden = false;
    stage.classList.remove("hidden");
    renderHudState(pendingState, true);
  }

  function renderHudState(state, entering) {
    if (!state || targetView !== "hud") {
      return;
    }

    const isPreview = Boolean(state.preview);
    const previewStarting = isPreview && !previewActive;
    const sessionChanged = currentSession !== state.session;
    const initial = entering || !receivedState || sessionChanged;
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
    if (previewStarting) {
      animateHudEntry(
        Math.max(0, state.revealed || 0),
        Boolean(state.textVisible));
    } else if (!(isPreview && entryTimers.length > 0)) {
      if (!isPreview) {
        cancelEntryAnimation();
      }
      revealIcons(Math.max(0, state.revealed || 0), initial);
      revealChallenge(Boolean(state.textVisible), initial);
    }
    previewActive = isPreview;
  }

  function updateStageVisibility() {
    stage.classList.toggle("hidden", result.hidden && brand.hidden);
  }

  function fitChallengeFallback() {
    challengeFallback.style.fontSize = "";
    challengeFallback.style.whiteSpace = "nowrap";
    challengeFallback.style.width = "auto";

    if (!challengeFallback.textContent ||
        challengeFallback.style.display === "none") {
      return;
    }

    const available = Math.max(160, window.innerWidth * 0.9);
    const naturalWidth = challengeFallback.scrollWidth;
    if (naturalWidth <= available) {
      return;
    }

    const baseSize = Number.parseFloat(
      window.getComputedStyle(challengeFallback).fontSize) || 34;
    const fittedSize = baseSize * available / naturalWidth;
    const minimumSize = baseSize * 0.6;
    if (fittedSize >= minimumSize) {
      challengeFallback.style.fontSize = `${fittedSize}px`;
      return;
    }

    challengeFallback.style.fontSize = `${minimumSize}px`;
    challengeFallback.style.width = `${available}px`;
    challengeFallback.style.whiteSpace = "normal";
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
      } else if (
        normalizedPath.startsWith("weapons/") ||
        normalizedPath.startsWith("supers/") ||
        normalizedPath.startsWith("charms/") ||
        normalizedPath.startsWith("modifiers/")
      ) {
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
    window.requestAnimationFrame(fitChallengeFallback);
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

  function animateHudEntry(nextCount, nextTextVisible) {
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
        if (currentView !== "hud" || targetView !== "hud" || exiting) {
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
        if (currentView !== "hud" || targetView !== "hud" || exiting) {
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
    if (exiting || result.hidden) {
      return;
    }

    exiting = true;
    const fastRetryExit = Boolean(
      pendingState && pendingState.fastRetryExit);
    exitMustComplete = Boolean(
      pendingState && pendingState.completeExit);
    const activeIconExitDuration = fastRetryExit
      ? groundRetryIconExitDuration
      : iconExitDuration;
    const activeChallengeExitDuration = fastRetryExit
      ? groundRetryChallengeExitDuration
      : challengeExitDuration;
    const activeChallengeExitDelay = fastRetryExit
      ? groundRetryChallengeExitDelay
      : challengeExitDelay;
    const activeIconExitStep = fastRetryExit
      ? groundRetryIconExitStep
      : iconExitStep;
    result.classList.toggle("fast-retry-exit", fastRetryExit);
    const icons = Array.from(iconsRoot.children)
      .slice(0, revealed);

    icons.forEach((icon, index) => {
      const timer = window.setTimeout(() => {
        icon.classList.remove("settled", "reveal", "exit");
        void icon.offsetWidth;
        icon.classList.add("exit");
      }, index * activeIconExitStep);
      exitTimers.push(timer);
    });

    const challengeStart = icons.length > 0
      ? (icons.length - 1) * activeIconExitStep +
        activeChallengeExitDelay
      : 0;
    if (textVisible) {
      exitTimers.push(window.setTimeout(() => {
        challenge.classList.remove("settled", "reveal", "exit");
        void challenge.offsetWidth;
        challenge.classList.add("exit");
      }, challengeStart));
    }

    const lastIconDelay = icons.length > 0
      ? (icons.length - 1) * activeIconExitStep +
        activeIconExitDuration
      : 0;
    const challengeEnd = textVisible
      ? challengeStart + activeChallengeExitDuration
      : 0;
    exitTimers.push(window.setTimeout(
      finishExitAnimation,
      Math.max(lastIconDelay, challengeEnd)));
  }

  function finishExitAnimation() {
    result.hidden = true;
    Array.from(iconsRoot.children).forEach(icon => {
      icon.classList.remove("settled", "reveal", "exit");
    });
    challenge.classList.remove("settled", "reveal", "exit");
    revealed = 0;
    textVisible = false;
    previewActive = false;
    exiting = false;
    exitMustComplete = false;
    exitTimers = [];
    result.classList.remove("fast-retry-exit");
    currentView = "hidden";
    updateStageVisibility();
    if (targetView === "logo") {
      hudToLogoTimer = window.setTimeout(() => {
        hudToLogoTimer = null;
        transitionToTarget();
      }, hudToLogoGapDuration);
      return;
    }
    transitionToTarget();
  }
  function cancelExitAnimation() {
    exitTimers.forEach(timer => window.clearTimeout(timer));
    exitTimers = [];
    exiting = false;
    exitMustComplete = false;
    result.classList.remove("fast-retry-exit");
    Array.from(iconsRoot.children).forEach(icon => {
      icon.classList.remove("exit");
    });
    challenge.classList.remove("exit");
  }

  function cancelEntryAnimation() {
    entryTimers.forEach(timer => window.clearTimeout(timer));
    entryTimers = [];
  }

  function hideHudImmediately() {
    cancelEntryAnimation();
    cancelExitAnimation();
    previewActive = false;
    result.hidden = true;
    revealed = 0;
    textVisible = false;
    Array.from(iconsRoot.children).forEach(icon => {
      icon.classList.remove("settled", "reveal", "exit");
    });
    challenge.classList.remove("settled", "reveal", "exit");
    if (currentView === "hud") {
      currentView = "hidden";
    }
    updateStageVisibility();
  }

  function hideOverlayImmediately() {
    targetView = "hidden";
    pendingState = null;
    if (logoExitTimer !== null) {
      window.clearTimeout(logoExitTimer);
      logoExitTimer = null;
    }
    if (hudToLogoTimer !== null) {
      window.clearTimeout(hudToLogoTimer);
      hudToLogoTimer = null;
    }
    brand.hidden = true;
    brand.setAttribute("aria-hidden", "true");
    brand.classList.remove("brand-enter", "brand-exit");
    hideHudImmediately();
    currentView = "hidden";
    updateStageVisibility();
  }
  window.addEventListener("resize", fitChallengeFallback);
  connect();
})();
