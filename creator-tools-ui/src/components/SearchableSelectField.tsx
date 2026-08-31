import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
} from "react";
import { Check, ChevronDown } from "lucide-react";

type SearchableSelectKey = string | number;

interface SearchableSelectFieldProps<T> {
  id: string;
  label: string;
  options: readonly T[];
  selectedKey: SearchableSelectKey | null;
  placeholder: string;
  noResults: string;
  disabled?: boolean;
  getKey: (option: T) => SearchableSelectKey;
  getLabel: (option: T) => string;
  getImage?: (option: T) => string | undefined;
  getMeta?: (option: T) => string | undefined;
  getSearchTerms?: (option: T) => string[];
  onSelect: (option: T) => void;
}

function normalizedSearchText(value: string) {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase();
}

function optionMatchesSearch<T>(
  option: T,
  normalizedQuery: string,
  getLabel: (option: T) => string,
  getSearchTerms?: (option: T) => string[],
) {
  const terms = [getLabel(option), ...(getSearchTerms?.(option) ?? [])];
  return terms.some((term) => normalizedSearchText(term).includes(normalizedQuery));
}

export function SearchableSelectField<T>({
  id,
  label,
  options,
  selectedKey,
  placeholder,
  noResults,
  disabled = false,
  getKey,
  getLabel,
  getImage,
  getMeta,
  getSearchTerms,
  onSelect,
}: SearchableSelectFieldProps<T>) {
  const reactId = useId().replace(/:/g, "");
  const listboxId = `${id}-${reactId}-listbox`;
  const rootRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [activeKey, setActiveKey] = useState<SearchableSelectKey | null>(null);
  const selected = options.find((option) => getKey(option) === selectedKey);
  const filteredOptions = useMemo(() => {
    const normalizedQuery = normalizedSearchText(query.trim());
    if (!normalizedQuery) return options;
    return options.filter((option) =>
      optionMatchesSearch(option, normalizedQuery, getLabel, getSearchTerms));
  }, [getLabel, getSearchTerms, options, query]);
  const filteredOptionKeys = filteredOptions.map(getKey);
  const filteredOptionKeysSignature = JSON.stringify(filteredOptionKeys);
  const activeIndex = activeKey === null
    ? -1
    : filteredOptionKeys.findIndex((optionKey) => optionKey === activeKey);

  useEffect(() => {
    if (!open) return;
    const closeOutside = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
        setQuery("");
        setActiveKey(null);
      }
    };
    document.addEventListener("pointerdown", closeOutside);
    return () => document.removeEventListener("pointerdown", closeOutside);
  }, [open]);

  useEffect(() => {
    if (!disabled) return;
    setOpen(false);
    setQuery("");
    setActiveKey(null);
  }, [disabled]);

  useEffect(() => {
    if (!open) return;
    setActiveKey((current) => {
      if (current !== null && filteredOptionKeys.includes(current)) return current;
      if (!query && selectedKey !== null && filteredOptionKeys.includes(selectedKey)) {
        return selectedKey;
      }
      return filteredOptionKeys[0] ?? null;
    });
  }, [filteredOptionKeysSignature, open, query, selectedKey]);

  useEffect(() => {
    if (!open || activeIndex < 0) return;
    const menu = menuRef.current;
    const option = menu?.querySelector<HTMLElement>(
      `[data-option-index="${activeIndex}"]`,
    );
    if (!menu || !option) return;

    const optionTop = option.offsetTop;
    const optionBottom = optionTop + option.offsetHeight;
    const viewportTop = menu.scrollTop;
    const viewportBottom = viewportTop + menu.clientHeight;
    if (optionTop < viewportTop) {
      menu.scrollTop = optionTop;
    } else if (optionBottom > viewportBottom) {
      menu.scrollTop = optionBottom - menu.clientHeight;
    }
  }, [activeIndex, open]);

  const openList = () => {
    if (disabled) return;
    setQuery("");
    const selectedIsAvailable = selectedKey !== null &&
      options.some((option) => getKey(option) === selectedKey);
    setActiveKey(selectedIsAvailable
      ? selectedKey
      : options[0] ? getKey(options[0]) : null);
    setOpen(true);
  };

  const choose = (option: T) => {
    onSelect(option);
    setOpen(false);
    setQuery("");
    setActiveKey(null);
    window.requestAnimationFrame(() => inputRef.current?.select());
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      event.stopPropagation();
      const direction = event.key === "ArrowDown" ? 1 : -1;
      if (!open) {
        const selectedIndex = selectedKey === null
          ? -1
          : options.findIndex((option) => getKey(option) === selectedKey);
        const nextIndex = selectedIndex < 0
          ? direction > 0 ? 0 : options.length - 1
          : (selectedIndex + direction + options.length) % options.length;
        setQuery("");
        setActiveKey(options[nextIndex] ? getKey(options[nextIndex]) : null);
        setOpen(true);
        return;
      }
      if (filteredOptions.length === 0) return;
      const nextIndex = activeIndex < 0
        ? direction > 0 ? 0 : filteredOptions.length - 1
        : (activeIndex + direction + filteredOptions.length) % filteredOptions.length;
      setActiveKey(getKey(filteredOptions[nextIndex]));
      return;
    }
    if (event.key === "Home" && open && filteredOptions.length > 0) {
      event.preventDefault();
      event.stopPropagation();
      setActiveKey(getKey(filteredOptions[0]));
      return;
    }
    if (event.key === "End" && open && filteredOptions.length > 0) {
      event.preventDefault();
      event.stopPropagation();
      setActiveKey(getKey(filteredOptions[filteredOptions.length - 1]));
      return;
    }
    if (event.key === "Enter") {
      event.preventDefault();
      event.stopPropagation();
      if (!open) {
        openList();
      } else {
        const option = filteredOptions[activeIndex] ?? filteredOptions[0];
        if (option) choose(option);
      }
      return;
    }
    if (event.key === "Escape" && open) {
      event.preventDefault();
      event.stopPropagation();
      setOpen(false);
      setQuery("");
      setActiveKey(null);
      return;
    }
    if (event.key === "Tab") {
      setOpen(false);
      setQuery("");
      setActiveKey(null);
    }
  };

  const selectedImage = selected ? getImage?.(selected) : undefined;
  const activeDescendant = open && activeIndex >= 0
    ? `${listboxId}-option-${activeIndex}`
    : undefined;

  return (
    <div className="searchable-select-field" ref={rootRef}>
      <label htmlFor={id}>{label}</label>
      <div className="searchable-select-control" data-open={open} data-disabled={disabled}>
        {selectedImage ? (
          <img
            src={selectedImage}
            alt=""
            onLoad={(event) => {
              event.currentTarget.hidden = false;
            }}
            onError={(event) => {
              event.currentTarget.hidden = true;
            }}
          />
        ) : null}
        <input
          ref={inputRef}
          id={id}
          type="search"
          role="combobox"
          autoComplete="off"
          spellCheck={false}
          disabled={disabled}
          aria-autocomplete="list"
          aria-expanded={open}
          aria-controls={listboxId}
          aria-activedescendant={activeDescendant}
          value={open ? query : selected ? getLabel(selected) : ""}
          placeholder={placeholder}
          onFocus={openList}
          onClick={() => {
            if (!open) openList();
          }}
          onChange={(event) => {
            const nextQuery = event.target.value;
            const normalizedQuery = normalizedSearchText(nextQuery.trim());
            const nextFilteredOptions = normalizedQuery
              ? options.filter((option) =>
                optionMatchesSearch(option, normalizedQuery, getLabel, getSearchTerms))
              : options;
            setQuery(nextQuery);
            setActiveKey(nextFilteredOptions[0] ? getKey(nextFilteredOptions[0]) : null);
            setOpen(true);
          }}
          onKeyDown={handleKeyDown}
        />
        <ChevronDown className="searchable-select-control__chevron" aria-hidden="true" />
      </div>

      {open ? (
        <div
          ref={menuRef}
          className="searchable-select-menu"
          id={listboxId}
          role="listbox"
          aria-label={label}
        >
          {filteredOptions.length === 0 ? (
            <p className="searchable-select-menu__empty" role="status">{noResults}</p>
          ) : filteredOptions.map((option, index) => {
            const optionKey = getKey(option);
            const image = getImage?.(option);
            const meta = getMeta?.(option);
            return (
              <button
                id={`${listboxId}-option-${index}`}
                className="searchable-select-option"
                type="button"
                role="option"
                tabIndex={-1}
                aria-selected={optionKey === selectedKey}
                data-active={index === activeIndex}
                data-option-index={index}
                key={optionKey}
                onPointerDown={(event) => {
                  if (event.pointerType === "mouse") event.preventDefault();
                }}
                onPointerMove={() => setActiveKey(optionKey)}
                onClick={() => choose(option)}
              >
                {image ? (
                  <img
                    src={image}
                    alt=""
                    onLoad={(event) => {
                      event.currentTarget.hidden = false;
                    }}
                    onError={(event) => {
                      event.currentTarget.hidden = true;
                    }}
                  />
                ) : null}
                <span>
                  <strong>{getLabel(option)}</strong>
                  {meta ? <small>{meta}</small> : null}
                </span>
                {optionKey === selectedKey ? (
                  <Check className="searchable-select-option__check" aria-hidden="true" />
                ) : null}
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
