#!/usr/bin/env python3
"""
Pre-generation script for game assets.
Generates narratives, music, and voice files based on config.json.
Output goes to Unity's StreamingAssets folder.

Usage:
    python generate.py
"""

import json
import os
import re
import shutil
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime
from pathlib import Path

import numpy as np
import ollama
import soundfile as sf
import torch
from transformers import AutoProcessor, MusicgenForConditionalGeneration

os.environ["COQUI_TOS_AGREED"] = "1"

def log(tag: str, msg: str, indent: int = 0):
    timestamp = datetime.now().strftime("%H:%M:%S")
    prefix = "  " * indent
    print(f"[{timestamp}] [{tag}] {prefix}{msg}")

def log_progress(tag: str, current: int, total: int, msg: str):
    timestamp = datetime.now().strftime("%H:%M:%S")
    bar_width = 20
    filled = int(bar_width * current / total)
    bar = "█" * filled + "░" * (bar_width - filled)
    print(f"[{timestamp}] [{tag}] [{bar}] {current}/{total} {msg}")

def load_config() -> dict:
    config_path = Path(__file__).parent / "config.json"
    with open(config_path) as f:
        return json.load(f)

def check_ollama(model: str) -> bool:
    try:
        ollama.show(model)
        log("OLLAMA", f"Model '{model}' available")
        return True
    except Exception as e:
        log("OLLAMA", f"Model '{model}' not found: {e}")
        log("OLLAMA", f"Run: ollama pull {model}")
        return False


class NarrativeGenerator:
    def __init__(self, config: dict):
        self.config = config
        self.model = config["models"]["narrative"]
        self.temperature = config["narrative"]["temperature"]
        self.max_retries = config["narrative"]["max_retries"]
        self.prompt_template = config["prompts"]["narrative_template"]

    def generate(self, chapter_idx: int, seed: int) -> dict:
        chapter = self.config["chapters"][chapter_idx]
        prompt = self.prompt_template.format(
            chapter_name=chapter["name"],
            boss=chapter["boss"],
            progress_context=chapter["progress_context"]
        )

        for attempt in range(self.max_retries):
            try:
                log("NARRATIVE", f"Chapter {chapter_idx} attempt {attempt + 1}/{self.max_retries}...", 1)
                start = time.time()

                response = ollama.generate(
                    model=self.model,
                    prompt=prompt,
                    options={"temperature": self.temperature, "seed": seed + chapter_idx + attempt},
                    stream=False
                )

                dialogue = self._parse_response(response["response"])
                elapsed = time.time() - start

                if len(dialogue) >= 3:
                    log("NARRATIVE", f"Chapter {chapter_idx} done in {elapsed:.1f}s - {len(dialogue)} lines", 1)
                    return self._build_narrative(chapter_idx, chapter, dialogue[:3])

                log("NARRATIVE", f"Chapter {chapter_idx} only got {len(dialogue)} lines, retrying...", 1)
            except Exception as e:
                log("NARRATIVE", f"Chapter {chapter_idx} attempt {attempt + 1} failed: {e}", 1)

        raise RuntimeError(f"Failed to generate narrative for chapter {chapter_idx}")

    def _parse_response(self, response: str) -> list:
        lines = []
        skip_phrases = ['sure', 'here are', 'here is', 'certainly', 'of course', "i'll", 'i will', 'let me']

        for line in response.strip().split('\n'):
            lower = line.lower().strip()
            if any(phrase in lower for phrase in skip_phrases):
                continue
            if lower.startswith(('okay', 'sure', 'certainly')):
                continue

            cleaned = re.sub(r'^[\d]+[.\):\-]\s*', '', line.strip())
            cleaned = cleaned.strip('"\'')
            cleaned = re.sub(r'\*[^*]+\*', '', cleaned)
            cleaned = re.sub(r'\([^)]+\)', '', cleaned)
            cleaned = re.sub(r'\[[^\]]+\]', '', cleaned)
            cleaned = cleaned.strip()

            if cleaned and len(cleaned) > 30 and not cleaned.lower().startswith('write'):
                lines.append(cleaned)

        return lines

    def _build_narrative(self, idx: int, chapter: dict, dialogue: list) -> dict:
        story = self.config["story"]["intro"] + "\n\n" + chapter["story"] if idx == 0 else chapter["story"]
        return {
            "roomIndex": idx,
            "environment": chapter["environment"],
            "npc": {"name": "The Narrator", "dialogue": dialogue},
            "quest": {"objective": f"Defeat {chapter['boss']} to proceed", "type": "Boss", "count": 1},
            "lore": {"title": chapter["name"], "content": story}
        }


