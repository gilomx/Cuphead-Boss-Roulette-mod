"""Extract Cuphead's native empty equipment sprite as a white PNG.

Requires UnityPy and Pillow. The source bundle path and output path can be
overridden with the first and second command-line arguments.
"""

from pathlib import Path
import sys

import UnityPy
from PIL import Image


DEFAULT_BUNDLE = Path(
    r"E:\SteamLibrary\steamapps\common\Cuphead\Cuphead_Data\StreamingAssets"
    r"\AssetBundles\atlas_equip_icons"
)
DEFAULT_OUTPUT = (
    Path(__file__).resolve().parents[1]
    / "assets"
    / "creator-tools"
    / "empty.png"
)
SPRITE_NAME = "equip_icon_empty_0001"


def main() -> None:
    bundle = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_BUNDLE
    output = Path(sys.argv[2]) if len(sys.argv) > 2 else DEFAULT_OUTPUT
    environment = UnityPy.load(str(bundle))

    for object_reader in environment.objects:
        if object_reader.type.name != "Sprite":
            continue
        sprite = object_reader.read()
        if sprite.m_Name != SPRITE_NAME:
            continue

        image = sprite.image.convert("RGBA")
        alpha = image.getchannel("A")
        white = Image.new("RGBA", image.size, (255, 255, 255, 0))
        white.putalpha(alpha)
        if alpha.getbbox() is None:
            raise RuntimeError("The native empty sprite has no visible pixels.")

        output.parent.mkdir(parents=True, exist_ok=True)
        white.save(output, "PNG", optimize=True)
        print(f"Extracted {SPRITE_NAME}: {image.width}x{image.height} -> {output}")
        return

    raise RuntimeError(f"Sprite not found: {SPRITE_NAME}")


if __name__ == "__main__":
    main()
