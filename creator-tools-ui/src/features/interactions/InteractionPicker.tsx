import { SearchableSelectField } from "../../components/SearchableSelectField";
import { useLocalization } from "../../i18n/LocalizationContext";
import { interactionItems } from "./interactionCatalog";

type InteractionItem = typeof interactionItems[number];

interface InteractionPickerProps {
  id: string;
  label: string;
  selectedKey: string | null;
  disabled?: boolean;
  onSelect: (item: InteractionItem) => void;
}

export function InteractionPicker({
  id,
  label,
  selectedKey,
  disabled = false,
  onSelect,
}: InteractionPickerProps) {
  const { t } = useLocalization();

  return (
    <SearchableSelectField
      id={id}
      label={label}
      options={interactionItems}
      selectedKey={selectedKey}
      placeholder={t("interactions.rules.editor.interactionPlaceholder")}
      noResults={t("interactions.rules.editor.noInteractionResults")}
      disabled={disabled}
      getKey={(item) => item.id}
      getLabel={(item) => t(item.titleKey)}
      getImage={(item) => item.image}
      getMeta={(item) => t(`interactions.groups.${item.group}`)}
      getSearchTerms={(item) => [
        item.id,
        t(item.typeKey),
        t(`interactions.categories.${item.category}`),
      ]}
      onSelect={onSelect}
    />
  );
}
