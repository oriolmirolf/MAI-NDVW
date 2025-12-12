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
    victory: Optional[NPCResponse] = None  # Victory dialogue after defeating boss

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
