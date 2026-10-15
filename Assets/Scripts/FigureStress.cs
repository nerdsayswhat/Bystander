using UnityEngine;

// Stress test for the particle budget. Add to an empty GameObject in the scene, drag the ParticleFigure prefab into
// Prefab and the clips into Clips (tick Loop off for one-shots). In a build it waits, then adds figures in front of the
// player, 1 to 3 m away, each with the next clip in the list, stepping the total up every StepSeconds.
// ExperienceLog reads fps, hitches and particles. Delete the file once the budget is known.
public class FigureStress : MonoBehaviour
{
    [System.Serializable] public class Entry { public string label; public AnimationClip clip; public bool loop = true; }   // label: type the clip's name (every Mixamo clip is called mixamo.com inside its FBX)

    public ParticleFigure prefab;
    public Entry[] clips;
    public int[] steps = { 4, 8, 12, 16, 24, 32 };    // total figures after each step (the scene's own figure counts as 1)
    public float stepSeconds = 15f, firstDelay = 10f;
    public bool runInEditor;

    int step, spawned; float timer;

    void Start() { timer = firstDelay; if (Application.isEditor && !runInEditor) enabled = false; }

    void Update()
    {
        if (prefab == null || clips == null || clips.Length == 0) { Debug.LogWarning("FigureStress: assign Prefab and Clips"); enabled = false; return; }
        if ((timer -= Time.deltaTime) > 0f || step >= steps.Length) return;
        timer = stepSeconds;
        int target = steps[step++];
        while (spawned + 1 < target) Add(++spawned);
        Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} stress step {step}: figures={spawned + 1} figureParticles={(spawned + 1) * prefab.particleCount}");
        if (step == steps.Length) Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} stress: last step reached");
    }

    // a fan of figures in front of the player (+Z), 1 to 3 m out, each facing the origin
    void Add(int i)
    {
        var e = clips[i % clips.Length];
        float angle = -70f + (i * 37f) % 140f, r = 1.2f + (i % 5) * 0.45f;
        var f = Instantiate(prefab);
        f.name = "StressFigure" + i + "_" + e.label;
        f.clip = e.clip; f.loop = e.loop;
        f.position = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0f, Mathf.Cos(angle * Mathf.Deg2Rad)) * r;   // set before Start builds the figure
        Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} stress spawn {f.name} at ({f.position.x:0.0},{f.position.z:0.0}) loop={e.loop}");
    }
}
