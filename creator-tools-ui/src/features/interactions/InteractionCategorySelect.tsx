import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionCategories, type InteractionCategoryFilter } from "./interactionCatalog";

interface InteractionCategorySelectProps {
  value: InteractionCategoryFilter;
  onChange: (category: InteractionCategoryFilter) => void;
}

export function InteractionCategorySelect({ value, onChange }: InteractionCategorySelectProps) {
  const { t } = useLocalization();
  return (
    <label className="interaction-category-filter">
      <span>{t("interactions.catalog.filterLabel")}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value as InteractionCategoryFilter)}
      >
        <option value="all">{t("interactions.categories.all")}</option>
        {interactionCategories.map((category) => (
          <option value={category} key={category}>
            {t(`interactions.categories.${category}`)}
          </option>
        ))}
      </select>
    </label>
  );
}
