using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class RoomScreenshotCapture : MonoBehaviour
{
    [Header("Screenshot Settings")]
    [SerializeField] private int screenshotWidth = 1024;
    [SerializeField] private int screenshotHeight = 1024;
    [SerializeField] private string screenshotFolder = "room_screenshots";

    private Camera screenshotCamera;
    private string ScreenshotPath => Path.Combine(Application.persistentDataPath, screenshotFolder);

    private void Awake()
    {
        Directory.CreateDirectory(ScreenshotPath);
    }

    public IEnumerator CaptureRoomScreenshot(int roomIndex, Action<string> onComplete)
    {
        string filename = $"room_{roomIndex}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string fullPath = Path.Combine(ScreenshotPath, filename);

        yield return CaptureScreenshotToFile(fullPath);

        Debug.Log($"Screenshot saved: {fullPath}");
        onComplete?.Invoke(fullPath);
    }

    private IEnumerator CaptureScreenshotToFile(string path)
    {
        yield return new WaitForEndOfFrame();

        RenderTexture renderTexture = new RenderTexture(screenshotWidth, screenshotHeight, 24);
        Camera cam = GetScreenshotCamera();

        RenderTexture previousRT = cam.targetTexture;
        cam.targetTexture = renderTexture;
        cam.Render();

        RenderTexture.active = renderTexture;
        Texture2D screenshot = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
        screenshot.Apply();

        cam.targetTexture = previousRT;
        RenderTexture.active = null;
        Destroy(renderTexture);

        byte[] bytes = screenshot.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Destroy(screenshot);
    }

    private Camera GetScreenshotCamera()
    {
        if (screenshotCamera == null)
        {
            screenshotCamera = Camera.main;
        }
        return screenshotCamera;
    }

    public void SetCamera(Camera camera)
    {
        screenshotCamera = camera;
    }
}
