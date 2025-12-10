using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour {
    private static DialogueUI instance;
    public static DialogueUI Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<DialogueUI>(true);
            }
            return instance;
        }
    }
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI buttonText;

    [Header("Settings")]
    [SerializeField] private GameObject[] hideWhileDialogueActive;

    private List<string> dialogueLines = new List<string>();
    private List<string> audioPaths = new List<string>();
    private int currentLineIndex = 0;
    private bool wasPlayerControlEnabled = true;

    private void Awake() {
        if (instance == null) {
            instance = this;
        } else if (instance != this) {
            Destroy(gameObject);
            return;
        }

        if (nextButton != null) {
            nextButton.onClick.AddListener(OnButtonClick);
        }
        HideDialogue();
    }

    public void ShowDialogue(NPCDialogue dialogue) {
        Debug.Log($"[DialogueUI] ShowDialogue called: {dialogue.npcName}, panel={(dialoguePanel != null ? "exists" : "NULL")}, npcNameText={(npcNameText != null ? "exists" : "NULL")}");

        if (npcNameText != null) {
            npcNameText.text = dialogue.npcName;
        }

        dialogueLines.Clear();
        dialogueLines.AddRange(dialogue.dialogueLines);

        audioPaths.Clear();
        if (dialogue.audioPaths != null) {
            audioPaths.AddRange(dialogue.audioPaths);
        }

        Debug.Log($"[DialogueUI] Loaded {dialogueLines.Count} lines, {audioPaths.Count} audio paths");

        currentLineIndex = 0;

        foreach (GameObject obj in hideWhileDialogueActive) {
            if (obj != null) obj.SetActive(false);
        }

        if (PlayerController.Instance != null) {
            wasPlayerControlEnabled = PlayerController.Instance.enabled;
            PlayerController.Instance.enabled = false;
        }

        if (dialoguePanel != null) {
            dialoguePanel.SetActive(true);
            Debug.Log($"[DialogueUI] Panel activated");
        } else {
            Debug.LogError("[DialogueUI] dialoguePanel is NULL - cannot show dialogue!");
        }

        try {
            Debug.Log($"[DialogueUI] About to call DisplayCurrentLine");
            DisplayCurrentLine();
            Debug.Log($"[DialogueUI] DisplayCurrentLine completed");
        } catch (System.Exception e) {
            Debug.LogError($"[DialogueUI] Exception in DisplayCurrentLine: {e.Message}\n{e.StackTrace}");
        }
    }

    private void OnButtonClick() {
        AdvanceDialogue();
    }

    private void AdvanceDialogue() {
        currentLineIndex++;

        if (currentLineIndex >= dialogueLines.Count) {
            HideDialogue();
        } else {
            DisplayCurrentLine();
        }
    }

    private void DisplayCurrentLine() {
        Debug.Log($"[DialogueUI] DisplayCurrentLine called: currentLineIndex={currentLineIndex}, dialogueLines.Count={dialogueLines.Count}");

        if (currentLineIndex < dialogueLines.Count) {
            Debug.Log($"[DialogueUI] Displaying line: dialogueText={(dialogueText != null ? "exists" : "NULL")}");
            if (dialogueText != null) {
                dialogueText.text = dialogueLines[currentLineIndex];
                Debug.Log($"[DialogueUI] Text set to: {dialogueLines[currentLineIndex]}");
            }
            UpdateButtonText();

            // Play audio for current line if available
            bool hasAudioManager = NarrativeAudioManager.Instance != null;
            bool hasAudioPath = currentLineIndex < audioPaths.Count && !string.IsNullOrEmpty(audioPaths[currentLineIndex]);

            Debug.Log($"[DialogueUI] Line {currentLineIndex}: hasAudioManager={hasAudioManager}, hasAudioPath={hasAudioPath}, audioPaths.Count={audioPaths.Count}");

            if (hasAudioManager && hasAudioPath)
            {
                Debug.Log($"[DialogueUI] Playing audio: {audioPaths[currentLineIndex]}");
                NarrativeAudioManager.Instance.PlayDialogue(new[] { audioPaths[currentLineIndex] });
            }
        }
    }

    private void UpdateButtonText() {
        if (buttonText == null) return;

        if (currentLineIndex >= dialogueLines.Count - 1) {
            buttonText.text = "Terminate [E]";
        } else {
            buttonText.text = "Continue [E]";
        }
    }

    public void HideDialogue() {
        dialoguePanel?.SetActive(false);
        dialogueLines.Clear();
        audioPaths.Clear();
        currentLineIndex = 0;

        // Stop any playing audio
        if (NarrativeAudioManager.Instance != null) {
            NarrativeAudioManager.Instance.StopPlayback();
        }

        foreach (GameObject obj in hideWhileDialogueActive) {
            if (obj != null) obj.SetActive(true);
        }

        if (PlayerController.Instance != null) {
            PlayerController.Instance.enabled = wasPlayerControlEnabled;
        }
    }

    private void Update() {
        if (dialoguePanel != null && dialoguePanel.activeSelf) {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) {
                AdvanceDialogue();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) {
                HideDialogue();
            }
        }
    }

    public bool IsDialogueActive() {
        return dialoguePanel != null && dialoguePanel.activeSelf;
    }
}
