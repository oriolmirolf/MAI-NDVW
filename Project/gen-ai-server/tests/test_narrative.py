#!/usr/bin/env python3
"""Test narrative generation independently (without voice)."""

import asyncio
import json
import sys
from pathlib import Path
sys.path.insert(0, '.')

from src.generators.narrative import NarrativeGenerator

OUTPUT_DIR = Path("tests/output/narrative")

async def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print("=" * 50)
    print("NARRATIVE GENERATION TEST (no voice)")
    print("=" * 50)

    narrative_gen = NarrativeGenerator(model="llama2", voice_generator=None)

    theme = "dark fantasy dungeon"
    total_rooms = 3
    seed = 42

    print(f"\nGenerating narratives for {total_rooms} rooms...")
    print(f"Theme: {theme}\n")

    results = []
    previous_context = None

    for room_index in range(total_rooms):
        print(f"\n{'='*40}")
        print(f"ROOM {room_index + 1}/{total_rooms}")
        print("=" * 40)

        narrative = await narrative_gen.generate(
            room_index=room_index,
            total_rooms=total_rooms,
            theme=theme,
            seed=seed + room_index,
            previous_context=previous_context,
            generate_voice=False
        )

        print(f"\nNPC: {narrative.npc.name}")
        print(f"Environment: {narrative.environment[:100]}...")
        print(f"\nDialogue:")
        for i, line in enumerate(narrative.npc.dialogue):
            print(f"  [{i+1}] {line[:80]}...")
        print(f"\nQuest: {narrative.quest.objective}")
        print(f"Lore: {narrative.lore.title}")

        results.append({
            "roomIndex": narrative.roomIndex,
            "environment": narrative.environment,
            "npc": {
                "name": narrative.npc.name,
                "dialogue": narrative.npc.dialogue
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

        previous_context = f"In room {room_index + 1}, met {narrative.npc.name}. {narrative.environment[:100]}"

    # Save results
    output_file = OUTPUT_DIR / "narratives.json"
    with open(output_file, "w") as f:
        json.dump(results, f, indent=2)

    print("\n" + "=" * 50)
    print(f"NARRATIVE TEST COMPLETE")
    print(f"Saved to: {output_file}")
    print("=" * 50)

if __name__ == "__main__":
    asyncio.run(main())
