#!/usr/bin/env python3
"""Test full pipeline: narrative + voice generation together."""

import asyncio
import json
import sys
from pathlib import Path
sys.path.insert(0, '.')

from src.generators.narrative import NarrativeGenerator
from src.generators.voice import VoiceGenerator

OUTPUT_DIR = Path("tests/output/pipeline")

async def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print("=" * 50)
    print("FULL PIPELINE TEST (narrative + voice)")
    print("=" * 50)

    voice_gen = VoiceGenerator(output_dir=str(OUTPUT_DIR / "voice"))
    narrative_gen = NarrativeGenerator(model="llama2", voice_generator=voice_gen)

    theme = "2D pixel art RPG adventure"
    total_rooms = 9
    seed = 42

    results = []

    # Test 3 rooms (one from each chapter)
    test_rooms = [0, 3, 6]  # Forest, Marsh, Citadel

    for room_index in test_rooms:
        print(f"\n{'='*40}")
        print(f"ROOM {room_index} (Chapter {room_index // 3 + 1})")
        print("=" * 40)

        narrative = await narrative_gen.generate(
            room_index=room_index,
            total_rooms=total_rooms,
            theme=theme,
            seed=seed + room_index,
            previous_context=None,
            generate_voice=True
        )

        print(f"\nNPC: {narrative.npc.name}")
        print(f"Environment: {narrative.environment[:80]}...")
        print(f"\nDialogue:")
        for i, line in enumerate(narrative.npc.dialogue):
            print(f"  [{i+1}] {line[:60]}...")

        if narrative.npc.audio_paths:
            print(f"\nAudio files: {len(narrative.npc.audio_paths)}")
            for path in narrative.npc.audio_paths:
                print(f"  - {path}")

        results.append({
            "roomIndex": narrative.roomIndex,
            "chapter": room_index // 3 + 1,
            "environment": narrative.environment,
            "npc": {
                "name": narrative.npc.name,
                "dialogue": narrative.npc.dialogue,
                "audio_paths": narrative.npc.audio_paths
            },
            "quest": {
                "objective": narrative.quest.objective,
                "type": narrative.quest.type,
                "count": narrative.quest.count
            },
            "lore": {
                "title": narrative.lore.title,
                "content": narrative.lore.content
            }
        })

    # Save results
    output_file = OUTPUT_DIR / "result.json"
    with open(output_file, "w") as f:
        json.dump(results, f, indent=2)

    print("\n" + "=" * 50)
    print(f"FULL PIPELINE TEST COMPLETE")
    print(f"Rooms tested: {len(results)}")
    print(f"Result: {output_file}")
    print(f"Audio: {OUTPUT_DIR / 'voice'}/")
    print("=" * 50)

if __name__ == "__main__":
    asyncio.run(main())
