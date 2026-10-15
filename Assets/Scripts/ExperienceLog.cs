using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Oculus.Interaction;

// Development logger: reports everything the player does and every element of the experience to the Quest's
// logcat, so it can be read from the PC while the build runs (filter on BYSTANDER). Spawns itself in builds.
//   BYSTANDER STATE ...  every `interval` seconds: frame rate, head, both hands, the chain, every break target,
//                        every particle figure, grabbables, particle totals, memory, battery, the sequencer step
//   BYSTANDER EVENT ...  the moment something changes: chain broken or armed, a hand aims at a target or goes past
//                        the chain limit, a grab or release, a frame hitch
// Delete this file (and nothing else) for the final build.
public class ExperienceLog : MonoBehaviour
{
    const bool InEditor = false;           // true to log in the editor Console as well
    const string Tag = "BYSTANDER";
    public float interval = 0.5f;          // seconds between STATE lines
    public float hitchMs = 22f;            // a frame longer than this is an event

    ChainWrap chain; SceneSequencer sequencer;
    Transform head, left, right;
    Grabbable[] grabbables; bool[] held;
    float acc, worst, findTimer; int frames, states, hitches;
    bool wasBroken, wasArmed = true;
    readonly bool[] aimedBefore = new bool[2], pastLimitBefore = new bool[2];
    readonly float[] maxHold = new float[2];
    readonly StringBuilder sb = new StringBuilder(512);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn() => new GameObject("ExperienceLog").AddComponent<ExperienceLog>();

