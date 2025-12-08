using UnityEngine;

public class AreaExit : MonoBehaviour
{
    [Header("Generator Settings")]
    public string sceneTransitionName; 

    [Header("Manual Settings")]
    [SerializeField] private string sceneToLoad;

    private void OnTriggerEnter2D(Collider2D other) 
    {
        if (other.CompareTag("Player")) 
        {
            // 1. Check if this is a Dungeon Portal (Generator ID exists)
            bool isDungeonPortal = !string.IsNullOrEmpty(sceneTransitionName);

            if (isDungeonPortal || string.IsNullOrEmpty(sceneToLoad)) 
            {
                // 2. Find all entrances in the scene
                var entrances = FindObjectsOfType<AreaEntrance>();
                foreach(var ent in entrances) 
                {
                    string entName = "";
                    
                    // Reflection to read private 'transitionName'
                    var field = typeof(AreaEntrance).GetField("transitionName", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.Public);
                    
                    if (field != null) entName = (string)field.GetValue(ent);

                    // 3. Teleport instantly if ID matches (and it's not our own entrance)
                    if (entName == this.sceneTransitionName && ent.transform.parent != this.transform) 
                    {
                        other.transform.position = ent.transform.position;

                        // Optional: Snap Cinemachine camera instantly if you have one
                        var vcam = FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
                        if (vcam) vcam.OnTargetObjectWarped(other.transform, ent.transform.position - other.transform.position);
                        
                        return;
                    }
                }
            }
            // else { ... Scene loading logic ... }
        }
    }
}