using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LLMNarrativeGenerator : Singleton<LLMNarrativeGenerator>
{
    [Header("Story Settings")]
    public int totalRooms = 9;
    [SerializeField] private string storyTheme = "dark fantasy dungeon";
    [SerializeField] private int seed = 12345;

    [Header("Cache Settings")]
    [SerializeField] private bool useCache = true;
    [SerializeField] private string cacheFileName = "narrative_cache.json";

    private Dictionary<int, RoomNarrative> narrativeCache = new Dictionary<int, RoomNarrative>();
    private bool isGenerating = false;
    private bool generationComplete = false;
    private System.Random random;
    private string CachePath => Path.Combine(Application.persistentDataPath, cacheFileName);

    protected override void Awake()
    {
        base.Awake();
        random = new System.Random(seed);
    }

    private void Start()
    {
        // Sync totalRooms with DungeonGraph if available
        if (DungeonGraph.Instance != null)
        {
            totalRooms = DungeonGraph.Instance.RoomCount;
            seed = DungeonGraph.Instance.Seed;
        }

        // Check cache validity (must match room count and seed)
        if (useCache && LoadFromCache() && narrativeCache.Count == totalRooms)
        {
            Debug.Log($"[Narrative] Loaded {narrativeCache.Count} narratives from cache");
            generationComplete = true;
        }
        else
        {
            if (narrativeCache.Count > 0 && narrativeCache.Count != totalRooms)
            {
                Debug.Log($"[Narrative] Cache mismatch ({narrativeCache.Count} vs {totalRooms} rooms), regenerating...");
                ClearCache();
            }
            Debug.Log($"[Narrative] Pre-generating all {totalRooms} narratives with voice...");
            Time.timeScale = 0f; // Pause game during generation
            GenerateAllNarratives(() =>
            {
                Debug.Log($"[Narrative] All {totalRooms} narratives ready with voice!");
                SaveToCache();
                Time.timeScale = 1f; // Resume game
            });
        }
    }

    public void GenerateAllNarratives(Action onComplete)
    {
        if (isGenerating) return;
        StartCoroutine(GenerateNarrativesCoroutine(onComplete));
    }

    private IEnumerator GenerateNarrativesCoroutine(Action onComplete)
    {
        isGenerating = true;

        for (int i = 0; i < totalRooms; i++)
        {
            Debug.Log($"[Narrative] Generating room {i + 1}/{totalRooms}...");
            yield return GenerateRoomNarrative(i);
            yield return new WaitForSecondsRealtime(0.1f); // Use realtime since game may be paused
        }

        isGenerating = false;
        generationComplete = true;
        Debug.Log($"[Narrative] Generation complete: {narrativeCache.Count}/{totalRooms} rooms succeeded");
        onComplete?.Invoke();
    }

    private IEnumerator GenerateRoomNarrative(int roomIndex)
    {
        bool completed = false;
        string errorMsg = null;

        string previousContext = BuildStoryContext(roomIndex);

        yield return GenAIClient.Instance.GenerateNarrative(
            roomIndex,
            totalRooms,
            storyTheme,
            seed,
            (response) =>
            {
                RoomNarrative narrative = ConvertResponse(response);
                narrativeCache[roomIndex] = narrative;
                Debug.Log($"Room {roomIndex} generated: {response.npc.name}");
                completed = true;
            },
            (error) =>
            {
                errorMsg = error;
                completed = true;
            },
            useCache,
            previousContext
        );

        yield return new WaitUntil(() => completed);

        if (errorMsg != null)
        {
            Debug.LogError($"Room {roomIndex} failed: {errorMsg}");
            // Create fallback narrative so game can still run
            narrativeCache[roomIndex] = CreateFallbackNarrative(roomIndex);
            Debug.Log($"[Narrative] Created fallback narrative for room {roomIndex}");
        }
    }

    private RoomNarrative CreateFallbackNarrative(int roomIndex)
    {
        string[] fallbackNames = { "Mysterious Stranger", "Ancient Spirit", "Wandering Soul", "Silent Guardian" };
        string[] fallbackDialogue = {
            "Welcome traveler. This dungeon holds many secrets.",
            "Be careful as you venture deeper. Danger lurks in every shadow.",
            "May fortune favor the bold. Good luck on your journey."
        };

        return new RoomNarrative
        {
            roomIndex = roomIndex,
            environmentDescription = "A dark chamber filled with mystery.",
            npcDialogues = new List<NPCDialogue>
            {
                new NPCDialogue
                {
                    npcName = fallbackNames[roomIndex % fallbackNames.Length],
                    dialogueLines = new List<string>(fallbackDialogue),
                    audioPaths = new List<string>(),
                    spawnPosition = GetRandomSafePosition()
                }
            },
            questObjective = new QuestObjective
            {
                objectiveText = "Clear the room of enemies",
                type = QuestType.DefeatEnemies,
                targetCount = 3
            },
            loreEntries = new List<LoreEntry>
            {
                new LoreEntry
                {
                    title = "Ancient Ruins",
                    content = "These ruins hold secrets from ages past.",
                    spawnPosition = GetRandomSafePosition()
                }
            }
        };
    }

    private string BuildStoryContext(int currentRoomIndex)
    {
        if (currentRoomIndex == 0) return null;

        System.Text.StringBuilder context = new System.Text.StringBuilder();

        for (int i = 0; i < currentRoomIndex; i++)
        {
            if (narrativeCache.TryGetValue(i, out RoomNarrative room))
            {
                if (room.npcDialogues.Count > 0)
                {
                    NPCDialogue npc = room.npcDialogues[0];
                    string firstDialogue = npc.dialogueLines.Count > 0 ? npc.dialogueLines[0] : "";
                    context.AppendLine($"Room {i + 1}: Met {npc.npcName} who said: \"{firstDialogue}\"");
                }

                if (room.questObjective != null)
                {
                    context.AppendLine($"Quest: {room.questObjective.objectiveText}");
                }
            }
        }

        return context.Length > 0 ? context.ToString() : null;
    }

    private RoomNarrative ConvertResponse(NarrativeResponse response)
    {
        var npcDialogue = new NPCDialogue
        {
            npcName = response.npc.name,
            dialogueLines = new List<string>(response.npc.dialogue),
            audioPaths = response.npc.audio_paths != null ? new List<string>(response.npc.audio_paths) : new List<string>(),
            spawnPosition = GetRandomSafePosition()
        };

        return new RoomNarrative
        {
            roomIndex = response.roomIndex,
            environmentDescription = response.environment,
            npcDialogues = new List<NPCDialogue> { npcDialogue },
            questObjective = new QuestObjective
            {
                objectiveText = response.quest.objective,
                type = ParseQuestType(response.quest.type),
                targetCount = response.quest.count
            },
            loreEntries = new List<LoreEntry>
            {
                new LoreEntry
                {
                    title = response.lore.title,
                    content = response.lore.content,
                    spawnPosition = GetRandomSafePosition()
                }
            }
        };
    }

    private bool LoadFromCache()
    {
        if (!File.Exists(CachePath)) return false;

        try
        {
            string json = File.ReadAllText(CachePath);
            NarrativeCacheData cacheData = JsonUtility.FromJson<NarrativeCacheData>(json);

            narrativeCache.Clear();
            foreach (RoomNarrative room in cacheData.rooms)
            {
                narrativeCache[room.roomIndex] = room;
            }

            return narrativeCache.Count > 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"Cache load failed: {e.Message}");
            return false;
        }
    }

    private void SaveToCache()
    {
        try
        {
            NarrativeCacheData cacheData = new NarrativeCacheData();
            foreach (var kvp in narrativeCache)
            {
                if (kvp.Value != null)
                {
                    cacheData.rooms.Add(kvp.Value);
                }
            }

            if (cacheData.rooms.Count == 0)
            {
                Debug.LogError("No narratives to save");
                return;
            }

            string json = JsonUtility.ToJson(cacheData, true);
            File.WriteAllText(CachePath, json);
            Debug.Log($"Saved {cacheData.rooms.Count} narratives to cache");
        }
        catch (Exception e)
        {
            Debug.LogError($"Cache save failed: {e.Message}");
        }
    }

    [ContextMenu("Clear Cache")]
    public void ClearCache()
    {
        if (File.Exists(CachePath))
        {
            File.Delete(CachePath);
            Debug.Log("Cache cleared");
        }
        narrativeCache.Clear();
    }

    [ContextMenu("Force Regenerate")]
    public void ForceRegenerate()
    {
        ClearCache();
        GenerateAllNarratives(() =>
        {
            Debug.Log($"Regenerated {totalRooms} narratives");
            SaveToCache();
        });
    }

    private Vector2 GetRandomSafePosition()
    {
        float x = (float)(random.NextDouble() * 30 - 15);
        float y = (float)(random.NextDouble() * 30 - 15);
        return new Vector2(x, y);
    }

    private QuestType ParseQuestType(string typeStr)
    {
        if (Enum.TryParse(typeStr, out QuestType type))
        {
            return type;
        }
        return QuestType.DefeatEnemies;
    }

    public RoomNarrative GetNarrative(int roomIndex)
    {
        Debug.Log($"[Narrative] GetNarrative({roomIndex}): cache has {narrativeCache.Count} entries, isGenerating={isGenerating}");
        if (narrativeCache.TryGetValue(roomIndex, out RoomNarrative narrative))
        {
            Debug.Log($"[Narrative] Found narrative for room {roomIndex}: {narrative.npcDialogues[0]?.npcName ?? "no NPC"}");
            return narrative;
        }
        Debug.LogWarning($"[Narrative] No narrative for room {roomIndex} - keys in cache: [{string.Join(", ", narrativeCache.Keys)}]");
        return null;
    }

    public bool IsReady()
    {
        return generationComplete;
    }
}
