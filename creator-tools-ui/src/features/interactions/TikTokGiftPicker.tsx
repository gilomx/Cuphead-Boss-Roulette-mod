import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
} from "react";
import { Check } from "lucide-react";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { TikTokGift } from "../../model";

interface TikTokGiftPickerProps {
  gifts: TikTokGift[];
  selectedId: string;
  disabled: boolean;
  onSelect: (gift: TikTokGift) => void;
}

export function TikTokGiftPicker({
  gifts,
  selectedId,
  disabled,
  onSelect,
}: TikTokGiftPickerProps) {
  const { t } = useLocalization();
  const reactId = useId().replace(/:/g, "");
  const gridId = `stream-gift-picker-${reactId}`;
  const gridRef = useRef<HTMLDivElement>(null);
  const [search, setSearch] = useState("");
  const [activeGiftId, setActiveGiftId] = useState<string | null>(null);
  const filteredGifts = useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    if (!query) return gifts;
    return gifts.filter((gift) =>
      gift.name.toLocaleLowerCase().includes(query) ||
      gift.giftId.toLocaleLowerCase().includes(query) ||
      String(gift.coinsPerUnit).includes(query) ||
      gift.aliases.some((alias) => alias.toLocaleLowerCase().includes(query)));
  }, [gifts, search]);
  const activeIndex = activeGiftId === null
    ? -1
    : filteredGifts.findIndex((gift) => gift.giftId === activeGiftId);

  useEffect(() => {
    setActiveGiftId((current) => {
      if (current && filteredGifts.some((gift) => gift.giftId === current)) return current;
      if (filteredGifts.some((gift) => gift.giftId === selectedId)) return selectedId;
      return filteredGifts[0]?.giftId ?? null;
    });
  }, [filteredGifts, selectedId]);

  useEffect(() => {
    if (activeIndex < 0) return;
    const grid = gridRef.current;
    const option = grid?.querySelector<HTMLElement>(`[data-gift-index="${activeIndex}"]`);
    if (!grid || !option) return;

    const optionTop = option.offsetTop;
    const optionBottom = optionTop + option.offsetHeight;
    const viewportTop = grid.scrollTop;
    const viewportBottom = viewportTop + grid.clientHeight;
    if (optionTop < viewportTop) {
      grid.scrollTop = optionTop;
    } else if (optionBottom > viewportBottom) {
      grid.scrollTop = optionBottom - grid.clientHeight;
    }
  }, [activeIndex]);

  const moveActive = (key: string) => {
    if (filteredGifts.length === 0) return -1;
    const columns = Math.max(
      1,
      gridRef.current
        ? getComputedStyle(gridRef.current).gridTemplateColumns.split(/\s+/).filter(Boolean).length
        : 1,
    );
    const currentIndex = activeIndex < 0 ? 0 : activeIndex;
    const delta = key === "ArrowLeft"
      ? -1
      : key === "ArrowRight"
        ? 1
        : key === "ArrowUp"
          ? -columns
          : columns;
    const nextIndex = (currentIndex + delta + filteredGifts.length) % filteredGifts.length;
    setActiveGiftId(filteredGifts[nextIndex].giftId);
    return nextIndex;
  };

  const handleSearchKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(event.key)) {
      event.preventDefault();
      event.stopPropagation();
      moveActive(event.key);
      return;
    }
    if (event.key === "Home" && filteredGifts.length > 0) {
      event.preventDefault();
      setActiveGiftId(filteredGifts[0].giftId);
      return;
    }
    if (event.key === "End" && filteredGifts.length > 0) {
      event.preventDefault();
      setActiveGiftId(filteredGifts[filteredGifts.length - 1].giftId);
      return;
    }
    if (event.key === "Enter") {
      event.preventDefault();
      event.stopPropagation();
      const gift = filteredGifts[activeIndex] ?? filteredGifts[0];
      if (gift) onSelect(gift);
    }
  };

  const handleGiftKeyDown = (
    event: KeyboardEvent<HTMLButtonElement>,
  ) => {
    if (!["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(event.key)) return;
    event.preventDefault();
    const nextIndex = moveActive(event.key);
    window.requestAnimationFrame(() => {
      gridRef.current
        ?.querySelector<HTMLButtonElement>(`[data-gift-index="${nextIndex}"]`)
        ?.focus();
    });
  };

  return (
    <fieldset className="stream-gift-picker stream-rule-form__wide">
      <legend className="stream-gift-picker__heading">
        <strong>{t("interactions.rules.editor.selectGift")}</strong>
        <span>
          {t("interactions.rules.editor.giftResults")
            .replace("{count}", String(filteredGifts.length))}
        </span>
      </legend>
      <input
        type="search"
        autoFocus
        role="combobox"
        disabled={disabled}
        value={search}
        aria-label={t("interactions.rules.editor.searchGift")}
        aria-controls={gridId}
        aria-expanded="true"
        aria-activedescendant={activeIndex >= 0
          ? `${gridId}-option-${activeIndex}`
          : undefined}
        placeholder={t("interactions.rules.editor.searchGift")}
        onChange={(event) => setSearch(event.target.value)}
        onKeyDown={handleSearchKeyDown}
      />
      {filteredGifts.length === 0 ? (
        <div className="stream-gift-picker__empty">
          {t("interactions.rules.editor.noGiftResults")}
        </div>
      ) : (
        <div
          ref={gridRef}
          className="stream-gift-picker__grid"
          id={gridId}
          role="listbox"
          aria-label={t("interactions.rules.editor.selectGift")}
        >
          {filteredGifts.map((gift, index) => (
            <button
              id={`${gridId}-option-${index}`}
              type="button"
              role="option"
              disabled={disabled}
              aria-selected={gift.giftId === selectedId}
              tabIndex={index === activeIndex ? 0 : -1}
              data-active={index === activeIndex}
              data-selected={gift.giftId === selectedId}
              data-gift-index={index}
              key={gift.giftId}
              onFocus={() => setActiveGiftId(gift.giftId)}
              onPointerMove={() => setActiveGiftId(gift.giftId)}
              onKeyDown={handleGiftKeyDown}
              onClick={() => onSelect(gift)}
            >
              <img src={gift.imagePath} alt="" />
              <span>{gift.name}</span>
              <small>
                {gift.coinsPerUnit} {t("interactions.rules.coins")}
              </small>
              <Check className="stream-gift-picker__check" aria-hidden="true" />
            </button>
          ))}
        </div>
      )}
    </fieldset>
  );
}
