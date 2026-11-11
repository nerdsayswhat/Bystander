using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using UnityEngine;

// Scene 2 / Beat 2 of the Event List: another sound, from behind you, as if someone kicked a can. You look back: a soda can near your feet,
// and the trash bin right by you. Starts `beat2At` seconds into the step (Beat 1 is 45 s). Lit only where the player looks, like the rest of the
// scene; everything within arm's reach (the player stands and turns on the spot). The can is a BreakTarget (reaching for it strains and breaks
// the chains); after the break it can be grabbed with the Meta hand grab and dropped in the bin, then 6 s later the film goes to the cards.
// Spawns itself in "Scene 2"; shares the step with OldManScene (which keeps its own 60 s timeout unless the can has been binned).
// Placeholder shapes (a red cylinder can, a pale blue cylinder bin) and no sound yet (the kick and the rolling can come with the sound effects).
public class CanBinScene : MonoBehaviour
{
    public float beat2At = 25f;                             // seconds after the step is visible (was 45, the Event List value: felt too long after the old man starts collecting at about 10 s)
    public float canLeft = 165f, canDist = 0.45f;           // degrees left of where the head pointed at the start (180 = straight behind), metres
    public float binLeft = 205f, binDist = 0.55f;
    public Color can = new Color(1f, 0.08f, 0.06f), bin = new Color(0.6f, 0.6f, 0.62f);   // grey
    public float emission = 1.6f;
    public float litFull = 35f, litFade = 75f;
    public float binReach = 0.25f;                          // metres along the floor: let go this close to the bin (and below 0.9 m) and the can goes in
    public float afterBin = 6f;                             // seconds from the can going in to the cards

    enum Phase { Idle, Waiting, Rolling, Resting }
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    SceneSequencer seq; ChainWrap chain; Transform head; Phase phase;
    Transform canTr, binTr; Renderer canR; Renderer[] binRs; float[] binK; HandGrabInteractable grab;
    Vector3 origin, fwd, canFrom, canRest, binPos, dropFrom; MaterialPropertyBlock mpb;
    float t, findTimer, rollT, canLit, binLit, vy, flash, doneAt, dropT; bool held, falling, binned, toCards;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Scene 2") new GameObject("CanBinScene").AddComponent<CanBinScene>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((findTimer -= dt) <= 0f)
        {
            findTimer = 1f;
            if (seq == null) seq = FindFirstObjectByType<SceneSequencer>();
            if (chain == null) chain = FindFirstObjectByType<ChainWrap>();
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }
        bool want = !SceneSequencer.TestBeat3Only && seq != null && head != null && seq.Index >= 0 && seq.steps[seq.Index].name.StartsWith("2");
        if (want && phase == Phase.Idle && !seq.Busy) Begin();
        if (!want && phase != Phase.Idle) { End(); return; }
        if (phase == Phase.Idle) return;

