using UnityEngine;

public class IntroductionDialogue : MonoBehaviour {
    [Header("Settings")]
    [SerializeField] private int introRoomIndex = 0;
    [SerializeField] private int introNPCIndex = 0;
    [SerializeField] private float delayBeforeShow = 1f;

    private bool hasShown = false;

    private void Start() {
        Invoke(nameof(ShowIntroduction), delayBeforeShow);
    }

    private void ShowIntroduction() {
        if (hasShown) return;

        if (LLMNarrativeGenerator.Instance == null) {
            Debug.LogWarning("LLMNarrativeGenerator not found, cannot show introduction");
            return;
        }

        if (!LLMNarrativeGenerator.Instance.IsReady()) {
            Debug.Log("Narratives not ready yet, waiting...");
            Invoke(nameof(ShowIntroduction), 1f);
            return;
        }

        if (DialogueUI.Instance == null) {
            Debug.LogWarning("DialogueUI not found, cannot show introduction");
            return;
        }

        RoomNarrative narrative = LLMNarrativeGenerator.Instance.GetNarrative(introRoomIndex);
        if (narrative != null && narrative.npcDialogues.Count > introNPCIndex) {
            DialogueUI.Instance.ShowDialogue(narrative.npcDialogues[introNPCIndex]);
            hasShown = true;
            Debug.Log($"Showing introduction dialogue from room {introRoomIndex}, NPC {introNPCIndex}");
        } else {
            Debug.LogWarning($"No introduction dialogue found for room {introRoomIndex}, NPC {introNPCIndex}");
        }
    }
}
