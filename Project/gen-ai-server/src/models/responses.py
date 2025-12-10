from pydantic import BaseModel, Field
from typing import List, Dict, Optional

class NPCResponse(BaseModel):
    name: str
    dialogue: List[str]
    audio_paths: Optional[List[str]] = None

class QuestResponse(BaseModel):
    objective: str
    type: str
    count: int

class LoreResponse(BaseModel):
    title: str
    content: str

class NarrativeResponse(BaseModel):
    roomIndex: int
    environment: str
    npc: NPCResponse
    quest: QuestResponse
    lore: LoreResponse

class VisionResponse(BaseModel):
    environment_type: str = Field(..., min_length=1, max_length=100)
    atmosphere: str = Field(..., min_length=10, max_length=500)
    features: List[str] = Field(..., min_items=3, max_items=10)
    mood: str = Field(..., min_length=10, max_length=500)

class MusicResponse(BaseModel):
    path: str
    seed: int

class EnemySpawn(BaseModel):
    type: str
    count: int = Field(..., ge=0, le=10)

class RoomContent(BaseModel):
    room_id: int
    enemies: List[EnemySpawn]
    description: str

class DungeonContentResponse(BaseModel):
    seed: int
    theme: str
    rooms: Dict[str, RoomContent]  # key is room_id as string
