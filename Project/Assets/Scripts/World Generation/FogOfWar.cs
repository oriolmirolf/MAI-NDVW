using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FogOfWar : MonoBehaviour {
    [SerializeField] private Light2D playerLight;
    [SerializeField] private float visionRadius = 10f;
    [SerializeField] private float lightIntensity = 1.2f;

    private void Start() {
        SetupPlayerLight();
    }

    private void SetupPlayerLight() {
        if (playerLight == null && PlayerController.Instance != null) {
            GameObject lightObj = new GameObject("PlayerVisionLight");
            lightObj.transform.parent = PlayerController.Instance.transform;
            lightObj.transform.localPosition = Vector3.zero;

            playerLight = lightObj.AddComponent<Light2D>();
            playerLight.lightType = Light2D.LightType.Point;
            playerLight.pointLightOuterRadius = visionRadius;
            playerLight.intensity = lightIntensity;
            playerLight.color = Color.white;
        }
    }

    private void Update() {
        if (playerLight == null) {
            SetupPlayerLight();
        }
    }
}
