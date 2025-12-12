import hashlib
import asyncio
import concurrent.futures
import os
import re
from pathlib import Path
from typing import Optional
import torch

# Auto-accept Coqui TTS license
os.environ["COQUI_TOS_AGREED"] = "1"


class VoiceGenerator:
    """XTTS v2 voice cloning generator."""

    def __init__(
        self,
        output_dir: str = "output/voice",
        speaker_wav: str = "tests/data/Nature Documentary Narration.wav",
        device: str = None
    ):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)

        self.speaker_wav = Path(speaker_wav)
        if not self.speaker_wav.exists():
            # Try mp3 version
            mp3_path = self.speaker_wav.with_suffix('.mp3')
            if mp3_path.exists():
                self.speaker_wav = mp3_path
            else:
                print(f"[VOICE] WARNING: Speaker sample not found: {speaker_wav}")

        if device is None:
            device = "cuda" if torch.cuda.is_available() else "cpu"
        self.device = device

        self.tts = None
        self.sample_rate = 24000
        self._executor = concurrent.futures.ThreadPoolExecutor(max_workers=1)
        print(f"[VOICE] XTTS v2 initialized (device={device}, lazy loading)")

    def _load_model(self):
        """Lazy load the XTTS model on first use."""
        if self.tts is None:
            print(f"[VOICE] Loading XTTS v2 on {self.device}...")
            from TTS.api import TTS

            self.tts = TTS("tts_models/multilingual/multi-dataset/xtts_v2").to(self.device)
            print(f"[VOICE] XTTS v2 loaded successfully")

    def _get_cache_path(self, text: str, seed: int) -> Path:
        """Generate cache path based on content hash."""
        key = f"{text}_{seed}_{self.speaker_wav.name}"
        hash_val = hashlib.sha256(key.encode()).hexdigest()[:16]
        return self.output_dir / f"{hash_val}.wav"

    def _clean_text_for_tts(self, text: str) -> str:
        """Clean text to prevent TTS artifacts."""
        # Remove action markers like *cough*, (laughs), [sighs]
        text = re.sub(r'\*[^*]+\*', '', text)
        text = re.sub(r'\([^)]+\)', '', text)
        text = re.sub(r'\[[^\]]+\]', '', text)

        # Remove problematic characters but keep basic punctuation
        text = re.sub(r'[#@~`^<>{}|\\]', '', text)

        # Normalize quotes and apostrophes
        text = text.replace('"', "'").replace('"', "'").replace('"', "'")
        text = text.replace(''', "'").replace(''', "'")

        # Remove repeated punctuation
        text = re.sub(r'([.!?,])\1+', r'\1', text)

        # Clean up whitespace
        text = re.sub(r'\s+', ' ', text).strip()

        # Ensure text ends with punctuation
        if text and text[-1] not in '.!?':
            text += '.'

        return text

    def _generate_sync(self, text: str, cache_path: Path) -> str:
        """Synchronous generation for use in thread pool."""
        self._load_model()

        clean_text = self._clean_text_for_tts(text)
        if not clean_text or len(clean_text) < 3:
            clean_text = "Hello there."

        print(f"[VOICE] Generating: '{clean_text[:60]}...'")

        try:
            self.tts.tts_to_file(
                text=clean_text,
                file_path=str(cache_path),
                speaker_wav=str(self.speaker_wav),
                language="en"
            )
            print(f"[VOICE] Saved: {cache_path.name}")
            return str(cache_path)
        except Exception as e:
            print(f"[VOICE] Generation failed: {e}")
            raise

    async def generate(
        self,
        text: str,
        voice_id: str = "default",
        seed: int = 12345,
        description: str = None,
    ) -> str:
        """Generate voice audio using XTTS v2 voice cloning."""
        cache_path = self._get_cache_path(text, seed)
        if cache_path.exists():
            print(f"[VOICE] Cache hit: {cache_path.name}")
            return str(cache_path)

        loop = asyncio.get_event_loop()
        result = await loop.run_in_executor(
            self._executor,
            self._generate_sync,
            text,
            cache_path
        )
        return result

    async def generate_batch(
        self,
        texts: list[str],
        voice_id: str = "default",
        seed: int = 12345,
        description: str = None
    ) -> list[str]:
        """Generate audio for multiple texts sequentially."""
        results = []
        for i, text in enumerate(texts):
            path = await self.generate(
                text,
                voice_id=voice_id,
                seed=seed + i,
                description=description
            )
            results.append(path)
        return results

    def get_relative_path(self, full_path: str) -> str:
        """Convert full path to relative path for serving."""
        return Path(full_path).name

    def __del__(self):
        """Cleanup executor on deletion."""
        if hasattr(self, '_executor'):
            self._executor.shutdown(wait=False)
