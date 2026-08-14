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
        hideOverlay();
      }
    });

    socket.addEventListener("close", reconnect);
    socket.addEventListener("error", () => socket.close());
  }

  function reconnect() {
    hideOverlay();
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
      hideOverlay();
      receivedState = true;
      return;
    }

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
    revealIcons(Math.max(0, state.revealed || 0), initial);
    revealChallenge(Boolean(state.textVisible), initial);
    receivedState = true;
  }

  function applySettings(settings) {
    const alignment = ["left", "center", "right"].includes(settings.alignment)
      ? settings.alignment
      : "center";
    stage.classList.remove("align-left", "align-center", "align-right");
    stage.classList.add(`align-${alignment}`);

    stage.classList.toggle("scale-2", settings.scale === 2);
    stage.classList.toggle("scale-1", settings.scale !== 2);
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
      image.alt = "";
      image.src = `/assets/${encodeURI(path)}`;
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
        icon.classList.remove("settled", "reveal");
        return;
      }
      if (initial || index < revealed) {
        icon.classList.remove("reveal");
        icon.classList.add("settled");
        return;
      }
      icon.classList.remove("settled", "reveal");
      void icon.offsetWidth;
      icon.classList.add("reveal");
    });
    revealed = nextCount;
  }

  function revealChallenge(nextVisible, initial) {
    if (!nextVisible) {
      challenge.classList.remove("settled", "reveal");
      textVisible = false;
      return;
    }
    if (initial || textVisible) {
      challenge.classList.remove("reveal");
      challenge.classList.add("settled");
    } else {
      challenge.classList.remove("settled", "reveal");
      void challenge.offsetWidth;
      challenge.classList.add("reveal");
    }
    textVisible = true;
  }

  function hideOverlay() {
    stage.classList.add("hidden");
  }

  connect();
})();
