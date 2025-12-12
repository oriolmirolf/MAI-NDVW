from pydantic import BaseModel, Field
from typing import Optional, List, Dict

class NarrativeRequest(BaseModel):
    roomIndex: int = Field(..., ge=0)
    totalRooms: int = Field(..., ge=1)
    theme: str = "2D pixel art RPG adventure"
    seed: int = 12345
    use_cache: bool = True
    previous_context: Optional[str] = None

class MusicRequest(BaseModel):
    description: Optional[str] = None
    chapter: Optional[int] = Field(None, ge=0, le=2)  # 0=Forest, 1=Night, 2=Desert
    seed: Optional[int] = None
    duration: float = Field(30.0, gt=0, le=120)
    use_cache: bool = True

class RoomInfo(BaseModel):
    id: int
    connections: List[str]  # ["north", "south", "east", "west"]
    is_start: bool = False
    is_boss: bool = False

class DungeonContentRequest(BaseModel):
    seed: int = 12345
    theme: str = "pixel art RPG"
    rooms: List[RoomInfo]
    available_enemies: List[str] = ["slime", "ghost", "grape"]
    use_cache: bool = True
