from pydantic import BaseModel, Field
from typing import Optional

class NarrativeRequest(BaseModel):
    roomIndex: int = Field(..., ge=0)
    totalRooms: int = Field(..., ge=1)
    theme: str = "dark fantasy dungeon"
    seed: int = 12345
    use_cache: bool = True
    previous_context: Optional[str] = None

class MusicRequest(BaseModel):
    description: str
    seed: Optional[int] = None
    duration: float = Field(30.0, gt=0, le=120)
    use_cache: bool = True

class VisionRequest(BaseModel):
    use_cache: bool = True
