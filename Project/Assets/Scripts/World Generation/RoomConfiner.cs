using UnityEngine;
using Cinemachine;

public class RoomConfiner : MonoBehaviour
{
    public PolygonCollider2D bounds;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var confiner = FindObjectOfType<CinemachineConfiner2D>();
            if (confiner)
            {
                // Switch the camera bounds to THIS room
                confiner.m_BoundingShape2D = bounds;
                confiner.InvalidateCache(); // Fixes the "InvalidatePathCache" error
            }
        }
    }
}