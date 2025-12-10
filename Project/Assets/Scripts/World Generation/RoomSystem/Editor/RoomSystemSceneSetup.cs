#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using System.IO;

public class RoomSystemSceneSetup : EditorWindow
{
    // Settings - 16:9 aspect ratio (32x18 tiles for widescreen)
    private Vector2Int roomSize = new Vector2Int(32, 18);
    private int roomCount = 8;
    private int seed = 12345;

    // Asset references (will try to find automatically)
    private ChapterTheme theme;
    private GameObject playerPrefab;
    private GameObject camerasPrefab;
    private GameObject managersPrefab;
    private GameObject uiCanvasPrefab;

    // Tile references for walls
    private TileBase groundTile;
    private TileBase wallTop;
    private TileBase wallBottom;
    private TileBase wallLeft;
    private TileBase wallRight;
    private TileBase cornerTL;
    private TileBase cornerTR;
    private TileBase cornerBL;
    private TileBase cornerBR;
    private TileBase innerTL;
    private TileBase innerTR;
    private TileBase innerBL;
    private TileBase innerBR;

    private Vector2 scrollPos;

    [MenuItem("Tools/Room System/Create New Dungeon Scene")]
    public static void ShowWindow()
    {
        var window = GetWindow<RoomSystemSceneSetup>("Dungeon Scene Setup");
        window.minSize = new Vector2(450, 700);
        window.LoadAssetReferences();
    }

    private void LoadAssetReferences()
    {
        // Try to find existing assets
        theme = AssetDatabase.LoadAssetAtPath<ChapterTheme>("Assets/Themes/Chapter1_ForestTheme.asset");
        playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        camerasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Scene Management/Cameras.prefab");
        managersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Scene Management/Managers.prefab");
        uiCanvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Scene Management/UI_Canvas.prefab");

        // Try to load some default tiles
        groundTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tilemap/Tiles/Outdoor Tile Spritesheet_0.asset");

        // Load wall tiles from BSP generator configuration (using GUIDs from CA_with_portal scene)
        wallTop = LoadTileByGUID("a62b2337b0a2fe6469c739f4851aafc8");      // topA
        wallBottom = LoadTileByGUID("ad69ea16e6e31e84b8dc665d2c1ffc0c");   // bottomA
        wallLeft = LoadTileByGUID("87e1582950f17d04493ba8b30ec6de28");     // left
        wallRight = LoadTileByGUID("981582b4ac2c5864aa70a350963b770f");    // right
        cornerTL = LoadTileByGUID("5aadebfcb509a73458b2272bedaf21fc");     // cornerTopLeft
        cornerTR = LoadTileByGUID("66d7259204fc09e4b9d2ed5ed4db8a96");     // cornerTopRight
        cornerBL = LoadTileByGUID("3e95ae00c8da0134cb61a3b284e55385");     // cornerBottomLeft
        cornerBR = LoadTileByGUID("87025bb9036a4834ab5336969b7b5dd3");     // cornerBottomRight
        innerTL = LoadTileByGUID("c4e384f5df7c2794d8d4a02f0dec37e3");      // innerTopLeft
        innerTR = LoadTileByGUID("2c6c9c1015d70d0419326f3267f0e5e2");      // innerTopRight
        innerBL = LoadTileByGUID("2982ce7a69caef54ab5edaf7c1e90332");      // innerBottomLeft
        innerBR = LoadTileByGUID("bd34191dd0f6fbd4e90ebc04eb001eb0");      // innerBottomRight
    }

    private TileBase LoadTileByGUID(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(path))
        {
            return AssetDatabase.LoadAssetAtPath<TileBase>(path);
        }
        return null;
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("🏰 Dungeon Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool creates a complete new scene with the room-based dungeon system.\n\n" +
            "It will:\n" +
            "• Create a new scene\n" +
            "• Set up DungeonManager with all components\n" +
            "• Create room prefabs and templates\n" +
            "• Configure camera, player, and UI",
            MessageType.Info);

