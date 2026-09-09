"""Compose the five native Devil fire sprites at their original orbit radius."""
import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "obj" / "interaction-pydeps"))
import UnityPy
from PIL import Image

from extract_native_robot_homing_bomb_preview import render_full_frame


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=ROOT / "assets" /
                        "creator-tools" / "interactions" / "devil-fire-circle.png")
    args = parser.parse_args()
    names = {"devil_ph1_fire_dance_0001", "devil_ph1_fire_dance_pink_0001"}
    env = UnityPy.load(str(args.bundle))
    sprites = {obj.peek_name(): obj.read() for obj in env.objects
               if obj.type.name == "Sprite" and obj.peek_name() in names}
    if set(sprites) != names:
        raise RuntimeError("Missing native Devil fire sprites")
    canvas = Image.new("RGBA", (900, 900))
    for x, y, pink in [(0, 0, True), (300, 0, False), (0, 300, False),
                        (-300, 0, False), (0, -300, False)]:
        sprite = sprites["devil_ph1_fire_dance" + ("_pink" if pink else "") + "_0001"]
        frame = render_full_frame(sprite)
        left = round(450 + x - sprite.m_Pivot.x * frame.width)
        top = round(450 - y - (1 - sprite.m_Pivot.y) * frame.height)
        canvas.alpha_composite(frame, (left, top))
    canvas = canvas.crop(canvas.getchannel("A").getbbox())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(args.output)
    print(f"Native Devil fire circle -> {args.output} ({canvas.width}x{canvas.height})")


if __name__ == "__main__":
    main()
