import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import en from "../locales/en.json";
import es from "../locales/es.json";

export type Locale = "es" | "en";

type TranslationTree = Record<string, unknown>;

interface LocalizationValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: string, fallback?: string) => string;
}

const dictionaries: Record<Locale, TranslationTree> = { es, en };
const STORAGE_KEY = "la-pichi-ruleta.locale";

function initialLocale(): Locale {
  const stored = window.localStorage.getItem(STORAGE_KEY);
  if (stored === "es" || stored === "en") return stored;
  return window.navigator.language.toLowerCase().startsWith("es") ? "es" : "en";
}

function readTranslation(tree: TranslationTree, key: string): string | undefined {
  let current: unknown = tree;
  for (const part of key.split(".")) {
    if (!current || typeof current !== "object" || !(part in current)) return undefined;
    current = (current as TranslationTree)[part];
  }
  return typeof current === "string" ? current : undefined;
}

const LocalizationContext = createContext<LocalizationValue | null>(null);

export function LocalizationProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(initialLocale);

  const setLocale = useCallback((next: Locale) => {
    window.localStorage.setItem(STORAGE_KEY, next);
    document.documentElement.lang = next;
    setLocaleState(next);
  }, []);

  const t = useCallback(
    (key: string, fallback?: string) =>
      readTranslation(dictionaries[locale], key) ??
      readTranslation(dictionaries.en, key) ??
      fallback ??
      key,
    [locale],
  );

  useEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);

  const value = useMemo(() => ({ locale, setLocale, t }), [locale, setLocale, t]);
  return <LocalizationContext.Provider value={value}>{children}</LocalizationContext.Provider>;
}

export function useLocalization() {
  const value = useContext(LocalizationContext);
  if (!value) throw new Error("useLocalization must be used inside LocalizationProvider");
  return value;
}