class MusicGenerator:
    def __init__(self, config: dict):
        self.config = config
        self.model_name = config["models"]["music"]
        self.duration = config["music"]["duration"]
        self.guidance_scale = config["music"]["guidance_scale"]
        self.crossfade_s = config["music"]["crossfade_seconds"]
        self.music_suffix = config["prompts"]["music_suffix"]
        self.sample_rate = 32000
        self.model = None
        self.processor = None
        self.device = "cuda" if torch.cuda.is_available() else "cpu"

    def _load_model(self):
        if self.model is None:
            log("MUSIC", f"Loading {self.model_name} on {self.device}...")
            start = time.time()
            self.processor = AutoProcessor.from_pretrained(self.model_name)
            dtype = torch.float16 if self.device == "cuda" else torch.float32
            self.model = MusicgenForConditionalGeneration.from_pretrained(self.model_name).to(self.device, dtype=dtype)
            log("MUSIC", f"Model loaded in {time.time() - start:.1f}s")

    def generate(self, chapter_idx: int, seed: int, output_path: Path):
        self._load_model()
        chapter = self.config["chapters"][chapter_idx]
        prompt = chapter["music_prompt"] + self.music_suffix

        log("MUSIC", f"Chapter {chapter_idx}: Generating {self.duration}s audio...", 1)
        log("MUSIC", f"Prompt: {prompt[:60]}...", 2)
        start = time.time()

        inputs = self.processor(text=[prompt], padding=True, return_tensors="pt").to(self.device)
        max_tokens = int(self.duration * self.model.config.audio_encoder.frame_rate)

        with torch.no_grad():
            audio_values = self.model.generate(
                **inputs,
                max_new_tokens=max_tokens,
                do_sample=True,
                guidance_scale=self.guidance_scale,
            )

        audio = audio_values[0].cpu().numpy()
        if audio.ndim > 1:
            audio = np.mean(audio, axis=0).astype(np.float32)

        audio = self._normalize(audio)
        audio = self._crossfade_loop(audio)

        sf.write(str(output_path), audio, self.sample_rate, subtype="PCM_16")
        elapsed = time.time() - start
        log("MUSIC", f"Chapter {chapter_idx}: Done in {elapsed:.1f}s ({len(audio)/self.sample_rate:.1f}s audio)", 1)

    def _normalize(self, audio: np.ndarray) -> np.ndarray:
        peak = np.max(np.abs(audio)) + 1e-6
        return (audio / peak) * 0.8

    def _crossfade_loop(self, audio: np.ndarray) -> np.ndarray:
        n = len(audio)
        k = int(min(n // 4, self.crossfade_s * self.sample_rate))
        if k <= 0:
            return audio
        head, tail = audio[:k].copy(), audio[-k:].copy()
        fade_out = np.linspace(1.0, 0.0, k, dtype=np.float32)
        mixed = head * (1 - fade_out) + tail * fade_out
        middle = audio[k:-k] if n > 2 * k else np.zeros(0, dtype=audio.dtype)
        return np.concatenate([mixed, middle])


class VoiceGenerator:
    def __init__(self, config: dict):
        self.speaker_wav = Path(__file__).parent / config["voice"]["speaker_sample"]
        self.tts = None
        self.device = "cuda" if torch.cuda.is_available() else "cpu"

    def _load_model(self):
        if self.tts is None:
            log("VOICE", f"Loading XTTS v2 on {self.device}...")
            start = time.time()
            from TTS.api import TTS
            self.tts = TTS("tts_models/multilingual/multi-dataset/xtts_v2").to(self.device)
            log("VOICE", f"Model loaded in {time.time() - start:.1f}s")

    def generate(self, chapter_idx: int, line_idx: int, text: str, output_path: Path):
        self._load_model()
        clean_text = self._clean_text(text)

        log("VOICE", f"Chapter {chapter_idx} line {line_idx}: \"{clean_text[:40]}...\"", 1)
        start = time.time()

        self.tts.tts_to_file(
            text=clean_text,
            file_path=str(output_path),
            speaker_wav=str(self.speaker_wav),
            language="en"
        )

        elapsed = time.time() - start
        log("VOICE", f"Chapter {chapter_idx} line {line_idx}: Done in {elapsed:.1f}s", 1)

    def _clean_text(self, text: str) -> str:
        text = re.sub(r'\*[^*]+\*|\([^)]+\)|\[[^\]]+\]|[#@~`^<>{}|\\]', '', text)
        text = text.replace('"', "'").replace('"', "'").replace('"', "'")
        text = re.sub(r'([.!?,])\1+', r'\1', text)
        text = re.sub(r'\s+', ' ', text).strip()
        if text and text[-1] not in '.!?':
            text += '.'
        return text if len(text) >= 3 else "Welcome, traveler."


def main():
    total_start = time.time()

    print("=" * 60)
    print("  GAME ASSET PRE-GENERATION")
    print("=" * 60)

    config = load_config()
    seed = config["seed"]
    num_chapters = len(config["chapters"])

    log("INIT", f"Seed: {seed}")
    log("INIT", f"Chapters: {num_chapters}")
    log("INIT", f"Generate: narrative={config['generate']['narrative']}, music={config['generate']['music']}, voice={config['generate']['voice']}")
    log("INIT", f"Device: {'CUDA' if torch.cuda.is_available() else 'CPU'}")

    if config["generate"]["narrative"] and not check_ollama(config["models"]["narrative"]):
        return

    # Primary output: Unity StreamingAssets (resolve to avoid .. issues)
    unity_output_dir = (Path(__file__).parent / config["output_dir"]).resolve()
    # Secondary output: local copy in gen-ai-server
    local_output_dir = (Path(__file__).parent / "output").resolve()

    for output_dir in [unity_output_dir, local_output_dir]:
        if output_dir.exists():
            shutil.rmtree(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)

    log("INIT", f"Unity output: {unity_output_dir}")
    log("INIT", f"Local output: {local_output_dir}")

    # Create chapter directories in both locations
    chapter_dirs = []
    local_chapter_dirs = []
    for idx in range(num_chapters):
        chapter_dir = unity_output_dir / "chapters" / f"chapter_{idx}"
        chapter_dir.mkdir(parents=True, exist_ok=True)
        chapter_dirs.append(chapter_dir)

        local_chapter_dir = local_output_dir / "chapters" / f"chapter_{idx}"
        local_chapter_dir.mkdir(parents=True, exist_ok=True)
        local_chapter_dirs.append(local_chapter_dir)

    narratives = {}
    manifest = {"seed": seed, "chapters": []}

    # =========================================================================
    # PHASE 1: Generate all narratives in parallel
    # =========================================================================
    if config["generate"]["narrative"]:
        print("\n" + "=" * 60)
        log("PHASE 1", "NARRATIVE GENERATION (parallel)")
        print("=" * 60)

        phase_start = time.time()
        narrative_gen = NarrativeGenerator(config)

        def generate_narrative(idx):
            narrative = narrative_gen.generate(idx, seed)
            # Save to both locations
            for dir_list in [chapter_dirs, local_chapter_dirs]:
                output_file = dir_list[idx] / "narrative.json"
                output_file.parent.mkdir(parents=True, exist_ok=True)
                with open(output_file, "w") as f:
                    json.dump(narrative, f, indent=2)
            return idx, narrative

        with ThreadPoolExecutor(max_workers=num_chapters) as executor:
            futures = {executor.submit(generate_narrative, i): i for i in range(num_chapters)}
            for future in as_completed(futures):
                idx, narrative = future.result()
                narratives[idx] = narrative
                for line in narrative["npc"]["dialogue"]:
                    log("NARRATIVE", f"  → \"{line[:50]}...\"", 1)

        log("PHASE 1", f"All narratives done in {time.time() - phase_start:.1f}s")

    # =========================================================================
    # PHASE 2: Generate all music (sequential - GPU bound)
    # =========================================================================
    if config["generate"]["music"]:
        print("\n" + "=" * 60)
        log("PHASE 2", "MUSIC GENERATION (sequential - GPU)")
        print("=" * 60)

        phase_start = time.time()
        music_gen = MusicGenerator(config)

        for idx in range(num_chapters):
            log_progress("MUSIC", idx + 1, num_chapters, config["chapters"][idx]["name"])
            music_path = chapter_dirs[idx] / "music.wav"
            music_gen.generate(idx, seed, music_path)
            # Copy to local output
            shutil.copy(music_path, local_chapter_dirs[idx] / "music.wav")

        log("PHASE 2", f"All music done in {time.time() - phase_start:.1f}s")

    # =========================================================================
    # PHASE 3: Generate all voice lines (sequential - GPU bound)
    # =========================================================================
    if config["generate"]["voice"] and narratives:
        print("\n" + "=" * 60)
        log("PHASE 3", "VOICE GENERATION (sequential - GPU)")
        print("=" * 60)

        phase_start = time.time()
        voice_gen = VoiceGenerator(config)

        total_lines = sum(len(n["npc"]["dialogue"]) for n in narratives.values())
        current_line = 0

        for idx in range(num_chapters):
            if idx not in narratives:
                continue
            dialogue = narratives[idx]["npc"]["dialogue"]
            for i, line in enumerate(dialogue):
                current_line += 1
                log_progress("VOICE", current_line, total_lines, f"Chapter {idx} line {i}")
                voice_path = chapter_dirs[idx] / f"voice_{i}.wav"
                voice_gen.generate(idx, i, line, voice_path)
                # Copy to local output
                shutil.copy(voice_path, local_chapter_dirs[idx] / f"voice_{i}.wav")

        log("PHASE 3", f"All voice done in {time.time() - phase_start:.1f}s")

    # =========================================================================
    # FINALIZE: Write manifest to both locations
    # =========================================================================
    for idx in range(num_chapters):
        chapter_manifest = {"chapter": idx, "files": {}}
        if config["generate"]["narrative"]:
            chapter_manifest["files"]["narrative"] = "narrative.json"
        if config["generate"]["music"]:
            chapter_manifest["files"]["music"] = "music.wav"
        if config["generate"]["voice"] and idx in narratives:
            chapter_manifest["files"]["voice"] = [f"voice_{i}.wav" for i in range(len(narratives[idx]["npc"]["dialogue"]))]
        manifest["chapters"].append(chapter_manifest)

    for output_dir in [unity_output_dir, local_output_dir]:
        with open(output_dir / "manifest.json", "w") as f:
            json.dump(manifest, f, indent=2)

    # =========================================================================
    # SUMMARY
    # =========================================================================
    total_elapsed = time.time() - total_start
    print("\n" + "=" * 60)
    print("  GENERATION COMPLETE")
    print("=" * 60)
    log("DONE", f"Total time: {total_elapsed:.1f}s ({total_elapsed/60:.1f} min)")
    log("DONE", f"Unity output:  {unity_output_dir}")
    log("DONE", f"Local output:  {local_output_dir}")

    # List generated files
    for idx in range(num_chapters):
        files = list(chapter_dirs[idx].glob("*"))
        log("DONE", f"Chapter {idx}: {len(files)} files")


if __name__ == "__main__":
    main()
