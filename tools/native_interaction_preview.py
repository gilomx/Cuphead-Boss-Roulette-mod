"""Shared native-sprite extraction and antialiased masks for catalog previews."""
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
for directory in ('interaction-pydeps-py312', 'interaction-pydeps'):
    dependencies = ROOT / 'obj' / directory
    if dependencies.is_dir():
        sys.path.insert(0, str(dependencies))
        break

import UnityPy
from PIL import Image, ImageChops, ImageDraw


def default_bundle(name):
    """Read local build settings or Steam libraries; never write to the game."""
    game_roots = []
    if os.environ.get('CUPHEAD_DIR'):
        game_roots.append(Path(os.path.expandvars(os.environ['CUPHEAD_DIR'])))
    local = ROOT / 'launcher-dev.local.json'
    if local.is_file():
        configured = json.loads(local.read_text(encoding='utf-8-sig')).get('cupheadDir')
        if configured:
            game_roots.append(ROOT / os.path.expandvars(configured))
    if os.name == 'nt':
        import winreg
        libraries = []
        for hive, key in [(winreg.HKEY_CURRENT_USER, r'Software\Valve\Steam'),
                          (winreg.HKEY_LOCAL_MACHINE, r'SOFTWARE\WOW6432Node\Valve\Steam')]:
            try:
                with winreg.OpenKey(hive, key) as entry:
                    for field in ('SteamPath', 'InstallPath'):
                        try:
                            libraries.append(Path(winreg.QueryValueEx(entry, field)[0]))
                        except OSError:
                            pass
            except OSError:
                pass
        for steam in list(libraries):
            vdf = steam / 'steamapps' / 'libraryfolders.vdf'
            if vdf.is_file():
                libraries.extend(Path(value.replace('\\\\', '\\')) for value in
                                 re.findall(r'"path"\s+"([^"]+)"', vdf.read_text(encoding='utf-8')))
        for library in set(libraries):
            manifest = library / 'steamapps' / 'appmanifest_268910.acf'
            if manifest.is_file():
                match = re.search(r'"installdir"\s+"([^"]+)"', manifest.read_text(encoding='utf-8'))
                if match:
                    game_roots.append(library / 'steamapps' / 'common' / match[1])
    for game in game_roots:
        bundle = game / 'Cuphead_Data' / 'StreamingAssets' / 'AssetBundles' / name
        if bundle.is_file():
            return bundle
    raise FileNotFoundError(f'Cannot locate {name}; pass --bundle explicitly.')


def cropped_sprites(bundle, names):
    names = set(names)
    environment = UnityPy.load(str(bundle))
    images = {}
    for obj in environment.objects:
        if obj.type.name != 'Sprite' or obj.peek_name() not in names:
            continue
        name = obj.peek_name()
        image = obj.read().image.convert('RGBA')
        bounds = image.getchannel('A').getbbox()
        if bounds is None:
            raise RuntimeError(f'Native sprite {name} is empty.')
        images[name] = image.crop(bounds)
    missing = names - images.keys()
    if missing:
        raise RuntimeError(f'Missing native sprites: {sorted(missing)}')
    return images


def clip_outline(image, start, segments):
    """Rasterize the approved line/quadratic/cubic mask at 4x resolution."""
    points = [start]
    for operation, *coordinates in segments:
        previous = points[-1]
        if operation == 'L':
            points.append(coordinates[0])
            continue
        if operation not in ('Q', 'C'):
            raise ValueError(f'Unsupported outline segment: {operation}')
        for step in range(1, 65):
            t = step / 64
            u = 1 - t
            if operation == 'Q':
                control, end = coordinates
                point = tuple(u*u*previous[axis] + 2*u*t*control[axis] + t*t*end[axis]
                              for axis in (0, 1))
            else:
                first, second, end = coordinates
                point = tuple(u**3*previous[axis] + 3*u*u*t*first[axis] +
                              3*u*t*t*second[axis] + t**3*end[axis] for axis in (0, 1))
            points.append(point)
    mask = Image.new('L', (image.width * 4, image.height * 4))
    ImageDraw.Draw(mask).polygon([(round(x*4), round(y*4)) for x, y in points], fill=255)
    mask = mask.resize(image.size, Image.Resampling.LANCZOS)
    clipped = image.copy()
    clipped.putalpha(ImageChops.multiply(image.getchannel('A'), mask))
    return clipped


def save_preview(canvas, output):
    if canvas.getchannel('A').getbbox() is None:
        raise RuntimeError('The composed preview is empty.')
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, optimize=True)
    print(f'Native preview -> {output} ({canvas.width}x{canvas.height})')
