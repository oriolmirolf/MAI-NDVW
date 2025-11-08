using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LLMNarrativeGenerator : Singleton<LLMNarrativeGenerator> {
    [Header("LLM Configuration")]
    [SerializeField] private string ollamaUrl = "http://localhost:11434/api/generate";
    [SerializeField] private string modelName = "llama2";
    public int totalRooms = 9;

    [Header("Story Settings")]
    [SerializeField] private string storyTheme = "dark fantasy dungeon";
    [SerializeField] private int seed = 12345;

    [Header("Cache Settings")]
    [SerializeField] private bool useCache = true;
    [SerializeField] private string cacheFileName = "narrative_cache.json";

    private Dictionary<int, RoomNarrative> narrativeCache = new Dictionary<int, RoomNarrative>();
    private bool isGenerating = false;
    private System.Random random;
    private string CachePath => Path.Combine(Application.persistentDataPath, cacheFileName);

    protected override void Awake() {
        base.Awake();
        random = new System.Random(seed);
    }

    private void Start() {
        if (useCache && LoadFromCache()) {
            Debug.Log($"✅ Loaded {narrativeCache.Count} room narratives from cache: {CachePath}");
        } else {
            Debug.Log(useCache ? "No cache found, generating new narratives..." : "Cache disabled, generating new narratives...");
            GenerateAllNarratives(() => {
                Debug.Log($"✅ All {totalRooms} room narratives generated!");
                SaveToCache();
            });
        }
    }

    public void GenerateAllNarratives(Action onComplete) {
        if (isGenerating) return;
        StartCoroutine(GenerateNarrativesCoroutine(onComplete));
    }

    private bool LoadFromCache() {
        if (!File.Exists(CachePath)) return false;

        try {
            string json = File.ReadAllText(CachePath);
            NarrativeCacheData cacheData = JsonUtility.FromJson<NarrativeCacheData>(json);

            narrativeCache.Clear();
            foreach (RoomNarrative room in cacheData.rooms) {
                narrativeCache[room.roomIndex] = room;
            }

            return narrativeCache.Count > 0;
        } catch (Exception e) {
            Debug.LogError($"Failed to load cache: {e.Message}");
            return false;
        }
    }

    private void SaveToCache() {
        try {
            NarrativeCacheData cacheData = new NarrativeCacheData();
            int validCount = 0;
            foreach (var kvp in narrativeCache) {
                if (kvp.Value != null) {
                    cacheData.rooms.Add(kvp.Value);
                    validCount++;
                }
            }

            if (validCount == 0) {
                Debug.LogError("❌ No valid narratives to save to cache!");
                return;
            }

            string json = JsonUtility.ToJson(cacheData, true);
            File.WriteAllText(CachePath, json);
            Debug.Log($"💾 {validCount}/{totalRooms} narratives saved to cache: {CachePath}");
        } catch (Exception e) {
            Debug.LogError($"Failed to save cache: {e.Message}");
        }
    }

    [ContextMenu("Clear Cache")]
    public void ClearCache() {
        if (File.Exists(CachePath)) {
            File.Delete(CachePath);
            Debug.Log("Cache cleared");
        }
        narrativeCache.Clear();
    }

    [ContextMenu("Force Regenerate")]
    public void ForceRegenerate() {
        ClearCache();
        GenerateAllNarratives(() => {
            Debug.Log($"✅ All {totalRooms} room narratives regenerated!");
            SaveToCache();
        });
    }

    private IEnumerator GenerateNarrativesCoroutine(Action onComplete) {
        isGenerating = true;
        Debug.Log($"Generating narratives for {totalRooms} rooms...");

        for (int i = 0; i < totalRooms; i++) {
            Debug.Log($"Starting generation for room {i}...");
            yield return GenerateRoomNarrative(i);
            yield return new WaitForSeconds(0.5f);
        }

        isGenerating = false;
        Debug.Log("All narratives generated!");
        onComplete?.Invoke();
    }

    private IEnumerator GenerateRoomNarrative(int roomIndex) {
        string prompt = BuildPrompt(roomIndex);

        yield return SendLLMRequest(prompt, (response) => {
            RoomNarrative narrative = ParseLLMResponse(response, roomIndex);
            narrativeCache[roomIndex] = narrative;
            Debug.Log($"Room {roomIndex} narrative generated");
        });
    }

    private string BuildPrompt(int roomIndex) {
        float progress = (float)roomIndex / (totalRooms - 1);
        string phase = progress < 0.33f ? "beginning" : progress < 0.66f ? "middle" : "final";

        StringBuilder prompt = new StringBuilder();
        prompt.AppendLine($"Generate a {storyTheme} story segment for room {roomIndex + 1} of {totalRooms}.");
        prompt.AppendLine($"This is the {phase} of the adventure.");
        prompt.AppendLine();
        prompt.AppendLine("IMPORTANT: Output ONLY valid JSON. No extra text before or after.");
        prompt.AppendLine("The dialogue field MUST be an array of exactly 3 strings, like this example:");
        prompt.AppendLine("\"dialogue\": [\"First line here\", \"Second line here\", \"Third line here\"]");
        prompt.AppendLine();
        prompt.AppendLine("JSON format:");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"environment\": \"Brief atmospheric description (2-3 sentences)\",");
        prompt.AppendLine("  \"npc\": {");
        prompt.AppendLine("    \"name\": \"NPC name\",");
        prompt.AppendLine("    \"dialogue\": [\"line1\", \"line2\", \"line3\"]");
        prompt.AppendLine("  },");
        prompt.AppendLine("  \"quest\": {");
        prompt.AppendLine("    \"objective\": \"Quest description\",");
        prompt.AppendLine("    \"type\": \"DefeatEnemies\",");
        prompt.AppendLine("    \"count\": 3");
        prompt.AppendLine("  },");
        prompt.AppendLine("  \"lore\": {");
        prompt.AppendLine("    \"title\": \"Lore title\",");
        prompt.AppendLine("    \"content\": \"Lore backstory (2-3 sentences)\"");
        prompt.AppendLine("  }");
        prompt.AppendLine("}");

        return prompt.ToString();
    }

    private IEnumerator SendLLMRequest(string prompt, Action<string> onResponse) {
        string jsonData = $"{{\"model\":\"{modelName}\",\"prompt\":{EscapeJson(prompt)},\"stream\":false,\"options\":{{\"temperature\":0.7,\"seed\":{seed}}}}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(ollamaUrl, "POST")) {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 60;
            request.certificateHandler = new AcceptAllCertificates();
            request.disposeCertificateHandlerOnDispose = true;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success) {
                string responseText = request.downloadHandler.text;
                Debug.Log($"LLM Response length: {responseText.Length} chars");
                OllamaResponse ollamaResp = JsonUtility.FromJson<OllamaResponse>(responseText);
                onResponse?.Invoke(ollamaResp.response);
            } else {
                Debug.LogError($"❌ LLM request failed: {request.error}");
                Debug.LogError($"Response code: {request.responseCode}");
                onResponse?.Invoke(null);
            }
        }
    }

    private RoomNarrative ParseLLMResponse(string response, int roomIndex) {
        if (string.IsNullOrEmpty(response)) {
            Debug.LogError($"❌ Room {roomIndex}: Response is null or empty - LLM request failed");
            return null;
        }

        try {
            int jsonStart = response.IndexOf('{');
            int jsonEnd = response.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart) {
                string jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart);
                Debug.Log($"Room {roomIndex} extracted JSON:\n{jsonStr}");

                jsonStr = CleanJsonString(jsonStr);

                LLMNarrativeResponse llmData = JsonUtility.FromJson<LLMNarrativeResponse>(jsonStr);

                if (llmData == null || llmData.npc == null || llmData.quest == null || llmData.lore == null) {
                    Debug.LogError($"❌ Room {roomIndex}: Parsed JSON is incomplete");
                    return null;
                }

                Debug.Log($"✅ Room {roomIndex}: Parsed successfully - NPC: {llmData.npc.name}");

                return new RoomNarrative {
                    roomIndex = roomIndex,
                    environmentDescription = llmData.environment,
                    npcDialogues = new List<NPCDialogue> {
                        new NPCDialogue {
                            npcName = llmData.npc.name,
                            dialogueLines = llmData.npc.dialogue ?? new List<string>(),
                            spawnPosition = GetRandomSafePosition()
                        }
                    },
                    questObjective = new QuestObjective {
                        objectiveText = llmData.quest.objective,
                        type = ParseQuestType(llmData.quest.type),
                        targetCount = llmData.quest.count
                    },
                    loreEntries = new List<LoreEntry> {
                        new LoreEntry {
                            title = llmData.lore.title,
                            content = llmData.lore.content,
                            spawnPosition = GetRandomSafePosition()
                        }
                    }
                };
            } else {
                Debug.LogError($"❌ Room {roomIndex}: No valid JSON found in response");
                Debug.LogError($"Full response:\n{response}");
            }
        } catch (Exception e) {
            Debug.LogError($"❌ Room {roomIndex}: Failed to parse LLM response - {e.Message}");
            Debug.LogError($"Full response:\n{response}");
        }

        return null;
    }

    private string CleanJsonString(string json) {
        json = json.Trim();

        json = FixMalformedDialogueArray(json);

        json = System.Text.RegularExpressions.Regex.Replace(json, @"\r\n?|\n", " ");
        json = System.Text.RegularExpressions.Regex.Replace(json, @"\s+", " ");
        return json;
    }

    private string FixMalformedDialogueArray(string json) {
        var dialogueMatch = System.Text.RegularExpressions.Regex.Match(
            json,
            @"""dialogue"":\s*\[(.*?)(?:""line\d+"":|])",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );

        if (dialogueMatch.Success && dialogueMatch.Value.Contains("\"line")) {
            int dialogueStart = json.IndexOf("\"dialogue\":");
            int arrayStart = json.IndexOf('[', dialogueStart);
            int potentialEnd = json.IndexOf(']', arrayStart);

            var lineMatches = System.Text.RegularExpressions.Regex.Matches(
                json.Substring(arrayStart, potentialEnd - arrayStart + 1),
                @"""line\d+"":\s*""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (lineMatches.Count > 0) {
                List<string> dialogueLines = new List<string>();

                var firstLineMatch = System.Text.RegularExpressions.Regex.Match(
                    json.Substring(arrayStart, potentialEnd - arrayStart),
                    @"\[""([^""]+)""",
                    System.Text.RegularExpressions.RegexOptions.Singleline
                );
                if (firstLineMatch.Success) {
                    dialogueLines.Add(firstLineMatch.Groups[1].Value);
                }

                foreach (System.Text.RegularExpressions.Match match in lineMatches) {
                    dialogueLines.Add(match.Groups[1].Value);
                }

                string fixedDialogue = "\"dialogue\": [" +
                    string.Join(", ", dialogueLines.ConvertAll(line => $"\"{line}\"")) +
                    "]";

                int endBracket = json.IndexOf(']', arrayStart);
                int nextComma = json.IndexOf(',', endBracket);
                if (nextComma == -1) nextComma = json.IndexOf('}', endBracket);

                json = json.Substring(0, dialogueStart) + fixedDialogue + json.Substring(nextComma);

                Debug.Log($"Fixed malformed dialogue array: {dialogueLines.Count} lines extracted");
            }
        }

        return json;
    }

    private Vector2 GetRandomSafePosition() {
        float x = (float)(random.NextDouble() * 30 - 15);
        float y = (float)(random.NextDouble() * 30 - 15);
        return new Vector2(x, y);
    }

    private QuestType ParseQuestType(string typeStr) {
        if (Enum.TryParse(typeStr, out QuestType type)) {
            return type;
        }
        return QuestType.DefeatEnemies;
    }

    public RoomNarrative GetNarrative(int roomIndex) {
        if (narrativeCache.TryGetValue(roomIndex, out RoomNarrative narrative)) {
            return narrative;
        }
        Debug.LogWarning($"No narrative found for room {roomIndex}");
        return null;
    }

    public bool IsReady() {
        return !isGenerating && narrativeCache.Count == totalRooms;
    }

    private string EscapeJson(string str) {
        return "\"" + str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
    }
}

[Serializable]
public class OllamaResponse {
    public string response;
}

[Serializable]
public class LLMNarrativeResponse {
    public string environment;
    public NPCData npc;
    public QuestData quest;
    public LoreData lore;
}

[Serializable]
public class NPCData {
    public string name;
    public List<string> dialogue;
}

[Serializable]
public class QuestData {
    public string objective;
    public string type;
    public int count;
}

[Serializable]
public class LoreData {
    public string title;
    public string content;
}

public class AcceptAllCertificates : UnityEngine.Networking.CertificateHandler {
    protected override bool ValidateCertificate(byte[] certificateData) {
        return true;
    }
}
