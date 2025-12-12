from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    host: str = "0.0.0.0"
    port: int = 8000
    narrative_model: str = "mistral-nemo"
    music_model: str = "facebook/musicgen-medium"
    output_dir: str = "output"
    enable_music: bool = True

    class Config:
        env_file = ".env"
        case_sensitive = False

settings = Settings()
