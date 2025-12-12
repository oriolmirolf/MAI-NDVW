using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ChapterMusicManager : Singleton<ChapterMusicManager>
{
    [Header("Settings")]
    [SerializeField] private float musicDuration = 30f;
    private int seed = 54321; // Not serialized - always use this value
    [SerializeField] private float crossfadeDuration = 2f;
    private bool enableAIMusic = true;

    [Header("Fallback Music")]
    [SerializeField] private AudioClip[] fallbackChapterMusic = new AudioClip[3];

    [Header("Runtime")]
    [SerializeField] private AudioSource musicSourceA;
    [SerializeField] private AudioSource musicSourceB;

    private Dictionary<int, AudioClip> chapterMusicCache = new Dictionary<int, AudioClip>();
    private int currentChapter = -1;
    private bool isSourceA = true;
    private float targetVolume = 1f;
    private Coroutine fadeCoroutine;
    private bool isReady = false;

    protected override void Awake()
    {
        base.Awake();
        SetupAudioSources();
    }

    private void Start()
    {
        if (enableAIMusic && GenAIClient.Instance != null)
        {
            StartCoroutine(PreGenerateAllMusic());
        }
        else
        {
            isReady = true;
        }
    }

    private IEnumerator PreGenerateAllMusic()
    {
        // Wait for narrative generation to complete first (it controls Time.timeScale)
        Debug.Log("[MUSIC] Waiting for narratives to complete before generating music...");
        while (LLMNarrativeGenerator.Instance == null || !LLMNarrativeGenerator.Instance.IsReady())
        {
            yield return new WaitForSecondsRealtime(0.5f);
        }

        Debug.Log("[MUSIC] Pre-generating all chapter music...");

        for (int chapter = 0; chapter < 3; chapter++)
        {
            if (chapterMusicCache.ContainsKey(chapter)) continue;

            Debug.Log($"[MUSIC] Generating chapter {chapter + 1}/3...");
            yield return LoadAIMusicBlocking(chapter);
        }

        isReady = true;
        Debug.Log("[MUSIC] All music ready!");
    }

    private IEnumerator LoadAIMusicBlocking(int chapter)
    {
        if (GenAIClient.Instance == null)
        {
            Debug.LogWarning($"[MUSIC] GenAIClient not available, using fallback for chapter {chapter}");
            if (fallbackChapterMusic != null && chapter < fallbackChapterMusic.Length && fallbackChapterMusic[chapter] != null)
                chapterMusicCache[chapter] = fallbackChapterMusic[chapter];
            yield break;
        }

        bool completed = false;
        string musicPath = null;

        yield return GenAIClient.Instance.GenerateMusic(
            chapter, seed, musicDuration,
            (response) => {
                musicPath = response.path;
                completed = true;
            },
            (error) => {
                Debug.LogWarning($"[MUSIC] Chapter {chapter} generation failed: {error}");
                completed = true;
            }
        );

        yield return new WaitUntil(() => completed);

        if (string.IsNullOrEmpty(musicPath))
        {
            if (fallbackChapterMusic != null && chapter < fallbackChapterMusic.Length && fallbackChapterMusic[chapter] != null)
                chapterMusicCache[chapter] = fallbackChapterMusic[chapter];
            yield break;
        }

        string filename = Path.GetFileName(musicPath);
        completed = false;
        AudioClip clip = null;

        yield return GenAIClient.Instance.DownloadAudio(
            filename,
            (c) => {
                clip = c;
                completed = true;
            },
            (error) => {
                Debug.LogWarning($"[MUSIC] Chapter {chapter} download failed: {error}");
                completed = true;
            }
        );

        yield return new WaitUntil(() => completed);

        if (clip != null)
        {
            chapterMusicCache[chapter] = clip;
            Debug.Log($"[MUSIC] Chapter {chapter} ready!");
        }
        else if (fallbackChapterMusic != null && chapter < fallbackChapterMusic.Length && fallbackChapterMusic[chapter] != null)
        {
            chapterMusicCache[chapter] = fallbackChapterMusic[chapter];
        }
    }

    public bool IsReady() => isReady;

    private void SetupAudioSources()
    {
        if (musicSourceA == null)
        {
            var goA = new GameObject("MusicSourceA");
            goA.transform.SetParent(transform);
            musicSourceA = goA.AddComponent<AudioSource>();
            musicSourceA.loop = true;
            musicSourceA.playOnAwake = false;
        }

        if (musicSourceB == null)
        {
            var goB = new GameObject("MusicSourceB");
            goB.transform.SetParent(transform);
            musicSourceB = goB.AddComponent<AudioSource>();
            musicSourceB.loop = true;
            musicSourceB.playOnAwake = false;
        }
    }

    public void PlayChapterMusic(int chapter)
    {
        if (chapter == currentChapter) return;
        if (chapter < 0 || chapter > 2) return;

        currentChapter = chapter;

        if (chapterMusicCache.TryGetValue(chapter, out AudioClip cached))
        {
            CrossfadeToClip(cached);
        }
        else if (fallbackChapterMusic != null && chapter < fallbackChapterMusic.Length && fallbackChapterMusic[chapter] != null)
        {
            CrossfadeToClip(fallbackChapterMusic[chapter]);
        }
    }

    private void CrossfadeToClip(AudioClip clip)
    {
        if (clip == null) return;
        StartCoroutine(CrossfadeCoroutine(clip));
    }

    private IEnumerator CrossfadeCoroutine(AudioClip newClip)
    {
        AudioSource fadeOut = isSourceA ? musicSourceA : musicSourceB;
        AudioSource fadeIn = isSourceA ? musicSourceB : musicSourceA;
        isSourceA = !isSourceA;

        fadeIn.clip = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();

        float elapsed = 0f;
        float startVolume = fadeOut.volume;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time to work when paused
            float t = elapsed / crossfadeDuration;
            fadeOut.volume = Mathf.Lerp(startVolume, 0f, t);
            fadeIn.volume = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        fadeOut.Stop();
        fadeOut.volume = 0f;
        fadeIn.volume = 1f;
    }

    public void StopMusic()
    {
        musicSourceA?.Stop();
        musicSourceB?.Stop();
        currentChapter = -1;
    }

    public void FadeOutForDialogue()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeVolume(0.02f));
    }

    public void FadeInAfterDialogue()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeVolume(targetVolume));
    }

    private IEnumerator FadeVolume(float target)
    {
        AudioSource active = GetActiveSource();
        if (active == null) yield break;

        float start = active.volume;
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            active.volume = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        active.volume = target;
    }

    private AudioSource GetActiveSource()
    {
        if (musicSourceA != null && musicSourceA.isPlaying) return musicSourceA;
        if (musicSourceB != null && musicSourceB.isPlaying) return musicSourceB;
        return null;
    }
}
