import { SearchableSelectField } from "../../components/SearchableSelectField";
import { useLocalization } from "../../i18n/LocalizationContext";
import type { DisplayOption } from "../../model";

interface RouletteSelectFieldProps<T extends DisplayOption> {
  id: string;
  label: string;
  value: number;
  options: readonly T[];
  getLabel: (option: T) => string;
  onChange: (value: number) => void;
}

export function RouletteSelectField<T extends DisplayOption>({
  id,
  label,
  value,
  options,
  getLabel,
  onChange,
}: RouletteSelectFieldProps<T>) {
  const { t } = useLocalization();

  return (
    <SearchableSelectField
      id={id}
      label={label}
      options={options}
      selectedKey={value}
      placeholder={t("roulette.force.selectPlaceholder")}
      noResults={t("roulette.force.noResults")}
      getKey={(option) => option.id}
      getLabel={getLabel}
      getImage={(option) => option.image}
      getSearchTerms={(option) => [option.name, option.key, String(option.id)]}
      onSelect={(option) => onChange(option.id)}
    />
  );
}
