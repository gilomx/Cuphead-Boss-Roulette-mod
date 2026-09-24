import argparse
from pathlib import Path

import UnityPy


CLIP_NAME = "sfx_level_pirate_shark_warning"


def main():
    parser = argparse.ArgumentParser(
        description="Extract Cuphead's native pirate shark warning WAV.")
    parser.add_argument("source", type=Path,
                        help="Path to sharedassets9.assets")
    parser.add_argument("destination", type=Path,
                        help="Output WAV path")
    args = parser.parse_args()

    environment = UnityPy.load(str(args.source))
    for obj in environment.objects:
        if obj.type.name != "AudioClip":
            continue
        clip = obj.read()
        if clip.m_Name != CLIP_NAME:
            continue
        samples = clip.samples
        if not samples:
            raise RuntimeError(f"{CLIP_NAME} has no decoded samples")
        _, data = next(iter(samples.items()))
        if not data.startswith(b"RIFF") or data[8:12] != b"WAVE":
            raise RuntimeError(f"{CLIP_NAME} did not decode as WAV")
        args.destination.parent.mkdir(parents=True, exist_ok=True)
        args.destination.write_bytes(data)
        print(f"{CLIP_NAME} -> {args.destination} ({len(data)} bytes)")
        return

    raise RuntimeError(f"Missing native clip: {CLIP_NAME}")


if __name__ == "__main__":
    main()
