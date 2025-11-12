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
    private System.Random random;
    private string CachePath => Path.Combine(Application.persistentDataPath, cacheFileName);

    protected override void Awake()
    {
        base.Awake();
        random = new System.Random(seed);
    }

    private void Start()
    {
        if (useCache && LoadFromCache())
        {
            Debug.Log($"Loaded {narrativeCache.Count} narratives from cache");
        }
        else
        {
            Debug.Log("Generating narratives...");
            GenerateAllNarratives(() =>
            {
                Debug.Log($"Generated {totalRooms} narratives");
                SaveToCache();
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
            yield return GenerateRoomNarrative(i);
            yield return new WaitForSeconds(0.5f);
        }

        isGenerating = false;
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
        }
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
        return new RoomNarrative
        {
            roomIndex = response.roomIndex,
            environmentDescription = response.environment,
            npcDialogues = new List<NPCDialogue>
            {
                new NPCDialogue
                {
                    npcName = response.npc.name,
                    dialogueLines = new List<string>(response.npc.dialogue),
                    spawnPosition = GetRandomSafePosition()
                }
            },
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
        if (narrativeCache.TryGetValue(roomIndex, out RoomNarrative narrative))
        {
            return narrative;
        }
        Debug.LogWarning($"No narrative for room {roomIndex}");
        return null;
    }

    public bool IsReady()
    {
        return !isGenerating && narrativeCache.Count == totalRooms;
    }
}
