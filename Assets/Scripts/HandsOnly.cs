using System.Collections;
using UnityEngine;

// The film is hands only. The comprehensive interaction rig also carries the controller parts (OVRControllers: the controller data
// sources, interactors and the controller models), and they wake up whenever the headset reports a controller. This switches them
// off a moment after the scene loads, so no controller model or controller interactor can appear. Hand tracking itself is set by
// the project (Hand Tracking Support: Hands Only) and the headset; this cannot stop the Quest from switching to controllers if one
// is picked up. Spawns itself; AutoSpawn = false turns it off.
public class HandsOnly : MonoBehaviour
{
    const bool AutoSpawn = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (AutoSpawn && FindFirstObjectByType<HandsOnly>() == null) new GameObject("HandsOnly").AddComponent<HandsOnly>();
    }

    IEnumerator Start()
    {
        int done = 0;
        for (int i = 0; i < 10; i++)         // the rig can finish building a little after the scene loads: try for 10 s
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeSelf) continue;
                if (t.name == "OVRControllers" || t.name == "OVRControllerDrivenHands" || t.name.StartsWith("OVRControllerVisual"))
                {
                    t.gameObject.SetActive(false);
                    done++;
                    Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} handsonly switched off {t.name}");
                }
            }
            yield return new WaitForSeconds(1f);
        }
        if (done == 0) Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} handsonly found no controller objects in the rig");
    }
}
