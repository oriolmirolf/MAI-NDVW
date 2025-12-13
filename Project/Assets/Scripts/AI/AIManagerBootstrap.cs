using UnityEngine;

/// <summary>
/// Automatically creates all AI-related managers if they don't exist.
/// Add this component to any persistent GameObject in the scene.
/// </summary>
public class AIManagerBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("AIManagers");
        DontDestroyOnLoad(go);

        go.AddComponent<GeneratedContentLoader>();
        go.AddComponent<LLMNarrativeGenerator>();
        go.AddComponent<ChapterMusicManager>();
        go.AddComponent<IntroductionDialogue>();

        Debug.Log("[AI] AIManagerBootstrap created all managers");
    }
}
