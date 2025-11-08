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
        if (npcNameText != null) {
            npcNameText.text = dialogue.npcName;
        }

        dialogueLines.Clear();
        dialogueLines.AddRange(dialogue.dialogueLines);
        currentLineIndex = 0;

        foreach (GameObject obj in hideWhileDialogueActive) {
            if (obj != null) obj.SetActive(false);
        }

        if (PlayerController.Instance != null) {
            wasPlayerControlEnabled = PlayerController.Instance.enabled;
            PlayerController.Instance.enabled = false;
        }

        dialoguePanel?.SetActive(true);
        DisplayCurrentLine();
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
        if (currentLineIndex < dialogueLines.Count) {
            if (dialogueText != null) {
                dialogueText.text = dialogueLines[currentLineIndex];
            }
            UpdateButtonText();
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
        currentLineIndex = 0;

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
