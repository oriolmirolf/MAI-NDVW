using System.Collections;
using UnityEngine;
using TMPro;

public class TransitionScreenUI : MonoBehaviour
{
    [Header("Death Screen (Black Panel)")]
    [SerializeField] private CanvasGroup deathCanvasGroup;
    [SerializeField] private TMP_Text rewritingText;
    
    [Header("Final Victory (Gold Panel)")]
    [SerializeField] private CanvasGroup finalVictoryCanvasGroup;
    [SerializeField] private TMP_Text victoryHeader;
    [SerializeField] private TMP_Text victorySubText;

    // --- THIS IS THE MISSING PART THAT MAKES THE SLOT APPEAR ---
    [Header("Normal Boss (Text Popup)")]
    [SerializeField] private CanvasGroup bossDefeatedTextGroup; 
    // -----------------------------------------------------------

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1.0f;

    private void Awake()
    {
        // Hide everything on start
        if (deathCanvasGroup) { deathCanvasGroup.alpha = 0f; deathCanvasGroup.blocksRaycasts = false; }
        if (finalVictoryCanvasGroup) { finalVictoryCanvasGroup.alpha = 0f; finalVictoryCanvasGroup.blocksRaycasts = false; }
        if (bossDefeatedTextGroup) { bossDefeatedTextGroup.alpha = 0f; bossDefeatedTextGroup.blocksRaycasts = false; }
    }

    // --- DEATH ---
    public void ShowDeathScreen(int chapterIndex)
    {
        if (rewritingText) rewritingText.text = $"Rewriting Chapter {chapterIndex + 1}...";
        StartCoroutine(FadeRoutine(deathCanvasGroup, 1f));
    }

    // --- NORMAL BOSS (Just Text, No Gold) ---
    public IEnumerator ShowBossDefeatedText()
    {
        // Fade Text In
        yield return StartCoroutine(FadeRoutine(bossDefeatedTextGroup, 1f));
        
        // Wait 3 seconds
        yield return new WaitForSeconds(3.0f);
        
        // Fade Text Out
        yield return StartCoroutine(FadeRoutine(bossDefeatedTextGroup, 0f));
    }

    // --- FINAL BOSS (Gold Screen Forever) ---
    public void ShowFinalVictory()
    {
        if (victoryHeader) victoryHeader.text = "VICTORY";
        if (victorySubText) victorySubText.text = "The loop is broken.";
        
        StartCoroutine(FadeRoutine(finalVictoryCanvasGroup, 1f));
    }

    public void HideAllScreens()
    {
        if (deathCanvasGroup && deathCanvasGroup.alpha > 0) StartCoroutine(FadeRoutine(deathCanvasGroup, 0f));
        if (finalVictoryCanvasGroup && finalVictoryCanvasGroup.alpha > 0) StartCoroutine(FadeRoutine(finalVictoryCanvasGroup, 0f));
    }

    private IEnumerator FadeRoutine(CanvasGroup cg, float endAlpha)
    {
        if (cg == null) yield break;

        float startAlpha = cg.alpha;
        float elapsed = 0f;
        
        if (endAlpha > 0) cg.blocksRaycasts = true;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            yield return null;
        }

        cg.alpha = endAlpha;
        if (endAlpha == 0) cg.blocksRaycasts = false;
    }
}