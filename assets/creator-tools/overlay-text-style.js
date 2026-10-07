(() => {
  "use strict";
  const FONTS = Object.freeze({
    clean: "sans-serif",
    system: 'system-ui, "Segoe UI", sans-serif',
    rounded: '"Trebuchet MS", "Segoe UI", sans-serif',
    serif: 'Georgia, "Times New Roman", serif',
  });
  const color = (value, fallback) => /^#[0-9a-f]{6}([0-9a-f]{2})?$/i.test(String(value || "")) ? value : fallback;
  const offset = (value, fallback, minimum = -20) => {
    const parsed = Number(value);
    return value == null || !Number.isFinite(parsed) ? fallback : Math.max(minimum, Math.min(20, Math.round(parsed)));
  };
  const apply = (root, presentation = {}) => {
    if (!root?.style) return;
    const font = Object.prototype.hasOwnProperty.call(FONTS, presentation.textFont) ? presentation.textFont : "clean";
    const weight = [400, 700, 900].includes(presentation.textWeight) ? presentation.textWeight : 700;
    const shadow = `${offset(presentation.textShadowX, 2)}px ${offset(presentation.textShadowY, 3)}px ${offset(presentation.textShadowBlur, 2, 0)}px ${color(presentation.textShadowColor, "#00000000")}`;
    root.style.setProperty("--overlay-font-family", FONTS[font]);
    root.style.setProperty("--overlay-font-weight", String(weight));
    root.style.setProperty("text-transform", "uppercase");
    root.style.setProperty("--overlay-text-color", color(presentation.textColor, "#ffffff"));
    root.style.setProperty("--overlay-text-shadow", shadow);
    root.style.setProperty("--overlay-text-drop-shadow", shadow);
  };
  window.CreatorToolsOverlayText = Object.freeze({ apply });
})();
