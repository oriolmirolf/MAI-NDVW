#!/usr/bin/env python3
"""Test music generation with chapter-based themes."""

import asyncio
import os
import sys
sys.path.insert(0, '.')

from src.generators.music import MusicGenerator

OUTPUT_DIR = "tests/output/music"

async def test_chapter_music():
    """Test music generation for each chapter."""
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    print("=" * 50)
    print("MUSIC GENERATION TEST (Chapter-based)")
    print("=" * 50)

    gen = MusicGenerator(
        model="facebook/musicgen-small",  # Use small for faster tests
        output_dir=OUTPUT_DIR
    )
    print(f"Device: {gen.device}\n")

    # Test each chapter theme
    chapters = [
        (0, "Forest - green, peaceful"),
        (1, "Night - moonlit, mysterious"),
        (2, "Desert - hot, adventurous")
    ]

    results = []
    for chapter, description in chapters:
        print(f"\n{'='*40}")
        print(f"Chapter {chapter}: {description}")
        print("=" * 40)

        file_path = await gen.generate(
            chapter=chapter,
            seed=42 + chapter,
            duration=10.0
        )

        assert file_path is not None, f"Failed to generate music for chapter {chapter}"
        assert os.path.exists(file_path), f"Music file not found: {file_path}"
        assert file_path.endswith(".wav"), f"Invalid file type: {file_path}"

        file_size = os.path.getsize(file_path) / 1024
        print(f"  Path: {file_path}")
        print(f"  Size: {file_size:.1f} KB")
        print(f"  Seed: {gen.last_seed}")

        results.append({
            "chapter": chapter,
            "path": file_path,
            "size_kb": file_size
        })

    print("\n" + "=" * 50)
    print("MUSIC TEST COMPLETE")
    print(f"Generated {len(results)} music files")
    print("=" * 50)

    return results

async def test_custom_description():
    """Test music generation with custom description."""
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    print("\n" + "=" * 50)
    print("CUSTOM DESCRIPTION TEST")
    print("=" * 50)

    gen = MusicGenerator(
        model="facebook/musicgen-small",
        output_dir=OUTPUT_DIR
    )

    file_path = await gen.generate(
        description="lo-fi chill haunted castle, soft pads, minimal, 65 bpm",
        seed=999,
        duration=10.0
    )

    assert file_path is not None
    assert os.path.exists(file_path)
    print(f"  Path: {file_path}")
    print(f"  Size: {os.path.getsize(file_path) / 1024:.1f} KB")

    print("\n✓ Custom description test passed")

if __name__ == "__main__":
    asyncio.run(test_chapter_music())
    asyncio.run(test_custom_description())
