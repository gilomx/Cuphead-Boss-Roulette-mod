import { useEffect, useMemo, useState } from "react";
import { useConfig } from "../config/ConfigContext";
import type { TikTokGiftCatalog } from "../model";

interface GiftCatalogState {
  catalog: TikTokGiftCatalog | null;
  error: boolean;
}

export function useTikTokGiftCatalog(): GiftCatalogState {
  const { streamRules } = useConfig();
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

  const catalog = useMemo(() => {
    if (!state.catalog) return null;
    let gifts = state.catalog.gifts;
    if (streamRules?.communityGift) {
      const learned = streamRules.communityGift;
      const placeholder = gifts.find(
        (gift) => gift.kind === "community" || gift.giftId === "0",
      );
      if (placeholder) {
        const communityGift = {
          ...placeholder,
          giftId: learned.giftId,
          name: learned.name,
          coinsPerUnit: learned.coinsPerUnit,
          imagePath: learned.imagePath || learned.placeholderImagePath,
          kind: "community" as const,
          learned: learned.learned,
        };
        gifts = gifts.map((gift) =>
          gift === placeholder ? communityGift : gift);
      }
    }
    return {
      ...state.catalog,
      gifts: [...gifts].sort((left, right) => {
        const leftCommunity = left.kind === "community" || left.giftId === "0";
        const rightCommunity = right.kind === "community" || right.giftId === "0";
        if (leftCommunity !== rightCommunity) return leftCommunity ? -1 : 1;
        return left.coinsPerUnit - right.coinsPerUnit ||
          left.name.localeCompare(right.name, "es", {
            sensitivity: "base",
            numeric: true,
          }) ||
          left.giftId.localeCompare(right.giftId, undefined, { numeric: true });
      }),
    };
  }, [state.catalog, streamRules?.communityGift]);

  return { catalog, error: state.error };
}
