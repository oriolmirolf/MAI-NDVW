#!/usr/bin/env python3
"""Test XTTS v2 voice cloning."""

import asyncio
import json
import sys
from pathlib import Path
sys.path.insert(0, '.')

from src.generators.voice import VoiceGenerator

OUTPUT_DIR = Path("tests/output/voice")
SPEAKER_WAV = Path("tests/data/Nature Documentary Narration.wav")

async def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print("=" * 50)
    print("XTTS v2 VOICE CLONING TEST")
    print("=" * 50)

    if not SPEAKER_WAV.exists():
        print(f"ERROR: Speaker sample not found: {SPEAKER_WAV}")
        return

    print(f"Speaker sample: {SPEAKER_WAV}")
    print(f"Output dir: {OUTPUT_DIR}\n")

    voice_gen = VoiceGenerator(
        output_dir=str(OUTPUT_DIR),
        speaker_wav=str(SPEAKER_WAV)
    )

    test_texts = [
        "Welcome brave adventurer to the depths of the forsaken dungeon.",
        "The creatures that dwell within these walls are ancient and dangerous.",
        "If you seek the secrets hidden in these depths, you must first prove your worth.",
    ]

    print(f"Generating {len(test_texts)} voice clips...\n")

    results = []
    for i, text in enumerate(test_texts):
        print(f"[{i+1}/{len(test_texts)}] {text[:50]}...")
        path = await voice_gen.generate(text=text, seed=12345 + i)
        results.append({"text": text, "audio": path})
        print(f"  -> Saved: {path}\n")

    # Save manifest
    manifest_file = OUTPUT_DIR / "manifest.json"
    with open(manifest_file, "w") as f:
        json.dump(results, f, indent=2)

    print("=" * 50)
    print(f"VOICE CLONING TEST COMPLETE")
    print(f"Audio files: {OUTPUT_DIR}/")
    print(f"Manifest: {manifest_file}")
    print("=" * 50)

if __name__ == "__main__":
    asyncio.run(main())
