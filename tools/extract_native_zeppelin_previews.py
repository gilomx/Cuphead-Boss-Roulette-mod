import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tmp" / "unitypy"))

import UnityPy
from PIL import Image


BUNDLE = Path(
    r"E:\SteamLibrary\steamapps\common\Cuphead\Cuphead_Data"
    r"\StreamingAssets\AssetBundles\atlas_flyingblimplevel"
)
OUTPUT = ROOT / "assets" / "creator-tools" / "interactions"
PREVIEWS = {
    "a_blimp_enemy_idle_0001": "green-zeppelin.png",
    "b_blimp_enemy_idle_0001": "purple-zeppelin.png",
}


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


OUTPUT.mkdir(parents=True, exist_ok=True)
environment = UnityPy.load(str(BUNDLE))
exported = set()
for obj in environment.objects:
    if obj.type.name != "Sprite":
        continue
    sprite = obj.read()
    destination_name = PREVIEWS.get(sprite.m_Name)
    if destination_name is None:
        continue
    destination = OUTPUT / destination_name
    render_full_frame(sprite).save(destination)
    exported.add(sprite.m_Name)
    print(f"{sprite.m_Name} -> {destination}")

missing = set(PREVIEWS) - exported
if missing:
    raise RuntimeError("Missing native sprites: " + ", ".join(sorted(missing)))
