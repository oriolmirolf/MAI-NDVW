import hashlib
import asyncio
import concurrent.futures
from pathlib import Path
from typing import Optional
import torch


class VoiceGenerator:
    """Parler-TTS Mini voice generator with parallel batch processing."""

    # Deep male narrator voice description
    DEFAULT_DESCRIPTION = (
        "Gary's voice is deep and gravelly with slow, deliberate pacing. "
        "The recording is very clear with no background noise. "
        "He speaks like a dramatic movie narrator with authority and gravitas."
    )

    def __init__(self, output_dir: str = "output/voice", device: str = None, description: str = None):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)

        if device is None:
            device = "cuda" if torch.cuda.is_available() else "cpu"
        self.device = device
        self.description = description or self.DEFAULT_DESCRIPTION

        self.model = None
        self.tokenizer = None
        self.sample_rate = 44100  # Parler-TTS outputs 44.1kHz
        self._executor = concurrent.futures.ThreadPoolExecutor(max_workers=1)
        print(f"[VOICE] Parler-TTS Mini initialized (device={device}, lazy loading)")

    def _load_model(self):
        """Lazy load the Parler-TTS model on first use."""
        if self.model is None:
            print(f"[VOICE] Loading Parler-TTS Mini on {self.device}...")
            from parler_tts import ParlerTTSForConditionalGeneration
            from transformers import AutoTokenizer

            self.model = ParlerTTSForConditionalGeneration.from_pretrained(
                "parler-tts/parler-tts-mini-v1"
            ).to(self.device)

            self.tokenizer = AutoTokenizer.from_pretrained(
                "parler-tts/parler-tts-mini-v1"
            )

            # Compile model for faster inference (PyTorch 2.0+)
            if hasattr(torch, 'compile') and self.device == "cuda":
                try:
                    self.model = torch.compile(self.model, mode="reduce-overhead")
                    print("[VOICE] Model compiled with torch.compile")
                except Exception as e:
                    print(f"[VOICE] torch.compile failed (non-critical): {e}")

            print(f"[VOICE] Parler-TTS Mini loaded successfully")

    def _get_cache_path(self, text: str, voice_id: str, seed: int) -> Path:
        """Generate cache path based on content hash."""
        key = f"{text}_{voice_id}_{seed}_{self.description}"
        hash_val = hashlib.sha256(key.encode()).hexdigest()[:16]
        return self.output_dir / f"{hash_val}.wav"

    def _clean_text_for_tts(self, text: str) -> str:
        """Clean text to prevent TTS artifacts."""
        import re
        # Remove any non-ASCII characters
        text = text.encode('ascii', 'ignore').decode('ascii')
        # Remove multiple spaces
        text = re.sub(r'\s+', ' ', text)
        # Remove special characters that cause issues
        text = re.sub(r'[#@~`^*]', '', text)
        # Remove emotion markers like *cough*, *laugh*
        text = re.sub(r'\*[^*]+\*', '', text)
        # Ensure proper sentence endings
        text = text.strip()
        if text and text[-1] not in '.!?':
            text += '.'
        return text

    def _generate_sync(self, text: str, seed: int, cache_path: Path) -> str:
        """Synchronous generation for use in thread pool."""
        import soundfile as sf

        self._load_model()

        clean_text = self._clean_text_for_tts(text)
        print(f"[VOICE] Generating: '{clean_text[:50]}...'")

        # Tokenize description and text
        input_ids = self.tokenizer(
            self.description, return_tensors="pt"
        ).input_ids.to(self.device)

        prompt_input_ids = self.tokenizer(
            clean_text, return_tensors="pt"
        ).input_ids.to(self.device)

        # Set seed for reproducibility
        torch.manual_seed(seed)
        if self.device == "cuda":
            torch.cuda.manual_seed(seed)

        # Generate audio
        with torch.inference_mode():
            generation = self.model.generate(
                input_ids=input_ids,
                prompt_input_ids=prompt_input_ids,
            )

        # Convert to numpy and save
        audio_arr = generation.cpu().numpy().squeeze()

        # Ensure output directory exists
        self.output_dir.mkdir(parents=True, exist_ok=True)

        sf.write(str(cache_path), audio_arr, self.sample_rate)
        duration = len(audio_arr) / self.sample_rate
        print(f"[VOICE] Saved: {cache_path.name} ({duration:.2f}s)")

        return str(cache_path)

    async def generate(
        self,
        text: str,
        voice_id: str = "default",
        seed: int = 12345,
        description: str = None,
    ) -> str:
        """
        Generate voice audio from text using Parler-TTS Mini.

        Args:
            text: Text to synthesize
            voice_id: Voice identifier for caching
            seed: Random seed for reproducibility
            description: Optional custom voice description (not used, kept for API compat)

        Returns:
            Path to generated WAV file
        """
        cache_path = self._get_cache_path(text, voice_id, seed)
        if cache_path.exists():
            print(f"[VOICE] Cache hit: {cache_path.name}")
            return str(cache_path)

        # Run generation in thread pool to not block event loop
        loop = asyncio.get_event_loop()
        result = await loop.run_in_executor(
            self._executor,
            self._generate_sync,
            text,
            seed,
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
        """Generate audio for multiple texts in parallel."""
        # Create tasks for all texts
        tasks = [
            self.generate(
                text,
                voice_id=voice_id,
                seed=seed + i,
                description=description
            )
            for i, text in enumerate(texts)
        ]

        # Run all tasks concurrently
        results = await asyncio.gather(*tasks)
        return list(results)

    def get_relative_path(self, full_path: str) -> str:
        """Convert full path to relative path for serving."""
        return Path(full_path).name

    def __del__(self):
        """Cleanup executor on deletion."""
        if hasattr(self, '_executor'):
            self._executor.shutdown(wait=False)
