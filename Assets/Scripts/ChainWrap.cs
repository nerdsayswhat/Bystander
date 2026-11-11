using System.Collections;
using Oculus.Interaction.Input;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Chains binding the player: a fixed helix around the torso plus a chain from a wrist cuff on each
// hand to a belt anchor. The chain has a limited reach: past it the rendered hand is held at the
// limit (SyntheticHand wrist lock) while the real hand keeps pulling, and the chain strains.
// The chain is drawn as particles: every link (and cuff) is a ring of points; the solid link meshes stay
// as an invisible skeleton (renderSolid shows them for comparison).
// Break: pull a hand past the limit towards a BreakTarget for holdFor and the particles burst apart, the
// hands are freed and onBroken fires. Release() disperses them without the event (scene 5).
// Add to an empty GameObject in the scene (rig at world origin).
public class ChainWrap : MonoBehaviour
{
    public Material linkMaterial;                  // flat emissive; falls back to Unlit/Color
    public Color linkColor = new Color(0.34591186f, 0.34591186f, 0.34591186f), strainColor = new Color(0.67295593f, 0.67295593f, 0.67295593f);
    public float torsoRadius = 0.2f, torsoHeight = 0.6f, torsoTurns = 3f, linkSpacing = 0.07f;
    public Vector3 linkSize = new Vector3(0.05f, 0.035f, 0.1f);   // chunky: thin links read as noodles
    public int handLinks = 14;                                    // max; extra links hide when the chain is short
    public Vector3 beltAnchor = new Vector3(0.28f, 0.6f, 0.12f);  // cuff chain end: side, height below head, forward (mirrored for left)
    public float sag = 0.06f;                                     // near taut: a slack chain reads as a rope
    public float reach = 0.35f;                                  // max anchor -> hand distance before the hand is held
    public float stretchLook = 2f;                                // how much the links themselves elongate and spread apart while stretched (1 = just the extra reach)
    public float stretch = 0.12f;                                 // extra reach (m) the chain gives, only while the hand is aimed at a BreakTarget
    public float pullRange = 0.25f;                               // extra real-hand pull (m) for full strain
    public float tremble = 0.002f;                                // peak shake (m) when the hand is pulled out in any OTHER direction; grows with the pull
    public float trembleTarget = 0.01f;                           // peak shake (m) when the pull is aimed at a BreakTarget: much more violent
    public float tremorStart = 0.75f;                             // fraction of reach where shaking and colour start
    public float awayStart = 0.3f, awayFull = 0.45f;              // hand distance from the body axis (m) where resistance fades in; nothing inside awayStart
    public float bodyBack = 0.08f;                                // chest sits this far behind the head
    public float follow = 2f;                                     // how fast the wrap re-centres on the player (1/s)

    public bool armed = true;                                     // the sequencer switches this off outside the break window
    public float holdFor = 1.0f;                                  // seconds of deliberate grab (fist while pulling) to break
    public float reachDist2 = 0.45f;                                // m: the real hand must be this close to a BreakTarget to count as reaching (a hand hanging at the hip is 0.4 m or more above a floor orange; bending down to one gets within 0.2 m)
    public float reachCone = 40f;                                // degrees: the pull must point this close to a BreakTarget
    public bool requireFist = false;                              // also demand a closed hand (see fistDistance)
    public float fistDistance = 0.12f;                            // mean fingertip distance from the wrist (m) below which the hand counts as closed
    public Material particleMaterial;                             // falls back to Resources/ChainParticle
    public float particleSize = 0.014f;
    public bool bloom = true;                                     // real glow: a cheap quarter-resolution bloom (costs frame time on Quest, check the FPS counter)
    public float bloomIntensity = 2f, bloomThreshold = 0.4f;      // low: every bright object glows, the resting chain included
    public float emissionRest = 1.5f, emissionPull = 10f;         // self-lit brightness: normal at rest, very bright glowing at full pull (above ~3 the greys clip to white)
    public int ringPoints = 30;                                   // particles per link: dense enough to read as a solid chain
    public bool renderSolid = false;                              // draw the solid link meshes too (for comparing)
    public float minPull = 0.12f;                                // how far past the limit (0..1) the hand must pull for the grab to count
    public float breakDuration = 1.2f, scatterSpeed = 3.5f;       // violent: the particles burst outwards and shrink over this long
    public UnityEvent onBroken;                                   // the sequencer listens: jump to the cards

