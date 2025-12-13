using UnityEngine;

public class AreaExit : MonoBehaviour
{
    [Header("Generator Settings")]
    public string sceneTransitionName;
    public int targetRoomIndex = -1;

    [Header("Manual Settings")]
    [SerializeField] private string sceneToLoad;

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool isPlayer = other.CompareTag("Player") || other.GetComponent<PlayerController>() != null;
        if (!isPlayer) return;

        bool isDungeonPortal = !string.IsNullOrEmpty(sceneTransitionName);
        if (!isDungeonPortal && !string.IsNullOrEmpty(sceneToLoad)) return;

        var entrances = FindObjectsOfType<AreaEntrance>();
        foreach (var ent in entrances)
        {
            string entName = "";
            var field = typeof(AreaEntrance).GetField("transitionName",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);

            if (field != null) entName = (string)field.GetValue(ent) ?? "";

            bool isSamePortal = ent.transform.parent == this.transform;
            bool idMatches = entName == this.sceneTransitionName;

            if (idMatches && !isSamePortal)
            {
                Vector3 targetPos = ent.transform.position;
                var targetPortal = ent.GetComponentInParent<AreaExit>();
                if (targetPortal != null)
                    targetPos += -targetPortal.transform.right * 2f;

                Vector3 delta = targetPos - other.transform.position;
                other.transform.position = targetPos;

                var vcam = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
                if (vcam) vcam.OnTargetObjectWarped(other.transform, delta);
                return;
            }
        }
    }

}