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
        string previousContext = null)
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
        int chapter,
        int seed,
        float duration,
        Action<MusicResponse> onSuccess,
        Action<string> onError,
        bool useCache = true)
    {
        string endpoint = $"{serverUrl}/generate/music";
        string jsonData = JsonUtility.ToJson(new MusicRequest
        {
            chapter = chapter,
            seed = seed,
            duration = duration,
            use_cache = useCache
        });

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

    public IEnumerator DownloadAudio(string filename, Action<AudioClip> onSuccess, Action<string> onError)
    {
        string url = $"{serverUrl}/audio/{filename}";
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                onSuccess?.Invoke(clip);
            }
            else
            {
                onError?.Invoke($"Failed to download audio: {request.error}");
            }
        }
    }

    public IEnumerator DownloadVoice(string filename, Action<AudioClip> onSuccess, Action<string> onError)
    {
        string url = $"{serverUrl}/voice/{filename}";
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                onSuccess?.Invoke(clip);
            }
            else
            {
                onError?.Invoke($"Failed to download voice: {request.error}");
            }
        }
    }

    private IEnumerator PostRequest(string url, string jsonData, Action<string> onSuccess, Action<string> onError)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 0; // No timeout - generation can take several minutes

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }
            else
            {
                onError?.Invoke($"Request failed: {request.error}");
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
    public NPCResponse victory; // Victory dialogue after defeating boss
}

[Serializable]
public class NPCResponse
{
    public string name;
    public string[] dialogue;
    public string[] audio_paths;
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
    public int chapter;
    public int seed;
    public float duration;
    public bool use_cache = true;
}

[Serializable]
public class MusicResponse
{
    public string path;
    public int seed;
}