    public float LeftStrain { get; private set; }                 // 0..1
    public float RightStrain { get; private set; }
    public bool Broken { get; private set; }

    bool hidden; ParticleSystemRenderer prend;
    // the opening (warning, tutorial) has no chains: hidden, the chain is not drawn, does not hold the hands and does not follow
    public bool Hidden
    {
        get => hidden;
        set
        {
            if (hidden && !value && Camera.main != null) { body = BodyTarget(Camera.main.transform); yaw = Camera.main.transform.eulerAngles.y; }   // reappear centred on the player, facing the way they face
            hidden = value;
            if (prend != null) prend.enabled = !hidden;
        }
    }
    // read-only state for ExperienceLog (what the chain is doing, per hand)
    public float LeftResist => left.resist;
    public float RightResist => right.resist;
    public float LeftAim => left.aim;
    public float RightAim => right.aim;
    public float LeftHold => left.hold;
    public float RightHold => right.hold;
    public float LeftShake => left.shake;
    public float RightShake => right.shake;

    class Side { public Transform hand, cuff; public Transform[] links; public Vector3 anchor; public SyntheticHand synth; public bool left; public float resist, hold, aim, shake, elong = 1f, dist, lim, fwd, ang; public bool fist; }
    static readonly HandJointId[] Tips = { HandJointId.HandIndexTip, HandJointId.HandMiddleTip, HandJointId.HandRingTip, HandJointId.HandPinkyTip };
    struct Src { public Transform t; public int n, group; public float y; public bool thick; }   // a ring of particles around a link or cuff
    Material pmat; Src[] srcs; ParticleSystem ps; ParticleSystem.Particle[] parts; Vector3[] vel; float[] size; float brokenAt;
    Side left = new Side { left = true }, right = new Side();
    MaterialPropertyBlock mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");   // URP Unlit colour (Assets/Materials/ChainLink.mat)
    Transform[] torso; Renderer[] torsoR; Vector3[] torsoOff;   // torso links and their offsets from the body centre
    Vector3 body; float topY, yaw;                               // smoothed body centre (XZ), head height at start, and smoothed body heading (degrees)

    IEnumerator Start()
    {
        // no locomotion of any kind (teleport, turn, slide, step), hands or controllers: the player stays put
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb is ILocomotionEventBroadcaster || mb is ILocomotionEventHandler) mb.enabled = false;

        var head = Camera.main.transform;
        // wait for tracking so head height is real (ponytail: 3 s cap, then use whatever we have)
        for (float t = 0; head.position.y < 0.3f && t < 3f; t += Time.deltaTime) yield return null;

        left.hand = GameObject.Find("LeftHandAnchor")?.transform;
        right.hand = GameObject.Find("RightHandAnchor")?.transform;
        foreach (var h in FindObjectsByType<SyntheticHand>(FindObjectsSortMode.None))
            (h.Handedness == Handedness.Left ? left : right).synth = h;
        if (left.synth == null || right.synth == null)
            Debug.LogWarning("ChainWrap: no SyntheticHand found; the rendered hand will not be held at the chain limit");

