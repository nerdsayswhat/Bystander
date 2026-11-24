using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The street of scene 3 ("Darkness lifts: EXT. STREET") and scene 6 ("the street again"), one script for both. The player is seen high above the street: the
// world is LOWERED by `viewHeight` metres (the player never moves, so the chains, the hands and the head height are untouched). Everything is drawn as thin
// glowing white lines on black (roads, block outlines, buildings with floor lines, a bicycle, a stroller, litter), the living things as blue particles:
// a crowd of still columns (the bystanders), and a few real ParticleFigures (a fallen person with a helper kneeling beside them, a stabbed person with a
// helper, and in scene 6 the scared woman walking). A crying baby is a tiny cluster in the stroller. The spectacle above is Spectacle's.
// Near the player's feet, at floor height, the story objects: scene 3 always has a squished orange with other fruit and more trash (the orange discs are
// BreakTargets, so reaching for them still breaks the chains); scene 6 shows them only for what the player did NOT do in scene 5 (SceneSequencer.gaveOrange,
// binnedCan, calmedDog): no orange given = squished oranges, can not binned = more trash, dog not calmed = the scared woman walking; if the player acted there is
// a clean lit circle of rings around their feet.
// Follows SceneSequencer by step name ("3 ..." and "6 ..."). Created in Scene 2.unity by Bystander > Create Scene 2 (which assigns the five clips).
public partial class StreetScene : MonoBehaviour
{
    public AnimationClip[] punchClips, hitClips, reactClips;   // fights: punching (4), being hit (taking a punch, an uppercut), onlookers reacting (reacting, surprised)
    public AnimationClip[] talkClips, textClips, runClips, sitClips, walkMix; public AnimationClip arguing;   // talking (4), texting, running (2), sitting talking (left, right), walking (plain, texting, shopping bag)
    public AnimationClip cowerClip;   // the lady cowering (scene 2's woman_cower_loop)
    ParticleDog dog; Renderer ballR2;
    public AnimationClip idle, idle2, idle3, lookUp, pickUp, walk, walkOld, gather, fallen, stabbed, kneel, scaredWalk;     // standing idle, lying in pain, stabbed on the ground, kneeling, the woman's scared walk
    public float viewHeight = 10f;                          // m: the street lies this far below the player's floor
    public float riseTime = 6f;                             // seconds the street takes to light up ("darkness starts lifting")
    public int columns = 90; public int strollerCount = 25, standerCount = 60;   // strollerCount: ordinary walkers, was walkerCount 130 (author 2026-11-24: the street did not feel menacing; renamed because Scene 2.unity holds the old value)                            // still columns (36 particles each)
    public float lineBright = 0.9f;
    public float fadeIn = 2.5f, liftDelay = 3f, riseSeconds = 16f;   // s: the lines fade in; the rise above the street starts after liftDelay and lasts riseSeconds
    public Color people = new Color(0.004f, 0.05f, 0.16f);  // x emission 10: the same azure as the particle figures
    public Color helper = new Color(0.004f, 0.05f, 0.16f); static readonly Color oldBlue = new Color(0.004f, 0.05f, 0.16f);   // the old man keeps the azure he has in scene 2
    public Color woman = new Color(0.06f, 0.012f, 0.16f);   // the same purple as the woman of scene 2

    enum Phase { Idle, Building, Built, Shown }   // Building/Built: the street is made hidden, a little at a time, while scene 2 is still on; Shown: it is on
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    SceneSequencer seq; Transform head; Phase phase; float t, findTimer, rise; bool warned, scene6;
    Vector3 origin, fwd, right; float baseY;
    readonly List<Vector3> lv = new List<Vector3>(); readonly List<int> li = new List<int>();
    GameObject root; MeshRenderer lines, ring; Material crowdMat; ParticleSystemRenderer crowdR, babyR; Material babyMat;
    MaterialPropertyBlock mpb;
    readonly List<ParticleFigure> figs = new List<ParticleFigure>(); ParticleFigure womanFig; float walkX;
    readonly List<Renderer> fruit = new List<Renderer>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Scene 2" && FindFirstObjectByType<StreetScene>() == null)
            new GameObject("StreetScene").AddComponent<StreetScene>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((findTimer -= dt) <= 0f)
        {
            findTimer = 1f;
            if (seq == null) seq = FindFirstObjectByType<SceneSequencer>();
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }
        string sn = seq != null && head != null && seq.Index >= 0 ? seq.steps[seq.Index].name : "";
        bool show = sn.StartsWith("3") || sn.StartsWith("6");
        bool prep = show || (sn.StartsWith("2") && seq.StepTime > 12f && OldManScene.HasPlaced && CanBinScene.HasPlaced && WomanDogScene.HasPlaced);   // scene 3's street is made hidden during scene 2
        if (prep && phase == Phase.Idle)
        {
            if (idle == null || fallen == null) { if (!warned) { warned = true; Debug.LogWarning("StreetScene: the clips are not assigned (use Bystander > Create Scene 2)"); } return; }
            phase = Phase.Building; buildCo = StartCoroutine(Prebuild()); return;
        }
        if (!prep && phase != Phase.Idle) { End(); return; }
        if (phase == Phase.Built && show && !seq.Busy) Activate();
        if (phase != Phase.Shown) return;

