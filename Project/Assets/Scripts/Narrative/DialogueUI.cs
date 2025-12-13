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
    private Dictionary<int, AudioClip> voiceClipCache = new Dictionary<int, AudioClip>();
    private int currentLineIndex = 0;
    private int currentChapter = 0;
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

    public void ShowDialogue(NPCDialogue dialogue, Action onComplete = null, int chapter = 0)
    {
        onDialogueComplete = onComplete;
        currentChapter = chapter;

        if (dialoguePanel == null)
            return;

        if (npcNameText != null)
            npcNameText.text = dialogue.npcName;

        dialogueLines.Clear();
        dialogueLines.AddRange(dialogue.dialogueLines);

        voiceClipCache.Clear();
        currentLineIndex = 0;

        foreach (var obj in hideWhileDialogueActive)
            if (obj != null) obj.SetActive(false);

        if (ChapterMusicManager.Instance != null)
            ChapterMusicManager.Instance.FadeOutForDialogue();

        isShowingDialogue = true;
        dialoguePanel.SetActive(true);
        Time.timeScale = 0f;

        if (enableVoice && GeneratedContentLoader.Instance != null && GeneratedContentLoader.Instance.HasContent)
            StartCoroutine(PreloadAndDisplay());
        else
            DisplayCurrentLine();
    }

    private IEnumerator PreloadAndDisplay()
    {
        // Load first voice clip before displaying
        bool firstDone = false;
        yield return GeneratedContentLoader.Instance.LoadChapterVoice(
            currentChapter, 0,
            clip => { if (clip != null) voiceClipCache[0] = clip; firstDone = true; },
            error => { firstDone = true; }
        );
        yield return new WaitUntil(() => firstDone);

        DisplayCurrentLine();

        // Continue loading remaining clips in background
        StartCoroutine(PreloadVoiceClips());
    }

    private IEnumerator PreloadVoiceClips()
    {
        // Start from 1 since index 0 is loaded in PreloadAndDisplay
        for (int i = 1; i < dialogueLines.Count; i++)
        {
            int index = i;
            bool done = false;

            yield return GeneratedContentLoader.Instance.LoadChapterVoice(
                currentChapter, i,
                clip => { if (clip != null) voiceClipCache[index] = clip; done = true; },
                error => { done = true; }
            );

            yield return new WaitUntil(() => done);
        }
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
            UnpauseGame();
            return;
        }

        StopVoice();

        if (voiceClipCache.TryGetValue(currentLineIndex, out AudioClip clip) && clip != null)
        {
            voiceSource.clip = clip;
            voiceSource.volume = voiceVolume;
            voiceSource.Play();
            UnpauseGame();

            if (autoAdvanceAfterVoice)
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfterClip(clip.length));
        }
        else
        {
            UnpauseGame();
        }
    }

    private void UnpauseGame()
    {
        if (Time.timeScale == 0f)
            Time.timeScale = 1f;
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
        voiceClipCache.Clear();
        currentLineIndex = 0;

        foreach (var obj in hideWhileDialogueActive)
            if (obj != null) obj.SetActive(true);

        Time.timeScale = 1f;

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
