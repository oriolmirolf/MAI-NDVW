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

    theme = "dark fantasy dungeon"
    room_index = 0
    total_rooms = 3
    seed = 42

    print(f"\nGenerating narrative with voice for room {room_index + 1}...")
    print(f"Theme: {theme}")
    print(f"Voice: {voice_gen.description[:60]}...\n")

    narrative = await narrative_gen.generate(
        room_index=room_index,
        total_rooms=total_rooms,
        theme=theme,
        seed=seed,
        previous_context=None,
        generate_voice=True
    )

    print(f"\n{'='*40}")
    print("RESULTS")
    print("=" * 40)
    print(f"\nNPC: {narrative.npc.name}")
    print(f"Environment: {narrative.environment[:100]}...")
    print(f"\nDialogue:")
    for i, line in enumerate(narrative.npc.dialogue):
        print(f"  [{i+1}] {line[:60]}...")

    if narrative.npc.audio_paths:
        print(f"\nAudio files:")
        for path in narrative.npc.audio_paths:
            print(f"  - {path}")

    # Save results
    result = {
        "roomIndex": narrative.roomIndex,
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
    }

    output_file = OUTPUT_DIR / "result.json"
    with open(output_file, "w") as f:
        json.dump(result, f, indent=2)

    print("\n" + "=" * 50)
    print(f"FULL PIPELINE TEST COMPLETE")
    print(f"Result: {output_file}")
    print(f"Audio: {OUTPUT_DIR / 'voice'}/")
    print("=" * 50)

if __name__ == "__main__":
    asyncio.run(main())
