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

    async def generate(self, room_index: int, total_rooms: int, theme: str, seed: int, previous_context: str = None, generate_voice: bool = True) -> NarrativeResponse:
        max_retries = 3
        last_error = None

        for attempt in range(max_retries):
            try:
                if attempt > 0:
                    print(f"[NARRATIVE] Retry {attempt}/{max_retries} for room {room_index}")

                prompt = self._build_prompt(room_index, total_rooms, theme, previous_context)

                response = ollama.generate(
                    model=self.model,
                    prompt=prompt,
                    options={"temperature": 0.7, "seed": seed + attempt},
                    stream=False
                )

                narrative_text = response["response"]
                narrative = self._parse_response(narrative_text, room_index)

                # Generate voice for dialogue if enabled
                if generate_voice and self.voice_gen and narrative.npc.dialogue:
                    # Clean dialogue text for TTS (remove emotion markers)
                    clean_dialogue = self._clean_dialogue_for_tts(narrative.npc.dialogue)
                    audio_paths = await self.voice_gen.generate_batch(
                        texts=clean_dialogue,
                        voice_id=narrative.npc.name.lower().replace(" ", "_"),
                        seed=seed
                    )
                    # Convert to relative paths for serving
                    narrative.npc.audio_paths = [
                        self.voice_gen.get_relative_path(p) for p in audio_paths
                    ]
                    print(f"[NARRATIVE] Generated {len(audio_paths)} voice clips for {narrative.npc.name}")

                return narrative

            except ValueError as e:
                last_error = e
                print(f"[NARRATIVE] Attempt {attempt + 1} failed: {e}")
                continue

        raise ValueError(f"Failed to generate valid narrative for room {room_index} after {max_retries} attempts: {last_error}")

    def _build_prompt(self, room_index: int, total_rooms: int, theme: str, previous_context: str = None) -> str:
        progress = room_index / (total_rooms - 1) if total_rooms > 1 else 0
        phase = "beginning" if progress < 0.33 else "middle" if progress < 0.66 else "final"

        context_section = ""
        if previous_context:
            context_section = f"""
Story so far:
{previous_context}

Continue the story coherently, building on what happened before.
"""

        # NPC name suggestions based on room position
        npc_suggestions = [
            "Ancient Keeper, Dusty Scholar, Forgotten Sage",
            "Shadow Walker, Wandering Spirit, Lost Soul",
            "Cursed Knight, Fallen Guardian, Hollow Warrior",
            "Witch of the Depths, Dark Seer, Mystic Oracle",
            "Tormented Ghost, Vengeful Shade, Restless Specter",
            "Dungeon Warden, Stone Golem, Iron Sentinel",
            "Demon Herald, Dark Messenger, Void Speaker",
            "Final Guardian, Ancient Evil, The Forgotten One"
        ]
        name_hint = npc_suggestions[room_index % len(npc_suggestions)]

        return f"""Generate a {theme} story segment for room {room_index + 1} of {total_rooms}.
This is the {phase} of the adventure.
Suggested NPC names for this room (pick ONE): {name_hint}
{context_section}

CRITICAL INSTRUCTIONS:
1. Output ONLY valid JSON - no explanations, no markdown, no extra text
2. Use proper JSON syntax with double quotes for all strings
3. Ensure all commas are correctly placed
4. The dialogue field MUST be an array with EXACTLY 3 strings
5. IMPORTANT: Each dialogue line should be 2-3 full sentences long (like a movie narrator)
6. Do not use apostrophes or single quotes in the JSON
7. Escape any quotes inside strings properly
8. VERY IMPORTANT: Write dialogue as plain spoken text only. Do NOT include any action markers, emotions, or stage directions like *coughs*, *laughs*, *sighs*, *whispers*, (pauses), [clears throat], etc. The text will be read by a voice synthesizer that cannot interpret these markers.

Required JSON structure (copy this EXACTLY):
{{
  "environment": "atmospheric description here",
  "npc": {{
    "name": "character name",
    "dialogue": ["first long dialogue line with 2-3 sentences", "second long dialogue line with 2-3 sentences", "third long dialogue line with 2-3 sentences"]
  }},
  "quest": {{
    "objective": "quest objective description",
    "type": "DefeatEnemies",
    "count": 3
  }},
  "lore": {{
    "title": "lore title",
    "content": "lore content and backstory"
  }}
}}

Example valid output:
{{
  "environment": "A grassy clearing with scattered ponds and small stone structures. The area has a peaceful yet mysterious atmosphere with gentle light filtering through the space.",
  "npc": {{
    "name": "Wandering Spirit",
    "dialogue": ["Welcome brave adventurer to the depths of the forsaken dungeon. Many have entered these halls seeking glory and treasure but few have returned to tell their tales. You must prove yourself worthy if you wish to survive.", "The creatures that dwell within these walls are ancient and dangerous. Ghosts of fallen warriors drift through the corridors seeking revenge on the living. Slimes ooze from every shadow waiting to consume the unwary traveler.", "If you seek the secrets hidden in these depths you must first clear this chamber of its guardians. Only then will the path forward reveal itself to those who have proven their strength and courage."]
  }},
  "quest": {{
    "objective": "Clear the room of hostile Ghosts and Slimes",
    "type": "DefeatEnemies",
    "count": 3
  }},
  "lore": {{
    "title": "The Haunted Depths",
    "content": "These dungeons were once peaceful halls. Now Ghosts drift through the corridors while Slimes ooze from the shadows. Only a skilled adventurer can navigate these perilous rooms."
  }}
}}

BAD dialogue examples (DO NOT use these patterns):
- "Welcome traveler *coughs* to my domain" - NO action markers
- "Beware... (pauses dramatically) the darkness awaits" - NO stage directions
- "[whispers] The secret lies within" - NO bracketed instructions
- "Ha ha ha! You fool!" - Write as: "You fool! Your arrogance amuses me greatly."

GOOD dialogue examples:
- "Welcome traveler to my domain. I have waited long for one such as yourself."
- "Beware the darkness that awaits you in the depths below. Many have fallen to its grasp."
- "The secret lies within these ancient walls. Listen carefully to my words."

Now generate the JSON for this room:"""

    def _parse_response(self, response: str, room_index: int) -> NarrativeResponse:
        json_match = re.search(r'\{.*\}', response, re.DOTALL)
        if not json_match:
            raise ValueError(f"No JSON found in LLM response for room {room_index}")

        json_str = json_match.group(0)

        # Try to fix common JSON issues
        json_str = self._fix_json_formatting(json_str)

        try:
            data = json.loads(json_str)

            if not all(k in data for k in ["environment", "npc", "quest", "lore"]):
                missing = [k for k in ["environment", "npc", "quest", "lore"] if k not in data]
                raise ValueError(f"Missing required fields: {missing}")

            # Validate with Pydantic
            narrative = NarrativeResponse(
                roomIndex=room_index,
                environment=data["environment"],
                npc=NPCResponse(
                    name=data["npc"]["name"],
                    dialogue=data["npc"]["dialogue"]
                ),
                quest=QuestResponse(
                    objective=data["quest"]["objective"],
                    type=data["quest"]["type"],
                    count=data["quest"]["count"]
                ),
                lore=LoreResponse(
                    title=data["lore"]["title"],
                    content=data["lore"]["content"]
                )
            )

            print(f"[NARRATIVE] ✓ Room {room_index}: {narrative.npc.name}")
            return narrative

        except json.JSONDecodeError as e:
            raise ValueError(f"Invalid JSON syntax: {e}")
        except (ValidationError, KeyError) as e:
            raise ValueError(f"Invalid narrative structure: {e}")

    def _fix_json_formatting(self, json_str: str) -> str:
        """Attempt to fix common JSON formatting issues"""
        # Remove extra whitespace
        json_str = re.sub(r'\s+', ' ', json_str)
        # Fix missing commas between fields (common LLM mistake)
        json_str = re.sub(r'"\s*\n\s*"', '", "', json_str)
        return json_str

    def _clean_dialogue_for_tts(self, dialogue_lines: list[str]) -> list[str]:
        """Clean dialogue text to remove emotion markers for TTS."""
        cleaned = []
        for text in dialogue_lines:
            # Remove *action* markers
            text = re.sub(r'\*[^*]+\*', '', text)
            # Remove (action) markers
            text = re.sub(r'\([^)]+\)', '', text)
            # Remove [action] markers
            text = re.sub(r'\[[^\]]+\]', '', text)
            # Remove common inline emotion words (standalone or with adverbs)
            text = re.sub(r'\b(giggles?|laughs?|coughs?|sighs?|chuckles?|snorts?|gasps?|whispers?|grins?|smiles?|frowns?|groans?|moans?|hisses?|growls?|shrieks?|screams?|yells?|mutters?|mumbles?)\s*(maniacally|softly|loudly|quietly|nervously|evilly|wickedly|darkly|menacingly)?\b', '', text, flags=re.IGNORECASE)
            # Remove "Hehe", "Haha", "Muahaha" etc.
            text = re.sub(r'\b(he+|ha+|ho+|mu+a+ha+)\s*(he+|ha+|ho+)?\b', '', text, flags=re.IGNORECASE)
            # Clean up multiple spaces and punctuation
            text = re.sub(r'\s+', ' ', text)
            text = re.sub(r'\s+([.,!?])', r'\1', text)
            text = re.sub(r'([.,!?])\1+', r'\1', text)
            text = re.sub(r'^\s*[.,!?]\s*', '', text)  # Remove leading punctuation
            text = text.strip()
            # Ensure proper sentence ending
            if text and text[-1] not in '.!?':
                text += '.'
            cleaned.append(text)
        return cleaned
