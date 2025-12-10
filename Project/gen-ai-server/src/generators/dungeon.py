import ollama
import json
import re
from typing import List, Dict, Optional
from src.models.requests import RoomInfo
from src.models.responses import DungeonContentResponse, RoomContent, EnemySpawn

class DungeonContentGenerator:
    def __init__(self, model: str = "llama2"):
        self.model = model
        print(f"[DUNGEON] Initialized with model: {model}")

    async def generate(
        self,
        seed: int,
        theme: str,
        rooms: List[RoomInfo],
        available_enemies: List[str],
        room_screenshots: Optional[Dict[str, str]] = None
    ) -> DungeonContentResponse:

        print(f"[DUNGEON] Generating content for {len(rooms)} rooms")
        print(f"[DUNGEON] Theme: {theme}, Enemies: {available_enemies}")

        prompt = self._build_prompt(seed, theme, rooms, available_enemies, room_screenshots)

        max_retries = 3
        last_error = None

        for attempt in range(max_retries):
            try:
                current_seed = seed + attempt
                print(f"[DUNGEON] Attempt {attempt + 1}/{max_retries} (seed={current_seed})")

                response = ollama.generate(
                    model=self.model,
                    prompt=prompt,
                    options={
                        "temperature": 0.7,
                        "seed": current_seed
                    }
                )

                raw_text = response["response"]
                print(f"[DUNGEON] Raw response length: {len(raw_text)}")

                parsed = self._parse_response(raw_text, rooms, available_enemies)

                result = DungeonContentResponse(
                    seed=seed,
                    theme=theme,
                    rooms=parsed
                )

                print(f"[DUNGEON] Successfully generated content")
                return result

            except Exception as e:
                last_error = e
                print(f"[DUNGEON] Attempt {attempt + 1} failed: {e}")

        print(f"[DUNGEON] All attempts failed, using fallback")
        return self._generate_fallback(seed, theme, rooms, available_enemies)

    def _build_prompt(
        self,
        seed: int,
        theme: str,
        rooms: List[RoomInfo],
        available_enemies: List[str],
        room_screenshots: Optional[Dict[str, str]] = None
    ) -> str:

        rooms_desc = []
        for room in rooms:
            room_type = "starting room" if room.is_start else "boss room" if room.is_boss else "combat room"
            connections = ", ".join(room.connections) if room.connections else "none"
            rooms_desc.append(f"  - Room {room.id}: {room_type}, connections: {connections}")

        rooms_text = "\n".join(rooms_desc)
        enemies_text = ", ".join(available_enemies)

        prompt = f"""You are a game designer creating enemy encounters for a roguelike dungeon crawler.

DUNGEON INFO:
- Theme: {theme}
- Total rooms: {len(rooms)}
- Available enemy types: {enemies_text}

ROOM LAYOUT:
{rooms_text}

ENEMY CHARACTERISTICS:
- slime: Basic melee enemy, slow but persistent. Good for introductory rooms.
- ghost: Ranged attacker with burst patterns. More dangerous, good for mid-dungeon.
- grape: Projectile launcher with area denial. High threat, good for challenging rooms.

DESIGN RULES:
1. Starting room (is_start=true) should have NO enemies (safe spawn)
2. Boss rooms (is_boss=true) should have 1 strong enemy or many weak ones
3. Rooms with more connections are usually larger - can have more enemies
4. Gradually increase difficulty as room ID increases
5. Mix enemy types for interesting combat
6. Total enemies per room: 0-5 for normal rooms

Generate enemy spawns for each room. Respond ONLY with valid JSON in this exact format:
{{
  "0": {{"enemies": [], "description": "Safe starting area"}},
  "1": {{"enemies": [{{"type": "slime", "count": 2}}], "description": "Light encounter"}},
  "2": {{"enemies": [{{"type": "slime", "count": 1}}, {{"type": "ghost", "count": 2}}], "description": "Mixed threat"}}
}}

Generate content for rooms 0 through {len(rooms) - 1}:"""

        return prompt

    def _parse_response(
        self,
        raw_text: str,
        rooms: List[RoomInfo],
        available_enemies: List[str]
    ) -> Dict[str, RoomContent]:

        json_match = re.search(r'\{[\s\S]*\}', raw_text)
        if not json_match:
            raise ValueError("No JSON found in response")

        json_str = json_match.group(0)
        json_str = self._fix_json(json_str)

        data = json.loads(json_str)

        result = {}
        for room in rooms:
            room_id = str(room.id)

            if room_id in data:
                room_data = data[room_id]
                enemies = []

                for enemy in room_data.get("enemies", []):
                    enemy_type = enemy.get("type", "").lower()
                    if enemy_type in [e.lower() for e in available_enemies]:
                        enemies.append(EnemySpawn(
                            type=enemy_type,
                            count=min(max(0, enemy.get("count", 1)), 10)
                        ))

                result[room_id] = RoomContent(
                    room_id=room.id,
                    enemies=enemies,
                    description=room_data.get("description", "A dungeon room")
                )
            else:
                result[room_id] = RoomContent(
                    room_id=room.id,
                    enemies=[],
                    description="Unexplored area"
                )

        return result

    def _fix_json(self, json_str: str) -> str:
        json_str = re.sub(r',\s*}', '}', json_str)
        json_str = re.sub(r',\s*]', ']', json_str)
        return json_str

    def _generate_fallback(
        self,
        seed: int,
        theme: str,
        rooms: List[RoomInfo],
        available_enemies: List[str]
    ) -> DungeonContentResponse:

        import random
        rng = random.Random(seed)

        result = {}
        for i, room in enumerate(rooms):
            room_id = str(room.id)

            if room.is_start:
                enemies = []
                description = "Safe starting area"
            elif room.is_boss:
                enemy_type = rng.choice(available_enemies)
                enemies = [EnemySpawn(type=enemy_type, count=3)]
                description = "Boss chamber"
            else:
                num_types = rng.randint(1, min(2, len(available_enemies)))
                enemies = []
                for _ in range(num_types):
                    enemy_type = rng.choice(available_enemies)
                    count = rng.randint(1, 3)
                    enemies.append(EnemySpawn(type=enemy_type, count=count))
                description = f"Combat room {room.id}"

            result[room_id] = RoomContent(
                room_id=room.id,
                enemies=enemies,
                description=description
            )

        return DungeonContentResponse(
            seed=seed,
            theme=theme,
            rooms=result
        )
