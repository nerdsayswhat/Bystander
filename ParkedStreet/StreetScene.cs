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
public class StreetScene : MonoBehaviour
{
    public AnimationClip idle, fallen, stabbed, kneel, scaredWalk;     // standing idle, lying in pain, stabbed on the ground, kneeling, the woman's scared walk
    public float viewHeight = 10f;                          // m: the street lies this far below the player's floor
    public float riseTime = 6f;                             // seconds the street takes to light up ("darkness starts lifting")
    public int bystanders = 140;                            // still columns (36 particles each)
    public float lineBright = 0.9f;
    public Color people = new Color(0.004f, 0.05f, 0.16f);  // x emission 10: the same azure as the particle figures
    public Color helper = new Color(0.004f, 0.05f, 0.16f);
    public Color woman = new Color(0.06f, 0.012f, 0.16f);   // the same purple as the woman of scene 2

    enum Phase { Idle, Shown }
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
        bool want = seq != null && head != null && seq.Index >= 0 && (seq.steps[seq.Index].name.StartsWith("3") || seq.steps[seq.Index].name.StartsWith("6"));
        if (want && phase == Phase.Idle && !seq.Busy) Begin();
        if (!want && phase != Phase.Idle) { End(); return; }
        if (phase == Phase.Idle) return;

