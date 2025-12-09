using UnityEngine;

public class PortalLifecycleDebugger : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log($"<color=green>[PortalDebugger] I was born!</color> Name: {name} | Pos: {transform.position}");
    }

    private void OnDestroy()
    {
        // This log will tell us EXACTLY who destroyed the portal and why
        Debug.Log($"<color=red>[PortalDebugger] I am dying!</color> \nStack Trace: {System.Environment.StackTrace}");
    }
}