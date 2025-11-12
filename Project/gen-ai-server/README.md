# Gen AI Server

FastAPI server for generative AI features (narrative + music generation).

## Project Structure

```
gen-ai-server/
├── src/
│   ├── generators/
│   │   ├── narrative.py    # LLM narrative generation with retry logic
│   │   ├── vision.py       # Vision analysis (moondream2)
│   │   └── music.py        # MusicGen audio generation
│   ├── models/
│   │   ├── requests.py     # Request schemas (Pydantic)
│   │   └── responses.py    # Response schemas (Pydantic)
│   └── utils/
│       ├── config.py       # Configuration management
│       ├── cache.py        # Caching system
│       └── health.py       # Ollama health checks
├── tests/
│   ├── test_narrative.py   # Narrative generation tests
│   ├── test_music.py       # Music generation tests
│   ├── test_vision_rooms.py # Vision analysis tests
│   └── test_server.py      # Full pipeline integration test
├── scripts/
│   └── clean.sh            # Cleanup cache and test outputs
├── main.py                 # Production server
└── .env.example            # Environment config template
```

## Setup

```bash
uv sync
```

## Run

```bash
uv run python main.py
```

Server runs on `http://localhost:8000`

## Configuration

Copy `.env.example` to `.env` and customize:

```bash
HOST=0.0.0.0
PORT=8000
NARRATIVE_MODEL=llama2
MUSIC_MODEL=facebook/musicgen-medium
OUTPUT_DIR=output
```

## API Endpoints

### `POST /generate/narrative`
Generate story narrative for a room with retry logic and story continuity.

```json
{
  "roomIndex": 0,
  "totalRooms": 9,
  "theme": "dark fantasy dungeon",
  "seed": 12345,
  "use_cache": true,
  "previous_context": "Room 0: Met Elder Sage..."
}
```

**Features:**
- Retry logic: 3 attempts with seed variation on failure
- Pydantic validation ensures proper JSON structure
- Story continuity via `previous_context`
- Server-side caching for faster regeneration

### `POST /generate/music`
Generate ambient music from description.

```json
{
  "description": "dark crypt with echoing water drops",
  "seed": 42,
  "duration": 30.0,
  "use_cache": true
}
```

### `POST /analyze/vision`
Analyze game screenshot and generate room description.

**Form data:**
- `file`: Image file (PNG/JPEG)
- `use_cache`: "true" or "false"

**Returns:**
```json
{
  "environment_type": "cave",
  "atmosphere": "Dark underground cavern with glowing crystals...",
  "features": ["stalagmites", "underground river", "crystal formations"],
  "mood": "mysterious and slightly eerie ambience"
}
```

### `GET /cache/stats`
Get cache statistics (narrative, music, vision counts).

### `POST /cache/clear`
Clear all cached data.

### `GET /audio/{filename}`
Retrieve generated audio file.

## Requirements

### 1. Ollama (for narrative generation)
```bash
# Install Ollama from https://ollama.ai
ollama serve

# In another terminal, pull the model
ollama pull llama2
```

The server will automatically check if Ollama is running and has the required model.

### 2. CUDA GPU (recommended for music generation)
- CPU will work but will be slower
- Music generation uses MusicGen which benefits from GPU acceleration

## Testing

Run individual tests:
```bash
# Test narrative generation (requires Ollama)
uv run python tests/test_narrative.py

# Test music generation (requires GPU/CPU, takes 30-60s)
uv run python tests/test_music.py

# Test full pipeline (requires server running)
# In terminal 1: uv run python main.py
# In terminal 2:
uv run python tests/test_server.py
```

Run all tests:
```bash
uv run pytest tests/
```

## Cleanup

Remove cache, test outputs, and generated files:
```bash
./scripts/clean.sh
```

This will remove:
- `cache/` - Cached generation results
- `tests/output/` - Test output files
- `output/*.wav` - Generated music files
- `temp_uploads/` - Temporary image uploads

## Future Improvements

### Batch Narrative Generation
**Current approach:** Generates narratives one room at a time with incremental context
**Proposed improvement:** Generate all room narratives in a single LLM prompt

**Benefits:**
- Better story coherence across entire dungeon
- More consistent narrative arc (beginning → middle → end)
- Reduced API calls and latency
- Stronger thematic connections between rooms

**Implementation:**
1. New endpoint: `POST /generate/narrative/batch`
2. Input: Array of room descriptions with spatial relationships
3. Output: Complete narrative structure for all rooms
4. LLM receives full dungeon layout to plan story progression

**Example prompt structure:**
```
Generate a cohesive story for a 9-room dungeon with the following layout:
Room 0 (entrance): [vision description]
Room 1 (north corridor): [vision description]
...
Room 8 (boss chamber): [vision description]

Create a unified narrative where:
- Story begins in room 0 and builds to climax in room 8
- NPCs and quests flow logically between connected rooms
- Lore entries reveal backstory progressively
- Theme and tone remain consistent
```

## Troubleshooting

### Server fails to start with MusicGen error
- Ensure Python 3.12 (not 3.14)
- Check `.python-version` file contains `3.12`
- Reinstall: `rm -rf .venv && uv sync`

### Ollama connection failed
- Start Ollama: `ollama serve`
- Pull model: `ollama pull llama2`
- Check running: `ollama list`

### Out of memory errors
- Use smaller models (MusicGen Small)
- Reduce audio duration
- Close other applications
