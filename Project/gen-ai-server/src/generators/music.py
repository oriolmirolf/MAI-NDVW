import os
import random
import time
import numpy as np
import soundfile as sf
import torch
from transformers import AutoProcessor, MusicgenForConditionalGeneration
from pathlib import Path

class MusicGenerator:
    # Chapter-specific music - Fast arcade action style
    CHAPTER_MUSIC = {
        0: "energetic chiptune music, fast 8-bit synth melody, driving beat, upbeat and action-packed, 140 bpm, retro arcade game soundtrack",
        1: "intense electronic music, pulsing synth bass, rapid arpeggios, mysterious and urgent, 150 bpm, action video game soundtrack",
        2: "epic fast-paced orchestral, intense percussion, heroic brass fanfare, triumphant and exciting, 160 bpm, boss battle soundtrack"
    }

    def __init__(self, model: str = "facebook/musicgen-medium", output_dir: str = "output"):
        self.model_name = model
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(exist_ok=True)
        self.sample_rate = 32000
        self.last_seed = None

        device = "cuda" if torch.cuda.is_available() else "cpu"
        self.device = device

        print(f"Loading music model {model} on {device}...")
        self.processor = AutoProcessor.from_pretrained(model)
        dtype = torch.float16 if device == "cuda" else torch.float32
        self.model = MusicgenForConditionalGeneration.from_pretrained(model).to(device, dtype=dtype)
        print("Music generator ready")

    async def generate(self, description: str = None, chapter: int = None, seed: int = None, duration: float = 30.0) -> str:
        if seed is None:
            seed = random.randint(0, 2**31 - 1)
        self.last_seed = seed

        # Use chapter-specific description if chapter is provided
        if chapter is not None and chapter in self.CHAPTER_MUSIC:
            description = self.CHAPTER_MUSIC[chapter]
            print(f"[MUSIC] Using chapter {chapter} theme")
        elif description is None:
            description = "whimsical fantasy MIDI music, playful melody, cheerful, 90 bpm, video game soundtrack"

        generator = torch.Generator(device=self.device).manual_seed(seed)
        prompt = self._make_prompt(description)

        print(f"Generating {duration}s music (seed: {seed})")
        start = time.time()

        inputs = self.processor(text=[prompt], padding=True, return_tensors="pt").to(self.device)
        max_tokens = int(duration * self.model.config.audio_encoder.frame_rate)

        audio_values = self.model.generate(
            **inputs,
            max_new_tokens=max_tokens,
            do_sample=True,
            guidance_scale=3.5,
        )

        elapsed = time.time() - start
        print(f"Generated in {elapsed:.1f}s")

        audio = audio_values[0].cpu().numpy()
        if audio.ndim > 1:
            audio = np.mean(audio, axis=0).astype(np.float32)

        audio = self._normalize(audio)
        audio = self._crossfade_loop(audio, crossfade_s=2.0)

        output_path = self.output_dir / f"music_{seed}.wav"
        sf.write(str(output_path), audio, self.sample_rate, subtype="PCM_16")

        print(f"Saved: {output_path}")
        return str(output_path)

    def _make_prompt(self, description: str) -> str:
        return f"{description}, loopable, no vocals"

    def _normalize(self, audio: np.ndarray) -> np.ndarray:
        peak = np.max(np.abs(audio)) + 1e-6
        return (audio / peak) * 0.8

    def _crossfade_loop(self, audio: np.ndarray, crossfade_s: float) -> np.ndarray:
        n = len(audio)
        k = int(min(n // 4, crossfade_s * self.sample_rate))
        if k <= 0:
            return audio

        head = audio[:k].copy()
        tail = audio[-k:].copy()
        fade_out = np.linspace(1.0, 0.0, k, dtype=np.float32)
        fade_in = 1.0 - fade_out

        mixed = head * fade_in + tail * fade_out
        middle = audio[k:-k] if n > 2 * k else np.zeros(0, dtype=audio.dtype)

        return np.concatenate([mixed, middle])
