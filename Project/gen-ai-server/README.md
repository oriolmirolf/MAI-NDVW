# Gen AI - Asset Pre-Generation

Pre-generates narrative, music, and voice assets for the game. Assets are saved to Unity's StreamingAssets folder and loaded at runtime.

## Project Structure

```
gen-ai-server/
├── generate.py      # Main generation script
├── config.json      # All prompts and settings
├── samples/         # Voice reference samples
├── scripts/         # Helper scripts
└── .env             # Environment config
```

## Requirements

### 1. Ollama (narrative generation)
```bash
# Install from https://ollama.ai
ollama serve
ollama pull mistral-nemo
```

### 2. Python 3.12
```bash
uv sync
```

### 3. CUDA GPU (recommended)
- Required for music generation (MusicGen)
- Required for voice synthesis (XTTS v2)
- CPU works but is much slower

## Configuration

Edit `config.json` to customize:

```json
{
  "seed": 54321,
  "generate": {
    "narrative": true,
    "music": true,
    "voice": true
  }
}
```

### Key Settings

| Setting | Description |
|---------|-------------|
| `seed` | Random seed for reproducible generation |
| `generate.narrative` | Enable/disable narrative generation |
| `generate.music` | Enable/disable music generation |
| `generate.voice` | Enable/disable voice synthesis |
| `models.narrative` | Ollama model for narratives |
| `models.music` | HuggingFace model for music |
| `music.duration` | Music length in seconds |
| `chapters` | Chapter definitions (story, prompts, etc.) |

## Usage

### Step 1: Generate Assets

```bash
cd Project/gen-ai-server
uv run python generate.py
```

This generates:
- `chapters/chapter_X/narrative.json` - Dialogue and lore
- `chapters/chapter_X/music.wav` - Background music
- `chapters/chapter_X/voice_0.wav`, `voice_1.wav`, `voice_2.wav` - Voice lines

Output: `Project/Assets/StreamingAssets/GeneratedContent/`

### Step 2: Run Unity

1. Open Unity project
2. Press Play

The game automatically loads generated content from StreamingAssets. If no generated content exists, fallback narratives are used (no music/voice).

## Generation Output

Files are saved to **two locations**:

1. **Unity StreamingAssets** (for the game):
   ```
   Assets/StreamingAssets/GeneratedContent/
   ```

2. **Local copy** (for reference/backup):
   ```
   gen-ai-server/output/
   ```

Structure:
```
chapters/
├── chapter_0/
│   ├── narrative.json
│   ├── music.wav
│   ├── voice_0.wav
│   ├── voice_1.wav
│   └── voice_2.wav
├── chapter_1/
│   └── ...
└── chapter_2/
    └── ...
```

## Chapters

The game has 3 chapters defined in `config.json`:

| Chapter | Name | Boss |
|---------|------|------|
| 0 | The Verdant Prison | Thornback |
| 1 | The Eternal Night | The Shade |
| 2 | The Burning End | The Scorcher |

Each chapter has its own narrative context, music prompt, and environment description.

## Troubleshooting

### Ollama not found
```bash
ollama serve          # Start Ollama
ollama pull mistral-nemo  # Pull model
```

### CUDA out of memory
- Close other GPU applications
- Use `facebook/musicgen-small` instead of medium
- Reduce `music.duration` in config

### Generation fails
- Check Ollama is running: `ollama list`
- Verify model exists: `ollama show mistral-nemo`
- Check Python version: `python --version` (needs 3.12)

## Cleanup

Remove generated content:
```bash
# Remove Unity content
rm -rf ../Assets/StreamingAssets/GeneratedContent

# Remove local copy
rm -rf output
```
