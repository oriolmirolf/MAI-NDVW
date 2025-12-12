using System;
using System.Collections;
using UnityEngine;

public class DungeonRunManager : MonoBehaviour
{
    // --- SINGLETON PATTERN ---
    public static DungeonRunManager Instance { get; private set; }

    [Header("Generator Settings")]
    [SerializeField] private BSPMSTDungeonGenerator worldGenerator;
    [SerializeField] private int totalChapters = 3;
    private int runSeed = 54321; // Not serialized - always use this value

    [Header("Transitions")]
    [SerializeField] private TransitionScreenUI transitionUI;
    [SerializeField] private float victoryWaitDuration = 4.0f; // Time to bask in glory

    [Header("Chapter Themes")]
    [Tooltip("Assign one theme per chapter (index matches chapter number)")]
    [SerializeField] private ChapterTheme[] chapterThemes = new ChapterTheme[3];

    [Serializable]
    private struct ChapterState
    {
        public int layoutSeed;
        public bool completed;
    }

    private ChapterState[] chapters;
    private int currentChapterIndex = 0;
    
    // Public Property so other scripts (like PlayerHealth) can read the chapter number
    public int CurrentChapterIndex => currentChapterIndex;

    private int checkpointChapterIndex = -1; // -1 = No checkpoint yet
    private System.Random rng;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (worldGenerator == null)
            worldGenerator = FindObjectOfType<BSPMSTDungeonGenerator>();

        rng = new System.Random(runSeed);
        chapters = new ChapterState[totalChapters];

        for (int i = 0; i < totalChapters; i++)
        {
            chapters[i].layoutSeed = rng.Next();
            chapters[i].completed = false;
        }
    }

    private void Start()
    {
        Debug.Log("[DungeonRunManager] Start() called. Attempting to load Chapter 0.");

        if (chapters != null && chapters.Length > 0)
        {
            LoadChapter(0);
        }
    }

    private void LoadChapter(int index)
    {
        if (index >= totalChapters)
        {
            Debug.Log("<b>[Game Cleared]</b> You have finished all dungeons!");
            // Optional: Load a "Win Scene" or Main Menu here
            return;
        }

        currentChapterIndex = Mathf.Clamp(index, 0, totalChapters - 1);
        int seed = chapters[currentChapterIndex].layoutSeed;

        Debug.Log($"<color=cyan>[DungeonRunManager]</color> Loading Chapter {currentChapterIndex + 1}/{totalChapters} (Seed: {seed})");

        // Set the theme for this chapter
        if (chapterThemes != null && currentChapterIndex < chapterThemes.Length && chapterThemes[currentChapterIndex] != null)
        {
            worldGenerator.SetThemeForNextGeneration(chapterThemes[currentChapterIndex]);
        }

        // 1. Generate the world
        worldGenerator.GenerateWithSeedAndPlacePlayer(seed);

        // 2. Find the PlayerHealth and heal them
        var playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
        }

        // 3. Show chapter introduction dialogue
        if (IntroductionDialogue.Instance != null)
        {
            IntroductionDialogue.Instance.ShowChapterIntroduction(currentChapterIndex);
        }
    }

    public void OnBossDefeated()
    {
        chapters[currentChapterIndex].completed = true;

        // Update checkpoint
        checkpointChapterIndex = Mathf.Max(checkpointChapterIndex, currentChapterIndex);

        Debug.Log($"[DungeonRunManager] Boss Defeated. Chapter {currentChapterIndex} Complete.");

        // Spawn the portal IF there is a next chapter
        if (currentChapterIndex < totalChapters - 1)
        {
            worldGenerator.SpawnChapterExitPortalAtBossRoom();
        }
        else
        {
            Debug.Log("Final Boss Defeated! Game Complete!");
        }
    }

    private IEnumerator ShowGameVictory()
    {
        // Wait for victory dialogue to finish
        yield return new WaitForSeconds(3f);

        // Show final victory message
        if (DialogueUI.Instance != null)
        {
            var victoryDialogue = new NPCDialogue
            {
                npcName = "Narrator",
                dialogueLines = new System.Collections.Generic.List<string>
                {
                    "Congratulations, brave adventurer!",
                    "You have conquered all challenges!",
                    "The land is safe. You are victorious!"
                },
                audioPaths = new System.Collections.Generic.List<string>()
            };
            DialogueUI.Instance.ShowDialogue(victoryDialogue);
        }
    }

    private void ShowBossVictoryDialogue()
    {
        if (LLMNarrativeGenerator.Instance == null || DialogueUI.Instance == null)
            return;

        // Boss room is the last room of each chapter
        int roomsPerChapter = Mathf.Max(1, LLMNarrativeGenerator.Instance.totalRooms / 3);
        int bossRoomIndex = (currentChapterIndex * roomsPerChapter) + roomsPerChapter - 1;

        var narrative = LLMNarrativeGenerator.Instance.GetNarrative(bossRoomIndex);
        if (narrative?.victoryDialogue != null &&
            narrative.victoryDialogue.dialogueLines != null &&
            narrative.victoryDialogue.dialogueLines.Count > 0)
        {
            DialogueUI.Instance.ShowDialogue(narrative.victoryDialogue);
        }
    }

    // --- NEW TRANSITION COROUTINE ---
    public IEnumerator UseChapterExitRoutine()
    {
        if (!CanUseChapterExit()) 
        {
            Debug.LogWarning("[DungeonRunManager] Cannot advance yet.");
            yield break;
        }

        Debug.Log("Starting victory transition...");

        // Show the "Good Vibes" screen
        int nextChapter = currentChapterIndex + 1;
        if (transitionUI != null) transitionUI.ShowVictoryScreen(nextChapter);

        // Wait for the glory (4 seconds)
        yield return new WaitForSeconds(victoryWaitDuration);

        // Actually Load
        LoadChapter(nextChapter);

        // Wait one frame for cleanup
        yield return null; 
        
        // Hide screens
        if (transitionUI != null) transitionUI.HideAllScreens();
    }

    public void OnPlayerDeath()
    {
        Debug.Log("<color=red>[DungeonRunManager] Player Died.</color>");

        // Always restart in the chapter we died in
        // (Even if we killed the boss, we didn't go through the portal, so restart here)
        int restartChapter = currentChapterIndex;

        // Re-roll the seed if we're in a chapter ahead of our checkpoint
        if (currentChapterIndex > checkpointChapterIndex)
        {
            chapters[currentChapterIndex].layoutSeed = rng.Next();
        }

        LoadChapter(restartChapter);
    }

    public bool CanUseChapterExit()
    {
        if (currentChapterIndex >= totalChapters - 1) return false;
        return chapters[currentChapterIndex].completed;
    }

    // Debug helpers
    public void DebugForceLoadChapter(int index) => LoadChapter(index);
}