using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoomNode
{
    public int id;
    public RoomTemplate template;
    public Vector2Int gridPosition;
    public RoomArchetype archetype;

    public Dictionary<Direction, RoomNode> connections = new Dictionary<Direction, RoomNode>();

    public bool isCleared;
    public bool isDiscovered;
    public bool isStartRoom;
    public bool isBossRoom;

    // Runtime references (null if room not loaded)
    [System.NonSerialized] public GameObject instance;
    [System.NonSerialized] public RoomInstance roomInstance;
    [System.NonSerialized] public List<GameObject> spawnedEnemies = new List<GameObject>();
    [System.NonSerialized] public List<GameObject> spawnedObjects = new List<GameObject>();

    public RoomNode(int id, Vector2Int gridPos)
    {
        this.id = id;
        this.gridPosition = gridPos;
        connections = new Dictionary<Direction, RoomNode>();
        spawnedEnemies = new List<GameObject>();
        spawnedObjects = new List<GameObject>();
    }

    public void Connect(Direction dir, RoomNode other)
    {
        if (other == null) return;
        connections[dir] = other;
        other.connections[dir.Opposite()] = this;
    }

    public RoomNode GetConnection(Direction dir)
    {
        return connections.TryGetValue(dir, out var node) ? node : null;
    }

    public bool HasConnection(Direction dir)
    {
        return connections.ContainsKey(dir);
    }

    public DoorMask GetRequiredDoors()
    {
        DoorMask mask = DoorMask.None;
        foreach (var kvp in connections)
        {
            mask = mask.Add(kvp.Key);
        }
        return mask;
    }

    public int ConnectionCount => connections.Count;

    public bool IsLoaded => instance != null;

    public void ClearRuntimeReferences()
    {
        if (instance != null)
        {
            Object.Destroy(instance);
            instance = null;
        }
        roomInstance = null;
        spawnedEnemies.Clear();
        spawnedObjects.Clear();
    }
}
