using UnityEngine;

// Seated (wheelchair or chair) mode, decided automatically (author 2026-11-24: "anyone that isn't disabled is standing", cut-off 1.2 m).
// The highest the head gets in the first `watchFor` seconds is compared with `cutoff`; under it = seated. Sampling stops after that, so bending
// or the raised street scenes cannot change it. While seated: things that lie on the floor (oranges, bag, can, ball, low tutorial phrases) lie at `Lift`
// above it (about lap height, the bin rim is already 0.5 m), and the chains need only a small lean (ChainWrap.seatedLean) instead of a deep bend.
// Spawns itself. Set `ForceSeated` to test it standing.
public class Accessibility : MonoBehaviour
{
    public const float cutoff = 1.2f, watchFor = 6f, settle = 3f, lift = 0.10f;   // author 2026-11-24: raise of the floor objects for a seated player (0.45 first, then 0, now 0.10)
    public static bool ForceSeated = false;

    static float maxY, sampleFrom; static bool locked, seen, sampling; static float startedAt;
    public static bool Seated => ForceSeated || (sampling && maxY < cutoff);
    public static float Lift => Seated ? lift : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        maxY = 0f; locked = seen = sampling = false; startedAt = Time.time;
        if (FindFirstObjectByType<Accessibility>() == null) new GameObject("Accessibility").AddComponent<Accessibility>();
    }

    void Update()
    {
        if (locked || Camera.main == null) return;
#if !UNITY_EDITOR
        if (!OVRPlugin.userPresent) { seen = false; maxY = 0f; return; }   // the headset is not on a head yet (it is being handed over, held at chest height): do not look at it
#endif
        float y = Camera.main.transform.position.y;
        if (y < 0.3f) return;                                   // not tracked yet
        if (!seen) { seen = true; startedAt = Time.time; }
        if (Time.time - startedAt < settle) return;             // the first seconds after putting it on: arms up, head moving, not the player's real height
        if (!sampling) { sampling = true; sampleFrom = Time.time; maxY = 0f; }
        maxY = Mathf.Max(maxY, y);
        if (Time.time - sampleFrom >= watchFor)
        {
            locked = true;
            Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} accessibility: highest head {maxY:0.00} m in {watchFor:0} s, cut-off {cutoff:0.00} m: {(Seated ? "SEATED mode" : "standing")}");
        }
    }
}
