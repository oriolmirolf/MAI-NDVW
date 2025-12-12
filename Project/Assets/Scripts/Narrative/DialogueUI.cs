using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    private static DialogueUI instance;
    public static DialogueUI Instance => instance ??= FindObjectOfType<DialogueUI>(true);

    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI buttonText;

    [Header("Voice Settings")]
    [SerializeField] private bool enableVoice = true;
    [SerializeField] private bool autoAdvanceAfterVoice = true;
    [SerializeField] private float autoAdvanceDelay = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float voiceVolume = 1f;

    [Header("Settings")]
    [SerializeField] private GameObject[] hideWhileDialogueActive;

    private List<string> dialogueLines = new List<string>();
    private List<string> audioPaths = new List<string>();
    private Dictionary<int, AudioClip> voiceClipCache = new Dictionary<int, AudioClip>();
    private int currentLineIndex = 0;
    private AudioSource voiceSource;
    private Coroutine autoAdvanceCoroutine;
    private Action onDialogueComplete;
    private bool isShowingDialogue = false;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetupAudioSource();
        nextButton?.onClick.AddListener(AdvanceDialogue);

        if (dialoguePanel != null && !isShowingDialogue)
            dialoguePanel.SetActive(false);
    }

    private void SetupAudioSource()
    {
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.volume = voiceVolume;
    }

    public void ShowDialogue(NPCDialogue dialogue, Action onComplete = null)
    {
        onDialogueComplete = onComplete;

        if (dialoguePanel == null)
            return;

        if (npcNameText != null)
            npcNameText.text = dialogue.npcName;

        dialogueLines.Clear();
        dialogueLines.AddRange(dialogue.dialogueLines);

        audioPaths.Clear();
        if (dialogue.audioPaths != null && dialogue.audioPaths.Count > 0)
        {
            audioPaths.AddRange(dialogue.audioPaths);
            Debug.Log($"[DIALOGUE] Voice enabled: {audioPaths.Count} audio paths loaded");
            for (int i = 0; i < audioPaths.Count; i++)
                Debug.Log($"[DIALOGUE] Audio path {i}: '{audioPaths[i]}'");
        }
        else
        {
            Debug.Log($"[DIALOGUE] No audio paths provided for {dialogue.npcName}");
        }

        voiceClipCache.Clear();
        currentLineIndex = 0;

        foreach (var obj in hideWhileDialogueActive)
            if (obj != null) obj.SetActive(false);

        // Fade out music during dialogue
        if (ChapterMusicManager.Instance != null)
            ChapterMusicManager.Instance.FadeOutForDialogue();

        isShowingDialogue = true;
        dialoguePanel.SetActive(true);

        // Pause game during dialogue
        Time.timeScale = 0f;

        if (enableVoice && audioPaths.Count > 0 && GenAIClient.Instance != null)
            StartCoroutine(PreloadVoiceClips());
        else if (!enableVoice)
            Debug.Log("[DIALOGUE] Voice disabled in settings");
        else if (GenAIClient.Instance == null)
            Debug.LogWarning("[DIALOGUE] GenAIClient not available for voice download");

        DisplayCurrentLine();
    }

    private IEnumerator PreloadVoiceClips()
    {
        Debug.Log($"[DIALOGUE] Starting to preload {audioPaths.Count} voice clips");
        for (int i = 0; i < audioPaths.Count; i++)
        {
            if (string.IsNullOrEmpty(audioPaths[i]))
            {
                Debug.LogWarning($"[DIALOGUE] Skipping empty audio path at index {i}");
                continue;
            }

            int index = i;
            bool done = false;
            string path = audioPaths[i];

            Debug.Log($"[DIALOGUE] Downloading voice clip {i}: {path}");
            yield return GenAIClient.Instance.DownloadVoice(
                path,
                (clip) => {
                    if (clip != null)
                    {
                        voiceClipCache[index] = clip;
                        Debug.Log($"[DIALOGUE] Voice clip {index} loaded: {clip.length:F2}s, {clip.frequency}Hz");
                    }
                    else
                    {
                        Debug.LogError($"[DIALOGUE] Voice clip {index} is null despite successful download");
                    }
                    done = true;
                },
                (error) => {
                    Debug.LogError($"[DIALOGUE] Failed to download voice clip {index} ({path}): {error}");
                    done = true;
                }
            );

            yield return new WaitUntil(() => done);
        }
        Debug.Log($"[DIALOGUE] Preload complete. Cached {voiceClipCache.Count}/{audioPaths.Count} clips");
    }

    private void AdvanceDialogue()
    {
        StopVoice();

        currentLineIndex++;
        if (currentLineIndex >= dialogueLines.Count)
            HideDialogue();
        else
            DisplayCurrentLine();
    }

    private void DisplayCurrentLine()
    {
        if (currentLineIndex < dialogueLines.Count && dialogueText != null)
        {
            dialogueText.text = dialogueLines[currentLineIndex];
            UpdateButtonText();
            PlayCurrentVoice();
        }
    }

    private void PlayCurrentVoice()
    {
        if (!enableVoice)
        {
            Debug.Log("[DIALOGUE] Voice playback disabled");
            // No voice - unpause immediately so player can move
            UnpauseGame();
            return;
        }

        StopVoice();

        if (voiceClipCache.TryGetValue(currentLineIndex, out AudioClip clip) && clip != null)
        {
            voiceSource.clip = clip;
            voiceSource.volume = voiceVolume;
            voiceSource.Play();
            Debug.Log($"[DIALOGUE] Playing voice clip {currentLineIndex} ({clip.length:F2}s)");

            // Unpause game when voice starts - player can move while listening
            UnpauseGame();

            if (autoAdvanceAfterVoice)
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfterClip(clip.length));
        }
        else if (currentLineIndex < audioPaths.Count && !string.IsNullOrEmpty(audioPaths[currentLineIndex]))
        {
            Debug.Log($"[DIALOGUE] Voice clip {currentLineIndex} not ready, waiting...");
            StartCoroutine(PlayVoiceWhenReady(currentLineIndex));
        }
        else
        {
            Debug.Log($"[DIALOGUE] No voice for line {currentLineIndex} (paths={audioPaths.Count}, cached={voiceClipCache.Count})");
            // No voice - unpause immediately so player can move
            UnpauseGame();
        }
    }

    private void UnpauseGame()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
            Debug.Log("[DIALOGUE] Game unpaused - player can move");
        }
    }

    private IEnumerator PlayVoiceWhenReady(int lineIndex)
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (!voiceClipCache.ContainsKey(lineIndex) && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (currentLineIndex == lineIndex && voiceClipCache.TryGetValue(lineIndex, out AudioClip clip))
        {
            voiceSource.clip = clip;
            voiceSource.volume = voiceVolume;
            voiceSource.Play();

            // Unpause game when voice starts - player can move while listening
            UnpauseGame();

            if (autoAdvanceAfterVoice)
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfterClip(clip.length));
        }
        else
        {
            // Voice didn't load in time - unpause anyway so player can move
            UnpauseGame();
        }
    }

    private IEnumerator AutoAdvanceAfterClip(float clipLength)
    {
        yield return new WaitForSecondsRealtime(clipLength + autoAdvanceDelay);

        if (dialoguePanel != null && dialoguePanel.activeSelf)
            AdvanceDialogue();
    }

    private void StopVoice()
    {
        if (autoAdvanceCoroutine != null)
        {
            StopCoroutine(autoAdvanceCoroutine);
            autoAdvanceCoroutine = null;
        }

        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.Stop();
    }

    private void UpdateButtonText()
    {
        if (buttonText != null)
        {
            string skipText = enableVoice && voiceSource != null && voiceSource.isPlaying ? " (Skip)" : "";
            buttonText.text = currentLineIndex >= dialogueLines.Count - 1
                ? $"Terminate [E]{skipText}"
                : $"Continue [E]{skipText}";
        }
    }

    public void HideDialogue()
    {
        bool wasActive = dialoguePanel != null && dialoguePanel.activeSelf;

        StopVoice();
        isShowingDialogue = false;
        dialoguePanel?.SetActive(false);
        dialogueLines.Clear();
        audioPaths.Clear();
        voiceClipCache.Clear();
        currentLineIndex = 0;

        foreach (var obj in hideWhileDialogueActive)
            if (obj != null) obj.SetActive(true);

        // Unpause game when dialogue ends
        Time.timeScale = 1f;

        // Fade in music after dialogue
        if (ChapterMusicManager.Instance != null)
            ChapterMusicManager.Instance.FadeInAfterDialogue();

        if (wasActive)
        {
            var callback = onDialogueComplete;
            onDialogueComplete = null;
            callback?.Invoke();
        }
    }

    private void Update()
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.E))
                AdvanceDialogue();
            if (Input.GetKeyDown(KeyCode.Escape))
                HideDialogue();
        }
    }

    public bool IsDialogueActive() => dialoguePanel != null && dialoguePanel.activeSelf;
    public bool IsVoicePlaying() => voiceSource != null && voiceSource.isPlaying;
}
