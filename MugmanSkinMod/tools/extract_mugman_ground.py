"""Extract and organize Mugman's ground-gameplay sprites for Photoshop.

The script reads Cuphead's ``atlas_player`` AssetBundle, selects the 787
Mugman-specific Sprite objects, and produces:

* original: tightly cropped PNGs as decoded from the atlas;
* aligned: 512x512 (or native logical size) PNGs positioned by Unity metadata;
* contact-sheets: one numbered preview sheet per animation sequence;
* manifest.json / manifest.csv: names, categories, pivots and placement data;
* summary.json: category and sequence counts.

Generated files are intentionally ignored by Git because they originate from
the user's locally installed copy of Cuphead.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
from collections import Counter
from pathlib import Path
import re
from typing import Any, Iterable

import UnityPy
from PIL import Image, ImageDraw, ImageFont


DEFAULT_GAME_DIR = Path(
    r"C:\Program Files (x86)\Steam\steamapps\common\Cuphead"
)
DEFAULT_OUTPUT = Path(__file__).resolve().parents[1] / "generated"
FRAME_SUFFIX = re.compile(r"_[0-9]+(?:[a-z]|_[a-z]+)*$", re.IGNORECASE)
NATURAL_PART = re.compile(r"(\d+)")

CATEGORY_ORDER = (
    "01_intro",
    "02_movimiento",
    "03_disparo",
    "04_ataques_ex",
    "05_parry",
    "06_dano",
    "07_muerte_reanimacion",
    "08_supers_transformaciones",
    "09_efectos_referencia",
    "99_sin_clasificar",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--game-dir",
        type=Path,
        default=Path(os.environ.get("CUPHEAD_DIR", DEFAULT_GAME_DIR)),
        help="Cuphead installation directory.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=DEFAULT_OUTPUT,
        help="Generated workspace directory.",
    )
    parser.add_argument(
        "--no-contact-sheets",
        action="store_true",
        help="Skip the per-sequence preview sheets.",
    )
    return parser.parse_args()


def natural_key(value: str) -> list[object]:
    return [
        int(part) if part.isdigit() else part.lower()
        for part in NATURAL_PART.split(value)
    ]


def is_mugman_sprite(name: str) -> bool:
    value = name.lower()
    return (
        value.startswith("mugman_")
        or value.startswith("player_mm_")
        or value.startswith("mm_")
        or ("player_mm_" in value and not value.startswith("player_mm_"))
    )


def sequence_name(name: str) -> str:
    return FRAME_SUFFIX.sub("", name).lower()


def category_for(sequence: str) -> str:
    effects = (
        sequence.startswith("heart_player_mm_"),
        sequence.startswith("shadow_player_mm_"),
        sequence == "mm_superiii_shadow",
        sequence.startswith("mugman_jump_dust_"),
        sequence == "mugman_jump_shadow",
        sequence == "mugman_super_intro_fx",
    )
    if any(effects):
        return "09_efectos_referencia"
    if sequence.startswith("mugman_intro_"):
        return "01_intro"
    if sequence.startswith("mugman_ex_"):
        return "04_ataques_ex"
    if "parry" in sequence:
        return "05_parry"
    if "_hit" in sequence or "scared" in sequence:
        return "06_dano"
    if (
        "super" in sequence
        or sequence == "player_mm_powerup"
    ):
        return "08_supers_transformaciones"
    if (
        "death" in sequence
        or "ghost" in sequence
        or "revive" in sequence
    ):
        return "07_muerte_reanimacion"
    if "aim" in sequence or "shoot" in sequence:
        return "03_disparo"
    if sequence.startswith(
        (
            "mugman_idle",
            "mugman_run",
            "mugman_jump",
            "mugman_dash",
            "mugman_duck",
        )
    ):
        return "02_movimiento"
    return "99_sin_clasificar"


def vector_value(vector: Any, field: str) -> float:
    return float(getattr(vector, field))


def aligned_image(sprite: Any, original: Image.Image) -> tuple[Image.Image, dict[str, int]]:
    logical_width = int(round(float(sprite.m_Rect.width)))
    logical_height = int(round(float(sprite.m_Rect.height)))
    center_x = logical_width / 2.0 + vector_value(sprite.m_Offset, "x")
    center_y = logical_height / 2.0 + vector_value(sprite.m_Offset, "y")
    left = int(round(center_x - original.width / 2.0))
    bottom = int(round(center_y - original.height / 2.0))
    top = logical_height - bottom - original.height

    canvas = Image.new("RGBA", (logical_width, logical_height), (0, 0, 0, 0))
    canvas.alpha_composite(original, (left, top))
    return canvas, {
        "left": left,
        "top": top,
        "bottom": bottom,
        "width": original.width,
        "height": original.height,
        "logical_width": logical_width,
        "logical_height": logical_height,
    }


def create_contact_sheet(
    entries: list[dict[str, Any]],
    output: Path,
    cell_size: int = 256,
    columns: int = 4,
) -> None:
    rows = int(math.ceil(len(entries) / columns))
    label_height = 32
    sheet = Image.new(
        "RGBA",
        (columns * cell_size, rows * (cell_size + label_height)),
        (28, 27, 25, 255),
    )
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, entry in enumerate(entries):
        image = Image.open(entry["aligned_absolute"]).convert("RGBA")
        image.thumbnail((cell_size - 16, cell_size - 16), Image.Resampling.LANCZOS)
        column = index % columns
        row = index // columns
        x = column * cell_size + (cell_size - image.width) // 2
        y = row * (cell_size + label_height) + (cell_size - image.height) // 2
        sheet.alpha_composite(image, (x, y))
        label = f"{index + 1:03d}  {entry['name']}"
        draw.text(
            (column * cell_size + 8, row * (cell_size + label_height) + cell_size + 8),
            label,
            fill=(245, 239, 218, 255),
            font=font,
        )

    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.convert("RGB").save(output, "JPEG", quality=92, optimize=True)


def write_manifest(entries: list[dict[str, Any]], output: Path) -> None:
    public_entries = []
    for entry in entries:
        public_entry = dict(entry)
        public_entry.pop("aligned_absolute", None)
        public_entries.append(public_entry)

    (output / "manifest.json").write_text(
        json.dumps(public_entries, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )

    columns = (
        "name",
        "category",
        "sequence",
        "frame_number",
        "edit_for_skin",
        "original_png",
        "aligned_png",
        "logical_width",
        "logical_height",
        "source_width",
        "source_height",
        "placement_left",
        "placement_top",
        "pivot_x",
        "pivot_y",
        "pixels_per_unit",
    )
    with (output / "manifest.csv").open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(public_entries)


def write_summary(entries: Iterable[dict[str, Any]], output: Path) -> None:
    entries = list(entries)
    by_category = Counter(entry["category"] for entry in entries)
    by_sequence = Counter(
        f"{entry['category']}/{entry['sequence']}" for entry in entries
    )
    summary = {
        "total_mugman_sprites": len(entries),
        "frames_to_edit": sum(entry["edit_for_skin"] for entry in entries),
        "reference_effects": sum(not entry["edit_for_skin"] for entry in entries),
        "by_category": {
            key: by_category[key] for key in CATEGORY_ORDER if by_category[key]
        },
        "by_sequence": dict(sorted(by_sequence.items(), key=lambda item: natural_key(item[0]))),
    }
    (output / "summary.json").write_text(
        json.dumps(summary, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )


def main() -> None:
    args = parse_args()
    bundle = (
        args.game_dir
        / "Cuphead_Data"
        / "StreamingAssets"
        / "AssetBundles"
        / "atlas_player"
    )
    if not bundle.is_file():
        raise FileNotFoundError(f"Cuphead atlas not found: {bundle}")

    output = args.output.resolve()
    original_root = output / "original"
    aligned_root = output / "aligned"
    contact_root = output / "contact-sheets"
    output.mkdir(parents=True, exist_ok=True)

    environment = UnityPy.load(str(bundle))
    sprites = []
    for object_reader in environment.objects:
        if object_reader.type.name != "Sprite":
            continue
        sprite = object_reader.read()
        if is_mugman_sprite(sprite.m_Name):
            sprites.append(sprite)
    sprites.sort(key=lambda sprite: natural_key(sprite.m_Name))

    entries: list[dict[str, Any]] = []
    for sprite in sprites:
        name = sprite.m_Name
        sequence = sequence_name(name)
        category = category_for(sequence)
        original = sprite.image.convert("RGBA")
        aligned, placement = aligned_image(sprite, original)

        original_path = original_root / category / sequence / f"{name}.png"
        aligned_path = aligned_root / category / sequence / f"{name}.png"
        original_path.parent.mkdir(parents=True, exist_ok=True)
        aligned_path.parent.mkdir(parents=True, exist_ok=True)
        original.save(original_path, "PNG", optimize=True)
        aligned.save(aligned_path, "PNG", optimize=True)

        match = re.search(
            r"(\d+)(?:[a-z]|_[a-z]+)*$",
            name,
            re.IGNORECASE,
        )
        entry = {
            "name": name,
            "category": category,
            "sequence": sequence,
            "frame_number": int(match.group(1)) if match else 0,
            "edit_for_skin": category != "09_efectos_referencia",
            "original_png": original_path.relative_to(output).as_posix(),
            "aligned_png": aligned_path.relative_to(output).as_posix(),
            "logical_width": placement["logical_width"],
            "logical_height": placement["logical_height"],
            "source_width": placement["width"],
            "source_height": placement["height"],
            "placement_left": placement["left"],
            "placement_top": placement["top"],
            "pivot_x": vector_value(sprite.m_Pivot, "x"),
            "pivot_y": vector_value(sprite.m_Pivot, "y"),
            "pixels_per_unit": float(sprite.m_PixelsToUnits),
            "aligned_absolute": str(aligned_path),
        }
        entries.append(entry)

    entries.sort(
        key=lambda entry: (
            CATEGORY_ORDER.index(entry["category"]),
            natural_key(entry["sequence"]),
            natural_key(entry["name"]),
        )
    )
    write_manifest(entries, output)
    write_summary(entries, output)

    if not args.no_contact_sheets:
        grouped: dict[tuple[str, str], list[dict[str, Any]]] = {}
        for entry in entries:
            grouped.setdefault((entry["category"], entry["sequence"]), []).append(entry)
        for (category, sequence), sequence_entries in grouped.items():
            create_contact_sheet(
                sequence_entries,
                contact_root / category / f"{sequence}.jpg",
            )

    category_counts = Counter(entry["category"] for entry in entries)
    print(f"Extracted {len(entries)} Mugman sprites from {bundle.name}")
    for category in CATEGORY_ORDER:
        if category_counts[category]:
            print(f"  {category}: {category_counts[category]}")
    print(f"Photoshop-ready workspace: {output}")


if __name__ == "__main__":
    main()
