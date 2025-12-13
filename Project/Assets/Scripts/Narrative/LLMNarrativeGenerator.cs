using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LLMNarrativeGenerator : Singleton<LLMNarrativeGenerator>
{
    [Header("Story Settings")]
    public int totalChapters = 3;

    private Dictionary<int, RoomNarrative> chapterCache = new Dictionary<int, RoomNarrative>();
    private bool generationComplete = false;

    private void Start()
    {
        Debug.Log("[AI] LLMNarrativeGenerator starting...");
        StartCoroutine(LoadAllNarratives());
    }

    private IEnumerator LoadAllNarratives()
    {
        while (GeneratedContentLoader.Instance == null)
            yield return null;

        while (!GeneratedContentLoader.Instance.IsLoaded)
            yield return null;

        if (!GeneratedContentLoader.Instance.HasContent)
        {
            Debug.LogError("[AI] Cannot load narratives - no generated content!");
            yield break;
        }

        for (int chapter = 0; chapter < totalChapters; chapter++)
            yield return LoadChapterFromFiles(chapter);

        generationComplete = true;
        Debug.Log($"[AI] Narratives loaded: {chapterCache.Count} chapters");
    }

    private IEnumerator LoadChapterFromFiles(int chapter)
    {
        bool completed = false;
        NarrativeData data = null;

        yield return GeneratedContentLoader.Instance.LoadChapterNarrative(
            chapter,
            result => { data = result; completed = true; },
            error => { Debug.LogError($"[AI] {error}"); completed = true; }
        );

        yield return new WaitUntil(() => completed);

        if (data != null)
            chapterCache[chapter] = ConvertNarrativeData(data, chapter);
    }

    private RoomNarrative ConvertNarrativeData(NarrativeData data, int chapter)
    {
        var dialogueLines = data.npc?.dialogue != null
            ? new List<string>(data.npc.dialogue)
            : new List<string>();

        return new RoomNarrative
        {
            roomIndex = chapter,
            environmentDescription = data.environment ?? "",
            npcDialogues = new List<NPCDialogue>
            {
                new NPCDialogue
                {
                    npcName = data.npc?.name ?? "The Narrator",
                    dialogueLines = dialogueLines,
                    audioPaths = new List<string>(),
                    spawnPosition = Vector2.zero
                }
            },
            questObjective = new QuestObjective
            {
                objectiveText = data.quest?.objective ?? "Explore",
                type = QuestType.DefeatEnemies,
                targetCount = data.quest?.count ?? 1
            },
            loreEntries = new List<LoreEntry>
            {
                new LoreEntry
                {
                    title = data.lore?.title ?? "",
                    content = data.lore?.content ?? "",
                    spawnPosition = Vector2.zero
                }
            }
        };
    }

    public RoomNarrative GetNarrative(int chapter)
    {
        return chapterCache.TryGetValue(chapter, out RoomNarrative narrative) ? narrative : null;
    }

    public bool IsReady() => generationComplete;
}
