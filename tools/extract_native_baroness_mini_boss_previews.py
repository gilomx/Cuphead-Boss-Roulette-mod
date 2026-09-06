"""Extract the five native Baroness mini-boss portraits from an installed game.

Requires UnityPy and Pillow. Existing interaction-pydeps installations are reused;
otherwise install those packages in the Python environment running this script.
"""

import argparse
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEPENDENCY_FOLDER = (
    "interaction-pydeps-py312"
    if sys.version_info[:2] == (3, 12)
    else "interaction-pydeps"
)
sys.path.insert(0, str(ROOT / "obj" / DEPENDENCY_FOLDER))

import UnityPy
from PIL import Image


DEFAULT_OUTPUT = ROOT / "assets" / "creator-tools" / "interactions"
COMMON_BUNDLES = (
    Path(
        r"C:\Program Files (x86)\Steam\steamapps\common\Cuphead\Cuphead_Data"
        r"\StreamingAssets\AssetBundles\atlas_baronesslevel"
    ),
    Path(
        r"E:\SteamLibrary\steamapps\common\Cuphead\Cuphead_Data"
        r"\StreamingAssets\AssetBundles\atlas_baronesslevel"
    ),
)

# Layers use their full native animation canvas, in back-to-front order. The
# gumball machine is assembled from the same three layers used in the game.
PREVIEWS = {
    "baroness-cupcake.png": ("cupcake_slam_0001",),
    "baroness-gumball.png": (
        "gumball_legs_0001",
        "gumball_body_run_0001",
        "gumball_lid_closed_0001",
    ),
    "baroness-waffle.png": ("waffle_flap_0001",),
    "baroness-candy-corn.png": ("candycorn_turn_b_0001",),
    "baroness-jawbreaker.png": ("jawbreak_0001",),
}


def render_full_frame(sprite):
    image = sprite.image.convert("RGBA")
    width = int(round(sprite.m_Rect.width))
    height = int(round(sprite.m_Rect.height))
    offset_x = int(round(sprite.m_RD.textureRectOffset.x))
    offset_y = int(round(sprite.m_RD.textureRectOffset.y))
    top = height - offset_y - image.height
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    canvas.alpha_composite(image, (offset_x, top))
    return canvas


def compose_preview(sprites, names):
    layers = [render_full_frame(sprites[name]) for name in names]
    canvas = Image.new("RGBA", layers[0].size, (0, 0, 0, 0))
    for layer in layers:
        if layer.size != canvas.size:
            raise RuntimeError(f"Native layers have mismatched canvases: {names}")
        canvas.alpha_composite(layer)
    bounds = canvas.getchannel("A").getbbox()
    if bounds is None:
        raise RuntimeError(f"Native sprites have no visible pixels: {names}")
    cropped = canvas.crop(bounds)
    margin = 24
    preview = Image.new(
        "RGBA", (cropped.width + margin * 2, cropped.height + margin * 2),
        (0, 0, 0, 0),
    )
    preview.alpha_composite(cropped, (margin, margin))
    return preview


def default_bundle():
    for candidate in COMMON_BUNDLES:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError(
        "Could not find atlas_baronesslevel; pass its location with --bundle."
    )


def parse_args():
    parser = argparse.ArgumentParser(
        description="Extract native previews for all five Baroness mini-bosses."
    )
    parser.add_argument("--bundle", type=Path)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    return parser.parse_args()


def main():
    args = parse_args()
    environment = UnityPy.load(str(args.bundle or default_bundle()))
    needed = {name for names in PREVIEWS.values() for name in names}
    sprites = {}
    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        if sprite.m_Name in needed:
            sprites[sprite.m_Name] = sprite
    missing = needed - sprites.keys()
    if missing:
        raise RuntimeError("Missing native sprites: " + ", ".join(sorted(missing)))
    args.output_dir.mkdir(parents=True, exist_ok=True)
    for filename, names in PREVIEWS.items():
        preview = compose_preview(sprites, names)
        destination = args.output_dir / filename
        preview.save(destination)
        print(
            f"{' + '.join(names)} -> {destination} "
            f"({preview.width}x{preview.height}, "
            f"alpha bbox={preview.getchannel('A').getbbox()})"
        )


if __name__ == "__main__":
    main()
