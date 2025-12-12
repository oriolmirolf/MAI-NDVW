using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LLMNarrativeGenerator : Singleton<LLMNarrativeGenerator>
{
    [Header("Story Settings")]
    public int totalRooms = 9;
    public int totalChapters = 3;
    private int seed = 54321; // Not serialized - always use this value
    private bool enableLLMGeneration = true;

    [Header("Cache Settings")]
    [SerializeField] private bool useCache = true;
    [SerializeField] private string cacheFileName = "chapter_cache.json";

    private Dictionary<int, RoomNarrative> chapterCache = new Dictionary<int, RoomNarrative>();
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
        Debug.Log($"[GEN-AI] LLMNarrativeGenerator: Starting (LLM={enableLLMGeneration}, chapters={totalChapters})");

        if (!enableLLMGeneration)
        {
            Debug.Log("[GEN-AI] Using FALLBACK narratives");
            for (int i = 0; i < totalChapters; i++)
                chapterCache[i] = CreateFallbackNarrative(i);
            generationComplete = true;
            return;
        }

        if (useCache && LoadFromCache() && chapterCache.Count == totalChapters)
        {
            Debug.Log($"[GEN-AI] Loaded {chapterCache.Count} chapter narratives from cache");
            generationComplete = true;
        }
        else
        {
            if (chapterCache.Count > 0 && chapterCache.Count != totalChapters)
                ClearCache();
            Debug.Log("[GEN-AI] Generating chapter narratives from server...");
            Time.timeScale = 0f;
            GenerateAllNarratives(() =>
            {
                SaveToCache();
                Debug.Log("[GEN-AI] All chapter narratives generated");
            });
        }
    }

    public void GenerateAllNarratives(Action onComplete)
    {
        if (isGenerating) return;
        StartCoroutine(GenerateChaptersCoroutine(onComplete));
    }

    private IEnumerator GenerateChaptersCoroutine(Action onComplete)
    {
        isGenerating = true;
        for (int chapter = 0; chapter < totalChapters; chapter++)
        {
            yield return GenerateChapterNarrative(chapter);
            yield return new WaitForSecondsRealtime(0.1f);
        }
        isGenerating = false;
        generationComplete = true;
        onComplete?.Invoke();
    }

    private IEnumerator GenerateChapterNarrative(int chapter)
    {
        if (GenAIClient.Instance == null)
        {
            Debug.LogWarning($"[GEN-AI] GenAIClient not available, using fallback for chapter {chapter}");
            chapterCache[chapter] = CreateFallbackNarrative(chapter);
            yield break;
        }

        bool completed = false;
        string errorMsg = null;

        // Use chapter as roomIndex for the API call
        yield return GenAIClient.Instance.GenerateNarrative(
            chapter, totalChapters, "chapter", seed,
            (response) =>
            {
                chapterCache[chapter] = ConvertResponse(response);
                completed = true;
            },
            (error) =>
            {
                errorMsg = error;
                completed = true;
            },
            useCache, null
        );

        yield return new WaitUntil(() => completed);

        if (errorMsg != null)
        {
            Debug.LogError($"Chapter {chapter} generation failed: {errorMsg}");
            chapterCache[chapter] = CreateFallbackNarrative(chapter);
        }
    }

    private RoomNarrative CreateFallbackNarrative(int chapter)
    {
        string[] chapterNames = { "The Verdant Woods", "The Twilight Marsh", "The Ember Wastes" };
        string[] environments = {
            "A sunlit forest clearing with ancient oaks.",
            "A misty swamp under eternal dusk.",
            "A scorched volcanic landscape."
        };
        string[][] dialogues = {
            new[] { "Darkness spreads through these woods.", "Thornback corrupts all he touches.", "Stay alert." },
            new[] { "The marsh hides many horrors.", "Fog conceals the Wraith.", "Tread carefully." },
            new[] { "Fire consumes everything here.", "Cinderax awaits at the end.", "This is the final test." }
        };

        return new RoomNarrative
        {
            roomIndex = chapter,
            environmentDescription = environments[chapter],
            npcDialogues = new List<NPCDialogue>
            {
                new NPCDialogue
                {
                    npcName = "The Wanderer",
                    dialogueLines = new List<string>(dialogues[chapter]),
                    audioPaths = new List<string>(),
                    spawnPosition = Vector2.zero
                }
            },
            questObjective = new QuestObjective
            {
                objectiveText = "Explore",
                type = QuestType.DefeatEnemies,
                targetCount = 1
            },
            loreEntries = new List<LoreEntry>
            {
                new LoreEntry { title = chapterNames[chapter], content = environments[chapter], spawnPosition = Vector2.zero }
            }
        };
    }

    private RoomNarrative ConvertResponse(NarrativeResponse response)
    {
        int audioPathCount = response.npc.audio_paths?.Length ?? 0;
        Debug.Log($"[GEN-AI] Chapter {response.roomIndex}: {audioPathCount} audio paths");

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
                    audioPaths = response.npc.audio_paths != null ? new List<string>(response.npc.audio_paths) : new List<string>(),
                    spawnPosition = Vector2.zero
                }
            },
            questObjective = new QuestObjective
            {
                objectiveText = "Explore",
                type = QuestType.DefeatEnemies,
                targetCount = 1
            },
            loreEntries = new List<LoreEntry>
            {
                new LoreEntry { title = response.lore.title, content = response.lore.content, spawnPosition = Vector2.zero }
            }
        };
    }

    private bool LoadFromCache()
    {
        if (!File.Exists(CachePath)) return false;
        try
        {
            string json = File.ReadAllText(CachePath);
            var cacheData = JsonUtility.FromJson<NarrativeCacheData>(json);
            chapterCache.Clear();
            foreach (var room in cacheData.rooms)
                chapterCache[room.roomIndex] = room;
            Debug.Log($"[GEN-AI] Loaded {chapterCache.Count} chapters from cache");
            return chapterCache.Count > 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GEN-AI] Cache load failed: {e.Message}");
            return false;
        }
    }

    private void SaveToCache()
    {
        try
        {
            var cacheData = new NarrativeCacheData();
            foreach (var kvp in chapterCache)
                if (kvp.Value != null)
                    cacheData.rooms.Add(kvp.Value);

            if (cacheData.rooms.Count > 0)
            {
                File.WriteAllText(CachePath, JsonUtility.ToJson(cacheData, true));
                Debug.Log($"[GEN-AI] Saved {cacheData.rooms.Count} chapters to cache");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GEN-AI] Cache save failed: {e.Message}");
        }
    }

    [ContextMenu("Clear Cache")]
    public void ClearCache()
    {
        if (File.Exists(CachePath))
            File.Delete(CachePath);
        chapterCache.Clear();
    }

    public RoomNarrative GetNarrative(int roomIndex)
    {
        // Convert room index to chapter
        int roomsPerChapter = Mathf.Max(1, totalRooms / totalChapters);
        int chapter = Mathf.Min(roomIndex / roomsPerChapter, totalChapters - 1);
        return chapterCache.TryGetValue(chapter, out RoomNarrative narrative) ? narrative : null;
    }

    public RoomNarrative GetChapterNarrative(int chapter)
    {
        return chapterCache.TryGetValue(chapter, out RoomNarrative narrative) ? narrative : null;
    }

    public bool IsReady() => generationComplete;
}
