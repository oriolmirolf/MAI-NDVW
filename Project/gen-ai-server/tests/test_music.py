import sys
import os
sys.path.insert(0, '.')

from src.generators.music import MusicGenerator

def test_music_generation():
    print("Testing music generation...")
    gen = MusicGenerator(
        model="facebook/musicgen-small",
        output_dir="test_output"
    )

    print(f"Device: {gen.device}")

    import asyncio
    async def run():
        return await gen.generate(
            description="dark crypt with echoing water drops",
            seed=42,
            duration=10.0
        )

    file_path = asyncio.run(run())

    assert file_path is not None
    assert os.path.exists(file_path)
    assert file_path.endswith(".wav")
    assert gen.last_seed == 42

    file_size = os.path.getsize(file_path) / 1024
    print(f"SUCCESS: Generated music file")
    print(f"  Path: {file_path}")
    print(f"  Size: {file_size:.1f} KB")
    print(f"  Seed: {gen.last_seed}")

if __name__ == "__main__":
    test_music_generation()
