using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraController : Singleton<CameraController>
{
    private CinemachineVirtualCamera cinemachineVirtualCamera;

    private void Start() {
        cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();

        // For dungeon scenes with room system, camera is controlled by RoomInstance.SnapCameraToRoom()
        // Only set player follow if there's no DungeonGraph (non-dungeon scenes)
        if (DungeonGraph.Instance == null)
        {
            SetPlayerCameraFollow();
        }
    }

    public void SetPlayerCameraFollow() {
        if (cinemachineVirtualCamera == null)
            cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();

        if (cinemachineVirtualCamera != null && PlayerController.Instance != null)
        {
            cinemachineVirtualCamera.Follow = PlayerController.Instance.transform;
        }
    }

    public void SetRoomCameraTarget(Transform target) {
        if (cinemachineVirtualCamera == null)
            cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();

        if (cinemachineVirtualCamera != null)
        {
            cinemachineVirtualCamera.Follow = target;
            cinemachineVirtualCamera.PreviousStateIsValid = false;
        }
    }
}
