// Automatic panel refreshes are useful only while its window has focus.
// Explicit refreshes after user commands remain owned by each caller.
export function startForegroundPolling(refresh: () => void, intervalMs: number) {
  let active = false;
  let disposed = false;
  let timer: number | undefined;

  const updateActivity = () => {
    if (disposed) return;
    const foreground = !document.hidden && document.hasFocus();
    if (foreground === active) return;
    active = foreground;
    if (timer !== undefined) {
      window.clearInterval(timer);
      timer = undefined;
    }
    if (!active) return;

    refresh();
    timer = window.setInterval(() => {
      if (disposed) return;
      // Recheck even if a browser delayed a visibility/focus notification.
      if (!document.hidden && document.hasFocus()) refresh();
      else updateActivity();
    }, intervalMs);
  };

  document.addEventListener("visibilitychange", updateActivity);
  window.addEventListener("focus", updateActivity);
  window.addEventListener("blur", updateActivity);
  updateActivity();

  return () => {
    disposed = true;
    if (timer !== undefined) window.clearInterval(timer);
    document.removeEventListener("visibilitychange", updateActivity);
    window.removeEventListener("focus", updateActivity);
    window.removeEventListener("blur", updateActivity);
  };
}
