using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
                onError?.Invoke($"Vision request failed: {request.error}");
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

    public IEnumerator GenerateDungeonContent(
        DungeonContentRequest request,
        Action<DungeonContentResponse> onSuccess,
        Action<string> onError
    )
    {
        string endpoint = $"{serverUrl}/generate/dungeon-content";
        string jsonData = JsonUtility.ToJson(request);

        yield return PostRequest(endpoint, jsonData, (response) =>
        {
            try
            {
                DungeonContentResponse content = ParseDungeonContentResponse(response);
                onSuccess?.Invoke(content);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to parse dungeon content: {e.Message}\nResponse: {response}");
            }
        }, onError);
    }

    private DungeonContentResponse ParseDungeonContentResponse(string json)
    {
        var response = new DungeonContentResponse();
        response.rooms = new Dictionary<string, RoomContent>();

        // Extract seed
        var seedMatch = Regex.Match(json, @"""seed""\s*:\s*(\d+)");
        if (seedMatch.Success) response.seed = int.Parse(seedMatch.Groups[1].Value);

        // Extract theme
        var themeMatch = Regex.Match(json, @"""theme""\s*:\s*""([^""]+)""");
        if (themeMatch.Success) response.theme = themeMatch.Groups[1].Value;

        // Find all enemy spawns: {"type": "X", "count": N}
        var enemyPattern = @"\{\s*""type""\s*:\s*""([^""]+)""\s*,\s*""count""\s*:\s*(\d+)\s*\}";
        var allEnemies = Regex.Matches(json, enemyPattern);

        // Find all room blocks with their IDs
        // Match "X": { "room_id": X, ... "description": "..." }
        var roomPattern = @"""(\d+)""\s*:\s*\{\s*""room_id""\s*:\s*(\d+)\s*,\s*""enemies""\s*:\s*\[(.*?)\]\s*,\s*""description""\s*:\s*""([^""]*)""";
        var roomMatches = Regex.Matches(json, roomPattern, RegexOptions.Singleline);

        foreach (Match roomMatch in roomMatches)
        {
            string roomKey = roomMatch.Groups[1].Value;
            int roomId = int.Parse(roomMatch.Groups[2].Value);
            string enemiesBlock = roomMatch.Groups[3].Value;
            string description = roomMatch.Groups[4].Value;

            var roomContent = new RoomContent();
            roomContent.room_id = roomId;
            roomContent.description = description;
            roomContent.enemies = new List<EnemySpawn>();

            // Parse enemies within this room's block
            var enemyMatches = Regex.Matches(enemiesBlock, enemyPattern);
            foreach (Match enemyMatch in enemyMatches)
            {
                roomContent.enemies.Add(new EnemySpawn
                {
                    type = enemyMatch.Groups[1].Value,
                    count = int.Parse(enemyMatch.Groups[2].Value)
                });
            }

            response.rooms[roomKey] = roomContent;
            Debug.Log($"[AI Parse] Room {roomId}: {roomContent.enemies.Count} enemy types, desc='{description}'");
        }

        Debug.Log($"[AI Parse] Total rooms parsed: {response.rooms.Count}");
        return response;
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
    public int dungeon_count;
    public int total_entries;
}

[Serializable]
public class RoomInfo
{
    public int id;
    public string[] connections;
    public bool is_start;
    public bool is_boss;
}

[Serializable]
public class DungeonContentRequest
{
    public int seed;
    public string theme;
    public RoomInfo[] rooms;
    public string[] available_enemies;
    public bool use_cache = true;
}

[Serializable]
public class EnemySpawn
{
    public string type;
    public int count;
}

[Serializable]
public class RoomContent
{
    public int room_id;
    public List<EnemySpawn> enemies = new List<EnemySpawn>();
    public string description;
}

public class DungeonContentResponse
{
    public int seed;
    public string theme;
    public Dictionary<string, RoomContent> rooms = new Dictionary<string, RoomContent>();

    public RoomContent GetRoom(int roomId)
    {
        string key = roomId.ToString();
        return rooms.ContainsKey(key) ? rooms[key] : null;
    }
}
