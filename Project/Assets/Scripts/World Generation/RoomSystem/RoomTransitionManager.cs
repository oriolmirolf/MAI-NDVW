using System.Collections;
using UnityEngine;
using Cinemachine;

public class RoomTransitionManager : MonoBehaviour
{
    public static RoomTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float fadeInDuration = 0.2f;
    [SerializeField] private bool disablePlayerDuringTransition = true;

    [Header("References")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    private bool isTransitioning;
    private PlayerController player;

    public bool IsTransitioning => isTransitioning;

    public event System.Action<RoomNode> OnTransitionStarted;
    public event System.Action<RoomNode> OnTransitionCompleted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        player = PlayerController.Instance;
    }

    public void TransitionTo(RoomNode targetRoom, Direction fromDirection)
    {
        if (isTransitioning || targetRoom == null) return;

        StartCoroutine(TransitionRoutine(targetRoom, fromDirection));
    }

    private IEnumerator TransitionRoutine(RoomNode targetRoom, Direction fromDirection)
    {
        isTransitioning = true;
        OnTransitionStarted?.Invoke(targetRoom);

        // Disable player input
        if (disablePlayerDuringTransition && player != null)
        {
            SetPlayerInputEnabled(false);
        }

        // Fade out
        if (UIFade.Instance != null)
        {
            UIFade.Instance.FadeToBlack();
            yield return new WaitForSeconds(fadeOutDuration);
        }

        // Update dungeon graph current room
        DungeonGraph.Instance.SetCurrentRoom(targetRoom);

        // Get spawn position from target room
        Vector3 spawnPos = GetSpawnPosition(targetRoom, fromDirection);

        // Teleport player
        if (player != null)
        {
            player.transform.position = spawnPos;
        }

        // Snap camera instantly
        SnapCamera(spawnPos);

        // Update camera bounds
        UpdateCameraBounds(targetRoom);

        // Small delay for everything to settle
        yield return new WaitForSeconds(0.05f);

        // Fade in
        if (UIFade.Instance != null)
        {
            UIFade.Instance.FadeToClear();
            yield return new WaitForSeconds(fadeInDuration);
        }

        // Re-enable player input
        if (disablePlayerDuringTransition && player != null)
        {
            SetPlayerInputEnabled(true);
        }

        isTransitioning = false;
        OnTransitionCompleted?.Invoke(targetRoom);
    }

    private Vector3 GetSpawnPosition(RoomNode targetRoom, Direction fromDirection)
    {
        if (targetRoom.roomInstance != null)
        {
            return targetRoom.roomInstance.GetSpawnPositionForDirection(fromDirection);
        }

        // Fallback: room center
        if (targetRoom.instance != null)
        {
            return targetRoom.instance.transform.position;
        }

        return Vector3.zero;
    }

    private void SnapCamera(Vector3 targetPosition)
    {
        if (virtualCamera == null) return;

        // Use Cinemachine's warp function for instant snap
        if (player != null)
        {
            virtualCamera.OnTargetObjectWarped(
                player.transform,
                targetPosition - player.transform.position
            );
        }

        // Also force position update
        virtualCamera.PreviousStateIsValid = false;
    }

    private void UpdateCameraBounds(RoomNode room)
    {
        if (room.roomInstance == null || room.roomInstance.CameraBounds == null) return;

        var confiner = FindObjectOfType<CinemachineConfiner2D>();
        if (confiner != null)
        {
            confiner.m_BoundingShape2D = room.roomInstance.CameraBounds;
            confiner.InvalidateCache();
        }
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        if (player == null) return;

        // Disable/enable player controller
        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = enabled;
        }

        // Also disable rigidbody movement
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (!enabled)
            {
                rb.velocity = Vector2.zero;
            }
        }
    }

    public void InstantTransition(RoomNode targetRoom, Direction fromDirection)
    {
        if (targetRoom == null) return;

        DungeonGraph.Instance.SetCurrentRoom(targetRoom);

        Vector3 spawnPos = GetSpawnPosition(targetRoom, fromDirection);

        if (player != null)
        {
            player.transform.position = spawnPos;
        }

        SnapCamera(spawnPos);
        UpdateCameraBounds(targetRoom);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
