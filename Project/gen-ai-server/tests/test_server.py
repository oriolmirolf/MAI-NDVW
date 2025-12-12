#!/usr/bin/env python3
"""Test server endpoints."""

import requests
import time
from pathlib import Path

BASE_URL = "http://localhost:8000"
OUTPUT_DIR = Path(__file__).parent / "output"

def test_health():
    """Test server health endpoint."""
    print("=== Testing Server Health ===\n")
    response = requests.get(f"{BASE_URL}/")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "online"
    print("✓ Server is online")
    print(f"  Services: {data['services']}")
    print(f"  Models: {data['models']}")
    print()

def test_narrative():
    """Test narrative generation endpoint."""
    print("=== Testing Narrative Generation ===\n")

    payload = {
        "roomIndex": 0,
        "totalRooms": 9,
        "theme": "2D pixel art RPG adventure",
        "seed": 12345,
        "use_cache": False
    }

    start = time.time()
    response = requests.post(f"{BASE_URL}/generate/narrative", json=payload)
    elapsed = time.time() - start

    assert response.status_code == 200, f"Failed: {response.text}"
    data = response.json()

    assert "npc" in data
    assert "environment" in data
    assert "quest" in data

    print(f"✓ Narrative generated ({elapsed:.2f}s)")
    print(f"  NPC: {data['npc']['name']}")
    print(f"  Dialogue lines: {len(data['npc']['dialogue'])}")
    print(f"  Quest: {data['quest']['objective'][:50]}...")
    print()

def test_music_chapter():
    """Test chapter-based music generation."""
    print("=== Testing Music Generation (Chapter) ===\n")

    payload = {
        "chapter": 0,  # Forest theme
        "seed": 42,
        "duration": 5.0,
        "use_cache": False
    }

    print("  Generating music for Chapter 0 (Forest)...")
    start = time.time()
    response = requests.post(f"{BASE_URL}/generate/music", json=payload, timeout=120)
    elapsed = time.time() - start

    assert response.status_code == 200, f"Failed: {response.text}"
    data = response.json()

    assert "path" in data
    assert "seed" in data

    print(f"✓ Music generated ({elapsed:.2f}s)")
    print(f"  Path: {data['path']}")
    print(f"  Seed: {data['seed']}")
    print()

def test_dungeon_content():
    """Test dungeon content generation."""
    print("=== Testing Dungeon Content Generation ===\n")

    payload = {
        "seed": 12345,
        "theme": "pixel art RPG",
        "rooms": [
            {"id": 0, "connections": ["east"], "is_start": True, "is_boss": False},
            {"id": 1, "connections": ["west", "east"], "is_start": False, "is_boss": False},
            {"id": 2, "connections": ["west"], "is_start": False, "is_boss": True}
        ],
        "available_enemies": ["slime", "ghost", "grape"],
        "use_cache": False
    }

    start = time.time()
    response = requests.post(f"{BASE_URL}/generate/dungeon-content", json=payload)
    elapsed = time.time() - start

    assert response.status_code == 200, f"Failed: {response.text}"
    data = response.json()

    assert "rooms" in data
    assert len(data["rooms"]) == 3

    print(f"✓ Dungeon content generated ({elapsed:.2f}s)")
    for room_id, content in data["rooms"].items():
        enemies = ", ".join([f"{e['type']}x{e['count']}" for e in content["enemies"]]) or "none"
        print(f"  Room {room_id}: {enemies}")
    print()

def test_cache_stats():
    """Test cache statistics endpoint."""
    print("=== Testing Cache Stats ===\n")

    response = requests.get(f"{BASE_URL}/cache/stats")
    assert response.status_code == 200

    stats = response.json()
    print(f"✓ Cache stats retrieved")
    print(f"  Narrative: {stats.get('narrative', 0)} entries")
    print(f"  Music: {stats.get('music', 0)} entries")
    print(f"  Dungeon: {stats.get('dungeon', 0)} entries")
    print()

if __name__ == "__main__":
    print("Make sure server is running: uv run python main.py\n")

    try:
        test_health()
        test_narrative()
        test_dungeon_content()
        test_cache_stats()

        # Music test is slow, run last
        print("Music generation is slow (~20-30s). Testing...")
        test_music_chapter()

        print("=" * 50)
        print("✓ All server tests passed!")
        print("=" * 50)

    except requests.exceptions.ConnectionError:
        print("\nERROR: Cannot connect to server. Is it running?")
        print("Run: uv run python main.py")
    except Exception as e:
        print(f"\nERROR: {e}")
        import traceback
        traceback.print_exc()
