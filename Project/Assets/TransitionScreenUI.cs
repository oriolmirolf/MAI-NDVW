using System.Collections;
using UnityEngine;
using TMPro;

// RENAMED SCRIPT
public class TransitionScreenUI : MonoBehaviour
{
    [Header("Death Screen Settings")]
    [SerializeField] private CanvasGroup deathCanvasGroup;
    [SerializeField] private TMP_Text rewritingText;
    
    [Header("Victory Screen Settings")]
    [SerializeField] private CanvasGroup victoryCanvasGroup;
    [SerializeField] private TMP_Text victorySubText;

    [Header("General Settings")]
    [SerializeField] private float fadeDuration = 1.5f;

    private Coroutine currentFadeRoutine;

    private void Awake()
    {
        // Ensure everything is hidden on start
        if (deathCanvasGroup) { deathCanvasGroup.alpha = 0f; deathCanvasGroup.blocksRaycasts = false; }
        if (victoryCanvasGroup) { victoryCanvasGroup.alpha = 0f; victoryCanvasGroup.blocksRaycasts = false; }
    }

    // --- PUBLIC TRIGGER METHODS ---

    public void ShowDeathScreen(int chapterIndex)
    {
        if (rewritingText != null)
            rewritingText.text = $"Rewriting Chapter {chapterIndex + 1}...";

        // Ensure victory screen is hidden just in case
        if (victoryCanvasGroup) victoryCanvasGroup.alpha = 0f;
        
        StartFade(deathCanvasGroup, 1f);
    }

    public void ShowVictoryScreen(int nextChapterIndex)
    {
        if (victorySubText != null)
            victorySubText.text = $"Entering Chapter {nextChapterIndex + 1}...";

        // Ensure death screen is hidden just in case
        if (deathCanvasGroup) deathCanvasGroup.alpha = 0f;

        StartFade(victoryCanvasGroup, 1f);
    }

    public void HideAllScreens()
    {
        // Hide whichever one is currently visible
        if (deathCanvasGroup && deathCanvasGroup.alpha > 0) StartFade(deathCanvasGroup, 0f);
        if (victoryCanvasGroup && victoryCanvasGroup.alpha > 0) StartFade(victoryCanvasGroup, 0f);
    }

    // --- INTERNAL FADE LOGIC ---

    private void StartFade(CanvasGroup targetGroup, float targetAlpha)
    {
        if (targetGroup == null) return;
        if (currentFadeRoutine != null) StopCoroutine(currentFadeRoutine);
        currentFadeRoutine = StartCoroutine(FadeRoutine(targetGroup, targetAlpha));
    }

    private IEnumerator FadeRoutine(CanvasGroup cg, float endAlpha)
    {
        float startAlpha = cg.alpha;
        float elapsed = 0f;
        
        if (endAlpha > 0) cg.blocksRaycasts = true; // Block clicks when fading in

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            yield return null;
        }

        cg.alpha = endAlpha;
        
        if (endAlpha == 0) cg.blocksRaycasts = false; // Allow clicks when hidden
        currentFadeRoutine = null;
    }
}