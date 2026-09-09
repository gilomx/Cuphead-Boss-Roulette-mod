import argparse
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "obj" / "interaction-pydeps"))

import UnityPy
from PIL import Image


DEFAULT_SPRITE = "train_bone_spiral_vertical_0001"
DEFAULT_OUTPUT = (
    ROOT / "assets" / "creator-tools" / "interactions"
    / "train-bone-ring.png"
)
COMMON_BUNDLES = (
    Path(
        r"C:\Program Files (x86)\Steam\steamapps\common\Cuphead\Cuphead_Data"
        r"\StreamingAssets\AssetBundles\atlas_trainlevel"
    ),
    Path(
        r"E:\SteamLibrary\steamapps\common\Cuphead\Cuphead_Data"
        r"\StreamingAssets\AssetBundles\atlas_trainlevel"
    ),
)


def render_full_frame(sprite):
    image = sprite.image.convert("RGBA")
    width = int(round(sprite.m_Rect.width))
    height = int(round(sprite.m_Rect.height))
    offset_x = int(round(sprite.m_RD.textureRectOffset.x))
    offset_y = int(round(sprite.m_RD.textureRectOffset.y))
    top = height - offset_y - image.height
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    canvas.paste(image, (offset_x, top), image)
    return canvas


def default_bundle():
    for candidate in COMMON_BUNDLES:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError(
        "Could not find atlas_trainlevel; pass its location with --bundle."
    )


def parse_args():
    parser = argparse.ArgumentParser(
        description="Extract the native Phantom Express bone ring preview."
    )
    parser.add_argument("--bundle", type=Path, help="Path to atlas_trainlevel.")
    parser.add_argument("--sprite", default=DEFAULT_SPRITE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    return parser.parse_args()


def main():
    args = parse_args()
    bundle = args.bundle or default_bundle()
    environment = UnityPy.load(str(bundle))

    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        sprite = obj.read()
        if sprite.m_Name != args.sprite:
            continue

        preview = render_full_frame(sprite)
        alpha = preview.getchannel("A")
        if alpha.getbbox() is None:
            raise RuntimeError(f"Sprite {args.sprite} has no visible pixels.")
        if alpha.getextrema()[0] != 0:
            raise RuntimeError(f"Sprite {args.sprite} has no transparent padding.")

        # The native canvas reserves extra space for the horizontal animation.
        # Trim that padding so the catalogue centers the visible vertical ring.
        preview = preview.crop(alpha.getbbox())

        args.output.parent.mkdir(parents=True, exist_ok=True)
        preview.save(args.output)
        print(
            f"{sprite.m_Name} -> {args.output} "
            f"({preview.width}x{preview.height}, alpha bbox={alpha.getbbox()})"
        )
        return

    raise RuntimeError(f"Missing native sprite: {args.sprite}")


if __name__ == "__main__":
    main()
