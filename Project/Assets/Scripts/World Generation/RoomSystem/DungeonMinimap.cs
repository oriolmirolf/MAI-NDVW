using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DungeonMinimap : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private GameObject roomIconPrefab;

    [Header("Settings")]
    [SerializeField] private float roomIconSize = 20f;
    [SerializeField] private float roomSpacing = 5f;
    [SerializeField] private Color undiscoveredColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color discoveredColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color currentRoomColor = Color.white;
    [SerializeField] private Color bossRoomColor = Color.red;
    [SerializeField] private Color clearedColor = Color.green;

    private Dictionary<int, RectTransform> roomIcons = new Dictionary<int, RectTransform>();
    private Dictionary<int, Image> roomImages = new Dictionary<int, Image>();

    private void OnEnable()
    {
        if (DungeonGraph.Instance != null)
        {
            DungeonGraph.Instance.OnDungeonGenerated += BuildMinimap;
            DungeonGraph.Instance.OnRoomChanged += UpdateMinimap;
        }
    }

    private void OnDisable()
    {
        if (DungeonGraph.Instance != null)
        {
            DungeonGraph.Instance.OnDungeonGenerated -= BuildMinimap;
            DungeonGraph.Instance.OnRoomChanged -= UpdateMinimap;
        }
    }

    private void Start()
    {
        if (DungeonGraph.Instance != null && DungeonGraph.Instance.Rooms.Count > 0)
        {
            BuildMinimap();
        }
    }

    public void BuildMinimap()
    {
        ClearMinimap();

        if (DungeonGraph.Instance == null) return;

        var rooms = DungeonGraph.Instance.Rooms;
        if (rooms.Count == 0) return;

        // Find bounds
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var room in rooms.Values)
        {
            minX = Mathf.Min(minX, room.gridPosition.x);
            maxX = Mathf.Max(maxX, room.gridPosition.x);
            minY = Mathf.Min(minY, room.gridPosition.y);
            maxY = Mathf.Max(maxY, room.gridPosition.y);
        }

        // Create icons
        foreach (var room in rooms.Values)
        {
            CreateRoomIcon(room, minX, minY);
        }

        // Draw connections
        foreach (var room in rooms.Values)
        {
            foreach (var connection in room.connections)
            {
                if (connection.Key == Direction.East || connection.Key == Direction.North)
                {
                    CreateConnectionLine(room, connection.Value, minX, minY);
                }
            }
        }

        UpdateAllRoomColors();
    }

    private void CreateRoomIcon(RoomNode room, int offsetX, int offsetY)
    {
        if (roomIconPrefab == null || mapContainer == null) return;

        var iconObj = Instantiate(roomIconPrefab, mapContainer);
        var rt = iconObj.GetComponent<RectTransform>();
        var img = iconObj.GetComponent<Image>();

        if (rt == null || img == null) return;

        // Position
        float x = (room.gridPosition.x - offsetX) * (roomIconSize + roomSpacing);
        float y = (room.gridPosition.y - offsetY) * (roomIconSize + roomSpacing);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(roomIconSize, roomIconSize);

        roomIcons[room.id] = rt;
        roomImages[room.id] = img;

        // Initial color
        img.color = undiscoveredColor;
    }

    private void CreateConnectionLine(RoomNode from, RoomNode to, int offsetX, int offsetY)
    {
        // Simple implementation - could be enhanced with line renderer
        // For now, connection visibility is implied by adjacent room icons
    }

    private void UpdateMinimap(RoomNode previous, RoomNode current)
    {
        UpdateAllRoomColors();
    }

    private void UpdateAllRoomColors()
    {
        if (DungeonGraph.Instance == null) return;

        var currentRoom = DungeonGraph.Instance.CurrentRoom;

        foreach (var room in DungeonGraph.Instance.Rooms.Values)
        {
            if (!roomImages.TryGetValue(room.id, out var img)) continue;

            Color color;

            if (room == currentRoom)
            {
                color = currentRoomColor;
            }
            else if (room.isBossRoom && room.isDiscovered)
            {
                color = room.isCleared ? clearedColor : bossRoomColor;
            }
            else if (room.isCleared)
            {
                color = clearedColor;
            }
            else if (room.isDiscovered)
            {
                color = discoveredColor;
            }
            else
            {
                color = undiscoveredColor;
            }

            img.color = color;
        }
    }

    private void ClearMinimap()
    {
        foreach (var icon in roomIcons.Values)
        {
            if (icon != null) Destroy(icon.gameObject);
        }
        roomIcons.Clear();
        roomImages.Clear();
    }

    public void RefreshMinimap()
    {
        UpdateAllRoomColors();
    }
}
