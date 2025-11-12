import requests
import time
import json
from pathlib import Path

BASE_URL = "http://localhost:8000"
DATA_DIR = Path(__file__).parent / "data"
OUTPUT_DIR = Path(__file__).parent / "output"

def test_server_health():
    print("=== Testing Server Health ===\n")
    response = requests.get(f"{BASE_URL}/")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "online"
    print("✓ Server is online")
    print(f"  Services: {data['services']}")
    print(f"  Models: {data['models']}")
    print()

def test_room_pipeline(image_path: Path, room_index: int, seed: int, previous_narratives: list = None):
    """Test pipeline for a single room"""
    print(f"Room {room_index + 1}: {image_path.name}")
    print("=" * 60)

    results = {
        "room": image_path.name,
        "room_index": room_index,
        "pipeline": []
    }

    # Build story context from previous rooms
    previous_context = None
    if previous_narratives:
        context_parts = []
        for i, prev in enumerate(previous_narratives):
            context_parts.append(
                f"Room {i + 1}: Met {prev['npc']['name']} who said: \"{prev['npc']['dialogue'][0]}\" "
                f"Quest: {prev['quest']['objective']}"
            )
        previous_context = "\n".join(context_parts)

    # Step 1: Vision Analysis
    print("Step 1/3: Vision Analysis")
    print("-" * 60)
    start = time.time()

    with open(image_path, "rb") as f:
        files = {"file": (image_path.name, f, "image/png")}
        data = {"use_cache": "false"}
        response = requests.post(
            f"{BASE_URL}/analyze/vision",
            files=files,
            data=data
        )

    elapsed = time.time() - start
    assert response.status_code == 200, f"Vision failed: {response.text}"

    vision_result = response.json()
    results["vision"] = vision_result
    results["pipeline"].append({"step": "vision", "time": elapsed})

    print(f"✓ Vision complete ({elapsed:.2f}s)")
    print(f"  Environment: {vision_result['environment_type']}")
    print(f"  Atmosphere: {vision_result['atmosphere'][:80]}...")
    print(f"  Features: {', '.join(vision_result['features'][:3])}...")
    print(f"  Mood: {vision_result['mood'][:60]}...")
    print()

    # Step 2: Narrative Generation (using vision context + story continuity)
    print("Step 2/3: Narrative Generation")
    print("-" * 60)
    print(f"Input: Vision mood = '{vision_result['mood'][:50]}...'")
    if previous_context:
        print(f"Story context: Building on {len(previous_narratives)} previous room(s)")

    start = time.time()
    narrative_payload = {
        "roomIndex": room_index,
        "totalRooms": 9,
        "theme": f"{vision_result['environment_type']} - {vision_result['mood']}",
        "seed": seed,
        "use_cache": False,
        "previous_context": previous_context
    }

    response = requests.post(
        f"{BASE_URL}/generate/narrative",
        json=narrative_payload
    )

    elapsed = time.time() - start
    assert response.status_code == 200, f"Narrative failed: {response.text}"

    narrative_result = response.json()
    results["narrative"] = narrative_result
    results["pipeline"].append({"step": "narrative", "time": elapsed})

    print(f"✓ Narrative complete ({elapsed:.2f}s)")
    print(f"  NPC: {narrative_result['npc']['name']}")
    print(f"  Quest: {narrative_result['quest']['objective'][:60]}...")
    print(f"  Lore: {narrative_result['lore']['title']}")
    print()

    # Step 3: Music Generation (using vision mood + narrative context)
    print("Step 3/3: Music Generation")
    print("-" * 60)

    music_description = f"{vision_result['mood']}, {vision_result['atmosphere'][:50]}"
    print(f"Input: '{music_description[:70]}...'")

    start = time.time()
    music_seed = seed + 100  # Different seed for music
    music_payload = {
        "description": music_description,
        "seed": music_seed,
        "duration": 10.0,
        "use_cache": False
    }

    print("  (Generating music, ~20s...)")
    response = requests.post(
        f"{BASE_URL}/generate/music",
        json=music_payload,
        timeout=120
    )

    elapsed = time.time() - start
    assert response.status_code == 200, f"Music failed: {response.text}"

    music_result = response.json()
    results["music"] = music_result
    results["pipeline"].append({"step": "music", "time": elapsed})

    print(f"✓ Music complete ({elapsed:.2f}s)")
    print(f"  Path: {music_result['path']}")
    print(f"  Seed: {music_result['seed']}")

    # Copy music file to test output directory
    import shutil
    source_music = Path(music_result['path'])
    if source_music.exists():
        dest_music = OUTPUT_DIR / source_music.name
        shutil.copy(source_music, dest_music)
        results["music"]["test_output_path"] = str(dest_music)
        print(f"  Copied to: tests/output/{source_music.name}")
    print()

    # Summary for this room
    total_time = sum(step["time"] for step in results["pipeline"])
    print("✓ Pipeline complete for this room!")
    print(f"  Total time: {total_time:.2f}s")
    print()

    return results

