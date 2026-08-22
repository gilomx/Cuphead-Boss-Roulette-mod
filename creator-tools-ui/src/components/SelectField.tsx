import { useEffect, useRef, useState } from "react";
import type { DisplayOption } from "../model";

interface SelectFieldProps<T extends DisplayOption> {
  id: string;
  label: string;
  value: number;
  options: T[];
  getLabel: (option: T) => string;
  onChange: (value: number) => void;
}

export function SelectField<T extends DisplayOption>({
  id,
  label,
  value,
  options,
  getLabel,
  onChange,
}: SelectFieldProps<T>) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const selected = options.find((option) => option.id === value) ?? options[0];

  useEffect(() => {
    if (!open) return;
    const closeOutside = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("pointerdown", closeOutside);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("pointerdown", closeOutside);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [open]);

  return (
    <div className="field" ref={rootRef}>
      <label className="field__label" id={`${id}-label`}>
        {label}
      </label>
      <button
        id={id}
        className="select-control"
        type="button"
        aria-labelledby={`${id}-label ${id}-value`}
        aria-haspopup="listbox"
        aria-expanded={open}
        disabled={!selected}
        onClick={() => setOpen((current) => !current)}
        onKeyDown={(event) => {
          if (event.key === "ArrowDown" || event.key === "ArrowUp") {
            event.preventDefault();
            setOpen(true);
          }
        }}
      >
        {selected ? (
          <>
            <img className="select-control__icon" src={selected.image} alt="" />
            <span className="select-control__value" id={`${id}-value`}>
              {getLabel(selected)}
            </span>
          </>
        ) : (
          <span className="select-control__value">—</span>
        )}
        <span className="select-control__chevron" aria-hidden="true" />
      </button>
      {open && (
        <div className="select-menu" role="listbox" aria-labelledby={`${id}-label`}>
          {options.map((option) => (
            <button
              className="select-option"
              type="button"
              role="option"
              aria-selected={option.id === value}
              key={option.id}
              onClick={() => {
                onChange(option.id);
                setOpen(false);
              }}
            >
              <img className="select-option__icon" src={option.image} alt="" />
              <span>{getLabel(option)}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
