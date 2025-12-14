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
    private int runSeed = 54321; 

    [Header("Transitions")]
    [SerializeField] private TransitionScreenUI transitionUI;
    [SerializeField] private float victoryWaitDuration = 4.0f; 

    [Header("Chapter Themes")]
    [SerializeField] private ChapterTheme[] chapterThemes = new ChapterTheme[3];

    [Serializable]
    private struct ChapterState
    {
        public int layoutSeed;
        public bool completed;
    }

    private ChapterState[] chapters;
    private int currentChapterIndex = 0;
    
    public int CurrentChapterIndex => currentChapterIndex;

    private int checkpointChapterIndex = -1; 
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
        if (index >= totalChapters) return;

        currentChapterIndex = Mathf.Clamp(index, 0, totalChapters - 1);
        int seed = chapters[currentChapterIndex].layoutSeed;

        Debug.Log($"Loading Chapter {currentChapterIndex + 1}/{totalChapters}");

        if (chapterThemes != null && currentChapterIndex < chapterThemes.Length && chapterThemes[currentChapterIndex] != null)
        {
            worldGenerator.SetThemeForNextGeneration(chapterThemes[currentChapterIndex]);
        }

        worldGenerator.GenerateWithSeedAndPlacePlayer(seed);

        var playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.ResetHealth();
        }

        if (currentChapterIndex > 0 && IntroductionDialogue.Instance != null)
        {
            IntroductionDialogue.Instance.OnChapterEntered(currentChapterIndex);
        }
    }

    // --- REPLACED: NEW BOSS LOGIC ---
    public void OnBossDefeated()
    {
        chapters[currentChapterIndex].completed = true;
        checkpointChapterIndex = Mathf.Max(checkpointChapterIndex, currentChapterIndex);

        Debug.Log($"<color=green>[DungeonRunManager] BOSS DEFEATED!</color>");

        // Start the sequence to decide between Text Popup or Gold Screen
        StartCoroutine(BossDefeatedSequence());
    }

    private IEnumerator BossDefeatedSequence()
    {
        bool isFinalChapter = (currentChapterIndex >= totalChapters - 1);

        if (transitionUI != null)
        {
            if (isFinalChapter)
            {
                // FINAL CHAPTER: Show GOLD SCREEN and stop.
                transitionUI.ShowFinalVictory();
                yield break; // Game Over - Stop here
            }
            else
            {
                // NORMAL CHAPTER: Show "BOSS DEFEATED" TEXT (No Gold)
                // We use StartCoroutine here so the manager waits for the text animation
                yield return StartCoroutine(transitionUI.ShowBossDefeatedText());
            }
        }
        else
        {
            // Fallback if UI is missing
            yield return new WaitForSeconds(3.0f);
        }

        // Spawn the portal after the text fades out
        if (!isFinalChapter)
        {
            worldGenerator.SpawnChapterExitPortalAtBossRoom();
        }
    }

    // --- REPLACED: INSTANT PORTAL TRANSITION ---
    public IEnumerator UseChapterExitRoutine()
    {
        if (!CanUseChapterExit()) yield break;

        // No UI, No Waiting. Just Go.
        int nextChapter = currentChapterIndex + 1;
        LoadChapter(nextChapter);
        
        yield return null;
    }

    public void OnPlayerDeath()
    {
        Debug.Log("<color=red>[DungeonRunManager] Player Died.</color>");
        
        // Show Death Screen via Manager in case PlayerHealth missed it
        if (transitionUI != null) transitionUI.ShowDeathScreen(currentChapterIndex);

        int restartChapter = currentChapterIndex;

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

    public void DebugForceLoadChapter(int index) => LoadChapter(index);
}