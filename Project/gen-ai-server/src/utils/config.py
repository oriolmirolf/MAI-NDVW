from pydantic_settings import BaseSettings
from typing import Optional

class Settings(BaseSettings):
    host: str = "0.0.0.0"
    port: int = 8000
    narrative_model: str = "llama2"
    music_model: str = "facebook/musicgen-medium"
    output_dir: str = "output"

    class Config:
        env_file = ".env"
        case_sensitive = False

settings = Settings()
