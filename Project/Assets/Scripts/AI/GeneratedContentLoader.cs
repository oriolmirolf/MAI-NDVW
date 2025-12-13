using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class GeneratedContentLoader : Singleton<GeneratedContentLoader>
{
    private const string CONTENT_FOLDER = "GeneratedContent";

    private ContentManifest manifest;
    private bool isLoaded = false;
    private bool hasContent = false;

    public bool IsLoaded => isLoaded;
    public bool HasContent => hasContent;

    private string ContentPath => Path.Combine(Application.streamingAssetsPath, CONTENT_FOLDER);

    private void Start()
    {
        Debug.Log("[AI] GeneratedContentLoader starting...");
        Debug.Log($"[AI] Content path: {ContentPath}");
        StartCoroutine(LoadManifest());
    }

    private IEnumerator LoadManifest()
    {
        string manifestPath = Path.Combine(ContentPath, "manifest.json");
        Debug.Log($"[AI] Loading manifest from: {manifestPath}");

        yield return LoadJsonFile<ContentManifest>(manifestPath, result =>
        {
            manifest = result;
            hasContent = manifest != null;
            if (hasContent)
                Debug.Log($"[AI] Manifest loaded: {manifest.chapters?.Count ?? 0} chapters");
            else
                Debug.LogError("[AI] No generated content found! Run: python generate.py");
            isLoaded = true;
        });
    }

    public IEnumerator LoadChapterNarrative(int chapter, Action<NarrativeData> onSuccess, Action<string> onError)
    {
        string path = Path.Combine(ContentPath, "chapters", $"chapter_{chapter}", "narrative.json");

        yield return LoadJsonFile<NarrativeData>(path, data =>
        {
            if (data != null)
                onSuccess?.Invoke(data);
            else
                onError?.Invoke($"Failed to load narrative chapter {chapter}");
        });
    }

    public IEnumerator LoadChapterMusic(int chapter, Action<AudioClip> onSuccess, Action<string> onError)
    {
        string path = Path.Combine(ContentPath, "chapters", $"chapter_{chapter}", "music.wav");

        yield return LoadAudioClip(path, clip =>
        {
            if (clip != null)
                onSuccess?.Invoke(clip);
            else
                onError?.Invoke($"Failed to load music chapter {chapter}");
        });
    }

    public IEnumerator LoadChapterVoice(int chapter, int lineIndex, Action<AudioClip> onSuccess, Action<string> onError)
    {
        string path = Path.Combine(ContentPath, "chapters", $"chapter_{chapter}", $"voice_{lineIndex}.wav");

        yield return LoadAudioClip(path, clip =>
        {
            if (clip != null)
                onSuccess?.Invoke(clip);
            else
                onError?.Invoke($"Failed to load voice chapter {chapter} line {lineIndex}");
        });
    }

    private IEnumerator LoadJsonFile<T>(string path, Action<T> onComplete) where T : class
    {
        string url = "file:///" + path.Replace("\\", "/");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    T data = JsonUtility.FromJson<T>(request.downloadHandler.text);
                    onComplete?.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AI] JSON parse error: {e.Message}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                onComplete?.Invoke(null);
            }
        }
    }

    private IEnumerator LoadAudioClip(string path, Action<AudioClip> onComplete)
    {
        string url = "file:///" + path.Replace("\\", "/");

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                onComplete?.Invoke(DownloadHandlerAudioClip.GetContent(request));
            else
                onComplete?.Invoke(null);
        }
    }
}

[Serializable]
public class ContentManifest
{
    public int seed;
    public List<ChapterManifestEntry> chapters;
}

[Serializable]
public class ChapterManifestEntry
{
    public int chapter;
    public ChapterFiles files;
}

[Serializable]
public class ChapterFiles
{
    public string narrative;
    public string music;
    public string[] voice;
}

[Serializable]
public class NarrativeData
{
    public int roomIndex;
    public string environment;
    public NpcData npc;
    public QuestData quest;
    public LoreData lore;
}

[Serializable]
public class NpcData
{
    public string name;
    public string[] dialogue;
}

[Serializable]
public class QuestData
{
    public string objective;
    public string type;
    public int count;
}

[Serializable]
public class LoreData
{
    public string title;
    public string content;
}
