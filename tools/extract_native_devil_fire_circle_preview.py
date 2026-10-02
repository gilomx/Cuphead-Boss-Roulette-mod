"""Compose the five native Devil flames in a compact, legible catalog icon."""
import argparse
from pathlib import Path

from native_interaction_preview import ROOT, Image, cropped_sprites, default_bundle, save_preview

DEFAULT_OUTPUT = ROOT / 'assets' / 'creator-tools' / 'interactions' / 'devil-fire-circle-v2.png'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path)
    parser.add_argument('--output', type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    blue = 'devil_ph1_fire_dance_0001'
    pink = 'devil_ph1_fire_dance_pink_0001'
    sprites = cropped_sprites(args.bundle or default_bundle('atlas_devillevelp1'), [blue, pink])
    canvas = Image.new('RGBA', (296, 352))
    for x, y in [(110, 6), (6, 113), (217, 113), (110, 221)]:
        canvas.alpha_composite(sprites[blue], (x, y))
    canvas.alpha_composite(sprites[pink], (109, 113))
    save_preview(canvas, args.output)


if __name__ == '__main__':
    main()