def test_full_pipeline():
    """Test the full generative pipeline on all 3 rooms"""
    print("=== Testing Full Generative Pipeline ===\n")
    print("Pipeline: Room Screenshot → Vision → Narrative → Music")
    print("Testing on 3 tutorial rooms\n")

    OUTPUT_DIR.mkdir(exist_ok=True)

    room_images = [
        (DATA_DIR / "game-1.png", 0, 12345),
        (DATA_DIR / "game-2.png", 1, 23456),
        (DATA_DIR / "game-3.png", 2, 34567),
    ]

    all_results = []
    previous_narratives = []

    for image_path, room_index, seed in room_images:
        if not image_path.exists():
            print(f"ERROR: Test image not found: {image_path}")
            continue

        try:
            result = test_room_pipeline(image_path, room_index, seed, previous_narratives)
            all_results.append(result)
            # Add this room's narrative to context for next room
            previous_narratives.append(result["narrative"])
        except Exception as e:
            print(f"ERROR processing {image_path.name}: {e}")
            import traceback
            traceback.print_exc()
            continue

    # Save all results
    output_file = OUTPUT_DIR / "pipeline_test.json"
    with open(output_file, "w") as f:
        json.dump(all_results, f, indent=2)

    # Overall summary
    print("=" * 60)
    print("✓ All Pipelines Complete!")
    print(f"  Rooms processed: {len(all_results)}/3")
    total_time = sum(
        sum(step["time"] for step in room["pipeline"])
        for room in all_results
    )
    print(f"  Total time: {total_time:.2f}s")
    print(f"\nOutput files:")
    print(f"  JSON: {output_file}")

    # List generated music files
    music_files = list(OUTPUT_DIR.glob("music_*.wav"))
    if music_files:
        print(f"  Music files ({len(music_files)}):")
        for music_file in sorted(music_files):
            print(f"    - {music_file.name}")
    else:
        print(f"  No music files found in {OUTPUT_DIR}")

    print("=" * 60)

if __name__ == "__main__":
    print("Make sure the server is running (uv run python main.py)\n")
    print("This test demonstrates the full generative AI pipeline:")
    print("  Screenshot → Vision → Narrative → Music")
    print("\nNOTE: Cache is DISABLED for testing (use_cache=False)\n")
    time.sleep(1)

    try:
        test_server_health()
        test_full_pipeline()
    except requests.exceptions.ConnectionError:
        print("\nERROR: Cannot connect to server. Is it running?")
        print("Run: uv run python main.py")
    except FileNotFoundError as e:
        print(f"\nERROR: {e}")
        print("Make sure game-1.png exists in data/ directory")
    except Exception as e:
        print(f"\nERROR: {e}")
        import traceback
        traceback.print_exc()
