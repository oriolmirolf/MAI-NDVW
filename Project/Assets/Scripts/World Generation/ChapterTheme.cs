using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Contains all themed assets for a chapter (textures, prefabs, colors)
/// </summary>
[CreateAssetMenu(fileName = "NewChapterTheme", menuName = "Game/Chapter Theme")]
public class ChapterTheme : ScriptableObject
{
    [Header("Floor Tiles")]
    [Tooltip("Main floor tile used throughout the room")]
    public TileBase mainFloorTile;

    [Tooltip("Optional variations to add visual variety")]
    public TileBase[] floorVariations;

    [Header("Blocking Obstacles (Combat Rooms Only)")]
    [Tooltip("Large obstacles that BLOCK movement - rocks, pillars, trees")]
    public GameObject[] blockingObstacles;

    [Tooltip("Environmental hazards that block movement - water, lava pools")]
    public TileBase[] hazardTiles;

    [Header("Non-Blocking Decorations (All Rooms)")]
    [Tooltip("Decorations that DON'T block movement - grass, flowers, small stones")]
    public GameObject[] nonBlockingDecorations;

    [Tooltip("Particle effects for atmosphere - dust, embers, mist")]
    public GameObject[] particleEffects;

    [Header("Enemy Prefabs")]
    [Tooltip("Common enemy types for combat rooms")]
    public GameObject[] commonEnemies;

    [Tooltip("Boss enemy for boss arena")]
    public GameObject bossPrefab;

    [Header("Visual Settings")]
    [Tooltip("Ambient lighting color for this chapter")]
    public Color ambientColor = Color.white;

    [Tooltip("Ambient light intensity")]
    public float ambientIntensity = 1.0f;

    [Header("CA Decoration Settings")]
    [Tooltip("Initial density of decoration seeds (0-1)")]
    [Range(0f, 1f)]
    public float decorationDensity = 0.3f;

    [Tooltip("Number of cellular automata iterations")]
    [Range(1, 10)]
    public int caIterations = 5;

    [Tooltip("Minimum neighbors for cell to survive")]
    [Range(2, 8)]
    public int caSurviveMin = 4;

    [Tooltip("Minimum neighbors for cell to be born")]
    [Range(2, 8)]
    public int caBirthMin = 3;
}