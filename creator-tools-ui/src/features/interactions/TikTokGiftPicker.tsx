import { useMemo, useState } from "react";
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
  const [search, setSearch] = useState("");
  const filteredGifts = useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    if (!query) return gifts;
    return gifts.filter((gift) =>
      gift.name.toLocaleLowerCase().includes(query) ||
      gift.giftId.toLocaleLowerCase().includes(query) ||
      String(gift.coinsPerUnit).includes(query) ||
      gift.aliases.some((alias) => alias.toLocaleLowerCase().includes(query)));
  }, [gifts, search]);

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
        disabled={disabled}
        value={search}
        aria-label={t("interactions.rules.editor.searchGift")}
        placeholder={t("interactions.rules.editor.searchGift")}
        onChange={(event) => setSearch(event.target.value)}
      />
      {filteredGifts.length === 0 ? (
        <div className="stream-gift-picker__empty">
          {t("interactions.rules.editor.noGiftResults")}
        </div>
      ) : (
        <div className="stream-gift-picker__grid">
          {filteredGifts.map((gift) => (
            <button
              type="button"
              disabled={disabled}
              aria-pressed={gift.giftId === selectedId}
              data-selected={gift.giftId === selectedId}
              key={gift.giftId}
              onClick={() => onSelect(gift)}
            >
              <img src={gift.imagePath} alt="" />
              <span>{gift.name}</span>
              <small>
                {gift.coinsPerUnit} {t("interactions.rules.coins")}
              </small>
              <i aria-hidden="true">&#10003;</i>
            </button>
          ))}
        </div>
      )}
    </fieldset>
  );
}
