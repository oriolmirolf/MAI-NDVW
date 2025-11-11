from pydantic import BaseModel, Field
from typing import List

class NPCResponse(BaseModel):
    name: str
    dialogue: List[str]

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
