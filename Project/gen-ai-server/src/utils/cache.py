import json
import hashlib
from pathlib import Path
from typing import Optional, Any

class CacheManager:
    def __init__(self, cache_dir: str = "cache"):
        self.cache_dir = Path(cache_dir)
        self.cache_dir.mkdir(exist_ok=True)

        self.vision_cache = self.cache_dir / "vision"
        self.narrative_cache = self.cache_dir / "narrative"
        self.music_cache = self.cache_dir / "music"

        self.vision_cache.mkdir(exist_ok=True)
        self.narrative_cache.mkdir(exist_ok=True)
        self.music_cache.mkdir(exist_ok=True)

    def _hash_key(self, key: str) -> str:
        return hashlib.sha256(key.encode()).hexdigest()[:16]

    def get_vision(self, image_hash: str) -> Optional[dict]:
        cache_file = self.vision_cache / f"{image_hash}.json"
        if cache_file.exists():
            with open(cache_file, 'r') as f:
                print(f"[CACHE HIT] Vision: {image_hash}")
                return json.load(f)
        return None

    def set_vision(self, image_hash: str, data: dict):
        cache_file = self.vision_cache / f"{image_hash}.json"
        with open(cache_file, 'w') as f:
            json.dump(data, f, indent=2)
        print(f"[CACHE SAVE] Vision: {image_hash}")

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
        cache_file = self.music_cache / f"{cache_hash}.txt"

        with open(cache_file, 'w') as f:
            f.write(audio_path)
        print(f"[CACHE SAVE] Music: {description[:30]}..., seed {seed}")

    def clear_all(self):
        for cache_type in [self.vision_cache, self.narrative_cache, self.music_cache]:
            for file in cache_type.glob("*"):
                file.unlink()
        print("[CACHE] Cleared all caches")

    def stats(self) -> dict:
        return {
            "vision": len(list(self.vision_cache.glob("*.json"))),
            "narrative": len(list(self.narrative_cache.glob("*.json"))),
            "music": len(list(self.music_cache.glob("*.txt")))
        }
