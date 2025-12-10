using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AspectRatioEnforcer : MonoBehaviour
{
    [SerializeField] private float targetAspect = 16f / 9f; // 32:18 = 16:9
    [SerializeField] private Color letterboxColor = Color.black;

    private Camera cam;
    private Camera letterboxCam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        SetupLetterboxCamera();
        UpdateViewport();
    }

    private void SetupLetterboxCamera()
    {
        // Create a camera to render black bars
        var letterboxObj = new GameObject("LetterboxCamera");
        letterboxObj.transform.SetParent(transform);
        letterboxCam = letterboxObj.AddComponent<Camera>();
        letterboxCam.depth = cam.depth - 1;
        letterboxCam.cullingMask = 0;
        letterboxCam.clearFlags = CameraClearFlags.SolidColor;
        letterboxCam.backgroundColor = letterboxColor;
        letterboxCam.orthographic = true;
    }

    private void Update()
    {
        UpdateViewport();
    }

    private void UpdateViewport()
    {
        float windowAspect = (float)Screen.width / Screen.height;
        float scaleHeight = windowAspect / targetAspect;

        if (scaleHeight < 1f)
        {
            // Pillarbox (black bars on sides)
            float scaleWidth = 1f / scaleHeight;
            cam.rect = new Rect((1f - scaleWidth) / 2f, 0f, scaleWidth, 1f);
        }
        else
        {
            // Letterbox (black bars on top/bottom)
            cam.rect = new Rect(0f, (1f - scaleHeight) / 2f, 1f, scaleHeight);
        }
    }

    public void SetTargetAspect(float width, float height)
    {
        targetAspect = width / height;
        UpdateViewport();
    }
}
