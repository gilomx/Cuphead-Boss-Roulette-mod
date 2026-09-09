"""Extract Beppi's regular and pink balloon dog previews from the native atlas."""
import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "obj" / "interaction-pydeps"))
import UnityPy
from extract_native_baroness_mini_boss_previews import compose_preview

PREVIEWS = {
    "beppi-balloon-dog.png": ("balloon_dog_chomp_0001",),
    "beppi-pink-balloon-dog.png": ("pink_balloon_dog_chomp_0001",),
}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path,
                        default=ROOT / "assets" / "creator-tools" / "interactions")
    args = parser.parse_args()
    needed = {name for names in PREVIEWS.values() for name in names}
    sprites = {}
    for obj in UnityPy.load(str(args.bundle)).objects:
        if obj.type.name == "Sprite":
            sprite = obj.read()
            if sprite.m_Name in needed:
                sprites[sprite.m_Name] = sprite
    if needed - sprites.keys():
        raise RuntimeError(f"Missing native sprites: {needed - sprites.keys()}")
    args.output_dir.mkdir(parents=True, exist_ok=True)
    for filename, names in PREVIEWS.items():
        preview = compose_preview(sprites, names)
        destination = args.output_dir / filename
        preview.save(destination)
        print(f"{names[0]} -> {destination} ({preview.width}x{preview.height})")

if __name__ == "__main__":
    main()
