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
        Debug.Log("[AI] IntroductionDialogue starting...");
        StartCoroutine(ShowChapterIntroDelayed());
    }

    private System.Collections.IEnumerator ShowChapterIntroDelayed()
    {
        Debug.Log("[AI] Waiting for systems to be ready...");
        yield return new WaitForSecondsRealtime(delayBeforeShow);

        while (LLMNarrativeGenerator.Instance == null || !LLMNarrativeGenerator.Instance.IsReady())
        {
            Debug.Log($"[AI] Waiting for LLMNarrativeGenerator... Instance={LLMNarrativeGenerator.Instance != null}, Ready={LLMNarrativeGenerator.Instance?.IsReady() ?? false}");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        while (ChapterMusicManager.Instance != null && !ChapterMusicManager.Instance.IsReady())
        {
            Debug.Log("[AI] Waiting for ChapterMusicManager...");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Debug.Log("[AI] All systems ready, showing chapter 0 introduction");
        ShowChapterIntroduction(0);
    }

    public void OnChapterEntered(int chapter)
    {
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
        Debug.Log($"[AI] ShowChapterIntroduction called for chapter {chapter}");

        if (shownChapters.Contains(chapter))
        {
            Debug.Log($"[AI] Chapter {chapter} already shown, playing music only");
            if (ChapterMusicManager.Instance != null)
                ChapterMusicManager.Instance.PlayChapterMusic(chapter);
            return;
        }

        if (LLMNarrativeGenerator.Instance == null || DialogueUI.Instance == null)
        {
            Debug.Log($"[AI] Missing instances - NarrativeGen={LLMNarrativeGenerator.Instance != null}, DialogueUI={DialogueUI.Instance != null}");
            StartCoroutine(RetryShowIntroDelayed(1f));
            return;
        }

        RoomNarrative narrative = LLMNarrativeGenerator.Instance.GetNarrative(chapter);

        if (narrative == null)
        {
            Debug.LogError($"[AI] Chapter {chapter} narrative not found!");
            StartCoroutine(RetryShowIntroDelayed(2f));
            return;
        }

        Debug.Log($"[AI] Found narrative for chapter {chapter}, dialogues count: {narrative.npcDialogues?.Count ?? 0}");

        if (narrative.npcDialogues != null && narrative.npcDialogues.Count > 0)
        {
            var npcDialogue = narrative.npcDialogues[0];
            Debug.Log($"[AI] NPC: {npcDialogue.npcName}, lines: {npcDialogue.dialogueLines?.Count ?? 0}");
            if (npcDialogue.dialogueLines != null && npcDialogue.dialogueLines.Count > 0)
            {
                pendingMusicChapter = startMusicAfterDialogue ? chapter : -1;
                DialogueUI.Instance.ShowDialogue(npcDialogue, OnDialogueComplete, chapter);
                shownChapters.Add(chapter);
                currentChapter = chapter;
                Debug.Log($"[AI] Chapter {chapter} dialogue started");
                return;
            }
        }

        Debug.Log($"[AI] No dialogue for chapter {chapter}, playing music only");
        if (ChapterMusicManager.Instance != null)
            ChapterMusicManager.Instance.PlayChapterMusic(chapter);
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

    public void ResetChapterIntros()
    {
        shownChapters.Clear();
        currentChapter = -1;
    }
}