        if (linkMaterial == null)
        {
            linkMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = linkColor };
            // fallback only; Assets/Materials/ChainLink.mat is assigned in the scene so the shader ships in builds
        }
        mpb = new MaterialPropertyBlock();

        topY = head.position.y;
        body = BodyTarget(head);
        yaw = head.eulerAngles.y;
        BuildTorso();
        foreach (var s in new[] { left, right })
        {
            s.links = new Transform[handLinks];
            for (int i = 0; i < handLinks; i++) s.links[i] = MakeLink(i);
            s.cuff = MakeCuff();
        }
        BuildParticles();
    }

    void BuildParticles()
    {
        var list = new System.Collections.Generic.List<Src>();
        foreach (var t in torso) list.Add(new Src { t = t, n = ringPoints, group = 0, thick = true });
        foreach (var s in new[] { left, right })
        {
            int g = s.left ? 1 : 2;
            foreach (var t in s.links) list.Add(new Src { t = t, n = ringPoints, group = g, thick = true });
            foreach (float y in new[] { -0.5f, 0f, 0.5f })                       // three rings: the cuff is a band
                list.Add(new Src { t = s.cuff, n = 20, group = g, y = y });
        }
        srcs = list.ToArray();
        int total = 0;
        foreach (var r in srcs) total += r.n;
        parts = new ParticleSystem.Particle[total]; vel = new Vector3[total]; size = new float[total];

        var go = new GameObject("ChainParticles");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = total;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = particleMaterial != null ? particleMaterial : Resources.Load<Material>("ChainParticle");
        prend = rend; prend.enabled = !hidden;
        pmat = rend.material;                                         // instance: the shared asset stays untouched
        pmat.SetTexture("_BaseMap", DiscTexture());                   // round and flat, no shading or shadow side; alpha-clipped so it stays opaque
        ps.Play();
        for (int i = 0; i < parts.Length; i++) { parts[i].startLifetime = 1000f; parts[i].remainingLifetime = 1000f; }
        if (!renderSolid)
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
        if (bloom) SetupGlow();
    }

    // post-processing is off in the project, so switch it on for the camera and add a global bloom volume at runtime
    void SetupGlow()
    {
        Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var vol = new GameObject("ChainGlow").AddComponent<Volume>();
        vol.transform.SetParent(transform, false);
        vol.isGlobal = true; vol.priority = 100;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var b = profile.Add<Bloom>(true);
        b.threshold.Override(bloomThreshold);
        b.intensity.Override(bloomIntensity);
        b.scatter.Override(0.6f);
        b.downscale.Override(BloomDownscaleMode.Quarter);
        vol.sharedProfile = profile;
    }

    // a flat white disc: the colour channels stay 1 everywhere (no shadow side), only the alpha makes it round; the edge is cut by the material
    static Texture2D DiscTexture()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01((1f - r) * 8f)));
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // every frame: a ring of points around each link, coloured by how hard its chain is being pulled
    void FillParticles()
    {
        float rt = Mathf.Max(left.resist, right.resist);
        pmat.SetColor(BaseColor, Color.white * Mathf.Lerp(emissionRest, emissionPull, rt));   // the glow builds with the pull
        var ct = Color.Lerp(linkColor, strainColor, rt);
        var cl = Color.Lerp(linkColor, strainColor, left.resist);
        var cr = Color.Lerp(linkColor, strainColor, right.resist);
        int k = 0;
        foreach (var r in srcs)
        {
            bool on = r.t.gameObject.activeSelf;
            var c = r.group == 0 ? ct : r.group == 1 ? cl : cr;
            float el = r.group == 0 ? 1f : r.group == 1 ? left.elong : right.elong;   // stretch along the link's long axis
            for (int j = 0; j < r.n; j++, k++)
            {
                float a = 2f * Mathf.PI * j / r.n;
                float y = r.thick ? (j % 2 == 0 ? 0.25f : -0.25f) : r.y;   // alternate above and below so a link has thickness
                parts[k].position = r.t.TransformPoint(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f * (r.thick ? el : 1f)));
                parts[k].startColor = c;
                size[k] = on ? particleSize : 0f;
                parts[k].startSize = size[k];
            }
        }
        ps.SetParticles(parts, parts.Length);
    }

    // chest centre: the head XZ pulled back along the facing direction (the head sits in front of the spine)
    Vector3 BodyTarget(Transform head)
    {
        var f = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        var p = head.position - f * bodyBack;
        return new Vector3(p.x, 0f, p.z);
    }

    void BuildTorso()
    {
        // chest-height spiral around the body centre; offsets are relative so the whole wrap can follow the player
        float top = topY - 0.25f, circ = 2f * Mathf.PI * torsoRadius * torsoTurns;
        int n = Mathf.CeilToInt(circ / linkSpacing);
        torso = new Transform[n]; torsoR = new Renderer[n]; torsoOff = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float u = (float)i / n, a = u * torsoTurns * 2f * Mathf.PI;
            var pos = new Vector3(Mathf.Cos(a) * torsoRadius, top - u * torsoHeight, Mathf.Sin(a) * torsoRadius);
            var tangent = new Vector3(-Mathf.Sin(a), -torsoHeight / circ, Mathf.Cos(a));
            torso[i] = MakeLink(i); torsoR[i] = torso[i].GetComponent<Renderer>(); torsoOff[i] = pos;
            PlaceLink(torso[i], body + pos, tangent, i);
        }
    }

    Transform MakeLink(int i)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);   // ellipsoid reads as a link
        Destroy(g.GetComponent<Collider>());                        // must not block the grab cube
        g.GetComponent<Renderer>().sharedMaterial = linkMaterial;
        g.transform.SetParent(transform, false);
        g.transform.localScale = i % 2 == 0 ? linkSize : new Vector3(linkSize.y, linkSize.x, linkSize.z);
        return g.transform;
    }

    // thick band at the wrist; follows the rendered (held) hand, not the real one
    Transform MakeCuff()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = linkMaterial;
        g.transform.SetParent(transform, false);
        g.transform.localScale = new Vector3(0.09f, 0.03f, 0.09f);
        return g.transform;
    }

    static void PlaceLink(Transform link, Vector3 pos, Vector3 tangent, int i)
    {
        link.position = pos;
        link.rotation = Quaternion.LookRotation(tangent.normalized) * Quaternion.Euler(0, 0, i % 2 == 0 ? 0 : 90);
    }

    void Update()
    {
        if (left.links == null) return;
        if (Broken) { Scatter(); return; }
        if (hidden)
        {
            foreach (var s in new[] { left, right }) if (s.synth != null) s.synth.FreeWrist(SyntheticHand.WristLockMode.Position);
            LeftStrain = RightStrain = left.resist = right.resist = left.hold = right.hold = 0f;
            return;
        }
        // the wrap and belt anchors drift after the player, slowly, so they stay centred but still hold the hands
        body = Vector3.Lerp(body, BodyTarget(Camera.main.transform), 1f - Mathf.Exp(-follow * Time.deltaTime));
        yaw = Mathf.LerpAngle(yaw, Camera.main.transform.eulerAngles.y, 1f - Mathf.Exp(-follow * Time.deltaTime));   // the belt turns with the player: left stays on the player's left whichever way they face
        var turn = Quaternion.Euler(0f, yaw, 0f);
        left.anchor = body + turn * new Vector3(-beltAnchor.x, 0f, beltAnchor.z) + Vector3.up * (topY - beltAnchor.y);
        right.anchor = body + turn * new Vector3(beltAnchor.x, 0f, beltAnchor.z) + Vector3.up * (topY - beltAnchor.y);
        LeftStrain = UpdateSide(left);
        RightStrain = UpdateSide(right);
        if (left.hold >= holdFor || right.hold >= holdFor) { Break(); return; }   // return: the rest of Update would refill the particles from hidden links (size 0)
        float r = Mathf.Max(left.resist, right.resist);   // the whole chain lightens when either hand pulls
        float tr = Mathf.Max(left.shake, right.shake);   // and shakes with the harder-shaking hand
        mpb.SetColor(BaseColor, Color.Lerp(linkColor, strainColor, r));
        for (int i = 0; i < torso.Length; i++)
        {
            torso[i].position = body + torsoOff[i] + Random.insideUnitSphere * tr;
            torsoR[i].SetPropertyBlock(mpb);
        }
        FillParticles();
    }

    // distance (m) from p to the closest BreakTarget
    static float NearestTarget(Vector3 p)
    {
        float m = 99f;
        foreach (var t in BreakTarget.All) m = Mathf.Min(m, Vector3.Distance(p, t.transform.position));
        return m;
    }

    // smallest angle (degrees) between the pull (dir runs from the belt anchor out to the real hand) and the direction to a BreakTarget
    float MinAngle(Vector3 from, Vector3 dir)
    {
        float m = 180f;
        foreach (var t in BreakTarget.All) m = Mathf.Min(m, Vector3.Angle(dir, t.transform.position - from));
        return m;
    }

    // one line of numbers per hand for the development log: why a hand does or does not count as reaching
    public string Describe(bool leftHand)
    {
        var s = leftHand ? left : right;
        return $"dist {s.dist:0.00} limit {s.lim:0.00} strain {(s.dist > s.lim ? (s.dist - s.lim) / pullRange : 0f):0.00} near {s.fwd:0.00} angle {s.ang:0} fist {(s.fist ? 1 : 0)} aim {s.aim:0.0} hold {s.hold:0.00} anchor ({s.anchor.x:0.00},{s.anchor.y:0.00},{s.anchor.z:0.00}) hand ({(s.hand != null ? s.hand.position.x : 0f):0.00},{(s.hand != null ? s.hand.position.y : 0f):0.00},{(s.hand != null ? s.hand.position.z : 0f):0.00})";
    }

    public static bool IsFist(SyntheticHand h, float limit = 0.12f)   // also used by the tutorial's grab
    {
        if (h == null || !h.IsConnected) return false;
        float sum = 0f; int n = 0;
        foreach (var id in Tips)
            if (h.GetJointPoseFromWrist(id, out var pose)) { sum += pose.position.magnitude; n++; }
        return n == Tips.Length && sum / n < limit;
    }

    public void Release() => Break(false);   // chains gone without the event, e.g. entering scene 5

    public void Break(bool fireEvent = true)
    {
        if (Broken || left.links == null) return;
        Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} chain break snapshot yaw {yaw:0} body ({body.x:0.00},{body.y:0.00},{body.z:0.00}) | L {Describe(true)} | R {Describe(false)}");
        Broken = true; brokenAt = Time.time;
        Debug.Log($"ChainWrap: chain broken at {Time.time:0.00}s (fireEvent {fireEvent})");   // lines this up with a trace in logcat
        foreach (var s in new[] { left, right })
            if (s.synth != null) s.synth.FreeWrist(SyntheticHand.WristLockMode.Position);   // hands are free from now on
        FillParticles();
        for (int i = 0; i < parts.Length; i++)
        {
            var away = parts[i].position - body; away.y = 0f;
            vel[i] = (away.normalized + Vector3.up * 0.5f + Random.insideUnitSphere * 0.8f).normalized * (scatterSpeed * Random.Range(0.5f, 1.5f));
        }
        foreach (var t in torso) t.gameObject.SetActive(false);   // hide the solid skeleton (only visible with renderSolid)
        foreach (var s in new[] { left, right }) { foreach (var t in s.links) t.gameObject.SetActive(false); s.cuff.gameObject.SetActive(false); }
        if (fireEvent) onBroken.Invoke();
    }

    void Scatter()
    {
        float k = (Time.time - brokenAt) / breakDuration, dt = Time.deltaTime;
        for (int i = 0; i < parts.Length; i++)
        {
            vel[i] *= 1f - 1.5f * dt;                              // drag: a burst, then a drift
            vel[i] += Physics.gravity * 0.2f * dt;
            parts[i].position += vel[i] * dt;
            parts[i].startSize = size[i] * Mathf.Clamp01(1f - k);
        }
        ps.SetParticles(parts, parts.Length);
        if (k >= 1f) { ps.Clear(); enabled = false; }
    }

    // returns strain 0..1. Chain is a quadratic bezier anchor -> hand with a small sag; links follow every frame.
    float UpdateSide(Side s)
    {
        if (s.hand == null) return 0f;
        var real = s.hand.position;
        var d = real - s.anchor;
        float dist = d.magnitude, strain = 0f;
        // a reach is out in front: a hand down at the hip or behind the back points at the floor oranges too, but is not reaching
        s.fwd = NearestTarget(real); s.ang = MinAngle(s.anchor, d);
        bool aimed = armed && s.fwd < reachDist2 && s.ang < reachCone;
        s.aim = Mathf.MoveTowards(s.aim, aimed ? 1f : 0f, Time.deltaTime * 6f);   // blend (shake, stretch) so nothing jumps
        float limit = reach + stretch * s.aim; s.dist = dist; s.lim = limit; s.fist = IsFist(s.synth, fistDistance);                       // the chain stretches a little when, and only when, reaching for the object
        var end = real;
        if (dist > limit)
        {
            end = s.anchor + d / dist * limit;                       // rendered hand stops at the chain limit
            strain = Mathf.Clamp01((dist - limit) / pullRange);
            // ponytail: assumes the OVR hand anchor ~ the wrist; tune reach if the held hand looks off
            if (s.synth != null)
                s.synth.LockWristPosition(s.synth.TrackingToWorldTransformer.ToTrackingPose(new Pose(end, Quaternion.identity)).position);
        }
        else if (s.synth != null) s.synth.FreeWrist(SyntheticHand.WristLockMode.Position);

        // resist: pull on the chain, but only while the hand is out away from the body, so bringing the wrists together stays quiet
        var off = real - body; off.y = 0f;
        s.resist = Mathf.InverseLerp(limit * tremorStart, limit + pullRange, dist) * Mathf.InverseLerp(awayStart, awayFull, off.magnitude);
        // deliberate reach: pulling past the limit towards a BreakTarget; the chain strains harder the longer it is held
        s.hold = aimed && strain >= minPull && (!requireFist || IsFist(s.synth, fistDistance)) ? s.hold + Time.deltaTime : 0f;
        s.resist = Mathf.Max(s.resist, Mathf.Clamp01(s.hold / holdFor));
        mpb.SetColor(BaseColor, Color.Lerp(linkColor, strainColor, s.resist));
        s.shake = Mathf.Lerp(tremble, trembleTarget, s.aim) * s.resist;
        var mid = (s.anchor + end) * 0.5f + Vector3.down * sag * (1f - strain);
        float len = Mathf.Max((end - s.anchor).magnitude, 0.01f);   // ponytail: straight-line length approximates the arc
        float stretched = Mathf.Max(1f, len / reach);                // 1 at normal reach, above 1 while stretched
        s.elong = 1f + (stretched - 1f) * stretchLook;               // each link elongates along the chain
        for (int i = 0; i < s.links.Length; i++)
        {
            float t = (i + 0.5f) * linkSpacing * stretched / len, u = 1f - t;   // same number of links, spread further apart
            s.links[i].gameObject.SetActive(t <= 1f);
            if (t > 1f) continue;
            var pos = u * u * s.anchor + 2f * u * t * mid + t * t * end;
            var tangent = 2f * u * (mid - s.anchor) + 2f * t * (end - mid);
            PlaceLink(s.links[i], pos + Random.insideUnitSphere * s.shake, tangent, i);
            s.links[i].GetComponent<Renderer>().SetPropertyBlock(mpb);
        }
        var dir = end - s.anchor;
        s.cuff.position = end + Random.insideUnitSphere * s.shake;
        if (dir.sqrMagnitude > 1e-4f) s.cuff.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
        s.cuff.GetComponent<Renderer>().SetPropertyBlock(mpb);
        return strain;
    }
}
