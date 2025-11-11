using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GenAIClient : Singleton<GenAIClient>
{
    [Header("Server Configuration")]
    [SerializeField] private string serverUrl = "http://localhost:8000";

    public IEnumerator GenerateNarrative(
        int roomIndex,
        int totalRooms,
        string theme,
        int seed,
        Action<NarrativeResponse> onSuccess,
        Action<string> onError,
        bool useCache = true,
        string previousContext = null
    )
    {
        string endpoint = $"{serverUrl}/generate/narrative";
        string jsonData = JsonUtility.ToJson(new NarrativeRequest
        {
            roomIndex = roomIndex,
            totalRooms = totalRooms,
            theme = theme,
            seed = seed,
            use_cache = useCache,
            previous_context = previousContext
        });

        yield return PostRequest(endpoint, jsonData, (response) =>
        {
            try
            {
                NarrativeResponse narrative = JsonUtility.FromJson<NarrativeResponse>(response);
                onSuccess?.Invoke(narrative);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to parse narrative: {e.Message}");
            }
        }, onError);
    }

    public IEnumerator GenerateMusic(
        string description,
        int? seed,
        float duration,
        Action<MusicResponse> onSuccess,
        Action<string> onError,
        bool useCache = true
    )
    {
        string endpoint = $"{serverUrl}/generate/music";
        MusicRequest request = new MusicRequest
        {
            description = description,
            duration = duration,
            use_cache = useCache
        };
        if (seed.HasValue) request.seed = seed.Value;

        string jsonData = JsonUtility.ToJson(request);

        yield return PostRequest(endpoint, jsonData, (response) =>
        {
            try
            {
                MusicResponse music = JsonUtility.FromJson<MusicResponse>(response);
                onSuccess?.Invoke(music);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to parse music response: {e.Message}");
            }
        }, onError);
    }

    public IEnumerator AnalyzeVision(
        Texture2D screenshot,
        Action<VisionResponse> onSuccess,
        Action<string> onError,
        bool useCache = true
    )
    {
        string endpoint = $"{serverUrl}/analyze/vision";

        byte[] imageBytes = screenshot.EncodeToPNG();

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageBytes, "screenshot.png", "image/png");
        form.AddField("use_cache", useCache ? "true" : "false");

        using (UnityWebRequest request = UnityWebRequest.Post(endpoint, form))
        {
            request.timeout = 60;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    VisionResponse vision = JsonUtility.FromJson<VisionResponse>(request.downloadHandler.text);
                    onSuccess?.Invoke(vision);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse vision response: {e.Message}");
                }
            }
            else
            {
                string error = $"Vision request failed: {request.error} (Code: {request.responseCode})";
                Debug.LogError(error);
                onError?.Invoke(error);
            }
        }
    }

    public IEnumerator GetCacheStats(Action<CacheStats> onSuccess, Action<string> onError)
    {
        string endpoint = $"{serverUrl}/cache/stats";

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    CacheStats stats = JsonUtility.FromJson<CacheStats>(request.downloadHandler.text);
                    onSuccess?.Invoke(stats);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse cache stats: {e.Message}");
                }
            }
            else
            {
                onError?.Invoke($"Failed to get cache stats: {request.error}");
            }
        }
    }

    public IEnumerator ClearCache(Action onSuccess, Action<string> onError)
    {
        string endpoint = $"{serverUrl}/cache/clear";

        yield return PostRequest(endpoint, "{}", (response) =>
        {
            onSuccess?.Invoke();
        }, onError);
    }

    private IEnumerator PostRequest(
        string url,
        string jsonData,
        Action<string> onSuccess,
        Action<string> onError
    )
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 120;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                string error = $"Request failed: {request.error} (Code: {request.responseCode})";
                Debug.LogError(error);
                onError?.Invoke(error);
            }
        }
    }
}

[Serializable]
public class NarrativeRequest
{
    public int roomIndex;
    public int totalRooms;
    public string theme;
    public int seed;
    public bool use_cache = true;
    public string previous_context = null;
}

[Serializable]
public class NarrativeResponse
{
    public int roomIndex;
    public string environment;
    public NPCResponse npc;
    public QuestResponse quest;
    public LoreResponse lore;
}

[Serializable]
public class NPCResponse
{
    public string name;
    public string[] dialogue;
}

[Serializable]
public class QuestResponse
{
    public string objective;
    public string type;
    public int count;
}

[Serializable]
public class LoreResponse
{
    public string title;
    public string content;
}

[Serializable]
public class MusicRequest
{
    public string description;
    public int seed = -1;
    public float duration;
    public bool use_cache = true;
}

[Serializable]
public class MusicResponse
{
    public string path;
    public int seed;
}

[Serializable]
public class VisionResponse
{
    public string environment_type;
    public string atmosphere;
    public string[] features;
    public string mood;
}

[Serializable]
public class CacheStats
{
    public int narrative_count;
    public int music_count;
    public int vision_count;
    public int total_entries;
}
