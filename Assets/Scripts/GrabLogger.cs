using UnityEngine;
using Oculus.Interaction;

// Debug aid: logs a line when a Grabbable is grabbed or released (nothing else).
// Delete this file once it is no longer useful.
public class GrabLogger : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create() => new GameObject("[GrabLogger]").AddComponent<GrabLogger>();

    void Start()
    {
        foreach (var g in FindObjectsByType<Grabbable>(FindObjectsSortMode.None))
        {
            string n = g.name;
            g.WhenPointerEventRaised += e =>
            {
                if (e.Type == PointerEventType.Select || e.Type == PointerEventType.Unselect)
                    Debug.Log($"[GrabLogger] {n}: {e.Type}");
            };
        }
    }
}
