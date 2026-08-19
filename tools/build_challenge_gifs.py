#!/usr/bin/env python3
"""Build finite challenge GIF previews from the exact in-game PNG frames."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


FRAME_DURATION_MS = 80  # EquipIconFramesPerSecond = 12.5f
CYCLES = 42
FRAME_COUNT_PER_CYCLE = 3

CHALLENGES = (
    ("01_no_dash.gif", "nodash"),
    ("02_no_mini_avion.gif", "nomini"),
    ("03_solo_mini_avion.gif", "mini"),
    ("04_no_disparo_bombas.gif", "nobombs"),
    ("05_no_disparo_peashooter.gif", "nopeashooter"),
    ("06_no_ex.gif", "noex"),
    ("07_blanco_y_negro.gif", "blacknwhite"),
)


def load_frames(asset_dir: Path, stem: str) -> list[Image.Image]:
    frames: list[Image.Image] = []
    for frame_number in range(1, FRAME_COUNT_PER_CYCLE + 1):
        path = asset_dir / f"{stem}_{frame_number:02d}.png"
        with Image.open(path) as source:
            frames.append(source.convert("RGBA").copy())

    sizes = {frame.size for frame in frames}
    if sizes != {(80, 80)}:
        raise ValueError(f"{stem}: expected three 80x80 frames, found {sizes}")
    return frames


def save_gif(output_path: Path, source_frames: list[Image.Image]) -> None:
    # Repeating complete three-frame cycles makes the finite animation stop on
    # frame 3. No GIF loop extension is written, so it plays once and holds.
    frames = [
        source_frames[index % FRAME_COUNT_PER_CYCLE].copy()
        for index in range(CYCLES * FRAME_COUNT_PER_CYCLE)
    ]
    frames[0].save(
        output_path,
        format="GIF",
        save_all=True,
        append_images=frames[1:],
        duration=FRAME_DURATION_MS,
        disposal=2,
        optimize=False,
    )
    for frame in frames:
        frame.close()


def verify_gif(path: Path) -> tuple[int, int, int | None]:
    with Image.open(path) as gif:
        durations = []
        for frame_index in range(gif.n_frames):
            gif.seek(frame_index)
            durations.append(int(gif.info.get("duration", 0)))
        if gif.n_frames != CYCLES * FRAME_COUNT_PER_CYCLE:
            raise ValueError(f"{path.name}: unexpected frame count {gif.n_frames}")
        if any(duration != FRAME_DURATION_MS for duration in durations):
            raise ValueError(f"{path.name}: a frame is not {FRAME_DURATION_MS} ms")
        if gif.n_frames % FRAME_COUNT_PER_CYCLE != 0:
            raise ValueError(f"{path.name}: animation does not end on frame 3")
        return gif.n_frames, sum(durations), gif.info.get("loop")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--assets", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    asset_dir = args.assets.resolve()
    output_dir = args.output.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    results = []
    for output_name, stem in CHALLENGES:
        source_frames = load_frames(asset_dir, stem)
        try:
            output_path = output_dir / output_name
            save_gif(output_path, source_frames)
            results.append((output_name, *verify_gif(output_path)))
        finally:
            for frame in source_frames:
                frame.close()

    readme = output_dir / "README.txt"
    readme.write_text(
        "La Pichi Ruleta - GIFs de retos\n"
        "\n"
        "Cadencia del juego: 12.5 FPS (80 ms por frame).\n"
        "Cada GIF: 3 frames x 42 ciclos = 126 frames y 10.08 segundos.\n"
        "Los GIFs se reproducen una vez y se detienen en el tercer frame.\n"
        "Resolucion: 80x80 px, con transparencia.\n",
        encoding="utf-8",
    )

    for name, frame_count, duration_ms, loop in results:
        print(f"{name}|{frame_count}|{duration_ms}|loop={loop}")


if __name__ == "__main__":
    main()
