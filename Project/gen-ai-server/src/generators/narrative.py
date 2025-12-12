import ollama
import json
import re
from typing import Optional
from pydantic import ValidationError
from src.models.responses import NarrativeResponse, NPCResponse, QuestResponse, LoreResponse
from src.generators.voice import VoiceGenerator

class NarrativeGenerator:
    def __init__(self, model: str = "llama2", voice_generator: Optional[VoiceGenerator] = None):
        self.model = model
        self.voice_gen = voice_generator
        print(f"Narrative generator initialized (model: {model}, voice={'enabled' if voice_generator else 'disabled'})")

    async def generate_chapter(self, chapter: int, seed: int, generate_voice: bool = True) -> NarrativeResponse:
        """Generate narrative for a chapter (0, 1, or 2)."""
        max_retries = 3
        last_error = None

        chapters = [
            {"name": "The Verdant Woods", "setting": "A sunlit forest clearing with ancient oaks", "boss": "Thornback the Wild", "intro": "These woods were once peaceful. Now Thornback corrupts them."},
            {"name": "The Twilight Marsh", "setting": "A misty swamp under eternal dusk", "boss": "The Bog Wraith", "intro": "We enter the marsh now. The Bog Wraith lurks in the fog."},
            {"name": "The Ember Wastes", "setting": "A scorched volcanic landscape with lava rivers", "boss": "Cinderax the Destroyer", "intro": "The final test awaits. Cinderax must fall."}
        ]
        ch = chapters[min(chapter, 2)]
        npc_name = "The Wanderer"

        prompt = self._build_simple_prompt(npc_name, ch, False, True, chapter)

        for attempt in range(max_retries):
            try:
                if attempt > 0:
                    print(f"[NARRATIVE] Retry {attempt}/{max_retries} for chapter {chapter}")

                response = ollama.generate(
                    model=self.model,
                    prompt=prompt,
                    options={"temperature": 0.8, "seed": seed + chapter + attempt},
                    stream=False
                )

                dialogue_lines = self._parse_simple_response(response["response"])
                narrative = self._build_chapter_narrative(chapter, ch, npc_name, dialogue_lines)

                # Generate voice for dialogue if enabled
                if generate_voice and self.voice_gen and narrative.npc.dialogue:
                    audio_paths = await self.voice_gen.generate_batch(
                        texts=narrative.npc.dialogue,
                        voice_id="narrator",
                        seed=seed + chapter
                    )
                    narrative.npc.audio_paths = [
                        self.voice_gen.get_relative_path(p) for p in audio_paths
                    ]
                    print(f"[NARRATIVE] Generated {len(audio_paths)} voice clips")

                print(f"[NARRATIVE] ✓ Chapter {chapter}: {dialogue_lines}")
                return narrative

            except ValueError as e:
                last_error = e
                print(f"[NARRATIVE] Attempt {attempt + 1} failed: {e}")
                continue

        raise ValueError(f"Failed to generate narrative for chapter {chapter} after {max_retries} attempts: {last_error}")

    def _build_simple_prompt(self, npc_name: str, ch: dict, is_boss_room: bool, is_chapter_start: bool, chapter: int) -> str:
        """Build a simple prompt for chapter intro."""
        chapter_contexts = [
            "The guide speaks of corruption spreading through once-peaceful woods.",
            "The guide describes the treacherous marsh and its lurking horrors.",
            "The guide prepares the hero for the final volcanic wasteland."
        ]
        return f"""Write 3 unique short lines. AVOID clichés like "Ah traveler" or "Greetings".
Context: {chapter_contexts[min(chapter, 2)]}
Style: Direct, ominous, poetic. Under 50 characters each.

1."""

    def _parse_simple_response(self, response: str) -> list[str]:
        """Parse plain text response into dialogue lines."""
        lines = []
        for line in response.strip().split('\n'):
            cleaned = re.sub(r'^[\d]+[.\)]\s*', '', line.strip())
            cleaned = cleaned.strip('"\'')
            cleaned = re.sub(r'\*[^*]+\*', '', cleaned)
            cleaned = re.sub(r'\([^)]+\)', '', cleaned)
            cleaned = re.sub(r'\[[^\]]+\]', '', cleaned)
            cleaned = cleaned.strip()
            if cleaned and len(cleaned) > 5:
                lines.append(cleaned)
        return lines[:3]

    def _build_chapter_narrative(self, chapter: int, ch: dict, npc_name: str, dialogue: list[str]) -> NarrativeResponse:
        """Build narrative response for a chapter."""
        return NarrativeResponse(
            roomIndex=chapter,
            environment=ch["setting"],
            npc=NPCResponse(name=npc_name, dialogue=dialogue),
            quest=QuestResponse(objective="Explore", type="Exploration", count=1),
            lore=LoreResponse(title=ch["name"], content=ch["intro"]),
            victory=None
        )
