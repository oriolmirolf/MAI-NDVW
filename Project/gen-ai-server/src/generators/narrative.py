import ollama
import json
import re
from pydantic import ValidationError
from src.models.responses import NarrativeResponse, NPCResponse, QuestResponse, LoreResponse

class NarrativeGenerator:
    def __init__(self, model: str = "llama2"):
        self.model = model
        print(f"Narrative generator initialized (model: {model})")

    async def generate(self, room_index: int, total_rooms: int, theme: str, seed: int, previous_context: str = None) -> NarrativeResponse:
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
                    options={"temperature": 0.7, "seed": seed + attempt},  # Vary seed on retry
                    stream=False
                )

                narrative_text = response["response"]
                return self._parse_response(narrative_text, room_index)

            except ValueError as e:
                last_error = e
                print(f"[NARRATIVE] Attempt {attempt + 1} failed: {e}")
                continue

        # All retries failed
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

        return f"""Generate a {theme} story segment for room {room_index + 1} of {total_rooms}.
This is the {phase} of the adventure.
{context_section}

CRITICAL INSTRUCTIONS:
1. Output ONLY valid JSON - no explanations, no markdown, no extra text
2. Use proper JSON syntax with double quotes for all strings
3. Ensure all commas are correctly placed
4. The dialogue field MUST be an array with EXACTLY 3 strings
5. Do not use apostrophes or single quotes in the JSON
6. Escape any quotes inside strings properly

Required JSON structure (copy this EXACTLY):
{{
  "environment": "atmospheric description here",
  "npc": {{
    "name": "character name",
    "dialogue": ["first line of dialogue", "second line of dialogue", "third line of dialogue"]
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
    "dialogue": ["You have entered the dungeon depths brave one.", "Beware of the Ghosts and Slimes that lurk in these rooms.", "They guard secrets that have been lost for ages."]
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