        EditorGUILayout.Space();
        DrawSeparator("Step 1: Generation Settings");

        roomSize = EditorGUILayout.Vector2IntField("Room Size (tiles)", roomSize);
        roomCount = EditorGUILayout.IntSlider("Room Count", roomCount, 4, 20);
        seed = EditorGUILayout.IntField("Random Seed", seed);

        EditorGUILayout.Space();
        DrawSeparator("Step 2: Required Prefabs");

        playerPrefab = (GameObject)EditorGUILayout.ObjectField("Player Prefab", playerPrefab, typeof(GameObject), false);
        camerasPrefab = (GameObject)EditorGUILayout.ObjectField("Cameras Prefab", camerasPrefab, typeof(GameObject), false);
        managersPrefab = (GameObject)EditorGUILayout.ObjectField("Managers Prefab", managersPrefab, typeof(GameObject), false);
        uiCanvasPrefab = (GameObject)EditorGUILayout.ObjectField("UI Canvas Prefab", uiCanvasPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        DrawSeparator("Step 3: Theme & Tiles");

        theme = (ChapterTheme)EditorGUILayout.ObjectField("Chapter Theme", theme, typeof(ChapterTheme), false);

        EditorGUILayout.Space();
        GUILayout.Label("Floor Tile:", EditorStyles.boldLabel);
        groundTile = (TileBase)EditorGUILayout.ObjectField("Ground Tile", groundTile, typeof(TileBase), false);

        EditorGUILayout.Space();
        GUILayout.Label("Wall Tiles (Optional - for visual walls):", EditorStyles.miniBoldLabel);
        wallTop = (TileBase)EditorGUILayout.ObjectField("Wall Top", wallTop, typeof(TileBase), false);
        wallBottom = (TileBase)EditorGUILayout.ObjectField("Wall Bottom", wallBottom, typeof(TileBase), false);
        wallLeft = (TileBase)EditorGUILayout.ObjectField("Wall Left", wallLeft, typeof(TileBase), false);
        wallRight = (TileBase)EditorGUILayout.ObjectField("Wall Right", wallRight, typeof(TileBase), false);

        EditorGUILayout.Space();
        GUILayout.Label("Corner Tiles:", EditorStyles.miniBoldLabel);
        cornerTL = (TileBase)EditorGUILayout.ObjectField("Corner Top-Left", cornerTL, typeof(TileBase), false);
        cornerTR = (TileBase)EditorGUILayout.ObjectField("Corner Top-Right", cornerTR, typeof(TileBase), false);
        cornerBL = (TileBase)EditorGUILayout.ObjectField("Corner Bottom-Left", cornerBL, typeof(TileBase), false);
        cornerBR = (TileBase)EditorGUILayout.ObjectField("Corner Bottom-Right", cornerBR, typeof(TileBase), false);

        EditorGUILayout.Space();
        DrawSeparator("Step 4: Create Everything");

        // Validation
        bool canCreate = playerPrefab != null && camerasPrefab != null && groundTile != null;

        if (!canCreate)
        {
            EditorGUILayout.HelpBox(
                "Please assign at least:\n• Player Prefab\n• Cameras Prefab\n• Ground Tile",
                MessageType.Warning);
        }

        EditorGUI.BeginDisabledGroup(!canCreate);

        EditorGUILayout.Space();
        GUIStyle bigButton = new GUIStyle(GUI.skin.button);
        bigButton.fontSize = 14;
        bigButton.fontStyle = FontStyle.Bold;
        bigButton.fixedHeight = 40;

        if (GUILayout.Button("🚀 CREATE DUNGEON SCENE", bigButton))
        {
            CreateDungeonScene();
        }

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        DrawSeparator("Individual Actions");

        if (GUILayout.Button("Create Room Prefab Only"))
        {
            CreateRoomPrefab("Room_Combat_NESW", true);
        }

        if (GUILayout.Button("Create RoomTemplate Asset Only"))
        {
            CreateRoomTemplateAsset("Room_Combat_NESW");
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSeparator(string title)
    {
        EditorGUILayout.Space();
        GUILayout.Label(title, EditorStyles.boldLabel);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        EditorGUILayout.Space();
    }

    private void CreateDungeonScene()
    {
        // Save current scene first
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
        }

        // Create new scene
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Remove default objects except camera
        var defaultCam = GameObject.Find("Main Camera");
        if (defaultCam) DestroyImmediate(defaultCam);
        var defaultLight = GameObject.Find("Directional Light");
        if (defaultLight) DestroyImmediate(defaultLight);

        // Ensure directories exist
        EnsureDirectoriesExist();

        // Create room prefab if it doesn't exist
        string roomPrefabPath = "Assets/Prefabs/Rooms/Room_Combat_NESW.prefab";
        if (!File.Exists(roomPrefabPath))
        {
            CreateRoomPrefab("Room_Combat_NESW", false);
        }

        // Create room template if it doesn't exist
        string templatePath = "Assets/ScriptableObjects/RoomTemplates/Room_Combat_NESW_Template.asset";
        if (!File.Exists(templatePath))
        {
            CreateRoomTemplateAsset("Room_Combat_NESW");
        }

        // Load the created assets
        var roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(roomPrefabPath);
        var roomTemplate = AssetDatabase.LoadAssetAtPath<RoomTemplate>(templatePath);

        // Assign prefab to template
        if (roomTemplate != null && roomPrefab != null)
        {
            roomTemplate.prefab = roomPrefab;
            roomTemplate.size = roomSize;
            EditorUtility.SetDirty(roomTemplate);
            AssetDatabase.SaveAssets();
        }

        // Create scene hierarchy
        CreateSceneHierarchy(roomTemplate);

        // Save the scene
        string scenePath = "Assets/Scenes/DungeonGenerated.unity";
        EditorSceneManager.SaveScene(newScene, scenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success!",
            $"Dungeon scene created at:\n{scenePath}\n\n" +
            "Next steps:\n" +
            "1. Press Play to test\n" +
            "2. Walk into yellow portals to transition\n" +
            "3. Adjust settings on DungeonManager if needed",
            "OK");
    }

    private void EnsureDirectoriesExist()
    {
        string[] dirs = {
            "Assets/Prefabs/Rooms",
            "Assets/ScriptableObjects/RoomTemplates",
            "Assets/Scenes"
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        AssetDatabase.Refresh();
    }

    private void CreateRoomPrefab(string name, bool showDialog)
    {
        EnsureDirectoriesExist();

        // Create root
        var root = new GameObject(name);

        // Add Grid
        var grid = new GameObject("Grid");
        grid.transform.SetParent(root.transform);
        var gridComp = grid.AddComponent<Grid>();
        gridComp.cellSize = new Vector3(1, 1, 1);

        // Add Floor Tilemap
        var floorObj = new GameObject("FloorTilemap");
        floorObj.transform.SetParent(grid.transform);
        var floorTilemap = floorObj.AddComponent<Tilemap>();
        var floorRenderer = floorObj.AddComponent<TilemapRenderer>();
        floorRenderer.sortingLayerName = "Default";
        floorRenderer.sortingOrder = -10;

        // Add Walls Tilemap
        var wallsObj = new GameObject("WallsTilemap");
        wallsObj.transform.SetParent(grid.transform);
        var wallsTilemap = wallsObj.AddComponent<Tilemap>();
        var wallsRenderer = wallsObj.AddComponent<TilemapRenderer>();
        wallsRenderer.sortingLayerName = "Default";
        wallsRenderer.sortingOrder = -5;
        wallsObj.AddComponent<TilemapCollider2D>();
        wallsObj.AddComponent<CompositeCollider2D>();
        var wallsRb = wallsObj.GetComponent<Rigidbody2D>();
        if (wallsRb) wallsRb.bodyType = RigidbodyType2D.Static;
        var wallsTilemapCol = wallsObj.GetComponent<TilemapCollider2D>();
        if (wallsTilemapCol) wallsTilemapCol.usedByComposite = true;

        // Add RoomInstance
        var roomInstance = root.AddComponent<RoomInstance>();

        // Add ProceduralRoomBuilder
        var builder = root.AddComponent<ProceduralRoomBuilder>();

        // Add RoomContentGenerator for CA-based decorations
        var contentGen = root.AddComponent<RoomContentGenerator>();

        // Add Camera Bounds (PolygonCollider2D)
        var boundsCollider = root.AddComponent<PolygonCollider2D>();
        boundsCollider.isTrigger = true;
        float pad = 1f;
        boundsCollider.SetPath(0, new Vector2[] {
            new Vector2(-pad, -pad),
            new Vector2(-pad, roomSize.y + pad),
            new Vector2(roomSize.x + pad, roomSize.y + pad),
            new Vector2(roomSize.x + pad, -pad)
        });

        // Add Player Spawn Point
        var spawnPoint = new GameObject("PlayerSpawn");
        spawnPoint.transform.SetParent(root.transform);
        spawnPoint.transform.localPosition = new Vector3(roomSize.x / 2f, roomSize.y / 2f, 0);

        // Portals are now spawned at runtime by ProceduralRoomBuilder using AreaExit prefab

        // Use reflection to set private fields
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // RoomInstance fields
        var riType = typeof(RoomInstance);
        riType.GetField("floorTilemap", flags)?.SetValue(roomInstance, floorTilemap);
        riType.GetField("wallsTilemap", flags)?.SetValue(roomInstance, wallsTilemap);
        riType.GetField("cameraBounds", flags)?.SetValue(roomInstance, boundsCollider);
        riType.GetField("playerSpawnCenter", flags)?.SetValue(roomInstance, spawnPoint.transform);

        // ProceduralRoomBuilder fields
        var builderType = typeof(ProceduralRoomBuilder);
        builderType.GetField("floorTilemap", flags)?.SetValue(builder, floorTilemap);
        builderType.GetField("wallsTilemap", flags)?.SetValue(builder, wallsTilemap);
        builderType.GetField("groundTile", flags)?.SetValue(builder, groundTile);

        // Assign AreaExit prefab for portal spawning
        var areaExitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Scene Management/AreaExit.prefab");
        if (areaExitPrefab != null)
        {
            builderType.GetField("areaExitPrefab", flags)?.SetValue(builder, areaExitPrefab);
        }

        // Set wall tiles if provided
        var wallTilesField = builderType.GetNestedType("WallTiles");
        if (wallTop != null || wallBottom != null)
        {
            // Create WallTiles instance via SerializedObject after prefab creation
        }

        // RoomContentGenerator setup - assign theme and torch prefab
        var contentGenType = typeof(RoomContentGenerator);

        // Always load and assign the theme
        var forestTheme = theme ?? AssetDatabase.LoadAssetAtPath<ChapterTheme>("Assets/Themes/Chapter1_ForestTheme.asset");
        if (forestTheme != null)
        {
            contentGenType.GetField("theme", flags)?.SetValue(contentGen, forestTheme);
        }

        // Assign torch prefab
        var torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Torch.prefab");
        if (torchPrefab != null)
        {
            contentGenType.GetField("torchPrefab", flags)?.SetValue(contentGen, torchPrefab);
        }

        // Save prefab
        string path = $"Assets/Prefabs/Rooms/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        DestroyImmediate(root);
        AssetDatabase.Refresh();

        // Configure wall tiles via SerializedObject
        var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (savedPrefab != null)
        {
            var prefabBuilder = savedPrefab.GetComponent<ProceduralRoomBuilder>();
            if (prefabBuilder != null)
            {
                var so = new SerializedObject(prefabBuilder);
                var wallTilesProp = so.FindProperty("wallTiles");
                if (wallTilesProp != null)
                {
                    SerializedProperty prop;
                    if (wallTop != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("topA");
                        if (prop != null) prop.objectReferenceValue = wallTop;
                        prop = wallTilesProp.FindPropertyRelative("topB");
                        if (prop != null) prop.objectReferenceValue = wallTop;
                    }
                    if (wallBottom != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("bottomA");
                        if (prop != null) prop.objectReferenceValue = wallBottom;
                        prop = wallTilesProp.FindPropertyRelative("bottomB");
                        if (prop != null) prop.objectReferenceValue = wallBottom;
                    }
                    if (wallLeft != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("left");
                        if (prop != null) prop.objectReferenceValue = wallLeft;
                    }
                    if (wallRight != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("right");
                        if (prop != null) prop.objectReferenceValue = wallRight;
                    }
                    if (cornerTL != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("cornerTopLeft");
                        if (prop != null) prop.objectReferenceValue = cornerTL;
                    }
                    if (cornerTR != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("cornerTopRight");
                        if (prop != null) prop.objectReferenceValue = cornerTR;
                    }
                    if (cornerBL != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("cornerBottomLeft");
                        if (prop != null) prop.objectReferenceValue = cornerBL;
                    }
                    if (cornerBR != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("cornerBottomRight");
                        if (prop != null) prop.objectReferenceValue = cornerBR;
                    }
                    if (innerTL != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("innerTopLeft");
                        if (prop != null) prop.objectReferenceValue = innerTL;
                    }
                    if (innerTR != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("innerTopRight");
                        if (prop != null) prop.objectReferenceValue = innerTR;
                    }
                    if (innerBL != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("innerBottomLeft");
                        if (prop != null) prop.objectReferenceValue = innerBL;
                    }
                    if (innerBR != null)
                    {
                        prop = wallTilesProp.FindPropertyRelative("innerBottomRight");
                        if (prop != null) prop.objectReferenceValue = innerBR;
                    }
                    so.ApplyModifiedProperties();
                }
            }
        }

        if (showDialog)
        {
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
            EditorUtility.DisplayDialog("Created", $"Room prefab created at:\n{path}", "OK");
        }
    }

    // Portals are now spawned at runtime by ProceduralRoomBuilder using AreaExit prefab

    private void CreateRoomTemplateAsset(string name)
    {
        EnsureDirectoriesExist();

        var template = CreateInstance<RoomTemplate>();
        template.size = roomSize;
        template.availableDoors = DoorMask.All;
        template.archetype = RoomArchetype.CombatRoom;
        template.difficulty = 1;
        template.selectionWeight = 5;
        template.useProceduralPopulation = true;
        template.minEnemies = 2;
        template.maxEnemies = 4;

        string path = $"Assets/ScriptableObjects/RoomTemplates/{name}_Template.asset";
        AssetDatabase.CreateAsset(template, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = template;
        EditorGUIUtility.PingObject(template);
    }

    private void CreateSceneHierarchy(RoomTemplate roomTemplate)
    {
        // 1. Instantiate Cameras prefab
        GameObject cameras = null;
        if (camerasPrefab != null)
        {
            cameras = (GameObject)PrefabUtility.InstantiatePrefab(camerasPrefab);
            cameras.name = "Cameras";
        }

        // 2. Instantiate Managers prefab
        if (managersPrefab != null)
        {
            var managers = (GameObject)PrefabUtility.InstantiatePrefab(managersPrefab);
            managers.name = "Managers";
        }

        // 3. Instantiate UI Canvas prefab
        if (uiCanvasPrefab != null)
        {
            var ui = (GameObject)PrefabUtility.InstantiatePrefab(uiCanvasPrefab);
            ui.name = "UI_Canvas";
        }

        // 4. Create DungeonManager
        var dungeonManager = new GameObject("DungeonManager");
        var graph = dungeonManager.AddComponent<DungeonGraph>();
        var transitionManager = dungeonManager.AddComponent<RoomTransitionManager>();

        // Create room container
        var roomContainer = new GameObject("RoomContainer");
        roomContainer.transform.SetParent(dungeonManager.transform);

        // Configure DungeonGraph via SerializedObject
        var graphSO = new SerializedObject(graph);
        graphSO.FindProperty("roomCount").intValue = roomCount;
        graphSO.FindProperty("seed").intValue = seed;
        graphSO.FindProperty("generateOnStart").boolValue = true;
        graphSO.FindProperty("preloadAdjacentRooms").boolValue = false;
        graphSO.FindProperty("roomContainer").objectReferenceValue = roomContainer.transform;

        // Set templates
        if (roomTemplate != null)
        {
            var combatTemplates = graphSO.FindProperty("combatTemplates");
            combatTemplates.arraySize = 1;
            combatTemplates.GetArrayElementAtIndex(0).objectReferenceValue = roomTemplate;

            graphSO.FindProperty("bossTemplate").objectReferenceValue = roomTemplate;
            graphSO.FindProperty("startTemplate").objectReferenceValue = roomTemplate;
        }
        graphSO.ApplyModifiedProperties();

        // Configure RoomTransitionManager
        var transSO = new SerializedObject(transitionManager);
        transSO.FindProperty("fadeOutDuration").floatValue = 0.2f;
        transSO.FindProperty("fadeInDuration").floatValue = 0.2f;
        transSO.FindProperty("disablePlayerDuringTransition").boolValue = true;

        // Find and assign virtual camera
        if (cameras != null)
        {
            var vcam = cameras.GetComponentInChildren<Cinemachine.CinemachineVirtualCamera>(true);
            if (vcam != null)
            {
                transSO.FindProperty("virtualCamera").objectReferenceValue = vcam;
            }
        }
        transSO.ApplyModifiedProperties();

        // 5. Instantiate Player prefab
        if (playerPrefab != null)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.position = new Vector3(roomSize.x / 2f, roomSize.y / 2f, 0);
        }

        // 6. Create EventSystem if UI exists and no EventSystem
        if (uiCanvasPrefab != null)
        {
            var existingEventSystem = GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (existingEventSystem == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        // 7. Add lighting (intensity 0.9 to match CA_with_portal scene)
        var light = new GameObject("Global Light 2D");
        var light2D = light.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light2D.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
        light2D.intensity = 0.9f;

        // 8. Add GenAIClient for AI server communication
        var aiClient = new GameObject("GenAIClient");
        aiClient.AddComponent<GenAIClient>();

        // 9. Add AIDungeonContentManager for AI-based enemy spawning
        var aiManager = dungeonManager.AddComponent<AIDungeonContentManager>();
        var aiManagerSO = new SerializedObject(aiManager);

        // Assign enemy prefabs
        var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Slime.prefab");
        var ghostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Ghost.prefab");
        var grapePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Grape/Grape.prefab");

        aiManagerSO.FindProperty("slimePrefab").objectReferenceValue = slimePrefab;
        aiManagerSO.FindProperty("ghostPrefab").objectReferenceValue = ghostPrefab;
        aiManagerSO.FindProperty("grapePrefab").objectReferenceValue = grapePrefab;
        aiManagerSO.FindProperty("useAIGeneration").boolValue = true;
        aiManagerSO.FindProperty("theme").stringValue = "dark forest";
        aiManagerSO.ApplyModifiedProperties();

        // 10. Add Narrative System (LLM + Voice)
        var narrativeManager = new GameObject("NarrativeManager");
        var narrativeGen = narrativeManager.AddComponent<LLMNarrativeGenerator>();
        var audioSource = narrativeManager.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        var narrativeAudio = narrativeManager.AddComponent<NarrativeAudioManager>();

        // Configure NarrativeAudioManager
        var narrativeAudioSO = new SerializedObject(narrativeAudio);
        narrativeAudioSO.FindProperty("audioSource").objectReferenceValue = audioSource;
        narrativeAudioSO.ApplyModifiedProperties();

        // Configure LLMNarrativeGenerator
        var narrativeGenSO = new SerializedObject(narrativeGen);
        narrativeGenSO.FindProperty("totalRooms").intValue = roomCount;
        narrativeGenSO.FindProperty("storyTheme").stringValue = "dark fantasy dungeon";
        narrativeGenSO.FindProperty("seed").intValue = seed;
        narrativeGenSO.FindProperty("useCache").boolValue = true;
        narrativeGenSO.ApplyModifiedProperties();

        // 11. Add DialogueUI prefab or create minimal dialogue UI
        var dialogueUIPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DialogueUI.prefab");
        if (dialogueUIPrefab != null)
        {
            var dialogueUI = (GameObject)PrefabUtility.InstantiatePrefab(dialogueUIPrefab);
            dialogueUI.name = "DialogueUI";
        }
        else
        {
            // Create minimal dialogue UI if prefab doesn't exist
            CreateMinimalDialogueUI();
        }
    }

    private void CreateMinimalDialogueUI()
    {
        // Find or create canvas
        var canvas = GameObject.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasObj = new GameObject("DialogueCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // Create dialogue panel
        var panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(canvas.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.1f, 0.1f);
        panelRect.anchorMax = new Vector2(0.9f, 0.35f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = panel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);

        // Create NPC name text
        var nameObj = new GameObject("NPCNameText");
        nameObj.transform.SetParent(panel.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.05f, 0.75f);
        nameRect.anchorMax = new Vector2(0.95f, 0.95f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        var nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
        nameText.text = "NPC Name";
        nameText.fontSize = 36;
        nameText.fontStyle = TMPro.FontStyles.Bold;

        // Create dialogue text
        var dialogueObj = new GameObject("DialogueText");
        dialogueObj.transform.SetParent(panel.transform, false);
        var dialogueRect = dialogueObj.AddComponent<RectTransform>();
        dialogueRect.anchorMin = new Vector2(0.05f, 0.2f);
        dialogueRect.anchorMax = new Vector2(0.95f, 0.7f);
        dialogueRect.offsetMin = Vector2.zero;
        dialogueRect.offsetMax = Vector2.zero;
        var dialogueText = dialogueObj.AddComponent<TMPro.TextMeshProUGUI>();
        dialogueText.text = "Dialogue text goes here...";
        dialogueText.fontSize = 18;

        // Create continue button
        var buttonObj = new GameObject("NextButton");
        buttonObj.transform.SetParent(panel.transform, false);
        var buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.7f, 0.05f);
        buttonRect.anchorMax = new Vector2(0.95f, 0.18f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        var buttonImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
        buttonImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        var button = buttonObj.AddComponent<UnityEngine.UI.Button>();

        var buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        var buttonTextRect = buttonTextObj.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;
        var buttonText = buttonTextObj.AddComponent<TMPro.TextMeshProUGUI>();
        buttonText.text = "Continue [E]";
        buttonText.fontSize = 16;
        buttonText.alignment = TMPro.TextAlignmentOptions.Center;

        // Add DialogueUI component to a parent container that stays active
        var dialogueContainer = new GameObject("DialogueUI");
        dialogueContainer.transform.SetParent(canvas.transform, false);
        var containerRect = dialogueContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // Re-parent panel to container
        panel.transform.SetParent(dialogueContainer.transform, false);

        var dialogueUI = dialogueContainer.AddComponent<DialogueUI>();
        var dialogueUISO = new SerializedObject(dialogueUI);
        dialogueUISO.FindProperty("dialoguePanel").objectReferenceValue = panel;
        dialogueUISO.FindProperty("npcNameText").objectReferenceValue = nameText;
        dialogueUISO.FindProperty("dialogueText").objectReferenceValue = dialogueText;
        dialogueUISO.FindProperty("nextButton").objectReferenceValue = button;
        dialogueUISO.FindProperty("buttonText").objectReferenceValue = buttonText;
        dialogueUISO.ApplyModifiedProperties();

        // Start hidden (the panel, not the container with DialogueUI)
        panel.SetActive(false);
    }
}

#endif
