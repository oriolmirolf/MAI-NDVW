import sys
sys.path.insert(0, '.')

from src.generators.narrative import NarrativeGenerator

def test_narrative_generation():
    print("Testing narrative generation...")
    gen = NarrativeGenerator(model="llama2")

    narrative = None
    import asyncio
    async def run():
        return await gen.generate(
            room_index=0,
            total_rooms=9,
            theme="dark fantasy dungeon",
            seed=12345
        )

    narrative = asyncio.run(run())

    assert narrative is not None
    assert narrative.roomIndex == 0
    assert narrative.npc is not None
    assert narrative.npc.name
    assert len(narrative.npc.dialogue) == 3
    assert narrative.quest is not None
    assert narrative.lore is not None

    print(f"SUCCESS: Generated narrative for room 0")
    print(f"  NPC: {narrative.npc.name}")
    print(f"  Dialogue lines: {len(narrative.npc.dialogue)}")
    print(f"  Quest: {narrative.quest.objective}")
    print(f"  Lore: {narrative.lore.title}")

if __name__ == "__main__":
    test_narrative_generation()