    void Start()
    {
        if (Application.isEditor && !InEditor) { enabled = false; return; }
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);   // one line per entry: no code trace after every log line
        grabbables = FindObjectsByType<Grabbable>(FindObjectsSortMode.None);
        held = new bool[grabbables.Length];
        for (int i = 0; i < grabbables.Length; i++)
        {
            int k = i; var g = grabbables[i];
            g.WhenPointerEventRaised += e =>
            {
                if (e.Type == PointerEventType.Select) held[k] = true;
                else if (e.Type == PointerEventType.Unselect) held[k] = false;
                else return;
                LogEvent($"{(held[k] ? "grab" : "release")} {g.name} at {V(g.transform.position)}");
            };
        }
        LogEvent($"start device={SystemInfo.deviceModel} refresh={Screen.currentResolution.refreshRateRatio.value:0} grabbables={grabbables.Length}");
    }

    void Find()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (left == null) left = GameObject.Find("LeftHandAnchor")?.transform;
        if (right == null) right = GameObject.Find("RightHandAnchor")?.transform;
        if (chain == null) chain = FindFirstObjectByType<ChainWrap>();
        if (sequencer == null) sequencer = FindFirstObjectByType<SceneSequencer>();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt * 1000f > hitchMs && Time.frameCount > 120)
        {
            hitches++;                                          // counted in each STATE line
            if (dt * 1000f > 80f) LogEvent($"hitch ms={dt * 1000f:0.0}");   // only a serious one is its own event
        }
        acc += dt; frames++; worst = Mathf.Max(worst, dt);
        if ((findTimer -= dt) <= 0f) { findTimer = 1f; Find(); }
        TrackChain();
        if (acc < interval) return;
        State();
        acc = 0f; frames = 0; worst = 0f; hitches = 0;
    }

    // moments, logged as they happen
    void TrackChain()
    {
        if (chain == null) return;
        if (chain.Broken != wasBroken) { wasBroken = chain.Broken; LogEvent($"chain {(wasBroken ? "BROKEN" : "restored")}"); }
        if (chain.armed != wasArmed) { wasArmed = chain.armed; LogEvent($"chain armed={wasArmed}"); }
        for (int i = 0; i < 2; i++)
        {
            string hand = i == 0 ? "L" : "R";
            float aim = i == 0 ? chain.LeftAim : chain.RightAim, strain = i == 0 ? chain.LeftStrain : chain.RightStrain, hold = i == 0 ? chain.LeftHold : chain.RightHold;
            bool aimed = aim > 0.5f, past = strain > 0f;
            if (aimed != aimedBefore[i]) { aimedBefore[i] = aimed; LogEvent($"hand {hand} {(aimed ? "aims at a break target" : "stops aiming at a break target")}"); }
            if (past != pastLimitBefore[i]) { pastLimitBefore[i] = past; LogEvent($"hand {hand} {(past ? "past the chain limit" : "back within the chain limit")}"); }
            if (hold > maxHold[i]) maxHold[i] = hold;
            if (hold <= 0f && maxHold[i] > 0f) { LogEvent($"hand {hand} hold ended after {maxHold[i]:0.00}s"); maxHold[i] = 0f; }
        }
    }

    void State()
    {
        states++;
        sb.Clear().Append(Tag).Append(" STATE t=").Append(Time.time.ToString("0.0"))
          .Append(" fps=").Append((frames / acc).ToString("0")).Append(" ms=").Append((1000f * acc / frames).ToString("0.0")).Append(" worst=").Append((1000f * worst).ToString("0")).Append(" hitches=").Append(hitches)
          .Append(" mem=").Append((Profiler.GetTotalAllocatedMemoryLong() >> 20)).Append("MB bat=").Append((SystemInfo.batteryLevel * 100f).ToString("0")).Append('%');

        if (sequencer != null && sequencer.Index >= 0) sb.Append(" scene=\"").Append(sequencer.steps[sequencer.Index].name).Append("\" chainsLine=").Append(sequencer.ShowChainsLine ? 1 : 0);

        if (head != null)
            sb.Append(" head=").Append(V(head.position)).Append(" yaw=").Append(head.eulerAngles.y.ToString("0")).Append(" pitch=").Append((-Mathf.DeltaAngle(0f, head.eulerAngles.x)).ToString("0"));
        Hand("L", left); Hand("R", right);

        if (chain != null)
        {
            sb.Append(" chain=").Append(chain.Broken ? "broken" : "intact").Append(" armed=").Append(chain.armed ? 1 : 0);
            if (!chain.Broken)
                sb.Append(" L[strain=").Append(chain.LeftStrain.ToString("0.00")).Append(" resist=").Append(chain.LeftResist.ToString("0.00")).Append(" aim=").Append(chain.LeftAim.ToString("0.00")).Append(" hold=").Append(chain.LeftHold.ToString("0.00")).Append(" shake=").Append(chain.LeftShake.ToString("0.0000")).Append(']')
                  .Append(" R[strain=").Append(chain.RightStrain.ToString("0.00")).Append(" resist=").Append(chain.RightResist.ToString("0.00")).Append(" aim=").Append(chain.RightAim.ToString("0.00")).Append(" hold=").Append(chain.RightHold.ToString("0.00")).Append(" shake=").Append(chain.RightShake.ToString("0.0000")).Append(']');
        }

        foreach (var t in BreakTarget.All)
        {
            sb.Append(" target[").Append(t.name).Append(" at=").Append(V(t.transform.position));
            if (head != null) sb.Append(" gaze=").Append(Vector3.Angle(head.forward, t.transform.position - head.position).ToString("0")).Append("deg");
            if (left != null) sb.Append(" dL=").Append(Vector3.Distance(left.position, t.transform.position).ToString("0.00"));
            if (right != null) sb.Append(" dR=").Append(Vector3.Distance(right.position, t.transform.position).ToString("0.00"));
            sb.Append(']');
        }

        var figures = FindObjectsByType<ParticleFigure>(FindObjectsSortMode.None);
        if (figures.Length > 3) sb.Append(" figures=").Append(figures.Length);   // stress test: list only the first three
        for (int fi = 0; fi < figures.Length && fi < 3; fi++)
        {
            var f = figures[fi];
            sb.Append(" figure[").Append(f.name).Append(" particles=").Append(f.ParticleCount).Append(" clip=").Append(f.clip != null ? f.clip.name : "none").Append(" t=").Append(f.ClipTime.ToString("0.0")).Append(" feetY=").Append(f.LowestY.ToString("0.000"));
            if (f.Skeleton != null && head != null) sb.Append(" at=").Append(V(f.Skeleton.position)).Append(" dist=").Append(Vector3.Distance(head.position, f.Skeleton.position).ToString("0.0")).Append(" gaze=").Append(Vector3.Angle(head.forward, f.Skeleton.position - head.position).ToString("0")).Append("deg");
            sb.Append(']');
        }

        for (int i = 0; i < grabbables.Length; i++)
            if (grabbables[i] != null) sb.Append(" grab[").Append(grabbables[i].name).Append(" at=").Append(V(grabbables[i].transform.position)).Append(held[i] ? " HELD" : "").Append(']');

        if (states % 4 == 1)    // every 2 s: the live particle total (a scene search, so not every line)
        {
            int ps = 0, total = 0;
            foreach (var p in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) { ps++; total += p.particleCount; }
            sb.Append(" particleSystems=").Append(ps).Append(" particles=").Append(total);
        }
        Debug.Log(sb.ToString());
    }

    void Hand(string name, Transform h)
    {
        if (h == null) return;
        sb.Append(' ').Append(name).Append('=').Append(V(h.position));
        if (head != null)
        {
            var d = h.position - head.position; d.y = 0f;
            sb.Append("(out ").Append(d.magnitude.ToString("0.00")).Append(" below ").Append((head.position.y - h.position.y).ToString("0.00")).Append(')');
        }
    }

    static string V(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";
    static void LogEvent(string msg) => Debug.Log($"{Tag} EVENT t={Time.time:0.00} {msg}");
}
