using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChapterMusicManager : Singleton<ChapterMusicManager>
{
    [Header("Settings")]
    [SerializeField] private float crossfadeDuration = 2f;

    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
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
        Debug.Log("[AI] ChapterMusicManager starting...");
        StartCoroutine(LoadAllMusic());
    }

    private IEnumerator LoadAllMusic()
    {
        while (GeneratedContentLoader.Instance == null)
            yield return null;

        while (!GeneratedContentLoader.Instance.IsLoaded)
            yield return null;

        if (!GeneratedContentLoader.Instance.HasContent)
        {
            Debug.LogError("[AI] Cannot load music - no generated content!");
            isReady = true;
            yield break;
        }

        for (int chapter = 0; chapter < 3; chapter++)
            yield return LoadChapterMusic(chapter);

        isReady = true;
        Debug.Log($"[AI] Music loaded: {chapterMusicCache.Count} tracks");
    }

    private IEnumerator LoadChapterMusic(int chapter)
    {
        if (chapterMusicCache.ContainsKey(chapter)) yield break;

        bool completed = false;
        AudioClip clip = null;

        yield return GeneratedContentLoader.Instance.LoadChapterMusic(
            chapter,
            result => { clip = result; completed = true; },
            error => { Debug.LogError($"[AI] {error}"); completed = true; }
        );

        yield return new WaitUntil(() => completed);

        if (clip != null)
            chapterMusicCache[chapter] = clip;
    }

    public bool IsReady() => isReady;

    private void SetupAudioSources()
    {
        var goA = new GameObject("MusicSourceA");
        goA.transform.SetParent(transform);
        musicSourceA = goA.AddComponent<AudioSource>();
        musicSourceA.loop = true;
        musicSourceA.playOnAwake = false;

        var goB = new GameObject("MusicSourceB");
        goB.transform.SetParent(transform);
        musicSourceB = goB.AddComponent<AudioSource>();
        musicSourceB.loop = true;
        musicSourceB.playOnAwake = false;
    }

    public void PlayChapterMusic(int chapter)
    {
        if (chapter == currentChapter || chapter < 0 || chapter > 2) return;

        currentChapter = chapter;

        if (chapterMusicCache.TryGetValue(chapter, out AudioClip cached))
            StartCoroutine(CrossfadeCoroutine(cached));
    }

    private IEnumerator CrossfadeCoroutine(AudioClip newClip)
    {
        if (newClip == null) yield break;

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
            elapsed += Time.unscaledDeltaTime;
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
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeVolume(0.02f));
    }

    public void FadeInAfterDialogue()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeVolume(targetVolume));
    }

    private IEnumerator FadeVolume(float target)
    {
        AudioSource active = musicSourceA?.isPlaying == true ? musicSourceA :
                            musicSourceB?.isPlaying == true ? musicSourceB : null;
        if (active == null) yield break;

        float start = active.volume;
        float elapsed = 0f;

        while (elapsed < 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            active.volume = Mathf.Lerp(start, target, elapsed / 0.5f);
            yield return null;
        }
        active.volume = target;
    }
}
