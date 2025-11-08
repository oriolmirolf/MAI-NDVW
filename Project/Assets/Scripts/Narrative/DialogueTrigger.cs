using System.Collections.Generic;
using UnityEngine;

public class DialogueTrigger : MonoBehaviour {
    [Header("Trigger Settings")]
    [SerializeField] private KeyCode triggerKey = KeyCode.T;
    [SerializeField] private bool useManualDialogue = true;

    [Header("Manual Dialogue")]
    [SerializeField] private string npcName = "Wanderer";
    [SerializeField] private List<string> dialogueLines = new List<string> {
        "Greetings, traveler.",
        "This place holds many dangers.",
        "Be careful on your journey."
    };

    [Header("LLM Dialogue (if useManualDialogue = false)")]
    [SerializeField] private int roomIndex = 0;
    [SerializeField] private int npcIndex = 0;

    private void Update() {
        if (Input.GetKeyDown(triggerKey)) {
            if (DialogueUI.Instance != null && DialogueUI.Instance.IsDialogueActive()) {
                return;
            }

            if (useManualDialogue) {
                TriggerDialogue();
            } else {
                TriggerLLMDialogue(roomIndex, npcIndex);
            }
        }
    }

    public void TriggerDialogue() {
        if (DialogueUI.Instance == null) {
            Debug.LogError("DialogueUI not found in scene!");
            return;
        }

        NPCDialogue dialogue = new NPCDialogue {
            npcName = npcName,
            dialogueLines = dialogueLines
        };

        DialogueUI.Instance.ShowDialogue(dialogue);
    }

    public void TriggerLLMDialogue(int room, int npc = 0) {
        if (DialogueUI.Instance == null) {
            Debug.LogError("DialogueUI not found in scene!");
            return;
        }

        if (LLMNarrativeGenerator.Instance == null) {
            Debug.LogError("LLMNarrativeGenerator not found in scene!");
            return;
        }

        RoomNarrative narrative = LLMNarrativeGenerator.Instance.GetNarrative(room);
        if (narrative != null && narrative.npcDialogues.Count > npc) {
            DialogueUI.Instance.ShowDialogue(narrative.npcDialogues[npc]);
        } else {
            Debug.LogWarning($"No dialogue found for room {room}, NPC {npc}");
        }
    }
}
