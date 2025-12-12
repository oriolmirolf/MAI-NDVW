import json
import hashlib
from pathlib import Path
from typing import Optional

class CacheManager:
    def __init__(self, cache_dir: str = "cache"):
        self.cache_dir = Path(cache_dir)
        self.cache_dir.mkdir(exist_ok=True)

        self.narrative_cache = self.cache_dir / "narrative"
        self.music_cache = self.cache_dir / "music"
        self.dungeon_cache = self.cache_dir / "dungeon"

        self.narrative_cache.mkdir(exist_ok=True)
        self.music_cache.mkdir(exist_ok=True)
        self.dungeon_cache.mkdir(exist_ok=True)

    def _hash_key(self, key: str) -> str:
        return hashlib.sha256(key.encode()).hexdigest()[:16]

    def get_narrative(self, room_index: int, total_rooms: int, theme: str, seed: int) -> Optional[dict]:
        key = f"{room_index}_{total_rooms}_{theme}_{seed}"
        cache_hash = self._hash_key(key)
        cache_file = self.narrative_cache / f"{cache_hash}.json"

        if cache_file.exists():
            with open(cache_file, 'r') as f:
                print(f"[CACHE HIT] Narrative: room {room_index}, seed {seed}")
                return json.load(f)
        return None

    def set_narrative(self, room_index: int, total_rooms: int, theme: str, seed: int, data: dict):
        key = f"{room_index}_{total_rooms}_{theme}_{seed}"
        cache_hash = self._hash_key(key)
        self.narrative_cache.mkdir(parents=True, exist_ok=True)
        cache_file = self.narrative_cache / f"{cache_hash}.json"

        with open(cache_file, 'w') as f:
            json.dump(data, f, indent=2)
        print(f"[CACHE SAVE] Narrative: room {room_index}, seed {seed}")

    def get_music(self, description: str, seed: int, duration: float) -> Optional[str]:
        key = f"{description}_{seed}_{duration}"
        cache_hash = self._hash_key(key)
        cache_file = self.music_cache / f"{cache_hash}.txt"

        if cache_file.exists():
            with open(cache_file, 'r') as f:
                audio_path = f.read().strip()
                if Path(audio_path).exists():
                    print(f"[CACHE HIT] Music: {description[:30]}..., seed {seed}")
                    return audio_path
        return None

    def set_music(self, description: str, seed: int, duration: float, audio_path: str):
        key = f"{description}_{seed}_{duration}"
        cache_hash = self._hash_key(key)
        self.music_cache.mkdir(parents=True, exist_ok=True)
        cache_file = self.music_cache / f"{cache_hash}.txt"

        with open(cache_file, 'w') as f:
            f.write(audio_path)
        print(f"[CACHE SAVE] Music: {description[:30]}..., seed {seed}")

    def get_dungeon(self, cache_key: str) -> Optional[dict]:
        cache_hash = self._hash_key(cache_key)
        cache_file = self.dungeon_cache / f"{cache_hash}.json"

        if cache_file.exists():
            with open(cache_file, 'r') as f:
                print(f"[CACHE HIT] Dungeon: {cache_key}")
                return json.load(f)
        return None

    def set_dungeon(self, cache_key: str, data: dict):
        cache_hash = self._hash_key(cache_key)
        self.dungeon_cache.mkdir(parents=True, exist_ok=True)
        cache_file = self.dungeon_cache / f"{cache_hash}.json"

        with open(cache_file, 'w') as f:
            json.dump(data, f, indent=2)
        print(f"[CACHE SAVE] Dungeon: {cache_key}")

    def clear_all(self):
        for cache_type in [self.narrative_cache, self.music_cache, self.dungeon_cache]:
            for file in cache_type.glob("*"):
                file.unlink()
        print("[CACHE] Cleared all caches")

    def stats(self) -> dict:
        return {
            "narrative": len(list(self.narrative_cache.glob("*.json"))),
            "music": len(list(self.music_cache.glob("*.txt"))),
            "dungeon": len(list(self.dungeon_cache.glob("*.json")))
        }
