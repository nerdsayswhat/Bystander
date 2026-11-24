using UnityEngine;

// Seated (wheelchair or chair) mode, decided automatically (author 2026-11-24: "anyone that isn't disabled is standing", cut-off 1.2 m).
// The highest the head gets in the first `watchFor` seconds is compared with `cutoff`; under it = seated. Sampling stops after that, so bending
// or the raised street scenes cannot change it. While seated: things that lie on the floor (oranges, bag, can, ball, low tutorial phrases) lie at `Lift`
// above it (about lap height, the bin rim is already 0.5 m), and the chains need only a small lean (ChainWrap.seatedLean) instead of a deep bend.
// Spawns itself. Set `ForceSeated` to test it standing.
public class Accessibility : MonoBehaviour
{
    public const float cutoff = 1.2f, watchFor = 6f, lift = 0.10f;   // author 2026-11-24: raise of the floor objects for a seated player (0.45 first, then 0, now 0.10)
    public static bool ForceSeated = false;

    static float maxY; static bool locked, seen; static float startedAt;
    public static bool Seated => ForceSeated || (seen && maxY < cutoff);
    public static float Lift => Seated ? lift : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        maxY = 0f; locked = seen = false; startedAt = Time.time;
        if (FindFirstObjectByType<Accessibility>() == null) new GameObject("Accessibility").AddComponent<Accessibility>();
    }

    void Update()
    {
        if (locked || Camera.main == null) return;
        float y = Camera.main.transform.position.y;
        if (y < 0.3f) return;                                   // not on the head yet (headset off or tracking not started)
        if (!seen) { seen = true; startedAt = Time.time; }
        maxY = Mathf.Max(maxY, y);
        if (Time.time - startedAt >= watchFor)
        {
            locked = true;
            Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} accessibility: highest head {maxY:0.00} m in {watchFor:0} s, cut-off {cutoff:0.00} m: {(Seated ? "SEATED mode" : "standing")}");
        }
    }
}
