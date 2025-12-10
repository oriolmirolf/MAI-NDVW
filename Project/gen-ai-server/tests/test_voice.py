#!/usr/bin/env python3
"""Test voice generation independently."""

import asyncio
import json
import sys
from pathlib import Path
sys.path.insert(0, '.')

from src.generators.voice import VoiceGenerator

OUTPUT_DIR = Path("tests/output/voice")

async def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print("=" * 50)
    print("VOICE GENERATION TEST")
    print("=" * 50)

    voice_gen = VoiceGenerator(output_dir=str(OUTPUT_DIR))

    test_texts = [
        "Welcome brave adventurer to the depths of the forsaken dungeon.",
        "The creatures that dwell within these walls are ancient and dangerous.",
        "If you seek the secrets hidden in these depths, you must first prove your worth.",
    ]

    print(f"\nGenerating {len(test_texts)} voice clips...")
    print(f"Voice: {voice_gen.description[:60]}...")
    print()

    results = []
    for i, text in enumerate(test_texts):
        print(f"[{i+1}/{len(test_texts)}] {text[:50]}...")
        path = await voice_gen.generate(text=text, voice_id="test", seed=12345 + i)
        results.append({"text": text, "audio": path})
        print(f"  -> Saved: {path}\n")

    # Save manifest
    manifest_file = OUTPUT_DIR / "manifest.json"
    with open(manifest_file, "w") as f:
        json.dump(results, f, indent=2)

    print("=" * 50)
    print(f"VOICE TEST COMPLETE")
    print(f"Audio files: {OUTPUT_DIR}/")
    print(f"Manifest: {manifest_file}")
    print("=" * 50)

if __name__ == "__main__":
    asyncio.run(main())