        t += dt;
        if (phase == Phase.Waiting && t >= beat2At) Appear();
        if (phase == Phase.Rolling)
        {
            rollT = Mathf.Min(1f, rollT + dt / 1.1f);
            var next = Vector3.Lerp(canFrom, canRest, 1f - Mathf.Pow(1f - rollT, 3f));
            var move = next - canTr.position;
            if (move.sqrMagnitude > 1e-8f) canTr.Rotate(Vector3.Cross(Vector3.up, move.normalized), move.magnitude / 0.03f * Mathf.Rad2Deg, Space.World);
            canTr.position = next;
            if (rollT >= 1f) phase = Phase.Resting;
        }
        if (phase == Phase.Resting)
        {
            if (!binned && chain != null && chain.Broken) grab.enabled = true;       // the hands are free: the can can be grabbed
            if (falling && !held)
            {
                vy -= 9.8f * dt;
                var p = canTr.position; p.y += vy * dt;
                if (p.y <= 0.03f) { p.y = 0.03f; falling = false; }
                canTr.position = p;
            }
            if (binned)                                                                 // slides into the bin and goes out
            {
                dropT = Mathf.Min(1f, dropT + dt / 0.35f);
                canTr.position = Vector3.Lerp(dropFrom, binPos + Vector3.up * 0.3f, Mathf.SmoothStep(0f, 1f, dropT));   // down into the hollow
                if (dropT >= 1f) canTr.gameObject.SetActive(false);
                flash = Mathf.MoveTowards(flash, 0f, dt / 0.8f);
                if (!toCards && t >= doneAt + afterBin) { toCards = true; Log("the can is in the bin: to the cards"); seq.GoToCards(); }
            }
        }
        if (binTr.gameObject.activeSelf)
        {
            binLit = Mathf.MoveTowards(binLit, Lit(binTr.position + Vector3.up * 0.25f), 3f * dt);
            for (int i = 0; i < binRs.Length; i++) Show(binRs[i], bin, binK[i] * emission * (1f + flash * 2f), Mathf.Max(binLit, flash));
        }
        if (canTr.gameObject.activeSelf)
        {
            canLit = Mathf.MoveTowards(canLit, held ? 1f : Lit(canTr.position), 3f * dt);
            Show(canR, can, emission, canLit);
        }
    }

    // the can, the bin and the grab components are made in the dark and kept hidden until the beat starts
    void Begin()
    {
        t = 0f; rollT = 0f; canLit = binLit = flash = 0f; held = falling = binned = toCards = false;
        fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        origin = new Vector3(head.position.x, 0f, head.position.z);
        mpb = new MaterialPropertyBlock();
        var mat = Resources.Load<Material>("OrangeSphere");

        binPos = At(binLeft, binDist);
        var b = new GameObject("TrashBin");
        binTr = b.transform; binTr.position = binPos;
        // a hollow trash can, 0.5 m tall, 0.30 m across at the foot and 0.38 m at the open top: a thin wall (outer face, darker inner face), a
        // dark floor, a bright lip round the rim and two raised bands. Flat unlit colours like everything else, so the inside is told apart by its shade
        var parts = new (Mesh mesh, float k)[]
        {
            (Tube(0.150f, 0.190f, 0.5f, 28, false), 1.0f),                 // outer wall
            (Tube(0.140f, 0.180f, 0.5f, 28, true), 0.3f),                  // inner wall, darker
            (Disc(0.140f, 0.02f, 28), 0.15f),                              // floor, darkest
            (Ring(0.18f, 0.205f, 0.5f, 28), 1.5f),                         // lip, brightest
            (Tube(0.166f, 0.169f + 0.01f, 0.03f, 28, false, 0.12f), 1.3f), // lower band
            (Tube(0.181f, 0.184f + 0.01f, 0.03f, 28, false, 0.36f), 1.3f), // upper band
            (Tube(0.172f, 0.172f, 0.04f, 28, false), 0.8f),                // base: a short wider foot
            (Ring(0.150f, 0.172f, 0.04f, 28), 1.2f),                       // top of the foot
            (Disc(0.172f, 0.001f, 28), 0.5f),                              // underside of the foot
        };
        binRs = new Renderer[parts.Length]; binK = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var piece = new GameObject("Bin" + i, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(binTr, false);
            piece.GetComponent<MeshFilter>().sharedMesh = parts[i].mesh;
            binRs[i] = piece.GetComponent<MeshRenderer>(); binK[i] = parts[i].k;
            Prepare(binRs[i], mat);
        }
        b.SetActive(false);

        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.SetActive(false);                                                     // components are added while inactive so the SDK injections land before Awake and Start
        Destroy(c.GetComponent<Collider>());
        canTr = c.transform; canR = c.GetComponent<Renderer>();
        canTr.localScale = new Vector3(0.065f, 0.06f, 0.065f);                  // a soda can: 0.065 m across, 0.12 m long
        canTr.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f);    // lying on its side
        canRest = At(canLeft, canDist) + Vector3.up * 0.033f;
        canFrom = At(canLeft + 8f, canDist + 1.1f) + Vector3.up * 0.033f;       // rolls in from further behind
        canTr.position = canFrom;
        Prepare(canR, mat);
        c.AddComponent<BreakTarget>();
        var col = c.AddComponent<SphereCollider>(); col.radius = 1.3f;          // about 0.08 m: easy to catch
        var rb = c.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var g = c.AddComponent<Grabbable>(); g.InjectOptionalRigidbody(rb); g.InjectOptionalThrowWhenUnselected(false);
        grab = c.AddComponent<HandGrabInteractable>();
        grab.InjectAllHandGrabInteractable(GrabTypeFlags.All, rb, GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
        grab.InjectOptionalPointableElement(g);
        grab.enabled = false;                                                   // opens when the chains break
        g.WhenPointerEventRaised += e => OnGrab(e.Type);
        phase = Phase.Waiting;
        Log("scene 2 beat 2 waits");
    }

    void Appear()
    {
        binTr.gameObject.SetActive(true); canTr.gameObject.SetActive(true);
        phase = Phase.Rolling;
        Log("a can rolls in from behind, the bin is by the player's feet");
    }

    void OnGrab(PointerEventType type)
    {
        if (binned) return;
        if (type == PointerEventType.Select && !held) { held = true; falling = false; Log("can grabbed"); }
        else if ((type == PointerEventType.Unselect || type == PointerEventType.Cancel) && held)
        {
            held = false;
            var p = canTr.position;
            float far = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(binPos.x, 0f, binPos.z));
            if (far < binReach && p.y < 0.9f)
            {
                binned = true; seq.binnedCan = true; dropFrom = p; dropT = 0f; flash = 1f; doneAt = t; grab.enabled = false;
                Log($"can dropped in the bin ({far:0.00} m from its centre, {p.y:0.00} m high)");
            }
            else { falling = true; vy = 0f; Log($"can dropped {far:0.00} m from the bin at {p.y:0.00} m high (it goes in within {binReach:0.00} m)"); }
        }
    }

    // mesh helpers for the bin; y0 lifts a band off the foot
    static Mesh Tube(float r0, float r1, float h, int n, bool inward, float y0 = 0f)
    {
        var v = new Vector3[(n + 1) * 2]; var t = new int[n * 6];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.PI * 2f * i / n, c = Mathf.Cos(a), s = Mathf.Sin(a);
            v[i * 2] = new Vector3(r0 * c, y0, r0 * s); v[i * 2 + 1] = new Vector3(r1 * c, y0 + h, r1 * s);
        }
        for (int i = 0; i < n; i++)
        {
            int b0 = i * 2, t0 = i * 2 + 1, b1 = i * 2 + 2, t1 = i * 2 + 3, k = i * 6;
            if (!inward) { t[k] = b0; t[k + 1] = t0; t[k + 2] = t1; t[k + 3] = b0; t[k + 4] = t1; t[k + 5] = b1; }
            else { t[k] = b0; t[k + 1] = t1; t[k + 2] = t0; t[k + 3] = b0; t[k + 4] = b1; t[k + 5] = t1; }
        }
        return Make(v, t);
    }

    static Mesh Disc(float r, float y, int n)                      // double sided
    {
        var v = new Vector3[n + 2]; var t = new int[n * 6];
        v[0] = new Vector3(0f, y, 0f);
        for (int i = 0; i <= n; i++) { float a = Mathf.PI * 2f * i / n; v[i + 1] = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)); }
        for (int i = 0; i < n; i++) { int k = i * 6; t[k] = 0; t[k + 1] = i + 2; t[k + 2] = i + 1; t[k + 3] = 0; t[k + 4] = i + 1; t[k + 5] = i + 2; }
        return Make(v, t);
    }

    static Mesh Ring(float rIn, float rOut, float y, int n)        // flat double sided ring
    {
        var v = new Vector3[(n + 1) * 2]; var t = new int[n * 12];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.PI * 2f * i / n, c = Mathf.Cos(a), s = Mathf.Sin(a);
            v[i * 2] = new Vector3(rIn * c, y, rIn * s); v[i * 2 + 1] = new Vector3(rOut * c, y, rOut * s);
        }
        for (int i = 0; i < n; i++)
        {
            int a0 = i * 2, b0 = i * 2 + 1, a1 = i * 2 + 2, b1 = i * 2 + 3, k = i * 12;
            t[k] = a0; t[k + 1] = b1; t[k + 2] = b0; t[k + 3] = a0; t[k + 4] = a1; t[k + 5] = b1;
            t[k + 6] = a0; t[k + 7] = b0; t[k + 8] = b1; t[k + 9] = a0; t[k + 10] = b1; t[k + 11] = a1;
        }
        return Make(v, t);
    }

    static Mesh Make(Vector3[] v, int[] t)
    {
        var m = new Mesh { vertices = v, triangles = t };
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    float Lit(Vector3 p) => Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(litFull, litFade, Vector3.Angle(head.forward, p - head.position)));

    void Show(Renderer r, Color c, float scale, float lit)
    {
        r.enabled = lit > 0.01f;
        mpb.SetColor(BaseColor, new Color(c.r * scale * lit, c.g * scale * lit, c.b * scale * lit, 1f));
        r.SetPropertyBlock(mpb);
    }

    static void Prepare(Renderer r, Material m)
    {
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    void End()
    {
        if (canTr != null) Destroy(canTr.gameObject);
        if (binTr != null) Destroy(binTr.gameObject);
        canTr = binTr = null; phase = Phase.Idle; t = 0f;
        Log("scene 2 beat 2 ends");
    }

    Vector3 At(float leftDeg, float dist) => origin + Quaternion.AngleAxis(-leftDeg, Vector3.up) * fwd * dist;
    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} cans {msg}");
}
