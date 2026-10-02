"""Extract the head-only Baroness frame selected as preview option 8."""
import argparse
from pathlib import Path

from native_interaction_preview import ROOT, Image, clip_outline, cropped_sprites, default_bundle, save_preview

DEFAULT_SPRITE = 'top_baroness_head_toss_0018'
DEFAULT_OUTPUT = ROOT / 'assets' / 'creator-tools' / 'interactions' / 'baroness-head-toss-v2.png'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path)
    parser.add_argument('--sprite', default=DEFAULT_SPRITE)
    parser.add_argument('--output', type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    sprites = cropped_sprites(args.bundle or default_bundle('atlas_baronesslevel'), [args.sprite])
    head = clip_outline(sprites[args.sprite], (80, 16), [
        ('L', (170, 16)), ('L', (170, 43)), ('L', (161, 64)), ('L', (182, 80)),
        ('Q', (189, 105), (165, 114)), ('Q', (151, 126), (126, 120)),
        ('L', (93, 113)), ('L', (80, 113)),
    ])
    # Match the approved SVG viewport; arms, body and cone remain outside it.
    canvas = Image.new('RGBA', (110, 112))
    canvas.alpha_composite(head.crop((80, 16, 190, 128)))
    save_preview(canvas, args.output)


if __name__ == '__main__':
    main()
