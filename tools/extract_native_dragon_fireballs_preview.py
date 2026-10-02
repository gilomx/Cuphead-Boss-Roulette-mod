"""Compose the approved post-shot dragon head and nearby native fireball."""
import argparse
from pathlib import Path

from native_interaction_preview import ROOT, Image, clip_outline, cropped_sprites, default_bundle, save_preview

DEFAULT_SPRITE = 'dragon_meteor_forward_0009'
DEFAULT_METEOR = 'meteor_0001'
DEFAULT_OUTPUT = ROOT / 'assets' / 'creator-tools' / 'interactions' / 'dragon-fireballs-v2.png'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path)
    parser.add_argument('--sprite', default=DEFAULT_SPRITE)
    parser.add_argument('--meteor', default=DEFAULT_METEOR)
    parser.add_argument('--output', type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    sprites = cropped_sprites(args.bundle or default_bundle('atlas_dragonlevel_nobg'),
                              [args.sprite, args.meteor])
    # Close the neck with a rounded cap just after the first dorsal triangle.
    head = clip_outline(sprites[args.sprite], (-1, -1), [
        ('L', (301, -1)), ('L', (301, 145)),
        ('C', (350, 166), (345, 224), (286, 240)),
        ('L', (195, 282)), ('L', (230, 455)), ('L', (-1, 455)),
    ])
    canvas = Image.new('RGBA', (456, 458))
    canvas.alpha_composite(head, (106, 4))
    meteor = sprites[args.meteor].resize((195, 151), Image.Resampling.LANCZOS)
    canvas.alpha_composite(meteor, (0, 214))
    save_preview(canvas, args.output)


if __name__ == '__main__':
    main()