        t += dt;
        rise = Mathf.Clamp01(t / Mathf.Max(fadeIn, 0.1f));                                // the darkness lifts
        float k = rise * rise * (3f - 2f * rise);
        // the scene starts on the street (the world is raised by viewHeight); the near buildings grow, then the world sinks and the player rises above the street
        float lf = Mathf.Clamp01((t - liftDelay) / Mathf.Max(riseSeconds, 0.1f)); lf = lf * lf * (3f - 2f * lf);
        float drop = viewHeight * (1f - lf);
        root.transform.position = new Vector3(0f, drop, 0f);
        if (adoptLady) AdoptLady();
        foreach (var f in figs) if (f != null) f.groundY = baseY + drop;
        if (growing) Grow();
        if (assembling) Assemble();
        if (oldManFig != null && oldManFig.Skeleton != null && !oldManFig.loop && gather != null && oldManFig.ClipTime >= 0f && oldManFig.clip != null && oldManFig.ClipTime >= oldManFig.clip.length - 0.03f) { oldManFig.Play(gather, true); }   // a one-shot from scene 2 (the fall, the wave) has finished: he goes back to gathering his oranges
        // scene 2's things carry on at the light they had (it followed where the player looked) and ease to full over 4 s; the street itself fades in as before
        float ez = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 4f));
        float kf = keep ? Mathf.Lerp(OldManScene.PlacedBright, 1f, ez) : k, kw = keep ? Mathf.Lerp(WomanDogScene.PlacedBright, 1f, ez) : k;
        if (dog != null) dog.brightness = kw;
        if (bagRs2 != null) for (int i = 0; i < bagRs2.Length; i++) { mpb.SetColor(BaseColor, new Color(OldManScene.BagTan.r, OldManScene.BagTan.g, OldManScene.BagTan.b, 1f) * 1.1f * bagKs2[i] * kf); bagRs2[i].SetPropertyBlock(mpb); }
        if (ballR2 != null) { mpb.SetColor(BaseColor, new Color(0.08f, 1.4f, 0.16f, 1f) * kw); ballR2.SetPropertyBlock(mpb); }
        SpawnPeople(); MovePeds(dt);
        mpb.SetColor(BaseColor, Color.white * lineBright * k); lines.SetPropertyBlock(mpb);
        if (ring != null) { mpb.SetColor(BaseColor, Color.white * 2.5f * k); ring.SetPropertyBlock(mpb); }
        crowdMat.SetColor(BaseColor, Color.white * 10f * k); babyMat.SetColor(BaseColor, Color.white * 10f * k);
        UpdatePolice(k);
        foreach (var f in figs) if (f != null) f.brightness = f == oldManFig && keep ? kf : f == ladyFig && keep ? kw : keptFigs.Contains(f) ? 1f : k;
        foreach (var r in fruit) { mpb.SetColor(BaseColor, new Color(0.9f * 1.6f, 0.3f * 1.6f, 0.04f * 1.6f, 1f) * kf); r.SetPropertyBlock(mpb); }
        if (womanFig != null && womanFig.Skeleton != null)                                 // the scared woman crosses the street, in place and moved along
        {
            womanFig.Skeleton.position += right * 0.7f * dt;                              // she starts 26 m to the left and 7 m ahead, and crosses in front of the player (passing at about 37 s)
        }
    }

    Vector3 P(float x, float y, float z) => new Vector3(origin.x, baseY, origin.z) + right * x + fwd * z + Vector3.up * y;

    Coroutine buildCo; readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch(); bool Over() => sw.ElapsedMilliseconds >= 4;   // the building work is shared out in slices of about 4 ms
    GameObject ringGo; bool nearChanged;

    // makes the street hidden (inactive root), a little each frame, so nothing stutters when it is shown
    IEnumerator Prebuild()
    {
        sw.Restart();
        scene6 = seq.steps[seq.Index].name.StartsWith("6");
        keep = OldManScene.HasPlaced && CanBinScene.HasPlaced && WomanDogScene.HasPlaced;
        cleanNear = scene6 && (seq.gaveOrange || seq.binnedCan || seq.calmedDog);   // the player acted in scene 5: everything within nearClean metres is problem free, the far problems go on
        t = 0f; rise = 0f;
        fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized; right = Vector3.Cross(Vector3.up, fwd);
        origin = new Vector3(head.position.x, 0f, head.position.z); baseY = -viewHeight;
        lv.Clear(); li.Clear(); blds.Clear(); keptFigs.Clear(); benchSpots.Clear(); queue.Clear(); peds.Clear(); sv.Clear(); si.Clear(); asm.Clear(); figDelay.Clear(); growing = true; assembling = false; hueN = 0; hue0 = Random.value; mpb = new MaterialPropertyBlock(); figs.Clear(); fruit.Clear(); womanFig = null; walkX = 0f;
        root = new GameObject("Street"); root.SetActive(false);
        var rnd = new System.Random(21);                                                    // the same street in both scenes
        yield return BuildCity(rnd);
        yield return BuildStreetDetails(rnd);
        yield return BuildTrafficLights();
        yield return BuildProps(rnd);
        BuildFeet(rnd); yield return null;
        PlanBuild(); yield return null;
        var mesh = new Mesh { name = "StreetLines", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(lv); mesh.SetIndices(li, MeshTopology.Lines, 0); streetMesh = mesh; origV = lv.ToArray(); curV = (Vector3[])origV.Clone(); for (int s = 0; s < curV.Length / 2; s++) curV[2 * s + 1] = origV[2 * s];   // every line starts as a point
        mesh.vertices = curV; mesh.bounds = new Bounds(new Vector3(origin.x, baseY, origin.z), new Vector3(400f, 120f, 400f));
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        lines = root.AddComponent<MeshRenderer>(); lines.sharedMaterial = Resources.Load<Material>("OrangeSphere");
        lines.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lines.receiveShadows = false;
        yield return null;
        var sm = new Mesh { name = "StreetSolid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        sm.SetVertices(sv); sm.SetIndices(si, MeshTopology.Triangles, 0); sm.bounds = mesh.bounds;
        var sgo = new GameObject("Solid"); sgo.transform.SetParent(root.transform, false); sgo.AddComponent<MeshFilter>().sharedMesh = sm;
        solidMat = new Material(Resources.Load<Material>("OrangeSphere")); solidMat.SetColor(BaseColor, Color.black); solidMat.SetFloat("_Cull", 0f);
        var sr = sgo.AddComponent<MeshRenderer>(); sr.sharedMaterial = solidMat; sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
        yield return null;
        phase = Phase.Built;
        Log("the street is built, hidden, waiting for scene " + (scene6 ? "6" : "3"));
    }

    // shows the street: the root goes on, the crowd and the near people begin (the queue makes them one per frame)
    void Activate()
    {
        var cam = Camera.main; if (cam != null) { prevNear = cam.nearClipPlane; cam.nearClipPlane = Mathf.Max(prevNear, 0.15f); nearChanged = true; }   // a nearer clip plane gives the depth buffer the precision to keep lines in front of their solids far away
        t = 0f; root.SetActive(true); if (ringGo != null) ringGo.SetActive(true);
        var rnd = new System.Random(22);
        BuildCrowd(rnd); BuildPolice();
        BuildFigures();
        phase = Phase.Shown;
        Log(scene6 ? "scene 6: the street again" : "scene 3: the street, darkness lifts");
    }

    // ---- line helpers ----
    void L(Vector3 a, Vector3 b) { li.Add(lv.Count); lv.Add(a); li.Add(lv.Count); lv.Add(b); }
    void Rect(Vector3 c, Vector3 ux, Vector3 uy, float w, float h)
    {
        var a = c - ux * w / 2 - uy * h / 2; var b = c + ux * w / 2 - uy * h / 2; var d = c + ux * w / 2 + uy * h / 2; var e = c - ux * w / 2 + uy * h / 2;
        L(a, b); L(b, d); L(d, e); L(e, a);
    }
    void Circle(Vector3 c, Vector3 ux, Vector3 uy, float r, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
            L(c + ux * Mathf.Cos(a0) * r + uy * Mathf.Sin(a0) * r, c + ux * Mathf.Cos(a1) * r + uy * Mathf.Sin(a1) * r);
        }
    }
    // buildings grow: each Box remembers its vertex range, and Grow() scales those vertices up from the ground, nearest the player first
    struct Bld { public int start, count, structCount; public float delay; }   // structCount: the frame lines (drawn in sequence); the rest are windows and doors
    readonly List<Bld> blds = new List<Bld>(); Mesh streetMesh; Vector3[] origV, curV; public float growDelay = 0.3f, growSpread = 9f, growTime = 0.45f; public int linesAtOnce = 3; public float buildTime = 3f, detailTime = 1.2f;   // growTime: seconds one line takes to draw
    class Asm { public ParticleSystem ps; public ParticleSystem.Particle[] arr; public Vector3[] tgt, from; public float[] delay, size; }
    readonly List<Asm> asm = new List<Asm>(); bool assembling; public float gatherTime = 2.5f;
    void Assemble()
    {
        float last = 0f;
        foreach (var a in asm)
        {
            for (int i = 0; i < a.arr.Length; i++)
            {
                float g = Mathf.Clamp01((t - a.delay[i]) / gatherTime); last = Mathf.Max(last, a.delay[i] + gatherTime);
                float e = g * g * (3f - 2f * g);
                a.arr[i].position = Vector3.Lerp(a.from[i], a.tgt[i], e);
                a.arr[i].startSize = a.size[i] * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, e));   // invisible until almost in place
            }
            a.ps.SetParticles(a.arr, a.arr.Length);
        }
        for (int i = 0; i < figs.Count; i++) if (figs[i] != null) { figs[i].gather = Mathf.Clamp01((t - figDelay[i]) / gatherTime); last = Mathf.Max(last, figDelay[i] + gatherTime); }
        assembling = t < last + 0.1f || queue.Count > 0;
        if (!assembling) foreach (var f in figs) if (f != null) f.gather = 1f;
    }
    int hueN; float hue0;
    // every person gets their own colour: hues spaced by the golden ratio so no two neighbours match (same brightness as the azure of the particle figures)
    // a wide palette: hues spaced by the golden ratio over red, orange, yellow, green, teal and crimson (not the azure of the old man or the purple-pink of the lady),
    // with varied saturation and brightness and the odd pale, nearly white person
    Color PersonColour()
    {
        float u = Mathf.Repeat(hue0 + hueN++ * 0.618034f, 1f), hue = u < 0.85f ? u / 0.85f * 0.5f : 0.9f + (u - 0.85f) / 0.15f * 0.1f;
        float sat = Random.value < 0.15f ? 0.2f : Random.Range(0.55f, 1f), val = 0.15f * Random.Range(0.8f, 1.25f);
        var c = Color.HSVToRGB(hue, sat, val); c.a = 1f; return c;
    }
    readonly List<float> figDelay = new List<float>();
    float[] segT, segD; float growEnd;
    // every line segment draws itself (first end to second end): building lines in sequence, linesAtOnce at a time; all other lines (kerbs, roads, crossings, props, litter) by distance from the player
    void PlanBuild()
    {
        int n = lv.Count / 2; segT = new float[n]; segD = new float[n]; growEnd = 0f;
        for (int s = 0; s < n; s++)
        {
            var m = (lv[2 * s] + lv[2 * s + 1]) * 0.5f; float d = Mathf.Sqrt((m.x - origin.x) * (m.x - origin.x) + (m.z - origin.z) * (m.z - origin.z));
            segT[s] = growDelay + d / 125f * growSpread; segD[s] = growTime * 2f;
        }
        foreach (var bl in blds)
        {
            int sc = bl.structCount / 2, dn = Mathf.Max(1, bl.count / 2 - sc);
            int conc = Mathf.Max(linesAtOnce, Mathf.CeilToInt(sc / (buildTime / growTime)));          // a building's frame takes about buildTime however many lines it has (rounded corners made more)
            float detail = bl.delay + Mathf.Ceil(sc / (float)conc) * growTime;
            for (int s = 0; s < bl.count / 2; s++)
            {
                int k = bl.start / 2 + s;
                if (s < sc) { segT[k] = bl.delay + (s / conc) * growTime; segD[k] = growTime; }
                else { segT[k] = detail + (s - sc) / (float)dn * detailTime; segD[k] = growTime * 2f; }       // windows and doors appear one after another once the frame is drawn, over detailTime
            }
        }
        for (int s = 0; s < n; s++) growEnd = Mathf.Max(growEnd, segT[s] + segD[s]);
        byStart = new int[n]; for (int s = 0; s < n; s++) byStart[s] = s;
        var cnt = new int[(int)(growEnd * 50f) + 4];                                           // a counting sort by start time (0.02 s steps): quick, no stutter
        for (int q = 0; q < n; q++) cnt[(int)(segT[q] * 50f) + 1]++;
        for (int q = 1; q < cnt.Length; q++) cnt[q] += cnt[q - 1];
        for (int q = 0; q < n; q++) byStart[cnt[(int)(segT[q] * 50f)]++] = q;
        nextStart = 0; active.Clear();
    }
    int[] byStart; int nextStart; readonly List<int> active = new List<int>();
    void Grow()
    {
        bool changed = false;
        while (nextStart < byStart.Length && segT[byStart[nextStart]] <= t) { active.Add(byStart[nextStart++]); changed = true; }
        for (int a = active.Count - 1; a >= 0; a--)                                       // only the lines being drawn right now are touched
        {
            int s = active[a]; float g = Mathf.Clamp01((t - segT[s]) / segD[s]);
            curV[2 * s + 1] = Vector3.Lerp(origV[2 * s], origV[2 * s + 1], g); changed = true;
            if (g >= 1f) { active[a] = active[active.Count - 1]; active.RemoveAt(active.Count - 1); }
        }
        if (changed) streetMesh.vertices = curV;
        growing = nextStart < byStart.Length || active.Count > 0;
    }
    bool growing; const float manX = -6f, manZ = -3f;                                       // where the old man kneels on the street

    static float BR(float w, float d) => Mathf.Min(1.2f, Mathf.Min(w, d) * 0.2f);          // the corner radius of a building
    // buildings have rounded corners (three points per corner); frame lines are the footprint loops at the base, the roof and each floor (floor lines only within 70 m)
    void Box(float cx, float cz, float w, float d, float h, float floor)
    {
        int s0 = lv.Count; float r = BR(w, d), dist = Mathf.Sqrt(cx * cx + cz * cz);
        var fb = Foot(cx, cz, w, d, r, 0f); var fr = Foot(cx, cz, w, d, r, h);
        Loop(fb);
        for (int i = 0; i < fb.Count; i++) if (i % 3 != 1) L(fb[i], fr[i]);
        Loop(fr);
        if (dist < 70f) for (float y = floor; y < h - 0.1f; y += floor) Loop(Foot(cx, cz, w, d, r, y));
        int sc = lv.Count - s0;
        float ins = 0.04f + dist * 0.004f;                                                                 // the solid sits further inside the wireframe with distance (depth precision falls off)
        var sb = Foot(cx, cz, w - 2f * ins, d - 2f * ins, Mathf.Max(r - ins, 0.1f), 0f); var st = Foot(cx, cz, w - 2f * ins, d - 2f * ins, Mathf.Max(r - ins, 0.1f), h - ins);   // the opaque body
        int b = sv.Count; sv.AddRange(sb); sv.AddRange(st); int n = sb.Count;
        for (int i = 0; i < n; i++) Quad(b + i, b + (i + 1) % n, b + n + (i + 1) % n, b + n + i);
        int cc = sv.Count; sv.Add(P(cx, h - ins, cz)); for (int i = 0; i < n; i++) { si.Add(cc); si.Add(b + n + i); si.Add(b + n + (i + 1) % n); }
        Windows(cx, cz, w, d, h, floor);
        blds.Add(new Bld { start = s0, count = lv.Count - s0, structCount = sc, delay = growDelay + dist / 125f * growSpread });
    }
    // a closed loop of points round a rounded rectangle at height y (corner radius r, n+1 points per corner)
    List<Vector3> Foot(float cx, float cz, float w, float d, float r, float y, int n = 2)
    {
        var pts = new List<Vector3>(); float[] sx = { 1f, -1f, -1f, 1f }, sz = { 1f, 1f, -1f, -1f };
        for (int c = 0; c < 4; c++)
            for (int k = 0; k <= n; k++)
            {
                float a = (c * 90f + k * 90f / n) * Mathf.Deg2Rad;
                pts.Add(P(cx + sx[c] * (w / 2 - r) + Mathf.Cos(a) * r, y, cz + sz[c] * (d / 2 - r) + Mathf.Sin(a) * r));
            }
        return pts;
    }
    void Loop(List<Vector3> pts) { for (int i = 0; i < pts.Count; i++) L(pts[i], pts[(i + 1) % pts.Count]); }
    void Arc(Vector3 c, Vector3 ux, Vector3 uy, float r, float a0, float a1, int n)
    {
        for (int i = 0; i < n; i++)
        {
            float t0 = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / (float)n), t1 = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, (i + 1) / (float)n);
            L(c + ux * Mathf.Cos(t0) * r + uy * Mathf.Sin(t0) * r, c + ux * Mathf.Cos(t1) * r + uy * Mathf.Sin(t1) * r);
        }
    }
    // an upright cylinder (rings, verticals and an opaque shell); c on the ground at the centre
    void Cylinder(Vector3 c, float rad, float h0, float h1, int n = 8)
    {
        Circle(c + Vector3.up * h0, right, fwd, rad, n); Circle(c + Vector3.up * h1, right, fwd, rad, n);
        for (int i = 0; i < n; i += Mathf.Max(1, n / 4)) { float a = i * Mathf.PI * 2f / n; var o = (right * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * rad; L(c + o + Vector3.up * h0, c + o + Vector3.up * h1); }
        int b = sv.Count;
        for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; var o = (right * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * (rad - 0.03f); sv.Add(c + o + Vector3.up * h0); sv.Add(c + o + Vector3.up * (h1 - 0.03f)); }
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; Quad(b + 2 * i, b + 2 * j, b + 2 * j + 1, b + 2 * i + 1); }
        int cc = sv.Count; sv.Add(c + Vector3.up * (h1 - 0.03f)); for (int i = 0; i < n; i++) { si.Add(cc); si.Add(b + 2 * i + 1); si.Add(b + 2 * ((i + 1) % n) + 1); }
    }
    // an ellipsoid with radii ra along ua, rv up and rb along ub: wire rings and meridians and an opaque shell
    void Ellipsoid(Vector3 c, Vector3 ua, Vector3 ub, float ra, float rv, float rb, int sl = 8, int st = 4)
    {
        var pts = new Vector3[(st + 1) * sl]; var inn = new Vector3[(st + 1) * sl];
        for (int j = 0; j <= st; j++) for (int i = 0; i < sl; i++)
        {
            float ph = Mathf.PI * j / st, th = Mathf.PI * 2f * i / sl;
            var d = ua * (Mathf.Sin(ph) * Mathf.Cos(th) * ra) + Vector3.up * (Mathf.Cos(ph) * rv) + ub * (Mathf.Sin(ph) * Mathf.Sin(th) * rb);
            pts[j * sl + i] = c + d; inn[j * sl + i] = c + d * 0.95f;
        }
        for (int j = 1; j < st; j++) for (int i = 0; i < sl; i++) L(pts[j * sl + i], pts[j * sl + (i + 1) % sl]);
        for (int j = 0; j < st; j++) for (int i = 0; i < sl; i++) L(pts[j * sl + i], pts[(j + 1) * sl + i]);
        int b = sv.Count; sv.AddRange(inn);
        for (int j = 0; j < st; j++) for (int i = 0; i < sl; i++) { int i1 = (i + 1) % sl; Quad(b + j * sl + i, b + j * sl + i1, b + (j + 1) * sl + i1, b + (j + 1) * sl + i); }
    }
    // a window or door with a round top: sides, bottom, a half circle on top, a centre bar and a sill
    void ArchRect(Vector3 c, Vector3 tg, float w, float h, bool bars)
    {
        var bl = c - tg * (w / 2) - Vector3.up * (h / 2); var br = c + tg * (w / 2) - Vector3.up * (h / 2); var spring = c + Vector3.up * (h / 2 - w / 2);
        L(bl, br); L(bl, spring - tg * (w / 2)); L(br, spring + tg * (w / 2)); Arc(spring, tg, Vector3.up, w / 2, 0f, 180f, 6);
        if (bars) { L(c - Vector3.up * (h / 2), spring + Vector3.up * (w / 2)); L(spring - tg * (w / 2), spring + tg * (w / 2)); L(bl - tg * 0.12f - Vector3.up * 0.06f, br + tg * 0.12f - Vector3.up * 0.06f); }
    }

    // roads every 28 m (8 m wide, the player above the central crossing), blocks of up to four buildings, kerbs, a dashed centre line, a crossing
    IEnumerator BuildCity(System.Random rnd)
    {
        for (int bi = -5; bi <= 4; bi++)
            for (int bj = -5; bj <= 4; bj++)
            {
                if (Over()) { yield return null; sw.Restart(); }
                float cx = 28f * bi + 14f, cz = 28f * bj + 14f, dist = Mathf.Sqrt(cx * cx + cz * cz);
                if (dist > 125f) continue;
                Loop(Foot(cx, cz, 20f, 20f, 3f, 0f, 4));                                    // the kerb round the block
                for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++)
                {
                    if (rnd.NextDouble() < 0.12) continue;
                    float px = cx + (a == 0 ? -5f : 5f), pz = cz + (b == 0 ? -5f : 5f);
                    float h = Mathf.Lerp(4f, 11f + dist * 0.22f, (float)rnd.NextDouble());
                    if (dist < 45f) h = Mathf.Min(h, 7f);                                    // near the player the buildings stay low: the player looks down on the street
                    Box(px, pz, 8f + (float)rnd.NextDouble(), 8f + (float)rnd.NextDouble(), h, 3f);
                }
            }
        for (int k = -5; k <= 5; k++)                                                        // dashed centre lines between the crossings
            for (int m = -5; m <= 4; m++)
            {
                if (Over()) { yield return null; sw.Restart(); }
                float a0 = 28f * m + 7f, a1 = 28f * (m + 1) - 7f;
                for (float s = a0; s < a1; s += 4f)
                {
                    L(P(s, 0f, 28f * k), P(s + 2f, 0f, 28f * k));                            // road along x
                    L(P(28f * k, 0f, s), P(28f * k, 0f, s + 2f));                            // road along z
                }
            }
        for (int side = 0; side < 4; side++)                                                 // the crossing under the player: five stripes each way
            for (int s = -2; s <= 2; s++)
            {
                var dir = side == 0 ? fwd : side == 1 ? -fwd : side == 2 ? right : -right; var across = side < 2 ? right : fwd;
                var c = P(0f, 0f, 0f) + dir * 7f + across * (s * 1.3f);
                Rect(c, across, dir, 0.7f, 3f);
            }
    }

    // opaque buildings: a black solid just inside the wireframe hides the lines behind it (less visual load); one mesh for all of them
    readonly List<Vector3> sv = new List<Vector3>(); readonly List<int> si = new List<int>(); Material solidMat;
    void Solid(float cx, float cz, float w, float d, float h) => SolidBox(P(cx, 0f, cz), right, fwd, w, d, 0f, h);
    static readonly int[] boxFaces = { 0, 1, 3, 2, 4, 5, 7, 6, 0, 1, 5, 4, 2, 3, 7, 6, 1, 3, 7, 5 };   // four walls and the roof as quads of corner indices
    // an opaque box (inset 3 cm so the wireframe lines stay in front of it): c is on the ground at the centre, ua and ub the horizontal axes, la x lb the footprint, h0 to h1 the heights
    void SolidBox(Vector3 c, Vector3 ua, Vector3 ub, float la, float lb, float h0, float h1)
    {
        int b = sv.Count; float hl = la / 2 - 0.03f, hb = lb / 2 - 0.03f;
        for (int i = 0; i < 4; i++) { var g = c + ua * ((i & 1) == 0 ? -hl : hl) + ub * ((i & 2) == 0 ? -hb : hb); sv.Add(g + Vector3.up * h0); sv.Add(g + Vector3.up * (h1 - 0.03f)); }
        for (int k = 0; k < 5; k++) Quad(b + boxFaces[4 * k], b + boxFaces[4 * k + 1], b + boxFaces[4 * k + 2], b + boxFaces[4 * k + 3]);
    }
    void Quad(int a, int b, int c, int d) { si.Add(a); si.Add(b); si.Add(c); si.Add(a); si.Add(c); si.Add(d); }
    void SolidQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) { int b = sv.Count; sv.Add(p0); sv.Add(p1); sv.Add(p2); sv.Add(p3); Quad(b, b + 1, b + 2, b + 3); }
    // a low-poly opaque sphere (8 slices, 6 stacks): tree crowns
    void SolidSphere(Vector3 c, float r)
    {
        int b = sv.Count; const int sl = 8, st = 6;
        for (int j = 0; j <= st; j++) for (int i = 0; i < sl; i++) { float ph = Mathf.PI * j / st, th = Mathf.PI * 2f * i / sl; sv.Add(c + new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Sin(th)) * r); }
        for (int j = 0; j < st; j++) for (int i = 0; i < sl; i++) { int i1 = (i + 1) % sl; Quad(b + j * sl + i, b + j * sl + i1, b + (j + 1) * sl + i1, b + (j + 1) * sl + i); }
    }

    // details for every building: windows on the faces that face the player (round-topped within 70 m), a round-topped door with a curved awning, curved balconies within 40 m,
    // and a roof that is either pitched (with a chimney) or flat with a rounded parapet, a vent and sometimes a water tank or an antenna. Far buildings get a window row every second floor.
    void Windows(float cx, float cz, float w, float d, float h, float floor)
    {
        float dist = Mathf.Sqrt(cx * cx + cz * cz), hash = Mathf.Repeat(Mathf.Abs(Mathf.Sin(cx * 12.9898f + cz * 78.233f) * 43758.5453f), 1f), r = BR(w, d);
        int step = dist > 70f ? 2 : 1; bool near = dist < 70f;
        var vis = new List<int>();
        for (int face = 0; face < 4; face++)
        {
            float nx = face == 0 ? 1f : face == 1 ? -1f : 0f, nz = face == 2 ? 1f : face == 3 ? -1f : 0f;
            if (-cx * nx - cz * nz > -2f) vis.Add(face);
        }
        int doorFace = vis.Count > 0 ? vis[(int)(hash * 10f) % vis.Count] : 0;
        foreach (int face in vis)
        {
            var n = face == 0 ? right : face == 1 ? -right : face == 2 ? fwd : -fwd;
            var tg = face < 2 ? fwd : right; float len = (face < 2 ? d : w) - 2f * r, half = face < 2 ? w / 2 : d / 2;
            var fc = P(cx, 0f, cz) + n * (half + 0.02f);
            int cnt = Mathf.Max(1, (int)((len - 1f) / 2.2f));
            for (int fl = 0; fl * floor + 2.4f < h; fl += (fl == 0 ? 1 : step))
                for (int i = 0; i < cnt; i++)
                {
                    float off = (i - (cnt - 1) / 2f) * 2.2f; var wc = fc + tg * off + Vector3.up * (fl * floor + 1.5f);
                    if (fl == 0 && face == doorFace)
                    {
                        if (i != (cnt - 1) / 2) continue;
                        var dc = fc + tg * off; ArchRect(dc + Vector3.up * 1.1f, tg, 1.2f, 2.2f, false);                                   // the door
                        var top = dc + Vector3.up * 2.55f; Vector3 pl = top - tg * 0.85f, pr = top + tg * 0.85f, ql = pl, qr = pr;           // the curved awning
                        for (int k = 1; k <= 4; k++)
                        {
                            float t = k / 4f * 1.5708f; var o = n * Mathf.Sin(t) * 1f - Vector3.up * 0.45f * (1f - Mathf.Cos(t));
                            L(ql, pl + o); L(qr, pr + o); SolidQuad(ql - Vector3.up * 0.02f, qr - Vector3.up * 0.02f, pr + o - Vector3.up * 0.02f, pl + o - Vector3.up * 0.02f); ql = pl + o; qr = pr + o;
                        }
                        L(pl, pr); L(ql, qr);
                        continue;
                    }
                    bool bal = dist < 40f && fl >= 1 && i == (cnt - 1) / 2;
                    if (bal) { var dc2 = fc + tg * off + Vector3.up * (fl * floor + 0.8f + 1.15f); ArchRect(dc2, tg, 1.1f, 2.3f, true); L(dc2 - tg * 0.3f - Vector3.up * 0.1f, dc2 - tg * 0.3f + Vector3.up * 0.2f); }   // a tall French door onto the balcony, with a handle
                    else if (near) ArchRect(wc, tg, 1f, 1.3f, true);
                    else Rect(wc, tg, Vector3.up, 1f, 1.2f);
                    if (bal)                                                                       // a curved balcony: a half-round slab and railing
                    {
                        var cb = fc + tg * off + Vector3.up * (fl * floor + 0.8f);
                        Arc(cb, tg, n, 0.85f, 0f, 180f, 6); L(cb - tg * 0.85f, cb + tg * 0.85f); Arc(cb + Vector3.up * 0.9f, tg, n, 0.85f, 0f, 180f, 6);
                        for (int q = 0; q <= 6; q += 3) { float a = q / 6f * Mathf.PI; var pp = cb + tg * Mathf.Cos(a) * 0.85f + n * Mathf.Sin(a) * 0.85f; L(pp, pp + Vector3.up * 0.9f); }
                    }
                }
        }
        if (hash > 0.55f && h < 14f)                                                                                                    // a pitched roof with a chimney
        {
            bool alongX = w >= d; var ua = alongX ? right : fwd; var ub = alongX ? fwd : right; float L1 = alongX ? w : d, S1 = alongX ? d : w, pk = 1.5f + hash;
            var c0 = P(cx, h, cz); var r0 = c0 - ua * (L1 / 2 - 1f) + Vector3.up * pk; var r1 = c0 + ua * (L1 / 2 - 1f) + Vector3.up * pk;
            var e00 = c0 - ua * (L1 / 2 - 0.1f) - ub * (S1 / 2 - 0.1f); var e01 = c0 - ua * (L1 / 2 - 0.1f) + ub * (S1 / 2 - 0.1f);
            var e10 = c0 + ua * (L1 / 2 - 0.1f) - ub * (S1 / 2 - 0.1f); var e11 = c0 + ua * (L1 / 2 - 0.1f) + ub * (S1 / 2 - 0.1f);
            L(r0, r1); L(r0, e00); L(r0, e01); L(r1, e10); L(r1, e11);
            L(e00, e10); L(e10, e11); L(e11, e01); L(e01, e00);                                                // the eave edges (the corners overhang the rounded walls)
            var dn = Vector3.down * (0.06f + dist * 0.004f);                                                                       // the roof solid sits a little below its lines so they are not hidden by it
            SolidQuad(e00 + dn, e10 + dn, r1 + dn, r0 + dn); SolidQuad(e01 + dn, e11 + dn, r1 + dn, r0 + dn); SolidQuad(e00 + dn, e01 + dn, r0 + dn, r0 + dn); SolidQuad(e10 + dn, e11 + dn, r1 + dn, r1 + dn);
            Cylinder(c0 + ua * (L1 * 0.25f) + ub * (S1 * 0.2f), 0.3f, 0f, pk + 1.1f, 6);
        }
        else                                                                                                                            // a flat roof: parapet, vent, sometimes a water tank, sometimes an antenna
        {
            Loop(Foot(cx, cz, w - 0.4f, d - 0.4f, Mathf.Max(r - 0.2f, 0.1f), h + 0.5f));
            var uc = P(cx + (hash - 0.5f) * 2.5f, h, cz + (Mathf.Repeat(hash * 7.3f, 1f) - 0.5f) * 2.5f);
            Cylinder(uc, 0.7f, 0f, 1.1f, 8);
            if (hash < 0.3f)
            {
                var tc = P(cx - (hash - 0.5f) * 2.5f, h, cz - 1.5f);
                Cylinder(tc, 0.8f, 1.2f, 2.4f, 10);
                for (int q = 0; q < 4; q++) { var o = (right * Mathf.Cos(q * 1.5708f) + fwd * Mathf.Sin(q * 1.5708f)) * 0.7f; L(tc + o, tc + o + Vector3.up * 1.2f); }
            }
            if (hash > 0.8f) L(uc + Vector3.up * 1.1f, uc + Vector3.up * (3.5f + hash * 3f));
        }
    }

    void OBox(Vector3 c, Vector3 ua, Vector3 ub, float la, float lb, float h0, float h1)
    {
        Rect(c + Vector3.up * h0, ua, ub, la, lb); Rect(c + Vector3.up * h1, ua, ub, la, lb);
        for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2) L(c + Vector3.up * h0 + ua * a * la / 2 + ub * b * lb / 2, c + Vector3.up * h1 + ua * a * la / 2 + ub * b * lb / 2);
    }
    void Lamp(Vector3 b, Vector3 toRoad)
    {
        var top = b + Vector3.up * 5f; var prev = top;
        Cylinder(b, 0.2f, 0f, 0.5f, 8);                                                                                          // a round base
        L(b + Vector3.up * 0.5f, top); Circle(b + Vector3.up * 1.2f, right, fwd, 0.1f, 6); Circle(b + Vector3.up * 3f, right, fwd, 0.08f, 6);   // pole and collars
        for (int i = 1; i <= 5; i++) { float t = i / 5f * 1.5708f; var p = top + toRoad * (1.3f * (1f - Mathf.Cos(t))) + Vector3.up * (0.5f * Mathf.Sin(t)); L(prev, p); prev = p; }   // a curved arm
        var hc = prev - Vector3.up * 0.12f;
        Ellipsoid(hc, toRoad, Vector3.Cross(Vector3.up, toRoad), 0.45f, 0.13f, 0.2f, 8, 4);                                       // a rounded lamp head
        Circle(hc - Vector3.up * 0.1f, right, fwd, 0.14f, 8);                                                                     // its lens
    }

    // lamp posts along the roads, parked cars at the kerb (not near the player, where the story objects are)
    IEnumerator BuildStreetDetails(System.Random rnd)
    {
        for (int k = -4; k <= 4; k++)
            for (int m = -5; m <= 4; m++)
                for (int along = 0; along < 2; along++)                                            // along = 0: road runs along z (x = 28k); 1: along x (z = 28k)
                {
                    if (Over()) { yield return null; sw.Restart(); }
                    float s0 = 28f * m;
                    foreach (float s in new[] { s0 + 10f, s0 + 18f })
                    {
                        float side = (s > s0 + 14f) ? -1f : 1f;
                        var b = along == 0 ? P(28f * k + 4.4f * side, 0f, s) : P(s, 0f, 28f * k + 4.4f * side);
                        var dist = new Vector2(b.x - origin.x, b.z - origin.z).magnitude;
                        if (dist < 14f || dist > 100f) continue;
                        Lamp(b, (along == 0 ? right : fwd) * -side);
                    }
                    if (((k + m) & 1) == 0)                                                           // a tree (a trunk and three crossed rings) on the pavement edge
                    {
                        float ts = s0 + 22f, tsd = along == 0 ? 1f : -1f;
                        var tb = along == 0 ? P(28f * k + 4.3f * tsd, 0f, ts) : P(ts, 0f, 28f * k + 4.3f * tsd);
                        float td = new Vector2(tb.x - origin.x, tb.z - origin.z).magnitude;
                        if (td > 14f && td < 80f)
                        {
                            Tree(tb, tb.x * 0.37f + tb.z * 0.71f);
                        }
                    }
                    else                                                                               // a bench and a bin on the pavement edge
                    {
                        float bs = s0 + 22f, bsd = along == 0 ? 1f : -1f;
                        var bp = along == 0 ? P(28f * k + 4.3f * bsd, 0f, bs) : P(bs, 0f, 28f * k + 4.3f * bsd);
                        float bd = new Vector2(bp.x - origin.x, bp.z - origin.z).magnitude;
                        if (bd > 14f && bd < 80f)
                        {
                            var ua = along == 0 ? fwd : right; var ub = along == 0 ? right : fwd;
                            Bench(bp, ua, ub, bsd); Bin(bp + ua * 1.5f); benchSpots.Add(new Vector3[] { bp, ua, ub * bsd });
                        }
                    }
                    for (float s = s0 + 8f; s < s0 + 21f; s += 6f)                                  // cars at the kerb, between the crossings
                        for (int sd = -1; sd <= 1; sd += 2)
                        {
                            if (rnd.NextDouble() > 0.3) continue;
                            var c = along == 0 ? P(28f * k + 3f * sd, 0f, s) : P(s, 0f, 28f * k + 3f * sd);
                            var dist = new Vector2(c.x - origin.x, c.z - origin.z).magnitude;
                            if (dist < 35f || dist > 75f) continue;
                            Car(c, along == 0 ? fwd : right, along == 0 ? right : fwd);
                        }
                }
    }

    // traffic lights: a pole and a light box at the corners of every crossing within 70 m (not the one under the player)
    IEnumerator BuildTrafficLights()
    {
        for (int k = -4; k <= 4; k++)
        {
            if (Over()) { yield return null; sw.Restart(); }
            for (int j = -4; j <= 4; j++)
                for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2)
                {
                    var c = P(28f * k + 4.6f * a, 0f, 28f * j + 4.6f * b);
                    float d = new Vector2(c.x - origin.x, c.z - origin.z).magnitude;
                    if (d < 10f || d > 70f) continue;
                    TrafficLight(c, -(right * a + fwd * b).normalized);
                }
        }
    }

    // a bicycle on its side, a stroller, litter on the street
    readonly List<Vector3> problemSpots = new List<Vector3>();
    IEnumerator BuildProps(System.Random rnd)
    {
        // problems far away: spots on the roads 28 to 105 m out, each with litter strewn round it (a person down, a stabbing or a fight is added there by BuildPeople)
        problemSpots.Clear(); var prnd = new System.Random(55);
        for (int tries = 0; problemSpots.Count < 16 && tries < 600; tries++)
        {
            int k = prnd.Next(-3, 4); bool alongX = prnd.NextDouble() < 0.5; float along = (float)(prnd.NextDouble() - 0.5) * 210f, lat = (float)(prnd.NextDouble() - 0.5) * 4f;
            var c = alongX ? P(along, 0f, 28f * k + lat) : P(28f * k + lat, 0f, along); float d = new Vector2(c.x - origin.x, c.z - origin.z).magnitude; bool close = false;
            if (d < 28f || d > 105f) continue; foreach (var q in problemSpots) if ((q - c).sqrMagnitude < 400f) close = true;
            if (!close) problemSpots.Add(c);
        }
        foreach (var c in problemSpots)
                for (int i = 0; i < 8; i++) { float a = (float)prnd.NextDouble() * Mathf.PI * 2f, r = 0.8f + (float)prnd.NextDouble() * 2.2f; Litter(c + (right * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * r, (float)prnd.NextDouble(), 1.8f); }
        // bicycle lying on the ground at (-3.5, 6): two wheels flat on the ground, a frame between them
        var bc = P(-3.5f, 0.05f, 6f);
        if (!cleanNear)                                                                                // the bicycle and the stroller are near problems: gone when the player acted
        {
            Circle(bc - fwd * 0.55f, right, fwd, 0.33f, 16); Circle(bc + fwd * 0.55f, right, fwd, 0.33f, 16);
            L(bc - fwd * 0.55f, bc + fwd * 0.1f); L(bc + fwd * 0.1f, bc + fwd * 0.55f); L(bc - fwd * 0.55f, bc + fwd * 0.55f + right * 0.1f);
            L(bc + fwd * 0.1f + right * 0.05f, bc + fwd * 0.4f + right * 0.45f);                  // handlebar
            Stroller(P(3f, 0f, -9f));
        }                                                                 // the stroller at (3, -9)
        // litter on the street: cans, bottles, cups, crumpled paper, newspaper and bags (more of it when the player did not bin the can, in scene 6)
        int n = scene6 ? 260 : 170;                                                              // scene 6: more of it; none within nearClean m if the player acted
        for (int i = 0; i < n; i++)
        {
            if (Over()) { yield return null; sw.Restart(); }
            float a = (float)(rnd.NextDouble() - 0.5) * 230f, b = (float)(rnd.NextDouble() - 0.5) * 6.5f;
            var p = rnd.NextDouble() < 0.5 ? P(a, 0f, 28f * Mathf.Round((float)(rnd.NextDouble() - 0.5) * 8f) + b) : P(28f * Mathf.Round((float)(rnd.NextDouble() - 0.5) * 8f) + b, 0f, a);
            float ld = (float)rnd.NextDouble(); if (cleanNear && new Vector2(p.x - origin.x, p.z - origin.z).magnitude < nearClean) continue;
            Litter(p, ld, 1.8f);
        }
    }

    // at the player's feet (floor height): the squished orange and fruit, more trash, or the clean lit circle
    void BuildFeet(System.Random rnd)
    {
        bool acted = scene6 && (seq.gaveOrange || seq.binnedCan || seq.calmedDog);
        bool oranges = !cleanNear, trash = !cleanNear;
        var floor = new Vector3(origin.x, 0.02f, origin.z);                                // the player's own floor (the clean circle)
        var spot = keep ? Ground(OldManScene.PlacedOldMan, 0.03f) : P(manX, 0.03f, manZ);   // the old man's spilled oranges and trash lie on the street (where scene 2 had them)
        if (oranges)
        {
            var mat = Resources.Load<Material>("OrangeSphere");
            int nOr = keep ? OldManScene.PlacedOranges.Count : 9;
            for (int i = 0; i < nOr; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(s.GetComponent<Collider>());
                float ang = (30f + i * 40f) * Mathf.Deg2Rad, r = 0.5f + 0.35f * (i % 4);
                s.transform.SetParent(root.transform, true);
                s.transform.position = keep ? Ground(OldManScene.PlacedOranges[i], 0.04f) : spot + (right * Mathf.Cos(ang) + fwd * Mathf.Sin(ang)) * r;   // exactly where scene 2 left each orange
                s.transform.localScale = keep ? Vector3.one * 0.08f : new Vector3(0.1f + 0.015f * (i % 2), 0.075f, 0.1f + 0.012f * (i % 3));        // real orange size (0.04 m radius, as in scene 2)
                var rd = s.GetComponent<Renderer>(); rd.sharedMaterial = mat; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rd.receiveShadows = false;
                if (i < 2 && !scene6) s.AddComponent<BreakTarget>();                                                  // reaching for these still breaks the chains in scene 3
                fruit.Add(rd);
            }
        }
        if (keep && !cleanNear)                                                                                       // the bag, the can and the bin stay where scene 2 put them
        {
            var mat2 = Resources.Load<Material>("OrangeSphere");
            var bag = CanBinScene.PaperBag(root.transform, mat2, out bagRs2, out bagKs2);
            bag.transform.position = Ground(OldManScene.PlacedBag, 0.05f); bag.transform.rotation = Quaternion.AngleAxis(60f, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
            var can = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(can.GetComponent<Collider>()); can.transform.SetParent(root.transform, true);
            can.transform.position = Ground(CanBinScene.PlacedCan, 0.033f); can.transform.localScale = new Vector3(0.065f, 0.06f, 0.065f); can.transform.rotation = Quaternion.Euler(0f, 200f, 90f);
            var cr = can.GetComponent<Renderer>(); cr.sharedMaterial = mat2; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cr.receiveShadows = false; fruit.Add(cr);
            Bin(Ground(CanBinScene.PlacedBin), 0.57f);                                                                  // 0.5 m tall, as the bin of scene 2
        }
        if (trash)
            for (int i = 0; i < 30; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.5f + (float)rnd.NextDouble() * 2.5f;
                var p = spot + (right * Mathf.Cos(ang) + fwd * Mathf.Sin(ang)) * r;
                Litter(new Vector3(p.x, baseY, p.z), (float)rnd.NextDouble(), 1.8f);
            }
        // author 2026-11-23: the clean lit rings round the player's feet were removed; the cleared area (nearClean) is the only sign of what the player did
    }

    // the bystanders: still columns (a head and a body of 36 particles) in clusters on the roads, and the baby in the stroller
    // author 2026-11-24: red and blue police lights, as if the cars were behind buildings. The cars are never drawn: only a few strobing cores (hard little lights, as if seen
    // through gaps and over roofs) and large soft halos around them, washing the facades and the air above the roofs. Red and blue alternate, each spot at its own phase.
    public int policeSpots = 8; public float policeFlash = 0.5f;
    ParticleSystem policePs; ParticleSystem.Particle[] policeParts; Material policeMat; int[] policeSpot; bool[] policeBlue, policeCore; float[] policeBase; float[] policePhase; static Texture2D softTex;
    static Texture2D Soft()
    {
        if (softTex != null) return softTex;
        const int n = 64; softTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Exp(-5f * r * r) * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1f, r)); px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * a)); }   // a Gaussian blur (the author reference: a red and a blue glow, very soft, black buildings in front), faded to nothing before the edge
        softTex.SetPixels32(px); softTex.Apply(true); return softTex;
    }
    // ChainParticle (the figures' material) is OPAQUE with alpha clip: it cuts every particle texture to a hard-edged disc, so no blur is possible with it (the 2026-11-24 "too hard" feedback).
    // The glow gets its own transparent, additive, no-depth-write material from the same URP particle shader, so the soft texture really fades to nothing.
    static Material GlowMaterial()
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) { Debug.LogWarning("StreetScene: URP particle shader not found for the police glow"); sh = Shader.Find("Sprites/Default"); }
        var m = new Material(sh) { name = "PoliceGlow" };
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); m.SetFloat("_AlphaClip", 0f); m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); m.DisableKeyword("_ALPHAMODULATE_ON");
        m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
        m.SetTexture("_BaseMap", Soft());
        return m;
    }
    void BuildPolice()
    {
        var prnd = new System.Random(303); var spots = new List<Vector3>(); var roadDirs = new List<Vector3>(); var roadPts = new List<Vector3>();
        for (int tries = 0; spots.Count < policeSpots && tries < 800; tries++)
        {
            // author 2026-11-24: strictly between the buildings, never on the streets next to the player: the middle of a block (buildings all round), and not in the blocks
            // beside the four streets that run in front of, behind, left and right of the player (those are within 28 m of their axes)
            int bi = prnd.Next(-4, 4), bj = prnd.Next(-4, 4); var c = P(28f * bi + 14f, 0f, 28f * bj + 14f);
            var rel = c - new Vector3(origin.x, c.y, origin.z); float lx = Mathf.Abs(Vector3.Dot(rel, right)), lz = Mathf.Abs(Vector3.Dot(rel, fwd));
            float d = new Vector2(c.x - origin.x, c.z - origin.z).magnitude; if (d < 60f || d > 110f || Mathf.Min(lx, lz) < 28f) continue;
            bool close = false; foreach (var q in spots) if ((q - c).sqrMagnitude < 1225f) close = true;
            if (!close) { bool ax = prnd.NextDouble() < 0.5; var rdir = ax ? right : fwd; var pdir = ax ? fwd : right; spots.Add(c); roadDirs.Add(rdir); roadPts.Add(c + pdir * (prnd.NextDouble() < 0.5 ? 14f : -14f)); }   // the road that runs along one side of the block
        }
        var pos = new List<Vector3>(); var sz = new List<float>(); var spot = new List<int>(); var blue = new List<bool>(); var core = new List<bool>(); var bas = new List<float>(); var ph = new List<float>();
        for (int s = 0; s < spots.Count; s++)
        {
            float phase = (float)prnd.NextDouble();
            // author 2026-11-24: the light bounces off only a few buildings: the two facing each other across the road next to the hidden source (red on one, blue on the opposite one) and the building
            // directly to the left and the right of each of them (fainter). Six soft patches on the walls, low, instead of a haze over every building.
            var rd = roadDirs[s]; var pr = Vector3.Cross(Vector3.up, rd);
            for (int w = 0; w < 2; w++)
                for (int a = -1; a <= 1; a++)
                {
                    var wp = roadPts[s] + pr * (w == 0 ? 8f : -8f) + rd * (a * 14f) + Vector3.up * (4f + (float)prnd.NextDouble() * 2f);
                    pos.Add(wp); sz.Add(13f); spot.Add(s); blue.Add(w == 1); core.Add(false); bas.Add(a == 0 ? 0.4f : 0.22f); ph.Add(phase);
                }
            for (int side = 0; side < 2; side++)                                       // side 0 red, side 1 blue: a light bar's two ends
            {
                var o = spots[s] + right * (side == 0 ? -7f : 7f) + Vector3.up * 9f;   // the afterglow hangs at roof height over the hidden source, so it no longer washes the street floor in front of the buildings (author 2026-11-24)
                pos.Add(o); sz.Add(34f); spot.Add(s); blue.Add(side == 1); core.Add(false); bas.Add(0.2f); ph.Add(phase);          // a very soft haze over the block, fainter than before (less disco)
                for (int h = 0; h < 1; h++)                                             // and one wider, fainter one above the roofs
                {
                    var hp = spots[s] + (right * ((float)prnd.NextDouble() - 0.5f) + fwd * ((float)prnd.NextDouble() - 0.5f)) * 14f + Vector3.up * (14f + (float)prnd.NextDouble() * 8f);
                    pos.Add(hp); sz.Add(42f + (float)prnd.NextDouble() * 16f); spot.Add(s); blue.Add(side == 1); core.Add(false); bas.Add(0.07f); ph.Add(phase);
                }
            }
        }
        if (pos.Count == 0) return;
        var go = new GameObject("PoliceGlow"); go.transform.SetParent(root.transform, false);
        policePs = go.AddComponent<ParticleSystem>();
        var main = policePs.main; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.playOnAwake = false; main.maxParticles = pos.Count;
        var em = policePs.emission; em.enabled = false; var sh = policePs.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Resources.Load<Material>("ChainParticle");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.maxParticleSize = 5f;
        policeMat = GlowMaterial(); r.sharedMaterial = policeMat;
        policeParts = new ParticleSystem.Particle[pos.Count];
        for (int i = 0; i < pos.Count; i++) { policeParts[i].position = pos[i]; policeParts[i].startSize = sz[i]; policeParts[i].startLifetime = 1000f; policeParts[i].remainingLifetime = 1000f; policeParts[i].startColor = Color.clear; }
        policePs.SetParticles(policeParts, policeParts.Length); policePs.Play();
        policeSpot = spot.ToArray(); policeBlue = blue.ToArray(); policeCore = core.ToArray(); policeBase = bas.ToArray(); policePhase = ph.ToArray();
    }
    void UpdatePolice(float k)
    {
        if (policePs == null) return;
        policeMat.SetColor(BaseColor, new Color(k, k, k, 1f));
        float tm = Time.time * policeFlash;
        for (int i = 0; i < policeParts.Length; i++)
        {
            float x = (tm + policePhase[i]) % 1f, xc = (x + (policeBlue[i] ? 0.5f : 0f)) % 1f;
            float on = xc < 0.5f ? 1f : 0f;   // a true strobe: red is on for half the cycle and blue the other half, switching instantly with no fade (author 2026-11-24)
            on = 0.06f + 0.94f * on;
            float v = on * policeBase[i];
            policeParts[i].startColor = policeBlue[i] ? new Color(0.12f, 0.3f, 1f, v) : new Color(1f, 0.05f, 0.04f, v);   // additive: rgb x alpha x the soft texture
        }
        policePs.SetParticles(policeParts, policeParts.Length);
    }

    void BuildCrowd(System.Random rnd)
    {
        var pp = new List<Vector3>(); var pc = new List<Color>(); var ps = new List<float>();
        int made = 0;
        while (made < columns)
        {
            bool alongX = rnd.NextDouble() < 0.5;
            float road = 28f * Mathf.Round((float)(rnd.NextDouble() - 0.5) * 6f), along = (float)(rnd.NextDouble() - 0.5) * 220f, lateral = (float)(rnd.NextDouble() - 0.5) * 6.5f;
            int groupSize = 3 + rnd.Next(5);
            for (int g = 0; g < groupSize && made < columns; g++, made++)
            {
                float a = along + g * 0.9f, l = lateral + (float)(rnd.NextDouble() - 0.5) * 1.6f;
                var foot = alongX ? P(a, 0f, road + l) : P(road + l, 0f, a);
                if (new Vector2(foot.x - origin.x, foot.z - origin.z).magnitude < 60f) continue;            // still columns only as a distant backdrop; people near you are animated
                var col = PersonColour();
                for (int j = 0; j < 8; j++) { pp.Add(foot + Vector3.up * 1.62f + (Vector3)Random.insideUnitSphere * 0.12f); pc.Add(col); ps.Add(Random.Range(0.05f, 0.09f)); }
                for (int j = 0; j < 28; j++)
                {
                    float hh = Random.Range(0.05f, 1.45f), rr = 0.2f * Mathf.Sqrt(Random.value), ang = Random.value * Mathf.PI * 2f;
                    pp.Add(foot + Vector3.up * hh + right * Mathf.Cos(ang) * rr + fwd * Mathf.Sin(ang) * rr); pc.Add(col); ps.Add(Random.Range(0.05f, 0.1f));
                }
            }
        }
        crowdR = MakeSystem("Crowd", pp, pc, ps, out crowdMat);
        // the crying baby in the stroller at (3, -9): a tiny tight cluster that trembles a little by being re-seeded not at all (a still cluster)
        var bp = new List<Vector3>(); var bc = new List<Color>(); var bs = new List<float>();
        if (!cleanNear)                                                                           // the abandoned baby is a near problem too
        for (int j = 0; j < 22; j++) { bp.Add(P(3f, 0.6f, -9f) + (Vector3)Random.insideUnitSphere * 0.13f); bc.Add(new Color(0.08f, 0.04f, 0.14f, 1f)); bs.Add(Random.Range(0.025f, 0.05f)); }
        babyR = MakeSystem("Baby", bp, bc, bs, out babyMat);
    }

    ParticleSystemRenderer MakeSystem(string name, List<Vector3> pos, List<Color> col, List<float> size, out Material mat)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.playOnAwake = false; main.maxParticles = pos.Count;   // local: moves with the street root as it lowers
        var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Resources.Load<Material>("ChainParticle");
        mat = r.material; mat.SetTexture("_BaseMap", ParticleFigure.DiscTexture());
        var parts = new ParticleSystem.Particle[pos.Count];
        for (int i = 0; i < parts.Length; i++) { parts[i].position = pos[i]; parts[i].startSize = size[i]; parts[i].startLifetime = 1000f; parts[i].remainingLifetime = 1000f; parts[i].startColor = col[i]; }
        ps.SetParticles(parts, parts.Length); ps.Play();
        var a = new Asm { ps = ps, arr = parts, tgt = pos.ToArray(), from = new Vector3[parts.Length], delay = new float[parts.Length], size = size.ToArray() };   // the particles start scattered and come together
        for (int i = 0; i < parts.Length; i++)
        {
            a.from[i] = a.tgt[i] + (Vector3)Random.insideUnitSphere * 3f;
            a.delay[i] = growDelay + 1f + Mathf.Sqrt((a.tgt[i].x - origin.x) * (a.tgt[i].x - origin.x) + (a.tgt[i].z - origin.z) * (a.tgt[i].z - origin.z)) / 125f * growSpread;
            parts[i].position = a.from[i];
        }
        ps.SetParticles(parts, parts.Length); asm.Add(a); assembling = true;
        return r;
    }

    // the real figures: a fallen person and a kneeling helper, a stabbed person and a standing helper, and (scene 6, dog not calmed) the scared woman walking
    void BuildFigures()
    {
        if (!cleanNear)                                                                           // the near problems: the old man (blue) gathering his oranges now, a person down with a helper and a stabbing with a helper (queued: made one per frame)
        {
            bool cont = keep && OldManScene.PlacedClip != null;                                   // he carries on from the pose he had in scene 2 (same clip, same moment)
            var oldMan = Fig(cont ? OldManScene.PlacedClip : gather != null ? gather : kneel, keep ? OldManScene.PlacedOldMan : P(manX, 0f, manZ), oldBlue, 2500, true, 1, 0, keep);
            if (cont) { oldMan.loop = OldManScene.PlacedLoop; oldMan.startTime = OldManScene.PlacedTime; }
            oldManFig = oldMan; if (keep) KeepFig(oldMan);
            queue.Add(new Spec { clip = fallen, pos = P(2.5f, 0f, 12f), count = 1500, keepFacing = true });
            if (kneel != null) queue.Add(new Spec { clip = kneel, pos = P(3.9f, 0f, 12.2f), count = 1500, keepFacing = true });
            if (stabbed != null) queue.Add(new Spec { clip = stabbed, pos = P(-3f, 0f, 22f), count = 1500, keepFacing = true });
            queue.Add(new Spec { clip = idle, pos = P(-1.7f, 0f, 21.4f), count = 1500, keepFacing = true });
        }
        if (scene6 && !seq.calmedDog && !cleanNear && scaredWalk != null)
        {
            womanFig = Fig(scaredWalk, P(-26f, 0f, 7f), woman, 1500, false, 1, 1);                            // the lady keeps her purple-pink
            peds.Add(new Ped { f = womanFig, dir = right, speed = 0f });                                       // turn her once to face the way she walks (she faced the player and crossed sideways)
        }
        if (!cleanNear)                                                                           // the lady and the dog of scene 2: she cowers from the barking dog (scene 3), a green ball lies between them
        {
            var lp = keep ? Ground(WomanDogScene.PlacedWoman) : P(7f, 0f, 3f); var dp = keep ? Ground(WomanDogScene.PlacedDog) : P(9.4f, 0f, 4.4f); var toDog = dp - lp; toDog.y = 0f;   // where scene 2 had them
            if (!scene6 && cowerClip != null && keep) { adoptLady = true; adoptWait = 0f; }             // scene 2's own lady object is handed over (AdoptLady), not copied
            else if (!scene6 && cowerClip != null)
            {
                bool cont2 = keep && WomanDogScene.PlacedClip != null;                                   // she carries on from the pose she had in scene 2
                var lady = Fig(cont2 ? WomanDogScene.PlacedClip : cowerClip, lp, woman, 2500, false, 1, 1, keep);
                if (cont2) { lady.loop = WomanDogScene.PlacedLoop; lady.startTime = WomanDogScene.PlacedTime; }
                ladyFig = lady; if (keep) KeepFig(lady);                                              // purple-pink, as in scene 2
                peds.Add(new Ped { f = lady, speed = 0f, hasTarget = true, target = dp });
            }
            var dg = new GameObject("Dog"); dg.transform.SetParent(root.transform, true); dg.transform.position = dp; dg.transform.rotation = Quaternion.LookRotation(-toDog);
            dog = dg.AddComponent<ParticleDog>(); dog.brightness = 0f; dog.barking = true;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(ball.GetComponent<Collider>()); ball.transform.SetParent(root.transform, true);
            ball.transform.position = (lp + dp) * 0.5f + Vector3.up * (keep ? 0.04f : 0.08f); ball.transform.localScale = Vector3.one * (keep ? 0.08f : 0.16f);
            ballR2 = ball.GetComponent<Renderer>(); ballR2.sharedMaterial = Resources.Load<Material>("OrangeSphere"); ballR2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ballR2.receiveShadows = false;
        }
        BuildPeople();
    }

    // animated people the whole time: walkers go up and down the roads (the walking clip, moved along), standers loop an idle, looking-up or pick-up clip in small groups.
    // They are queued and made one per frame (each builds a skeleton), nearer ones with more particles.
    class Ped { public ParticleFigure f; public Vector3 dir; public float speed, dist, range, yaw; public bool init, hasTarget; public Vector3 target; }   // target: she turns so that her right hand points at it
    struct Spec { public AnimationClip clip; public Vector3 pos, dir; public float speed, yaw; public int count, sex; public bool keepFacing; }   // sex: 0 random, 1 male, 2 female; count 1000 or more: a hero (full particles)
    ParticleFigure oldManFig, ladyFig;
    readonly HashSet<ParticleFigure> keptFigs = new HashSet<ParticleFigure>();
    bool adoptLady; float adoptWait;
    // the lady of scene 2 is the same object in the street: WomanDogScene.End hands her over instead of destroying her, and she joins the street root without any change of pose,
    // facing, size or light. If she never arrives (1.5 s), a copy is made as before.
    void AdoptLady()
    {
        var f = WomanDogScene.HandOver;
        if (f == null)
        {
            if ((adoptWait += Time.deltaTime) > 1.5f && cowerClip != null)
            {
                adoptLady = false;
                var lp = Ground(WomanDogScene.PlacedWoman); var dp = Ground(WomanDogScene.PlacedDog);
                var lady = Fig(cowerClip, lp, woman, 2500, false, 1, 1, true);
                ladyFig = lady; KeepFig(lady);
                peds.Add(new Ped { f = lady, speed = 0f, hasTarget = true, target = dp });
            }
            return;
        }
        adoptLady = false; WomanDogScene.HandOver = null;
        f.transform.SetParent(root.transform, true);
        f.gather = 1f; figs.Add(f); figDelay.Add(-100f); keptFigs.Add(f); ladyFig = f;
    }
    void KeepFig(ParticleFigure f) { keptFigs.Add(f); figDelay[figs.IndexOf(f)] = -100f; f.gather = 1f; }     // present at once: no assembling, full brightness
    float prevNear = 0.01f; bool cleanNear, keep; Renderer[] bagRs2; float[] bagKs2;   // keep: scene 2's old man, can, bin, woman and dog stay exactly where scene 2 put them
    Vector3 Ground(Vector3 w, float y = 0f) => new Vector3(w.x, baseY + y, w.z); const float nearClean = 25f;
    readonly List<Vector3[]> benchSpots = new List<Vector3[]>();
    readonly List<Spec> queue = new List<Spec>(); readonly List<Ped> peds = new List<Ped>();
    static readonly Vector2[] heroes = { new Vector2(-6f, -3f), new Vector2(2.5f, 12f), new Vector2(3.9f, 12.2f), new Vector2(-3f, 22f), new Vector2(-1.7f, 21.4f), new Vector2(3f, -9f), new Vector2(-3.5f, 6f) };
    void BuildPeople()
    {
        queue.Clear(); peds.Clear(); var rnd = new System.Random(77);
        if (walk == null) return;
        var standL = new List<AnimationClip> { idle, idle2, idle3, lookUp }; if (talkClips != null) standL.AddRange(talkClips); if (textClips != null) standL.AddRange(textClips); standL.RemoveAll(c => c == null); var stand = standL.ToArray(); int placed = 0;
        for (int tries = 0; placed < strollerCount && tries < 400; tries++)
        {
            int k = rnd.Next(1, 4) * (rnd.NextDouble() < 0.5 ? -1 : 1); bool alongX = rnd.NextDouble() < 0.5; float along = (float)(rnd.NextDouble() - 0.5) * 120f, lat = (float)(rnd.NextDouble() - 0.5) * 6.4f;
            var pos = alongX ? P(along, 0f, 28f * k + lat) : P(28f * k + lat, 0f, along); float d = new Vector2(pos.x - origin.x, pos.z - origin.z).magnitude;
            if (d < 12f || d > 95f) continue;
            var dir = (alongX ? right : fwd) * (rnd.NextDouble() < 0.5 ? 1f : -1f); bool old = placed % 9 == 0 && walkOld != null;
            queue.Add(new Spec { clip = old ? walkOld : (walkMix != null && walkMix.Length > 0 ? walkMix[rnd.Next(walkMix.Length)] ?? walk : walk), pos = pos, dir = dir, speed = old ? 0.7f : 1.1f + (float)rnd.NextDouble() * 0.5f, yaw = 0f, count = d < 30f ? 450 : d < 55f ? 300 : 220 }); placed++;
        }
        placed = 0;
        int standN = Mathf.Max(0, standerCount - problemSpots.Count * 2);                              // the problems below replace some standers, so the number of people stays the same
        for (int tries = 0; placed < standN && tries < 600; tries++)
        {
            int k = rnd.Next(-3, 4); bool alongX = rnd.NextDouble() < 0.5; float along = (float)(rnd.NextDouble() - 0.5) * 120f, lat = (float)(rnd.NextDouble() - 0.5) * 6.4f;
            int group = 1 + rnd.Next(3);
            for (int g = 0; g < group && placed < standN; g++)
            {
                float a = along + g * 1.1f, l = lat + (float)(rnd.NextDouble() - 0.5) * 1.2f;
                var pos = alongX ? P(a, 0f, 28f * k + l) : P(28f * k + l, 0f, a); float d = new Vector2(pos.x - origin.x, pos.z - origin.z).magnitude;
                bool near = false; foreach (var h in heroes) if ((new Vector2(pos.x - P(h.x, 0f, h.y).x, pos.z - P(h.x, 0f, h.y).z)).magnitude < 5f) near = true;
                if (d < 10f || d > 70f || near) continue;
                var clip = (rnd.NextDouble() < 0.12 && pickUp != null) ? pickUp : stand[rnd.Next(stand.Length)];
                queue.Add(new Spec { clip = clip, pos = pos, dir = Vector3.zero, speed = 0f, yaw = (float)rnd.NextDouble() * 360f, count = d < 30f ? 450 : d < 55f ? 300 : 220 }); placed++;
            }
        }
        // problems far away: someone down with helpers kneeling, a stabbing with a helper, someone alone on the ground with people looking
        for (int i = 0; i < problemSpots.Count; i++)
        {
            var c = problemSpots[i]; float yw = (float)rnd.NextDouble() * Mathf.PI * 2f; var ax = right * Mathf.Cos(yw) + fwd * Mathf.Sin(yw); var ay = Vector3.Cross(Vector3.up, ax); int type = i % 6;
            if (fallen != null && type != 1 && type != 5) queue.Add(new Spec { clip = fallen, pos = c, yaw = yw * Mathf.Rad2Deg, count = 260 });
            if (type == 0 || type == 3) { if (kneel != null) queue.Add(new Spec { clip = kneel, pos = c + ax * 1.1f, yaw = yw * Mathf.Rad2Deg + 180f, count = 260 }); }
            if (type == 3 && kneel != null) queue.Add(new Spec { clip = kneel, pos = c - ax * 1.1f + ay * 0.4f, yaw = yw * Mathf.Rad2Deg, count = 260 });
            if (type == 1 && stabbed != null) { queue.Add(new Spec { clip = stabbed, pos = c, yaw = yw * Mathf.Rad2Deg, count = 260 }); queue.Add(new Spec { clip = idle, pos = c + ax * 1.3f, yaw = yw * Mathf.Rad2Deg + 180f, count = 260 }); }
            if (type == 4 || type == 5)                                                              // a crowd gathering round someone down (4) or round a fight (5): a ring of people, all looking in
            {
                int ring = type == 4 ? 10 : 7; float rr = type == 4 ? 2.2f : 3.2f;
                if (type == 5 && punchClips != null && punchClips.Length > 0 && hitClips != null && hitClips.Length > 0)
                {
                    queue.Add(new Spec { clip = punchClips[rnd.Next(punchClips.Length)], pos = c - ax * 0.55f, dir = ax, speed = 0f, count = 260 });
                    queue.Add(new Spec { clip = hitClips[rnd.Next(hitClips.Length)], pos = c + ax * 0.55f, dir = -ax, speed = 0f, count = 260 });
                }
                else if (type == 5 && arguing != null)
                {
                    queue.Add(new Spec { clip = arguing, pos = c - ax * 0.45f, dir = ax, speed = 0f, count = 260 });
                    queue.Add(new Spec { clip = arguing, pos = c + ax * 0.45f, dir = -ax, speed = 0f, count = 260 });
                }
                for (int j = 0; j < ring; j++)
                {
                    float a = yw + j * Mathf.PI * 2f / ring + (float)(rnd.NextDouble() - 0.5) * 0.3f, r = rr + (float)rnd.NextDouble() * 0.9f;
                    var u = right * Mathf.Cos(a) + fwd * Mathf.Sin(a);
                    var look = reactClips != null && reactClips.Length > 0 && rnd.NextDouble() < 0.6 ? reactClips[rnd.Next(reactClips.Length)] : stand[rnd.Next(stand.Length)];   // mostly shocked or reacting
                    queue.Add(new Spec { clip = look, pos = c + u * r, yaw = a * Mathf.Rad2Deg + 180f, count = 220 });
                }
            }
            if (type == 2) { queue.Add(new Spec { clip = lookUp != null ? lookUp : idle, pos = c + ax * 2.2f + ay * 0.6f, yaw = 0f, count = 220 }); queue.Add(new Spec { clip = idle2 != null ? idle2 : idle, pos = c - ax * 2.0f - ay * 0.5f, yaw = 90f, count = 220 }); }
        }
        // pairs arguing, face to face
        if (arguing != null)
            for (int i = 0; i < 20; i++)
            {
                bool alongX = rnd.NextDouble() < 0.5; int k = rnd.Next(-3, 4); float along = (float)(rnd.NextDouble() - 0.5) * 100f, lat = (float)(rnd.NextDouble() - 0.5) * 5f;
                var c = alongX ? P(along, 0f, 28f * k + lat) : P(28f * k + lat, 0f, along); float d = new Vector2(c.x - origin.x, c.z - origin.z).magnitude; if (d < 12f || d > 65f || (cleanNear && d < nearClean)) continue;
                var axis = alongX ? right : fwd;
                bool brawl = i % 3 == 0 && punchClips != null && punchClips.Length > 0 && hitClips != null && hitClips.Length > 0;   // every third pair is a fist fight
                queue.Add(new Spec { clip = brawl ? punchClips[i % punchClips.Length] : arguing, pos = c - axis * (brawl ? 0.55f : 0.45f), dir = axis, speed = 0f, count = 300 }); queue.Add(new Spec { clip = brawl ? hitClips[i % hitClips.Length] : arguing, pos = c + axis * (brawl ? 0.55f : 0.45f), dir = -axis, speed = 0f, count = 300 });
            }
        // author 2026-11-24: the runners are gone; the same 24 places are action instead, to make the street feel lawless: fist fights, a mugging (someone beaten on the ground), and a gang beating
        // one person, with the pairs and groups spread along the roads. (Needs the punching and hit clips; with none assigned nothing is added.)
        if (punchClips != null && punchClips.Length > 0 && hitClips != null && hitClips.Length > 0)
            for (int i = 0; i < 24; i++)
            {
                int k = rnd.Next(1, 4) * (rnd.NextDouble() < 0.5 ? -1 : 1); bool alongX = rnd.NextDouble() < 0.5; float along = (float)(rnd.NextDouble() - 0.5) * 100f, lat = (float)(rnd.NextDouble() - 0.5) * 5f;
                var c = alongX ? P(along, 0f, 28f * k + lat) : P(28f * k + lat, 0f, along); float d = new Vector2(c.x - origin.x, c.z - origin.z).magnitude; if (d < 14f || d > 70f) continue;
                var ax = alongX ? right : fwd; var ay = Vector3.Cross(Vector3.up, ax); int cnt = d < 30f ? 450 : 300; int type = i % 4;
                if (type == 0 || type == 1)                                                      // a fist fight: one punching, one taking it
                {
                    queue.Add(new Spec { clip = punchClips[rnd.Next(punchClips.Length)], pos = c - ax * 0.55f, dir = ax, speed = 0f, count = cnt });
                    queue.Add(new Spec { clip = hitClips[rnd.Next(hitClips.Length)], pos = c + ax * 0.55f, dir = -ax, speed = 0f, count = cnt });
                }
                else if (type == 2 && fallen != null)                                            // a mugging: someone on the ground, someone standing over them punching down
                {
                    queue.Add(new Spec { clip = fallen, pos = c, yaw = (float)rnd.NextDouble() * 360f, count = cnt });
                    queue.Add(new Spec { clip = punchClips[rnd.Next(punchClips.Length)], pos = c + ay * 0.7f, dir = -ay, speed = 0f, count = cnt });
                }
                else                                                                              // a gang: three punching one, who is taking it in the middle
                {
                    queue.Add(new Spec { clip = hitClips[rnd.Next(hitClips.Length)], pos = c, dir = ax, speed = 0f, count = cnt });
                    for (int g = 0; g < 3; g++) { float a = g * 2.1f + (float)rnd.NextDouble() * 0.6f; var u = right * Mathf.Cos(a) + fwd * Mathf.Sin(a); queue.Add(new Spec { clip = punchClips[rnd.Next(punchClips.Length)], pos = c + u * 0.7f, dir = -u, speed = 0f, count = cnt }); }
                }
            }
        // people sitting on the benches, talking to each other
        if (sitClips != null && sitClips.Length > 0)
            foreach (var bs in benchSpots)
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    if (rnd.NextDouble() > 0.8) continue;
                    var seat = bs[0] + bs[1] * (0.4f * sd); float d = new Vector2(seat.x - origin.x, seat.z - origin.z).magnitude;
                    queue.Add(new Spec { clip = sitClips[sd > 0 ? 0 : sitClips.Length - 1], pos = seat, dir = bs[2], speed = 0f, count = d < 35f ? 450 : 300 });
                }
        queue.Sort((x, y) => QDist(y).CompareTo(QDist(x)));                                  // nearest last: they are made first
    }
    float QDist(Spec sp) => new Vector2(sp.pos.x - origin.x, sp.pos.z - origin.z).magnitude;
    void SpawnPeople()
    {
        if (queue.Count == 0) return;
        var sp = queue[queue.Count - 1]; queue.RemoveAt(queue.Count - 1);
        float d = new Vector2(sp.pos.x - origin.x, sp.pos.z - origin.z).magnitude;                // far people get fewer particles and update less often
        bool hero = sp.count >= 1000;
        var f = Fig(sp.clip, sp.pos, people, hero ? sp.count : Mathf.Max(sp.count > 0 && d > 45f ? 300 : 0, d < 25f ? 600 : d < 45f ? 360 : d < 70f ? 220 : 150), false, hero ? 1 : d < 25f ? 1 : d < 45f ? 2 : d < 70f ? 3 : 4, sp.sex - 1);
        peds.Add(new Ped { f = f, dir = sp.dir, speed = sp.speed, range = 40f, yaw = sp.yaw, init = sp.keepFacing }); assembling = true;
    }
    void MovePeds(float dt)
    {
        foreach (var p in peds)
        {
            if (p.f == null || p.f.Skeleton == null) continue;
            if (!p.init)
            {
                p.init = true; var sk = p.f.Skeleton; var hips = p.f.Bone(HumanBodyBones.Hips); if (hips == null) continue;
                Vector3 face = p.dir != Vector3.zero ? p.dir : Quaternion.Euler(0f, p.yaw, 0f) * Vector3.forward;
                if (p.hasTarget) { var toT = p.target - p.f.position; toT.y = 0f; face = Quaternion.AngleAxis(-90f, Vector3.up) * toT.normalized; if (keep && WomanDogScene.HasYaw) face = Quaternion.Euler(0f, WomanDogScene.PlacedYaw, 0f) * Vector3.forward; }       // author 2026-11-23: scene 2 is right, so with scene 2 on record she takes exactly its final turn; otherwise scene 2's rule (right hand towards the dog)
                sk.RotateAround(hips.position, Vector3.up, Vector3.SignedAngle(Vector3.ProjectOnPlane(sk.forward, Vector3.up), face, Vector3.up));      // turn about her hips so she stays where she is
            }
            if (p.speed <= 0f) continue;
            p.f.Skeleton.position += p.dir * (p.speed * dt); p.dist += p.speed * dt;
            if (p.dist > p.range) { p.dist = 0f; p.dir = -p.dir; p.f.Skeleton.rotation = Quaternion.LookRotation(p.dir); }                  // at the end of its stretch it turns round and walks back
        }
    }
    ParticleFigure Fig(AnimationClip clip, Vector3 pos, Color col, int count = 1500, bool keepColour = false, int every = 1, int sex = -1, bool matchScene2 = false)
    {
        var prefab = Resources.Load<GameObject>("Prefab/ParticleFigure");
        var go = Instantiate(prefab); go.transform.SetParent(root.transform, true);
        var f = go.GetComponent<ParticleFigure>();
        f.clip = clip; f.loop = true; f.color = keepColour || col == woman ? col : PersonColour(); f.particleCount = count; f.updateEvery = every; if (!matchScene2) { f.sizeMin = 0.03f; f.sizeMax = 0.07f; f.particleSize = 0.05f; }   // scene 2's figures (matchScene2) keep the prefab's own size and count, so nothing changes size at the hand-over;
        if (BodyLibrary.Available) BodyLibrary.Apply(f, sex < 0 ? Random.value < 0.5f : sex == 1);      // a real body surface (male or female) instead of tubes; heroes are fixed, the crowd random
        f.position = new Vector3(pos.x, 0f, pos.z); f.groundY = baseY + (root != null ? root.transform.position.y : 0f); f.brightness = 0f;
        f.gather = 0f; figs.Add(f); figDelay.Add(Mathf.Max(t + 0.2f, growDelay + 1f + Mathf.Sqrt((pos.x - origin.x) * (pos.x - origin.x) + (pos.z - origin.z) * (pos.z - origin.z)) / 125f * growSpread));
        return f;
    }

    void End()
    {
        foreach (var f in figs) if (f != null) Destroy(f.gameObject);
        if (WomanDogScene.HandOver != null) { Destroy(WomanDogScene.HandOver.gameObject); WomanDogScene.HandOver = null; }     // a lady handed over but never adopted
        adoptLady = false;
        figs.Clear(); fruit.Clear(); queue.Clear(); peds.Clear(); oldManFig = null; ladyFig = null; dog = null; ballR2 = null; bagRs2 = null;
        if (buildCo != null) { StopCoroutine(buildCo); buildCo = null; }
        if (root != null) Destroy(root);
        if (ringGo != null) { Destroy(ringGo); ringGo = null; }
        if (nearChanged && Camera.main != null) Camera.main.nearClipPlane = prevNear; nearChanged = false;
        if (solidMat != null) Destroy(solidMat);
        if (crowdMat != null) Destroy(crowdMat);
        if (babyMat != null) Destroy(babyMat);
        if (policeMat != null) Destroy(policeMat);
        root = null; lines = null; ring = null; womanFig = null; phase = Phase.Idle; t = 0f;
        Log("street ends");
    }

    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} street {msg}");
}
