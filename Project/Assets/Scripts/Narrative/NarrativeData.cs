using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NarrativeCacheData {
    public List<RoomNarrative> rooms = new List<RoomNarrative>();
}

[Serializable]
public class RoomNarrative {
    public int roomIndex;
    public string environmentDescription;
    public List<NPCDialogue> npcDialogues = new List<NPCDialogue>();
    public QuestObjective questObjective;
    public List<LoreEntry> loreEntries = new List<LoreEntry>();
}

[Serializable]
public class NPCDialogue {
    public string npcName;
    public List<string> dialogueLines;
    public Vector2 spawnPosition;
}

[Serializable]
public class QuestObjective {
    public string objectiveText;
    public QuestType type;
    public int targetCount;
}

[Serializable]
public class LoreEntry {
    public string title;
    public string content;
    public Vector2 spawnPosition;
}

public enum QuestType {
    DefeatEnemies,
    FindItem,
    ReachLocation,
    Survive
}
