from fastapi import FastAPI, HTTPException, UploadFile, File
from fastapi.responses import FileResponse
from contextlib import asynccontextmanager
import uvicorn
import sys
import hashlib
import warnings
import logging
from pathlib import Path

from src.generators.narrative import NarrativeGenerator
from src.generators.music import MusicGenerator
from src.generators.vision import VisionGenerator
from src.models.requests import NarrativeRequest, MusicRequest, VisionRequest
from src.models.responses import MusicResponse, VisionResponse
from src.utils.config import settings
from src.utils.health import check_ollama
from src.utils.cache import CacheManager

# Suppress verbose transformers warnings
warnings.filterwarnings("ignore", category=UserWarning)
logging.getLogger("transformers").setLevel(logging.ERROR)

narrative_gen = None
music_gen = None
vision_gen = None
cache_manager = None

@asynccontextmanager
async def lifespan(app: FastAPI):
    global narrative_gen, music_gen, vision_gen, cache_manager

    if not check_ollama(settings.narrative_model):
        print("ERROR: Ollama check failed. Exiting.")
        sys.exit(1)

    print("Initializing AI generators...")
    cache_manager = CacheManager()
    narrative_gen = NarrativeGenerator(model=settings.narrative_model)
    music_gen = MusicGenerator(
        model=settings.music_model,
        output_dir=settings.output_dir
    )
    vision_gen = VisionGenerator()
    print("Server ready")

    cache_stats = cache_manager.stats()
    print(f"Cache stats: {cache_stats}")

    yield

    print("Shutting down...")
    if vision_gen is not None:
        print("Cleaning up vision model...")
        del vision_gen
        import torch
        torch.cuda.empty_cache()
    print("Shutdown complete")

app = FastAPI(title="Gen AI Server", lifespan=lifespan)

@app.get("/")
def root():
    return {
        "status": "online",
        "services": ["narrative", "music", "vision"],
        "models": {
            "narrative": settings.narrative_model,
            "music": settings.music_model,
            "vision": "moondream2"
        },
        "cache_stats": cache_manager.stats()
    }

@app.post("/generate/narrative")
async def generate_narrative(request: NarrativeRequest):
    try:
        if request.use_cache:
            cached = cache_manager.get_narrative(
                request.roomIndex, request.totalRooms, request.theme, request.seed
            )
            if cached:
                return cached

        print(f"[NARRATIVE] Generating for room {request.roomIndex}/{request.totalRooms}")
        if request.previous_context:
            print(f"[NARRATIVE] Using story context from previous rooms")
        narrative = await narrative_gen.generate(
            request.roomIndex,
            request.totalRooms,
            request.theme,
            request.seed,
            request.previous_context
        )

        narrative_dict = narrative.model_dump() if hasattr(narrative, 'model_dump') else narrative

        if request.use_cache:
            cache_manager.set_narrative(
                request.roomIndex, request.totalRooms, request.theme, request.seed, narrative_dict
            )

        return narrative_dict
    except Exception as e:
        print(f"Narrative generation failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/generate/music", response_model=MusicResponse)
async def generate_music(request: MusicRequest):
    try:
        seed = request.seed if request.seed is not None else music_gen.last_seed

        if request.use_cache:
            cached_path = cache_manager.get_music(request.description, seed, request.duration)
            if cached_path:
                return MusicResponse(path=cached_path, seed=seed)

        print(f"Generating music: {request.description[:50]}...")
        file_path = await music_gen.generate(
            request.description,
            request.seed,
            request.duration
        )

        if request.use_cache:
            cache_manager.set_music(request.description, music_gen.last_seed, request.duration, file_path)

        return MusicResponse(path=file_path, seed=music_gen.last_seed)
    except Exception as e:
        print(f"Music generation failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/analyze/vision", response_model=VisionResponse)
async def analyze_vision(file: UploadFile = File(...), use_cache: str = "true"):
    global vision_gen
    try:
        # Parse use_cache from form data (comes as string)
        cache_enabled = use_cache.lower() in ("true", "1", "yes")
        print(f"[API] Vision request: {file.filename} (cache={'ON' if cache_enabled else 'OFF'})")
        contents = await file.read()
        image_hash = hashlib.sha256(contents).hexdigest()[:16]

        if cache_enabled:
            cached = cache_manager.get_vision(image_hash)
            if cached:
                return cached

        temp_path = Path("temp_uploads") / f"{image_hash}.png"
        temp_path.parent.mkdir(exist_ok=True)

        with open(temp_path, "wb") as f:
            f.write(contents)

        result = await vision_gen.describe_room(str(temp_path))

        temp_path.unlink()

        if cache_enabled:
            cache_manager.set_vision(image_hash, result)

        print(f"[API] ✓ Vision analysis complete")
        return result
    except Exception as e:
        print(f"[API] ✗ Vision analysis failed: {e}")
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

if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host=settings.host,
        port=settings.port,
        reload=False
    )
