using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIFade : MonoBehaviour
{
    // Singleton pattern: allows us to call UIFade.Instance from anywhere
    public static UIFade Instance;

    private Image fadeImage;
    [SerializeField] private float fadeSpeed = 2f;

    private void Awake()
    {
        // Ensure there is only one UIFade in the game
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        fadeImage = GetComponent<Image>();
    }

    public void FadeToBlack()
    {
        // Stop any running fades so they don't fight each other
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(1f));
    }

    public void FadeToClear()
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(0f));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (!fadeImage) yield break;

        float startAlpha = fadeImage.color.a;
        float percent = 0;

        while (percent < 1)
        {
            percent += Time.deltaTime * fadeSpeed;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, percent);
            
            // Apply color
            Color c = fadeImage.color;
            c.a = newAlpha;
            fadeImage.color = c;

            yield return null; // Wait for the next frame
        }
    }
}