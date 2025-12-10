using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class NarrativeAudioManager : Singleton<NarrativeAudioManager>
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://localhost:8000";

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;

    private Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();
    private string localCachePath;

    protected override void Awake()
    {
        base.Awake();
        localCachePath = Path.Combine(Application.persistentDataPath, "voice_cache");
        Directory.CreateDirectory(localCachePath);

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    public void PlayDialogue(string[] audioPaths, Action onComplete = null)
    {
        if (audioPaths == null || audioPaths.Length == 0)
        {
            Debug.Log("[NarrativeAudio] No audio paths provided");
            onComplete?.Invoke();
            return;
        }

        Debug.Log($"[NarrativeAudio] PlayDialogue called with {audioPaths.Length} paths: {string.Join(", ", audioPaths)}");
        StartCoroutine(PlayDialogueSequence(audioPaths, onComplete));
    }

    private IEnumerator PlayDialogueSequence(string[] audioPaths, Action onComplete)
    {
        foreach (var audioPath in audioPaths)
        {
            if (string.IsNullOrEmpty(audioPath)) continue;

            AudioClip clip = null;

            // Check memory cache
            if (audioCache.TryGetValue(audioPath, out clip))
            {
                yield return PlayClip(clip);
                continue;
            }

            // Check local file cache
            string localPath = Path.Combine(localCachePath, audioPath);
            if (File.Exists(localPath))
            {
                yield return LoadLocalAudio(localPath, (loadedClip) => clip = loadedClip);
                if (clip != null)
                {
                    audioCache[audioPath] = clip;
                    yield return PlayClip(clip);
                    continue;
                }
            }

            // Download from server
            yield return DownloadAudio(audioPath, (downloadedClip) => clip = downloadedClip);
            if (clip != null)
            {
                audioCache[audioPath] = clip;
                yield return PlayClip(clip);
            }
        }

        onComplete?.Invoke();
    }

    private IEnumerator PlayClip(AudioClip clip)
    {
        Debug.Log($"[NarrativeAudio] Playing clip: {clip.name} ({clip.length}s)");
        audioSource.clip = clip;
        audioSource.Play();
        yield return new WaitForSecondsRealtime(clip.length + 0.2f); // Use realtime in case game is paused
    }

    private IEnumerator DownloadAudio(string filename, Action<AudioClip> onComplete)
    {
        string url = $"{serverUrl}/voice/{filename}";
        Debug.Log($"[NarrativeAudio] Downloading: {url}");

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                clip.name = filename;
                Debug.Log($"[NarrativeAudio] Downloaded: {filename} ({clip.length}s)");

                // Save to local cache
                SaveToLocalCache(filename, request.downloadHandler.data);

                onComplete?.Invoke(clip);
            }
            else
            {
                Debug.LogError($"[NarrativeAudio] Failed to download {filename}: {request.error}");
                onComplete?.Invoke(null);
            }
        }
    }

    private IEnumerator LoadLocalAudio(string path, Action<AudioClip> onComplete)
    {
        string url = "file://" + path;

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                onComplete?.Invoke(clip);
            }
            else
            {
                onComplete?.Invoke(null);
            }
        }
    }

    private void SaveToLocalCache(string filename, byte[] data)
    {
        try
        {
            string path = Path.Combine(localCachePath, filename);
            File.WriteAllBytes(path, data);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NarrativeAudio] Failed to cache {filename}: {e.Message}");
        }
    }

    public void PreloadAudio(string[] audioPaths, Action onComplete = null)
    {
        StartCoroutine(PreloadAudioCoroutine(audioPaths, onComplete));
    }

    private IEnumerator PreloadAudioCoroutine(string[] audioPaths, Action onComplete)
    {
        foreach (var audioPath in audioPaths)
        {
            if (string.IsNullOrEmpty(audioPath) || audioCache.ContainsKey(audioPath))
                continue;

            string localPath = Path.Combine(localCachePath, audioPath);
            if (File.Exists(localPath))
            {
                yield return LoadLocalAudio(localPath, (clip) =>
                {
                    if (clip != null) audioCache[audioPath] = clip;
                });
            }
            else
            {
                yield return DownloadAudio(audioPath, (clip) =>
                {
                    if (clip != null) audioCache[audioPath] = clip;
                });
            }
        }

        onComplete?.Invoke();
    }

    public void StopPlayback()
    {
        audioSource.Stop();
        StopAllCoroutines();
    }

    public bool IsPlaying => audioSource.isPlaying;

    public void ClearCache()
    {
        audioCache.Clear();
        if (Directory.Exists(localCachePath))
        {
            foreach (var file in Directory.GetFiles(localCachePath))
            {
                File.Delete(file);
            }
        }
    }
}
