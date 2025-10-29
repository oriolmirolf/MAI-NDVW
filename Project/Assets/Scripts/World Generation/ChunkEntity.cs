using UnityEngine;

public class ChunkEntity : MonoBehaviour {
    public Chunk ParentChunk { get; set; }
    public GameObject OriginalPrefab { get; set; }

    public void ReturnToPool() {
        Destroy(gameObject);
    }
}
