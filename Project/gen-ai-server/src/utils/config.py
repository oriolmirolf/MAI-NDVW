from pydantic_settings import BaseSettings
from typing import Optional

class Settings(BaseSettings):
    host: str = "0.0.0.0"
    port: int = 8000
    narrative_model: str = "llama2"
    music_model: str = "facebook/musicgen-medium"
    output_dir: str = "output"
    enable_vision: bool = False  # Disable by default to save ~2GB VRAM
    enable_music: bool = True

    class Config:
        env_file = ".env"
        case_sensitive = False

settings = Settings()
