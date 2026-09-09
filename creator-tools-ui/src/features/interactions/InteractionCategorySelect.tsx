import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionGroups, type InteractionCategoryFilter } from "./interactionCatalog";

interface InteractionCategorySelectProps {
  value: InteractionCategoryFilter;
  onChange: (category: InteractionCategoryFilter) => void;
}

export function InteractionCategorySelect({ value, onChange }: InteractionCategorySelectProps) {
  const { t } = useLocalization();
  return (
    <label className="interaction-category-filter">
      <span>{t("interactions.groups.filter")}</span>
      <select
        value={value}
        onChange={(event) => onChange(event.target.value as InteractionCategoryFilter)}
      >
        <option value="all">{t("interactions.groups.all")}</option>
        {interactionGroups.map((category) => (
          <option value={category} key={category}>
            {t(`interactions.groups.${category}`)}
          </option>
        ))}
      </select>
    </label>
  );
}