        t += dt;
        rise = Mathf.Clamp01(t / Mathf.Max(riseTime, 0.1f));                              // the darkness lifts
        float k = rise * rise * (3f - 2f * rise);
        mpb.SetColor(BaseColor, Color.white * lineBright * k); lines.SetPropertyBlock(mpb);
        if (ring != null) { mpb.SetColor(BaseColor, Color.white * 2.5f * k); ring.SetPropertyBlock(mpb); }
        crowdMat.SetColor(BaseColor, Color.white * 10f * k); babyMat.SetColor(BaseColor, Color.white * 10f * k);
        foreach (var f in figs) if (f != null) f.brightness = k;
        foreach (var r in fruit) { mpb.SetColor(BaseColor, new Color(0.9f * 1.6f, 0.3f * 1.6f, 0.04f * 1.6f, 1f) * k); r.SetPropertyBlock(mpb); }
        if (womanFig != null && womanFig.Skeleton != null)                                 // the scared woman crosses the street, in place and moved along
        {
            womanFig.Skeleton.position += right * 0.7f * dt;                              // 80 m in 115 s: she is crossing the whole street
        }
    }

    Vector3 P(float x, float y, float z) => new Vector3(origin.x, baseY, origin.z) + right * x + fwd * z + Vector3.up * y;

    void Begin()
    {
        if (idle == null || fallen == null)
        {
            if (!warned) { warned = true; Debug.LogWarning("StreetScene: the clips are not assigned (use Bystander > Create Scene 2)"); }
            return;
        }
        scene6 = seq.steps[seq.Index].name.StartsWith("6");
        t = 0f; rise = 0f;
        fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized; right = Vector3.Cross(Vector3.up, fwd);
        origin = new Vector3(head.position.x, 0f, head.position.z); baseY = -viewHeight;
        lv.Clear(); li.Clear(); mpb = new MaterialPropertyBlock(); figs.Clear(); fruit.Clear(); womanFig = null; walkX = 0f;
        root = new GameObject("Street");
        var rnd = new System.Random(21);                                                    // the same street in both scenes

        BuildCity(rnd);
        BuildProps(rnd);
        BuildFeet(rnd);
        var mesh = new Mesh { name = "StreetLines", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(lv); mesh.SetIndices(li, MeshTopology.Lines, 0); mesh.bounds = new Bounds(new Vector3(origin.x, baseY, origin.z), new Vector3(400f, 120f, 400f));
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        lines = root.AddComponent<MeshRenderer>(); lines.sharedMaterial = Resources.Load<Material>("OrangeSphere");
        lines.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lines.receiveShadows = false;

        BuildCrowd(rnd);
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
    void Box(float cx, float cz, float w, float d, float h, float floor)
    {
        Rect(P(cx, 0f, cz), right, fwd, w, d);                       // base
        Rect(P(cx, h, cz), right, fwd, w, d);                        // roof
        for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
            L(P(cx + sx * w / 2, 0f, cz + sz * d / 2), P(cx + sx * w / 2, h, cz + sz * d / 2));
        for (float y = floor; y < h - 0.1f; y += floor) Rect(P(cx, y, cz), right, fwd, w, d);   // floor lines
    }

    // roads every 28 m (8 m wide, the player above the central crossing), blocks of up to four buildings, kerbs, a dashed centre line, a crossing
    void BuildCity(System.Random rnd)
    {
        for (int bi = -5; bi <= 4; bi++)
            for (int bj = -5; bj <= 4; bj++)
            {
                float cx = 28f * bi + 14f, cz = 28f * bj + 14f, dist = Mathf.Sqrt(cx * cx + cz * cz);
                if (dist > 125f) continue;
                Rect(P(cx, 0f, cz), right, fwd, 20f, 20f);                                    // the kerb round the block
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

    // a bicycle on its side, a stroller, litter on the street
    void BuildProps(System.Random rnd)
    {
        // bicycle lying on the ground at (-3.5, 6): two wheels flat on the ground, a frame between them
        var bc = P(-3.5f, 0.05f, 6f);
        Circle(bc - fwd * 0.55f, right, fwd, 0.33f, 16); Circle(bc + fwd * 0.55f, right, fwd, 0.33f, 16);
        L(bc - fwd * 0.55f, bc + fwd * 0.1f); L(bc + fwd * 0.1f, bc + fwd * 0.55f); L(bc - fwd * 0.55f, bc + fwd * 0.55f + right * 0.1f);
        L(bc + fwd * 0.1f + right * 0.05f, bc + fwd * 0.4f + right * 0.45f);                  // handlebar
        // stroller at (3, -9): a frame, a hood, two wheels, a handle
        var sc = P(3f, 0f, -9f);
        Rect(sc + Vector3.up * 0.45f, right, fwd, 0.5f, 0.85f); Rect(sc + Vector3.up * 0.75f, right, fwd, 0.5f, 0.85f);
        for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2) L(sc + right * sx * 0.25f + fwd * sz * 0.425f + Vector3.up * 0.45f, sc + right * sx * 0.25f + fwd * sz * 0.425f + Vector3.up * 0.75f);
        Circle(sc + right * 0.3f - fwd * 0.3f + Vector3.up * 0.15f, fwd, Vector3.up, 0.15f, 10); Circle(sc - right * 0.3f - fwd * 0.3f + Vector3.up * 0.15f, fwd, Vector3.up, 0.15f, 10);
        L(sc - fwd * 0.425f + Vector3.up * 0.75f, sc - fwd * 0.8f + Vector3.up * 1.05f);
        // litter on the street: short crossed strokes (more of it when the player did not bin the can, in scene 6)
        int n = scene6 && !seq.binnedCan ? 120 : scene6 ? 50 : 70;
        for (int i = 0; i < n; i++)
        {
            float a = (float)(rnd.NextDouble() - 0.5) * 100f, b = (float)(rnd.NextDouble() - 0.5) * 6.5f;
            var p = rnd.NextDouble() < 0.5 ? P(a, 0.02f, 28f * Mathf.Round((float)(rnd.NextDouble() - 0.5) * 6f) + b) : P(28f * Mathf.Round((float)(rnd.NextDouble() - 0.5) * 6f) + b, 0.02f, a);
            var d = new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f).normalized * 0.18f;
            L(p - d, p + d); L(p + new Vector3(-d.z, 0f, d.x) * 0.6f, p - new Vector3(-d.z, 0f, d.x) * 0.6f);
        }
    }

    // at the player's feet (floor height): the squished orange and fruit, more trash, or the clean lit circle
    void BuildFeet(System.Random rnd)
    {
        bool acted = scene6 && (seq.gaveOrange || seq.binnedCan || seq.calmedDog);
        bool oranges = !scene6 || !seq.gaveOrange, trash = !scene6 || !seq.binnedCan;
        var floor = new Vector3(origin.x, 0.02f, origin.z);
        if (oranges)
        {
            var mat = Resources.Load<Material>("OrangeSphere");
            for (int i = 0; i < 6; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(s.GetComponent<Collider>());
                float ang = (30f + i * 55f) * Mathf.Deg2Rad, r = 0.45f + 0.12f * (i % 3);
                s.transform.SetParent(root.transform, true);
                s.transform.position = floor + (right * Mathf.Cos(ang) + fwd * Mathf.Sin(ang)) * r;
                s.transform.localScale = new Vector3(0.12f + 0.04f * (i % 2), 0.025f, 0.12f + 0.03f * (i % 3));          // squashed flat
                var rd = s.GetComponent<Renderer>(); rd.sharedMaterial = mat; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rd.receiveShadows = false;
                if (i < 2 && !scene6) s.AddComponent<BreakTarget>();                                                  // reaching for these still breaks the chains in scene 3
                fruit.Add(rd);
            }
        }
        if (trash)
            for (int i = 0; i < 14; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.35f + (float)rnd.NextDouble() * 0.7f;
                var p = floor + (right * Mathf.Cos(ang) + fwd * Mathf.Sin(ang)) * r;
                var d = new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f).normalized * 0.07f;
                L(p - d, p + d); L(p + new Vector3(-d.z, 0f, d.x) * 0.6f, p - new Vector3(-d.z, 0f, d.x) * 0.6f);
            }
        if (acted)                                                                                                       // the clean lit circle: rings round the feet, brighter than the street
        {
            var rv = new List<Vector3>(); var ri = new List<int>();
            foreach (float r in new[] { 0.5f, 0.85f, 1.2f })
                for (int i = 0; i < 48; i++)
                {
                    float a0 = i * Mathf.PI * 2f / 48f, a1 = (i + 1) * Mathf.PI * 2f / 48f;
                    ri.Add(rv.Count); rv.Add(floor + (right * Mathf.Cos(a0) + fwd * Mathf.Sin(a0)) * r); ri.Add(rv.Count); rv.Add(floor + (right * Mathf.Cos(a1) + fwd * Mathf.Sin(a1)) * r);
                }
            var go = new GameObject("CleanCircle"); go.transform.SetParent(root.transform, false);
            var m = new Mesh { name = "CleanCircle" }; m.SetVertices(rv); m.SetIndices(ri, MeshTopology.Lines, 0); m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            ring = go.AddComponent<MeshRenderer>(); ring.sharedMaterial = Resources.Load<Material>("OrangeSphere");
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ring.receiveShadows = false;
        }
    }

    // the bystanders: still columns (a head and a body of 36 particles) in clusters on the roads, and the baby in the stroller
    void BuildCrowd(System.Random rnd)
    {
        var pp = new List<Vector3>(); var pc = new List<Color>(); var ps = new List<float>();
        int made = 0;
        while (made < bystanders)
        {
            bool alongX = rnd.NextDouble() < 0.5;
            float road = 28f * Mathf.Round((float)(rnd.NextDouble() - 0.5) * 6f), along = (float)(rnd.NextDouble() - 0.5) * 110f, lateral = (float)(rnd.NextDouble() - 0.5) * 6.5f;
            int groupSize = 3 + rnd.Next(5);
            for (int g = 0; g < groupSize && made < bystanders; g++, made++)
            {
                float a = along + g * 0.9f, l = lateral + (float)(rnd.NextDouble() - 0.5) * 1.6f;
                var foot = alongX ? P(a, 0f, road + l) : P(road + l, 0f, a);
                var col = people * Random.Range(0.8f, 1.15f); col.a = 1f;
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
        for (int j = 0; j < 22; j++) { bp.Add(P(3f, 0.6f, -9f) + (Vector3)Random.insideUnitSphere * 0.13f); bc.Add(new Color(0.08f, 0.04f, 0.14f, 1f)); bs.Add(Random.Range(0.025f, 0.05f)); }
        babyR = MakeSystem("Baby", bp, bc, bs, out babyMat);
    }

    ParticleSystemRenderer MakeSystem(string name, List<Vector3> pos, List<Color> col, List<float> size, out Material mat)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = pos.Count;
        var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Resources.Load<Material>("ChainParticle");
        mat = r.material; mat.SetTexture("_BaseMap", ParticleFigure.DiscTexture());
        var parts = new ParticleSystem.Particle[pos.Count];
        for (int i = 0; i < parts.Length; i++) { parts[i].position = pos[i]; parts[i].startSize = size[i]; parts[i].startLifetime = 1000f; parts[i].remainingLifetime = 1000f; parts[i].startColor = col[i]; }
        ps.SetParticles(parts, parts.Length); ps.Play();
        return r;
    }

    // the real figures: a fallen person and a kneeling helper, a stabbed person and a standing helper, and (scene 6, dog not calmed) the scared woman walking
    void BuildFigures()
    {
        Fig(fallen, P(2.5f, 0f, 12f), people);
        if (kneel != null) Fig(kneel, P(3.9f, 0f, 12.2f), helper);
        if (stabbed != null) Fig(stabbed, P(-3f, 0f, 22f), people);
        Fig(idle, P(-1.7f, 0f, 21.4f), helper);
        if (scene6 && !seq.calmedDog && scaredWalk != null)
        {
            womanFig = Fig(scaredWalk, P(-40f, 0f, -14f), woman);
        }
    }

    ParticleFigure Fig(AnimationClip clip, Vector3 pos, Color col)
    {
        var prefab = Resources.Load<GameObject>("Prefab/ParticleFigure");
        var go = Instantiate(prefab); go.transform.SetParent(root.transform, true);
        var f = go.GetComponent<ParticleFigure>();
        f.clip = clip; f.loop = true; f.color = col; f.particleCount = 900; f.sizeMin = 0.03f; f.sizeMax = 0.07f; f.particleSize = 0.05f;
        f.position = new Vector3(pos.x, 0f, pos.z); f.groundY = baseY; f.brightness = 0f;
        figs.Add(f);
        return f;
    }

    void End()
    {
        foreach (var f in figs) if (f != null) Destroy(f.gameObject);
        figs.Clear(); fruit.Clear();
        if (root != null) Destroy(root);
        if (crowdMat != null) Destroy(crowdMat);
        if (babyMat != null) Destroy(babyMat);
        root = null; lines = null; ring = null; womanFig = null; phase = Phase.Idle; t = 0f;
        Log("street ends");
    }

    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} street {msg}");
}
