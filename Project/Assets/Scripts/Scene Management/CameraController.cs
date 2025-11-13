using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class CameraController : Singleton<CameraController>
{
    private CinemachineVirtualCamera cinemachineVirtualCamera;

    private void Start() {
        SetPlayerCameraFollow();
    }

    public void SetPlayerCameraFollow() {
        cinemachineVirtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        
        if (PlayerController.Instance != null)
        {
            cinemachineVirtualCamera.Follow = PlayerController.Instance.transform;
        }
        else
        {
            cinemachineVirtualCamera.Follow = null;
            cinemachineVirtualCamera.transform.position = new Vector3(0, 0.5f, -10);
        }
    }
}
