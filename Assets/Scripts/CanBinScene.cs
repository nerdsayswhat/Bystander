using System.Collections.Generic;
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
    public float scene5At = 20f;                            // scene 5: seconds into the step (author 2026-11-20: scene 2 was 25, a little faster)
    public float beat2At = 25f;                             // seconds after the step is visible (was 45, the Event List value: felt too long after the old man starts collecting at about 10 s)
    public float canLeft = 165f, canAway = 0.7f;           // degrees left of where the head pointed at the start (180 = straight behind), metres
    public float binLeft = 205f, binAway = 0.8f;
    public Color can = new Color(1f, 0.08f, 0.06f), bin = new Color(0.6f, 0.6f, 0.62f);   // grey
    public float emission = 1.6f;
    public float litFull = 35f, litFade = 75f;
    public float binReach = 0.25f;                          // metres along the floor: let go this close to the bin (and below 0.9 m) and the can goes in
    public float afterBin = 6f;                             // seconds from the can going in to the cards

    enum Phase { Idle, Waiting, Rolling, Resting }
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    SceneSequencer seq; ChainWrap chain; Transform head; Phase phase;
    Transform canTr, binTr; Renderer canR; Renderer[] binRs; float[] binK; Color[] binC; HandGrabInteractable grab;
    Vector3 origin, fwd, canFrom, canRest, binPos, dropFrom; MaterialPropertyBlock mpb;
    float t, findTimer, rollT, canLit, binLit, vy, flash, doneAt, dropT; bool scene5, held, falling, binned, toCards;

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
        bool want = !SceneSequencer.TestBeat3Only && seq != null && head != null && seq.Index >= 0 && (seq.steps[seq.Index].name.StartsWith("2") || seq.steps[seq.Index].name.StartsWith("5"));
        if (want && phase == Phase.Idle && !seq.Busy) Begin();
        if (!want && phase != Phase.Idle) { End(); return; }
        if (phase == Phase.Idle) return;

        t += dt;
        if (phase == Phase.Waiting && t >= (scene5 ? scene5At : beat2At)) Appear();
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
            if (!binned && ((chain != null && chain.Broken) || scene5)) grab.enabled = true;       // the hands are free: the can can be grabbed
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
                // author 2026-11-23: the can going in no longer jumps to the cards; the dog beat still follows (SceneSequencer.Advance)
            }
        }
        if (binTr.gameObject.activeSelf)
        {
            binLit = Mathf.MoveTowards(binLit, Lit(binTr.position + Vector3.up * 0.25f), 3f * dt);
            for (int i = 0; i < binRs.Length; i++) Show(binRs[i], binC[i], binK[i] * emission * (1f + flash * 2f), Mathf.Max(binLit, flash));
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
        scene5 = seq.steps[seq.Index].name.StartsWith("5");
        t = 0f; rollT = 0f; canLit = binLit = flash = 0f; held = falling = binned = toCards = false;
        fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        origin = new Vector3(head.position.x, 0f, head.position.z);
        mpb = new MaterialPropertyBlock();
        var mat = Resources.Load<Material>("OrangeSphere");

        binPos = At(binLeft, binAway); PlacedBin = binPos; HasPlaced = true;
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
        // litter piled inside the bin, from its floor (y 0.02) up to about 0.3 (the rim is at 0.5): mixed rubbish, each its own pale colour so the kinds read apart
        var lie = Quaternion.Euler(0f, 0f, 90f);                                        // lays a Y-axis tube on its side
        Quaternion Yaw(float d) => Quaternion.Euler(0f, d, 0f);
        var trash = new (Mesh mesh, float k, Color c)[]
        {
            (Xf(Blob(0.035f, Vector3.zero, 1), new Vector3(-0.07f, 0.07f, 0.04f), Quaternion.identity, Vector3.one), 1.0f, new Color(0.9f, 0.9f, 0.85f)),     // crumpled paper
            (Xf(Blob(0.030f, Vector3.zero, 2), new Vector3(0.07f, 0.065f, -0.04f), Quaternion.identity, Vector3.one), 0.8f, new Color(0.9f, 0.9f, 0.85f)),
            (Xf(Blob(0.040f, Vector3.zero, 3), new Vector3(0f, 0.045f, 0f), Quaternion.identity, new Vector3(1.4f, 0.5f, 1.2f)), 1.0f, new Color(0.8f, 0.85f, 1f)),   // a flattened plastic bag
            (Xf(Merge(Tube(0.032f, 0.032f, 0.12f, 14, false, -0.06f), Disc(0.032f, -0.06f, 14), Disc(0.032f, 0.06f, 14)), new Vector3(0f, 0.10f, 0.06f), Yaw(30f) * lie, Vector3.one), 1.2f, new Color(0.45f, 0.65f, 1f)),   // a soda can on its side
            (Xf(Merge(Tube(0.028f, 0.028f, 0.12f, 14, false, -0.09f), Tube(0.028f, 0.010f, 0.05f, 14, false, 0.03f), Disc(0.028f, -0.09f, 14)), new Vector3(0f, 0.12f, -0.05f), Yaw(-50f) * lie, new Vector3(1f, 0.75f, 1f)), 1.3f, new Color(0.3f, 0.9f, 0.65f)),   // a bottle
            (Xf(Merge(Tube(0.026f, 0.036f, 0.09f, 14, false, -0.045f), Tube(0.023f, 0.033f, 0.09f, 14, true, -0.045f), Disc(0.026f, -0.045f, 14)), new Vector3(-0.05f, 0.18f, -0.03f), Yaw(110f) * lie, Vector3.one), 1.2f, new Color(1f, 0.4f, 0.35f)),   // a paper cup
            (Xf(Quad(0.18f, 0.12f), new Vector3(0.02f, 0.25f, 0f), Quaternion.Euler(8f, 25f, 6f), Vector3.one), 0.9f, new Color(0.8f, 0.75f, 0.62f)),                          // a sheet of newspaper
            (Xf(Quad(0.10f, 0.07f), new Vector3(-0.05f, 0.27f, 0.03f), Quaternion.Euler(-6f, -35f, 5f), Vector3.one), 0.8f, new Color(0.9f, 0.9f, 0.85f)),                         // a scrap of paper
        };
        int np = parts.Length;
        binRs = new Renderer[np + trash.Length]; binK = new float[np + trash.Length]; binC = new Color[np + trash.Length];
        for (int i = 0; i < binRs.Length; i++)
        {
            var piece = new GameObject("Bin" + i, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(binTr, false);
            piece.GetComponent<MeshFilter>().sharedMesh = i < np ? parts[i].mesh : trash[i - np].mesh;
            binRs[i] = piece.GetComponent<MeshRenderer>(); binK[i] = i < np ? parts[i].k : trash[i - np].k; binC[i] = i < np ? bin : trash[i - np].c;
            Prepare(binRs[i], mat);
        }
        b.SetActive(false);

        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.SetActive(false);                                                     // components are added while inactive so the SDK injections land before Awake and Start
        Destroy(c.GetComponent<Collider>());
        canTr = c.transform; canR = c.GetComponent<Renderer>();
        canTr.localScale = new Vector3(0.065f, 0.06f, 0.065f);                  // a soda can: 0.065 m across, 0.12 m long
        canTr.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f);    // lying on its side
        canRest = At(canLeft, canAway) + Vector3.up * 0.033f; PlacedCan = canRest;
        canFrom = At(canLeft + 8f, canAway + 1.1f) + Vector3.up * 0.033f;       // rolls in from further behind
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

    // the old man's paper grocery bag (scenes 2, 3, 5, 6): a hollow tapering sack 0.30 m long along local Y (closed foot at -0.15, open mouth at +0.15, rim folded over),
    // centred on its own origin so it takes the same "lying on its side" rotation as the old cylinder. k = brightness of each piece (inside darker, rim brightest)
    public static GameObject PaperBag(Transform parent, Material mat, out Renderer[] rs, out float[] ks)
    {
        // flat and boxy, not round (a round tapered tube read as a paper cup): local X is the thin way (up, when it lies on its side), Z the wide way
        var parts = new (Mesh mesh, float k)[]
        {
            (Sack(0.040f, 0.075f, 0.052f, 0.085f, 0.30f, 24, false, -0.15f, 0.012f, 7, true), 1.0f),    // outer wall with a flat closed foot and an uneven torn top
            (Sack(0.035f, 0.070f, 0.047f, 0.080f, 0.30f, 24, true, -0.15f, 0.012f, 7, false), 0.25f),   // inner wall, dark
            (Sack(0.054f, 0.087f, 0.054f, 0.087f, 0.035f, 24, false, 0.115f, 0.012f, 7, false), 1.4f),  // the folded-over cuff at the mouth
            (Sack(0.042f, 0.077f, 0.042f, 0.077f, 0.02f, 24, false, -0.12f, 0f, 1, false), 0.7f),       // the fold near the foot
            (Merge(Quad4(-0.012f, 0.012f, -0.13f, 0.12f, 0.078f, 0.088f), Quad4(-0.012f, 0.012f, -0.13f, 0.12f, -0.078f, -0.088f)), 1.6f),   // the two gusset creases down the sides
        };
        var root = new GameObject("PaperBag");
        if (parent != null) root.transform.SetParent(parent, false);
        rs = new Renderer[parts.Length]; ks = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var piece = new GameObject("Bag" + i, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(root.transform, false);
            piece.GetComponent<MeshFilter>().sharedMesh = parts[i].mesh;
            rs[i] = piece.GetComponent<MeshRenderer>(); ks[i] = parts[i].k;
            Prepare(rs[i], mat);
        }
        return root;
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

    // a lofted sack with a squarish (superellipse) cross-section: half-sizes a (X) and b (Z) at the foot (y0) and at the top (y0 + h); the top edge is uneven by `wave`
    static Mesh Sack(float a0, float b0, float a1, float b1, float h, int n, bool inward, float y0, float wave, int seed, bool cap)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= n; i++)
        {
            float ang = Mathf.PI * 2f * i / n, c = Mathf.Cos(ang), s = Mathf.Sin(ang);
            float sx = Mathf.Sign(c) * Mathf.Sqrt(Mathf.Abs(c)), sz = Mathf.Sign(s) * Mathf.Sqrt(Mathf.Abs(s));
            float w = wave * (0.6f * Mathf.Sin(3f * ang + seed) + 0.4f * Mathf.Sin(7f * ang + seed * 2.3f));
            v.Add(new Vector3(a0 * sx, y0, b0 * sz)); v.Add(new Vector3(a1 * sx, y0 + h + w, b1 * sz));
        }
        for (int i = 0; i < n; i++)
        {
            int b00 = i * 2, t0 = i * 2 + 1, b01 = i * 2 + 2, t1 = i * 2 + 3;
            if (!inward) { t.AddRange(new[] { b00, t0, t1, b00, t1, b01 }); } else { t.AddRange(new[] { b00, t1, t0, b00, b01, t1 }); }
        }
        if (cap)
        {
            int c0 = v.Count; v.Add(new Vector3(0f, y0, 0f));
            for (int i = 0; i < n; i++) { t.AddRange(new[] { c0, i * 2 + 2, i * 2 }); t.AddRange(new[] { c0, i * 2, i * 2 + 2 }); }
        }
        return Make(v.ToArray(), t.ToArray());
    }

    static Mesh Quad4(float x0, float x1, float y0, float y1, float z0, float z1)     // a flat double-sided strip from (x0, y0, z0) to (x1, y1, z1)
    {
        var v = new[] { new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1) };
        return Make(v, Dbl(new[] { 0, 2, 3, 0, 3, 1 }));
    }

    static Mesh Quad(float w, float d)                                                // a flat double-sided rectangle on the floor, centred
    {
        var v = new[] { new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0, -d / 2), new Vector3(-w / 2, 0, d / 2), new Vector3(w / 2, 0, d / 2) };
        return Make(v, Dbl(new[] { 0, 2, 3, 0, 3, 1 }));
    }

    static int[] Dbl(int[] t)                                                         // every triangle again, reversed: visible from both sides
    {
        var r = new int[t.Length * 2]; t.CopyTo(r, 0);
        for (int i = 0; i < t.Length; i += 3) { r[t.Length + i] = t[i]; r[t.Length + i + 1] = t[i + 2]; r[t.Length + i + 2] = t[i + 1]; }
        return r;
    }

    static Mesh Merge(params Mesh[] ms)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        foreach (var m in ms) { int o = v.Count; v.AddRange(m.vertices); foreach (var i in m.triangles) t.Add(i + o); }
        return Make(v.ToArray(), t.ToArray());
    }

    static Mesh Xf(Mesh m, Vector3 pos, Quaternion rot, Vector3 scale)               // place a mesh: scale, then rotate, then move
    {
        var v = m.vertices;
        for (int i = 0; i < v.Length; i++) v[i] = pos + rot * Vector3.Scale(v[i], scale);
        return Make(v, m.triangles);
    }

    static Mesh Blob(float r, Vector3 c, int seed)                 // a crumpled lump: a bumpy low-poly ball
    {
        const int N = 10, M = 6;
        var rnd = new System.Random(seed);
        var v = new Vector3[N * (M - 1) + 2]; var t = new int[N * (M - 1) * 6];
        v[0] = c + Vector3.down * r * 0.8f; v[v.Length - 1] = c + Vector3.up * r;
        for (int j = 1; j < M; j++)
            for (int i = 0; i < N; i++)
            {
                float a = Mathf.PI * 2f * i / N, p = Mathf.PI * j / M, k = r * (0.7f + 0.5f * (float)rnd.NextDouble());
                v[1 + (j - 1) * N + i] = c + new Vector3(Mathf.Cos(a) * Mathf.Sin(p), Mathf.Cos(p), Mathf.Sin(a) * Mathf.Sin(p)) * k;
            }
        int n = 0;
        for (int i = 0; i < N; i++)                                // double sided, so no winding worries
        {
            int i1 = (i + 1) % N;
            n = Tri(t, n, 0, 1 + i, 1 + i1);
            n = Tri(t, n, v.Length - 1, 1 + (M - 2) * N + i1, 1 + (M - 2) * N + i);
            for (int j = 0; j < M - 2; j++)
            {
                int a0 = 1 + j * N + i, a1 = 1 + j * N + i1, b0 = a0 + N, b1 = a1 + N;
                n = Tri(t, n, a0, b0, b1); n = Tri(t, n, a0, b1, a1);
            }
        }
        return Make(v, Dbl(t));
    }

    static int Tri(int[] t, int n, int a, int b, int c) { t[n] = a; t[n + 1] = b; t[n + 2] = c; return n + 3; }

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

    public static bool HasPlaced; public static Vector3 PlacedCan, PlacedBin;      // for the street scenes that follow: the can and the bin stay exactly there
    Vector3 At(float leftDeg, float dist) => origin + Quaternion.AngleAxis(-leftDeg, Vector3.up) * fwd * dist;
    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} cans {msg}");
}
