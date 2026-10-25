export const DESIGNER_HISTORY_LIMIT = 10;

export interface EditHistory<T> {
  past: T[];
  present: T;
  future: T[];
  group: string | null;
}

export function createEditHistory<T>(present: T): EditHistory<T> {
  return { past: [], present, future: [], group: null };
}

export function recordEdit<T>(
  history: EditHistory<T>,
  present: T,
  group: string | null = null,
): EditHistory<T> {
  return {
    past: group !== null && group === history.group
      ? history.past
      : [...history.past, history.present].slice(-DESIGNER_HISTORY_LIMIT),
    present,
    future: [],
    group,
  };
}

export function undoEdit<T>(history: EditHistory<T>): EditHistory<T> {
  if (!history.past.length) return history;
  return {
    past: history.past.slice(0, -1),
    present: history.past[history.past.length - 1],
    future: [history.present, ...history.future],
    group: null,
  };
}

export function redoEdit<T>(history: EditHistory<T>): EditHistory<T> {
  if (!history.future.length) return history;
  return {
    past: [...history.past, history.present].slice(-DESIGNER_HISTORY_LIMIT),
    present: history.future[0],
    future: history.future.slice(1),
    group: null,
  };
}
