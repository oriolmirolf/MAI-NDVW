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

    [Tooltip("Water RuleTile for automatic border generation")]
    public RuleTile waterRuleTile;

    [Tooltip("Path RuleTile for automatic path generation")]
    public RuleTile pathRuleTile;

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

    [Header("Path Generation Settings")]
    [Tooltip("Enable path generation in rooms")]
    public bool enablePaths = true;

    [Tooltip("Initial path density near portals (0-1)")]
    [Range(0f, 1f)]
    public float portalPathDensity = 0.9f;

    [Tooltip("Initial path density away from portals (0-1)")]
    [Range(0f, 1f)]
    public float ambientPathDensity = 0.2f;

    [Tooltip("Radius around portals to seed paths (in tiles)")]
    [Range(1, 8)]
    public int portalSeedRadius = 3;

    [Tooltip("Number of CA iterations for path smoothing")]
    [Range(1, 10)]
    public int pathIterations = 4;

    [Tooltip("Minimum neighbors for path cell to survive")]
    [Range(2, 8)]
    public int pathSurviveMin = 3;

    [Tooltip("Minimum neighbors for path cell to be born")]
    [Range(2, 8)]
    public int pathBirthMin = 2;

    public void ValidateAndLog()
    {
        if (mainFloorTile == null)
            Debug.LogError($"[Theme:{name}] Missing mainFloorTile!");
        if (blockingObstacles == null || blockingObstacles.Length == 0)
            Debug.LogError($"[Theme:{name}] No blockingObstacles (trees/rocks)!");
        if (nonBlockingDecorations == null || nonBlockingDecorations.Length == 0)
            Debug.LogWarning($"[Theme:{name}] No decorations (grass/flowers)");
        if (bossPrefab == null)
            Debug.LogError($"[Theme:{name}] No bossPrefab assigned!");

        // Check if boss is accidentally in commonEnemies
        if (bossPrefab != null && commonEnemies != null)
        {
            foreach (var enemy in commonEnemies)
            {
                if (enemy == bossPrefab)
                {
                    Debug.LogError($"[Theme:{name}] CRITICAL: bossPrefab is in commonEnemies array! Remove it to prevent multiple bosses!");
                }
            }
        }
    }

    private void OnValidate()
    {
        // Auto-check in editor when values change
        if (bossPrefab != null && commonEnemies != null)
        {
            foreach (var enemy in commonEnemies)
            {
                if (enemy == bossPrefab)
                {
                    Debug.LogError($"[Theme:{name}] Boss prefab detected in commonEnemies! This will cause multiple bosses to spawn.");
                }
            }
        }
    }
}