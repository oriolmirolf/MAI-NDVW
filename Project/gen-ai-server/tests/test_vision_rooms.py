import requests
import time
import json
from pathlib import Path

BASE_URL = "http://localhost:8000"
DATA_DIR = Path(__file__).parent / "data"
OUTPUT_DIR = Path(__file__).parent / "output"

def test_vision_room_analysis():
    print("\n=== Testing Vision Analysis on Tutorial Rooms ===\n")

    OUTPUT_DIR.mkdir(exist_ok=True)

    room_images = [
        DATA_DIR / "game-1.png",
        DATA_DIR / "game-2.png",
        DATA_DIR / "game-3.png"
    ]

    vision_results = []

    for i, image_path in enumerate(room_images, 1):
        if not image_path.exists():
            print(f"ERROR: Image not found: {image_path}")
            continue

        print(f"Room {i}: {image_path.name}")
        print("-" * 60)

        print("\nAnalyzing room (cache disabled for testing)...")
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
        assert response.status_code == 200, f"Failed: {response.text}"

        result = response.json()
        vision_results.append({
            "room": i,
            "image": image_path.name,
            "vision": result
        })

        print(f"   Time: {elapsed:.2f}s")
        print(f"   Environment: {result.get('environment_type', 'N/A')}")
        print(f"   Atmosphere: {result.get('atmosphere', 'N/A')}")
        print(f"   Features: {result.get('features', [])}")
        print(f"   Mood: {result.get('mood', 'N/A')}")
        print(f"\n✓ Room {i} analysis complete\n")

    # Save vision results for later use
    output_file = OUTPUT_DIR / "vision_analysis.json"
    with open(output_file, "w") as f:
        json.dump(vision_results, f, indent=2)
    print(f"✓ Vision results saved to: {output_file}\n")

    return vision_results

def test_cache_stats():
    print("=== Cache Statistics ===\n")

    response = requests.get(f"{BASE_URL}/cache/stats")
    assert response.status_code == 200

    stats = response.json()
    print(f"Vision cache entries: {stats.get('vision', 0)}")
    print(f"Narrative cache entries: {stats.get('narrative', 0)}")
    print(f"Music cache entries: {stats.get('music', 0)}")
    print()

if __name__ == "__main__":
    print("Make sure server is running: uv run python main.py\n")
    print("This test will analyze the 3 tutorial room images from data/")
    print("NOTE: Cache is DISABLED for testing (use_cache=false)\n")
    time.sleep(1)

    try:
        vision_results = test_vision_room_analysis()
        test_cache_stats()

        print("=" * 60)
        print("✓ All vision tests passed!")
        print(f"✓ Results saved to test_output/vision_analysis.json")
        print("=" * 60)
    except requests.exceptions.ConnectionError:
        print("ERROR: Cannot connect to server. Is it running?")
        print("Run: uv run python main.py")
    except FileNotFoundError as e:
        print(f"ERROR: {e}")
        print("Make sure room images exist in data/ directory")
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()
