import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
} from "react";

type SearchableSelectKey = string | number;

interface SearchableSelectFieldProps<T> {
  id: string;
  label: string;
  options: T[];
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
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(-1);
  const selected = options.find((option) => getKey(option) === selectedKey);
  const filteredOptions = useMemo(() => {
    const normalizedQuery = normalizedSearchText(query.trim());
    if (!normalizedQuery) return options;
    return options.filter((option) => {
      const terms = [getLabel(option), ...(getSearchTerms?.(option) ?? [])];
      return terms.some((term) => normalizedSearchText(term).includes(normalizedQuery));
    });
  }, [getLabel, getSearchTerms, options, query]);

  useEffect(() => {
    if (!open) return;
    const closeOutside = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
        setQuery("");
      }
    };
    document.addEventListener("pointerdown", closeOutside);
    return () => document.removeEventListener("pointerdown", closeOutside);
  }, [open]);

  useEffect(() => {
    if (!disabled) return;
    setOpen(false);
    setQuery("");
  }, [disabled]);

  useEffect(() => {
    if (!open || filteredOptions.length === 0) {
      setActiveIndex(-1);
      return;
    }
    const selectedIndex = query
      ? -1
      : filteredOptions.findIndex((option) => getKey(option) === selectedKey);
    setActiveIndex(selectedIndex >= 0 ? selectedIndex : 0);
  }, [filteredOptions, getKey, open, query, selectedKey]);

  useEffect(() => {
    if (!open || activeIndex < 0) return;
    document.getElementById(`${listboxId}-option-${activeIndex}`)?.scrollIntoView({
      block: "nearest",
    });
  }, [activeIndex, listboxId, open]);

  const openList = () => {
    if (disabled) return;
    setQuery("");
    setOpen(true);
  };

  const choose = (option: T) => {
    onSelect(option);
    setOpen(false);
    setQuery("");
    window.requestAnimationFrame(() => inputRef.current?.select());
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (!open) {
        openList();
        return;
      }
      if (filteredOptions.length === 0) return;
      const direction = event.key === "ArrowDown" ? 1 : -1;
      setActiveIndex((current) => {
        if (current < 0) return direction > 0 ? 0 : filteredOptions.length - 1;
        return (current + direction + filteredOptions.length) % filteredOptions.length;
      });
      return;
    }
    if (event.key === "Home" && open && filteredOptions.length > 0) {
      event.preventDefault();
      setActiveIndex(0);
      return;
    }
    if (event.key === "End" && open && filteredOptions.length > 0) {
      event.preventDefault();
      setActiveIndex(filteredOptions.length - 1);
      return;
    }
    if (event.key === "Enter" && open && activeIndex >= 0) {
      event.preventDefault();
      const option = filteredOptions[activeIndex];
      if (option) choose(option);
      return;
    }
    if (event.key === "Escape" && open) {
      event.preventDefault();
      setOpen(false);
      setQuery("");
      return;
    }
    if (event.key === "Tab") {
      setOpen(false);
      setQuery("");
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
            setQuery(event.target.value);
            setOpen(true);
          }}
          onKeyDown={handleKeyDown}
        />
        <span className="searchable-select-control__chevron" aria-hidden="true" />
      </div>

      {open ? (
        <div className="searchable-select-menu" id={listboxId} role="listbox" aria-label={label}>
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
                key={optionKey}
                onPointerDown={(event) => {
                  if (event.pointerType === "mouse") event.preventDefault();
                }}
                onPointerMove={() => setActiveIndex(index)}
                onClick={() => choose(option)}
              >
                {image ? (
                  <img
                    src={image}
                    alt=""
                    onError={(event) => {
                      event.currentTarget.hidden = true;
                    }}
                  />
                ) : null}
                <span>
                  <strong>{getLabel(option)}</strong>
                  {meta ? <small>{meta}</small> : null}
                </span>
                {optionKey === selectedKey ? <i aria-hidden="true">&#10003;</i> : null}
              </button>
            );
          })}
        </div>
      ) : null}
    </div>
  );
}
