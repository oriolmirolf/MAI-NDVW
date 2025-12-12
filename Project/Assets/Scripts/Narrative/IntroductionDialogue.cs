using UnityEngine;
using System.Collections.Generic;

public class IntroductionDialogue : Singleton<IntroductionDialogue>
{
    [Header("Settings")]
    [SerializeField] private float delayBeforeShow = 1f;
    [SerializeField] private bool startMusicAfterDialogue = true;

    private HashSet<int> shownChapters = new HashSet<int>();
    private int currentChapter = -1;
    private int pendingMusicChapter = -1;

    private void Start()
    {
        StartCoroutine(ShowChapterIntroDelayed());
    }

    private System.Collections.IEnumerator ShowChapterIntroDelayed()
    {
        // Wait for narrative generation to complete (uses unscaled time to work during pause)
        yield return new WaitForSecondsRealtime(delayBeforeShow);

        // Wait until LLMNarrativeGenerator is ready
        while (LLMNarrativeGenerator.Instance == null || !LLMNarrativeGenerator.Instance.IsReady())
        {
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Debug.Log("[INTRO] Narratives ready");

        // Wait until ChapterMusicManager is ready (music generated)
        while (ChapterMusicManager.Instance != null && !ChapterMusicManager.Instance.IsReady())
        {
            yield return new WaitForSecondsRealtime(0.5f);
        }
        Debug.Log("[INTRO] Music ready, showing chapter 0 intro");

        ShowChapterIntroduction(0);
    }

    public void OnRoomEntered(int roomIndex)
    {
        int chapter = GetChapterForRoom(roomIndex);

        if (chapter != currentChapter)
        {
            currentChapter = chapter;
            ShowChapterIntroduction(chapter);
        }
        else if (ChapterMusicManager.Instance != null)
        {
            ChapterMusicManager.Instance.PlayChapterMusic(chapter);
        }
    }

    public void ShowChapterIntroduction(int chapter)
    {
        if (shownChapters.Contains(chapter))
        {
            if (ChapterMusicManager.Instance != null)
                ChapterMusicManager.Instance.PlayChapterMusic(chapter);
            return;
        }

        if (LLMNarrativeGenerator.Instance == null)
        {
            StartCoroutine(RetryShowIntroDelayed(1f));
            return;
        }

        if (DialogueUI.Instance == null)
            return;

        int firstRoomOfChapter = GetFirstRoomOfChapter(chapter);
        RoomNarrative narrative = LLMNarrativeGenerator.Instance.GetNarrative(firstRoomOfChapter);

        // If narrative not ready yet, retry later
        if (narrative == null)
        {
            Debug.Log($"[INTRO] Room {firstRoomOfChapter} narrative not ready, retrying...");
            StartCoroutine(RetryShowIntroDelayed(2f));
            return;
        }

        if (narrative.npcDialogues.Count > 0)
        {
            pendingMusicChapter = startMusicAfterDialogue ? chapter : -1;
            DialogueUI.Instance.ShowDialogue(narrative.npcDialogues[0], OnDialogueComplete);
            shownChapters.Add(chapter);
            currentChapter = chapter;
            Debug.Log($"[INTRO] Showing dialogue for chapter {chapter}");
        }
        else
        {
            Debug.Log($"[INTRO] No dialogues for chapter {chapter}, playing music");
            if (ChapterMusicManager.Instance != null)
                ChapterMusicManager.Instance.PlayChapterMusic(chapter);
        }
    }

    private void OnDialogueComplete()
    {
        if (pendingMusicChapter >= 0 && ChapterMusicManager.Instance != null)
        {
            ChapterMusicManager.Instance.PlayChapterMusic(pendingMusicChapter);
            pendingMusicChapter = -1;
        }
    }

    private System.Collections.IEnumerator RetryShowIntroDelayed(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        int chapter = currentChapter >= 0 ? currentChapter : 0;
        if (!shownChapters.Contains(chapter))
            ShowChapterIntroduction(chapter);
    }

    private int GetChapterForRoom(int roomIndex)
    {
        int totalRooms = LLMNarrativeGenerator.Instance != null
            ? LLMNarrativeGenerator.Instance.totalRooms
            : 9;
        int roomsPerChapter = Mathf.Max(1, totalRooms / 3);
        return Mathf.Min(roomIndex / roomsPerChapter, 2);
    }

    private int GetFirstRoomOfChapter(int chapter)
    {
        int totalRooms = LLMNarrativeGenerator.Instance != null
            ? LLMNarrativeGenerator.Instance.totalRooms
            : 9;
        int roomsPerChapter = Mathf.Max(1, totalRooms / 3);
        return chapter * roomsPerChapter;
    }

    public void ResetChapterIntros()
    {
        shownChapters.Clear();
        currentChapter = -1;
    }
}
