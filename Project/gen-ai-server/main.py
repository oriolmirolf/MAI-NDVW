from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse
from contextlib import asynccontextmanager
import uvicorn
import sys
import warnings
import logging
from pathlib import Path

from src.generators.narrative import NarrativeGenerator
from src.generators.music import MusicGenerator
from src.generators.dungeon import DungeonContentGenerator
from src.generators.voice import VoiceGenerator
from src.models.requests import NarrativeRequest, MusicRequest, DungeonContentRequest
from src.models.responses import MusicResponse, DungeonContentResponse
from src.utils.config import settings
from src.utils.health import check_ollama
from src.utils.cache import CacheManager

# Suppress verbose transformers warnings
warnings.filterwarnings("ignore", category=UserWarning)
logging.getLogger("transformers").setLevel(logging.ERROR)

narrative_gen = None
music_gen = None
dungeon_gen = None
voice_gen = None
cache_manager = None

@asynccontextmanager
async def lifespan(app: FastAPI):
    global narrative_gen, music_gen, dungeon_gen, voice_gen, cache_manager

    if not check_ollama(settings.narrative_model):
        print("ERROR: Ollama check failed. Exiting.")
        sys.exit(1)

    print("Initializing AI generators...")
    cache_manager = CacheManager()
    voice_gen = VoiceGenerator(output_dir="output/voice")
    narrative_gen = NarrativeGenerator(model=settings.narrative_model, voice_generator=voice_gen)

    if settings.enable_music:
        music_gen = MusicGenerator(
            model=settings.music_model,
            output_dir=settings.output_dir
        )
    else:
        print("[MUSIC] Disabled (set ENABLE_MUSIC=true to enable)")

    dungeon_gen = DungeonContentGenerator(model=settings.narrative_model)
    print("Server ready")

    cache_stats = cache_manager.stats()
    print(f"Cache stats: {cache_stats}")

    yield

    print("Shutting down...")
    print("Shutdown complete")

app = FastAPI(title="Gen AI Server", lifespan=lifespan)

@app.get("/")
def root():
    return {
        "status": "online",
        "services": ["narrative", "music", "voice", "dungeon"],
        "models": {
            "narrative": settings.narrative_model,
            "music": settings.music_model,
            "voice": "xtts-v2",
            "dungeon": settings.narrative_model
        },
        "cache_stats": cache_manager.stats()
    }

@app.post("/generate/narrative")
async def generate_narrative(request: NarrativeRequest):
    """Generate narrative for a chapter (0, 1, or 2)."""
    try:
        chapter = request.roomIndex  # Treat roomIndex as chapter for simplicity

        if request.use_cache:
            cached = cache_manager.get_narrative(chapter, 3, "chapter", request.seed)
            if cached:
                print(f"[NARRATIVE] Cache hit for chapter {chapter}")
                return cached

        print(f"[NARRATIVE] Generating for chapter {chapter}")
        narrative = await narrative_gen.generate_chapter(chapter, request.seed)

        narrative_dict = narrative.model_dump() if hasattr(narrative, 'model_dump') else narrative

        if request.use_cache:
            cache_manager.set_narrative(chapter, 3, "chapter", request.seed, narrative_dict)

        return narrative_dict
    except Exception as e:
        print(f"Narrative generation failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/generate/music", response_model=MusicResponse)
async def generate_music(request: MusicRequest):
    if music_gen is None:
        raise HTTPException(status_code=503, detail="Music model is disabled. Set ENABLE_MUSIC=true to enable.")
    try:
        seed = request.seed if request.seed is not None else music_gen.last_seed

        # Build cache key from chapter or description
        cache_key = f"chapter_{request.chapter}" if request.chapter is not None else request.description
        if request.use_cache and cache_key:
            cached_path = cache_manager.get_music(cache_key, seed, request.duration)
            if cached_path:
                return MusicResponse(path=cached_path, seed=seed)

        desc_preview = f"chapter {request.chapter}" if request.chapter is not None else (request.description[:50] if request.description else "default")
        print(f"[MUSIC] Generating: {desc_preview}...")
        file_path = await music_gen.generate(
            description=request.description,
            chapter=request.chapter,
            seed=request.seed,
            duration=request.duration
        )

        if request.use_cache and cache_key:
            cache_manager.set_music(cache_key, music_gen.last_seed, request.duration, file_path)

        return MusicResponse(path=file_path, seed=music_gen.last_seed)
    except Exception as e:
        print(f"Music generation failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/generate/dungeon-content", response_model=DungeonContentResponse)
async def generate_dungeon_content(request: DungeonContentRequest):
    try:
        cache_key = f"{request.seed}_{len(request.rooms)}_{request.theme}"

        if request.use_cache:
            cached = cache_manager.get_dungeon(cache_key)
            if cached:
                print(f"[API] Dungeon content cache hit")
                return cached

        print(f"[API] Generating dungeon content for {len(request.rooms)} rooms")
        result = await dungeon_gen.generate(
            seed=request.seed,
            theme=request.theme,
            rooms=request.rooms,
            available_enemies=request.available_enemies
        )

        result_dict = result.model_dump()

        if request.use_cache:
            cache_manager.set_dungeon(cache_key, result_dict)

        return result_dict
    except Exception as e:
        print(f"[API] Dungeon content generation failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.get("/cache/stats")
def get_cache_stats():
    return cache_manager.stats()

@app.post("/cache/clear")
def clear_cache():
    cache_manager.clear_all()
    return {"message": "Cache cleared", "stats": cache_manager.stats()}

@app.get("/audio/{filename}")
def get_audio(filename: str):
    file_path = f"{settings.output_dir}/{filename}"
    return FileResponse(file_path, media_type="audio/wav")

@app.get("/voice/{filename}")
def get_voice(filename: str):
    file_path = f"output/voice/{filename}"
    if not Path(file_path).exists():
        raise HTTPException(status_code=404, detail="Voice file not found")
    return FileResponse(file_path, media_type="audio/wav")

if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host=settings.host,
        port=settings.port,
        reload=False
    )
