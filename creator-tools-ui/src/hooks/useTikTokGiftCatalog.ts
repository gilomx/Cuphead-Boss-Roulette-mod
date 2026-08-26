import { useEffect, useState } from "react";
import type { TikTokGiftCatalog } from "../model";

interface GiftCatalogState {
  catalog: TikTokGiftCatalog | null;
  error: boolean;
}

export function useTikTokGiftCatalog(): GiftCatalogState {
  const [state, setState] = useState<GiftCatalogState>({
    catalog: null,
    error: false,
  });

  useEffect(() => {
    const controller = new AbortController();
    fetch("/assets/creator-tools/gifts/catalog.json", {
      cache: "no-store",
      signal: controller.signal,
    })
      .then((response) => {
        if (!response.ok) throw new Error("HTTP " + response.status);
        return response.json() as Promise<TikTokGiftCatalog>;
      })
      .then((catalog) => setState({ catalog, error: false }))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setState({ catalog: null, error: true });
      });
    return () => controller.abort();
  }, []);

  return state;
}
