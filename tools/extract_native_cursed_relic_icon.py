"""Export the original grade-0 relic icon for the in-game fallback.

Requires UnityPy and Pillow. Read atlas_equip_icons_dlc from a local Cuphead
installation with --bundle, or set CUPHEAD_DIR. Game files are read-only.
The Divine Relic artwork already supplied with the mod is left intact.
The browser overlay and control panel use separate user-supplied artwork,
which is not overwritten.
"""

import argparse
import os
from pathlib import Path

import UnityPy


ROOT = Path(__file__).resolve().parents[1]
SPRITE_NAME = "equip_icon_charm_curse_1_0001"
OUTPUTS = (
    ROOT / "assets" / "charms" / "reliquiamaldita.png",
)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, help="Local atlas_equip_icons_dlc bundle.")
    args = parser.parse_args()
    bundle = args.bundle
    if bundle is None:
        cuphead = os.environ.get("CUPHEAD_DIR")
        if not cuphead:
            parser.error("Pass --bundle or set CUPHEAD_DIR to the local game references.")
        bundle = Path(cuphead) / "Cuphead_Data/StreamingAssets/AssetBundles/atlas_equip_icons_dlc"

    environment = UnityPy.load(str(bundle))
    matches = []
    for obj in environment.objects:
        if obj.type.name == "Sprite":
            sprite = obj.read()
            if sprite.m_Name == SPRITE_NAME:
                matches.append(sprite)
    if len(matches) != 1:
        raise RuntimeError(f"Expected one {SPRITE_NAME}; found {len(matches)}.")

    image = matches[0].image
    if image.mode != "RGBA" or image.getchannel("A").getbbox() is None:
        raise RuntimeError("The native sprite must contain visible RGBA pixels.")
    # Export the native sprite directly; no recoloring, redrawing or resizing.
    for output in OUTPUTS:
        output.parent.mkdir(parents=True, exist_ok=True)
        image.save(output, "PNG", optimize=True)
        print(f"{SPRITE_NAME}: {image.width}x{image.height} -> {output.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
