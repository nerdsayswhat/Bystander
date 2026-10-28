using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using UnityEngine;

// Scene 2 / Beat 1 of the Event List: a faint sound near you, someone fell. You look near your feet: a faceless old man (particles) lies
// on the ground at your left, his bag of oranges spilled, and one orange has rolled to your feet. "Don't bother."
// The void is lit only where the player looks: the old man, the bag and each orange glow while the head points at them and go dark when
// it does not. Oranges are orange spheres lit like the particles (flat, above the bloom threshold). Every orange is a BreakTarget:
// reaching for one is what strains and breaks the chains (the sequencer's break window is open in this step).
// After the chains break the scene does not end: the hands are free, an orange can be picked up (with the Meta Interaction SDK hand grab), carried to
// the old man and let go within reach of him, and he takes it (oldman_receive_low / mid / high by the height of the orange). A few
// seconds later the film goes to the cards. If nothing is given the film goes to the cards freedTimeout seconds after the break.
// Follows the SceneSequencer by step name ("2 ..."). The Bystander > Create Scene 2 menu builds Scene 2.unity with this component and
// its clips assigned. No sound yet (the faint thud and the old man's line come with the sound effects).
public class OldManScene : MonoBehaviour
{
    public AnimationClip fall, reach, dismiss;              // the fall (plays once), reaching for the oranges (loops), the dismissive wave "Don't bother" (plays once, holds)
    public AnimationClip receiveLow, receiveMid, receiveHigh;   // taking the orange from the player's hand, by how high it is held
    public float fallDelay = 3f;                            // seconds of darkness at the start of the beat before he falls
    public float spillDelay = 0.8f;                         // the bag hits the floor this long after the fall starts
    public int oranges = 7;                                 // spilled across the floor, plus the two at the player's feet
    public float orangeRadius = 0.04f;
    public Color orange = new Color(1f, 0.32f, 0.03f);      // x orangeEmission: (1.6, 0.5, 0.05), which clips to orange on the headset and blooms like the figure's particles
    public float orangeEmission = 1.6f;
    public Color bag = new Color(0.55f, 0.22f, 0.04f);      // placeholder bag: a dimmer brown cylinder lying on its side
    public float litInner = 20f, litOuter = 50f;            // degrees from where the head points: fully lit inside, dark outside
    public float dismissAfterLook = 2f, dismissLatest = 25f;   // "Don't bother": after this long looking at him (and at least 6 s after the fall), or this long after the fall at the latest
    public float oldManLeft = 65f, oldManAt = 0.85f;       // where he lies: degrees left of where the head pointed as the beat began, and metres away
    public float bagLeft = 50f, bagAt = 0.7f;
    public float footLeft = 22f, footDist = 0.5f;           // the orange at the player's feet
    public float toesLeft = 8f, toesDist = 0.6f;             // and one closer still, almost at the toes
    public float receiveHoldAt = 0.5f;                      // 0..1: how far into the receive clip he gets (hand out) before he waits for the orange
    public float giveReach = 0.55f;                          // metres along the floor: let go this close to the old man (and below head height) and he reaches out and takes it
    public float afterGive = 6f, freedTimeout = 60f;        // seconds from the orange being taken, or from the break with nothing given, to the cards

    public string Status => phase == Phase.Idle ? null : $"phase={phase} t={t:0.0} lit={(figure != null ? figure.brightness : 0f):0.00} look={dwell:0.0}s freed={(freed ? 1 : 0)} given={(given ? 1 : 0)} held={held}";

    enum Phase { Idle, Dark, Falling, Down, Dismissed }
    class Orange
    {
        public Transform tr; public Renderer r; public HandGrabInteractable grab; public Vector3 start, rest, giveFrom;
        public float delay, dur, k, lit, giveT, vy;
        public bool given, falling, arrived; public int holder = -1; public Transform hand;
    }

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    SceneSequencer seq; ChainWrap chain; Transform head; Phase phase; ParticleFigure figure;
    readonly List<Orange> list = new List<Orange>();
    Transform bagTr; Renderer bagR; float bagLit;
    Vector3 origin, fwd, oldManPos; MaterialPropertyBlock mpb;
    AnimationClip receiving; bool offering; int held; float dismissedAt; bool dismissed; float t, fallAt, dwell, findTimer, freedAt, givenAt; bool spilled, looked, warned, freed, given, toCards;

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
        bool want = seq != null && head != null && seq.Index >= 0 && seq.steps[seq.Index].name.StartsWith("2");
        if (want && phase == Phase.Idle && !seq.Busy) Begin();
        if (!want && phase != Phase.Idle) { End(); return; }
        if (phase == Phase.Idle) return;

        t += dt;
        switch (phase)
        {
            case Phase.Dark:
                if (t >= fallDelay)
                {
                    figure.playing = true; fallAt = t; phase = Phase.Falling;
                    Log("old man falls");
                }
                break;
            case Phase.Falling:
                if (t >= fallAt + fall.length) { figure.Play(reach, true); phase = Phase.Down; }
                break;
            case Phase.Down:
                if (Lit(oldManPos + Vector3.up * 0.3f) > 0.5f || (list.Count > 0 && Lit(list[0].rest) > 0.5f))
                {
                    if (!looked) { looked = true; Log($"player is looking at him after {t - fallAt:0.0}s"); }
                    dwell += dt;
                }
                if (dismiss != null && !dismissed && !freed && ((dwell >= dismissAfterLook && t >= fallAt + 6f) || t >= fallAt + dismissLatest))
                {
                    figure.Play(dismiss, false); phase = Phase.Dismissed; dismissedAt = t;
                    Log($"old man dismisses ({t - fallAt:0.0}s after the fall, looked {dwell:0.0}s)");
                }
                break;
            case Phase.Dismissed:        // the wave is over: he goes back to gathering his oranges until the beat ends (unless he is being handed one)
                if (!given && !offering && t >= dismissedAt + dismiss.length) { figure.Play(reach, true); phase = Phase.Down; dismissed = true; }
                break;
        }
        if (phase >= Phase.Falling && !spilled && t >= fallAt + spillDelay) Spill();
        if (!freed && chain != null && chain.Broken)
        {
            freed = true; freedAt = t;
            Log($"chains broken at {t:0.0}s: the player can now grab an orange and give it to him ({FindObjectsByType<HandGrabInteractor>(FindObjectsSortMode.None).Length} hand grab interactors in the rig)");
        }
        if (offering && !given && figure != null && figure.playing && figure.ClipTime >= receiving.length * receiveHoldAt)
            figure.playing = false;                       // hand out: he waits there for the orange
        if (freed) Freed(dt);
        Roll(dt);
        Light(dt);
    }

    // set the beat up in the dark: the old man in place but not moving, the bag and oranges hidden, all unlit
    void Begin()
    {
        if (fall == null || reach == null)
        {
            if (!warned) { warned = true; Debug.LogWarning("OldManScene: the fall and reach clips are not assigned (use Bystander > Create Scene 2)"); }
            return;
        }
        t = 0f; dwell = 0f; spilled = false; looked = false; freed = false; given = false; toCards = false; dismissed = false; offering = false;
        seq.deferBreak = true;                                // the break does not jump to the cards by itself in this scene
        fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        origin = new Vector3(head.position.x, 0f, head.position.z);
        oldManPos = At(oldManLeft, oldManAt);
        mpb = new MaterialPropertyBlock();

        var prefab = Resources.Load<GameObject>("Prefab/ParticleFigure");
        var go = Instantiate(prefab);
        go.name = "OldMan";
        figure = go.GetComponent<ParticleFigure>();
        figure.clip = fall; figure.loop = false; figure.position = oldManPos; figure.groundY = 0f;
        figure.playing = false; figure.brightness = 0f;       // set before its Start runs next frame

        var mat = Resources.Load<Material>("OrangeSphere");
        var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(b.GetComponent<Collider>());
        bagTr = b.transform; bagR = b.GetComponent<Renderer>();
        bagTr.localScale = new Vector3(0.16f, 0.13f, 0.16f);
        bagTr.position = At(bagLeft, bagAt) + Vector3.up * 0.08f;
        bagTr.rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Quaternion.Euler(0f, 0f, 90f);   // lying on its side
        Prepare(bagR, mat);
        b.SetActive(false);

        // the first two oranges are the ones at the player's feet (0.5 m and 0.28 m); the rest are spread over the floor between him and the player
        var rests = new List<Vector3>();
        for (int i = 0; i < oranges + 2; i++)
        {
            Vector3 rest = i == 0 ? At(footLeft, footDist) : At(toesLeft, toesDist);
            if (i > 1)
                for (int tries = 0; tries < 40; tries++)
                {
                    rest = At(Random.Range(28f, 75f), Random.Range(0.4f, 0.85f));   // all within a bend and a reach: nobody walks in this film
                    bool ok = Flat(rest - oldManPos).magnitude > 0.3f && Flat(rest - bagTr.position).magnitude > 0.2f;
                    foreach (var r in rests) ok &= Flat(rest - r).magnitude > 0.14f;
                    if (ok) break;
                }
            rests.Add(rest);
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.SetActive(false);                                   // components are added while inactive so the SDK injections land before Awake and Start
            s.GetComponent<SphereCollider>().radius = 1f;         // 0.08 m: easier to catch with a hand than the 0.04 m sphere
            s.name = i == 0 ? "OrangeAtFeet" : i == 1 ? "OrangeAtToes" : "Orange" + i;
            s.transform.localScale = Vector3.one * orangeRadius * 2f;
            s.AddComponent<BreakTarget>();
            var o = new Orange { tr = s.transform, r = s.GetComponent<Renderer>() };
            var rb = s.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;   // stays kinematic: the roll and the drop are animated here
            var g = s.AddComponent<Grabbable>(); g.InjectOptionalRigidbody(rb); g.InjectOptionalThrowWhenUnselected(false);
            o.grab = s.AddComponent<HandGrabInteractable>();
            o.grab.InjectAllHandGrabInteractable(GrabTypeFlags.All, rb, GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
            o.grab.InjectOptionalPointableElement(g);
            o.grab.enabled = false;                               // grabbing opens when the chains break
            g.WhenPointerEventRaised += e => OnGrab(o, e.Type);
            o.rest = rest + Vector3.up * orangeRadius;
            o.start = bagTr.position;
            o.delay = Random.Range(0f, 0.25f);
            o.dur = 0.5f + Vector3.Distance(o.start, o.rest) * 0.9f;
            Prepare(o.r, mat);
            list.Add(o);
        }
        phase = Phase.Dark;
        Log("scene 2 beat 1 begins");
    }

    void Spill()
    {
        spilled = true;
        bagTr.gameObject.SetActive(true);
        foreach (var o in list) { o.tr.position = o.start; o.tr.gameObject.SetActive(true); }
        Log($"bag hits the floor, {list.Count} oranges spill (two roll to the player's feet)");
    }

    // oranges roll from the bag to where they rest, slowing down, turning as they go
    void Roll(float dt)
    {
        if (!spilled) return;
        foreach (var o in list)
        {
            if (o.k >= 1f) continue;
            o.delay -= dt;
            if (o.delay > 0f) continue;
            o.k = Mathf.Min(1f, o.k + dt / o.dur);
            var next = Vector3.Lerp(o.start, o.rest, 1f - Mathf.Pow(1f - o.k, 3f));
            var move = next - o.tr.position;
            if (move.sqrMagnitude > 1e-8f) o.tr.Rotate(Vector3.Cross(Vector3.up, move.normalized), move.magnitude / orangeRadius * Mathf.Rad2Deg, Space.World);
            o.tr.position = next;
        }
    }

    // the chains are broken: the oranges that have stopped rolling can be grabbed with the SDK hand grab. Let go near the old man and he takes
    // it; let go anywhere else and it falls to the floor.
    void Freed(float dt)
    {
        foreach (var o in list)
        {
            if (!given && !o.given && o.k >= 1f && o.tr.gameObject.activeSelf) o.grab.enabled = true;
            if (o.falling && o.holder < 0)        // dropped: falls to the floor
            {
                o.vy -= 9.8f * dt;
                var p = o.tr.position; p.y += o.vy * dt;
                if (p.y <= orangeRadius) { p.y = orangeRadius; o.falling = false; }
                o.tr.position = p;
            }
            if (o.given && o.hand != null)        // he holds his pose while the orange slides to his hand; when it touches, the receive clip plays and the orange stays in his hand
            {
                if (!o.arrived)
                {
                    o.giveT += dt / 0.4f;
                    o.tr.position = Vector3.Lerp(o.giveFrom, o.hand.position, Mathf.SmoothStep(0f, 1f, o.giveT));
                    if (o.giveT >= 1f)
                    {
                        o.arrived = true; givenAt = t;
                        figure.playing = true;                      // the rest of the receive clip: he takes it
                        Log($"{o.tr.name} touches his hand: he takes it");
                    }
                }
                else o.tr.position = o.hand.position;
            }
        }
        if (!toCards && ((given && list.Exists(x => x.arrived) && t >= givenAt + afterGive) || (!given && t >= freedAt + freedTimeout)))
        {
            toCards = true;
            Log(given ? "the old man has his orange: to the cards" : "nothing was given: to the cards");
            seq.GoToCards();
        }
    }

    // pointer events from the SDK: Select = a hand took hold, Unselect or Cancel = the last hand let go
    void OnGrab(Orange o, PointerEventType type)
    {
        if (o.given) return;
        if (type == PointerEventType.Select && o.holder < 0)
        {
            o.holder = 0; o.falling = false; held++;
            Log($"{o.tr.name} grabbed {t - freedAt:0.0}s after the break");
            if (!given && figure != null)                 // he turns to receive it as soon as it is in the player's hand
            {
                float y = o.tr.position.y;
                receiving = y < 0.5f ? receiveLow : y < 1f ? receiveMid : receiveHigh;
                if (receiving == null) receiving = receiveLow != null ? receiveLow : receiveMid != null ? receiveMid : receiveHigh;
                if (receiving != null) { figure.Play(receiving, false); offering = true; Log($"old man holds out his hand ({receiving.name})"); }
            }
        }
        else if ((type == PointerEventType.Unselect || type == PointerEventType.Cancel) && o.holder >= 0)
        {
            o.holder = -1; held--;
            float far = Flat(o.tr.position - oldManPos).magnitude;
            if (!given && far < giveReach && o.tr.position.y < 1.5f) Give(o);
            else
            {
                if (offering && !given && held == 0 && figure != null) { offering = false; figure.Play(reach, true); }   // nothing handed over: back to gathering oranges
                o.falling = true; o.vy = 0f; Log($"{o.tr.name} dropped {far:0.00} m from him at {o.tr.position.y:0.00} m high (he takes it within {giveReach:0.0} m)");
            }
        }
    }

    void Give(Orange o)
    {
        given = true; givenAt = t; o.given = true; foreach (var x in list) x.grab.enabled = false;   // no more grabbing; the slide-in takes over
        o.giveFrom = o.tr.position; o.giveT = 0f;
        float y = o.tr.position.y;
        var clip = y < 0.5f ? receiveLow : y < 1f ? receiveMid : receiveHigh;
        if (clip == null) clip = receiveLow != null ? receiveLow : receiveMid != null ? receiveMid : receiveHigh;
        if (receiving == null) receiving = clip;
        if (figure != null)
        {
            figure.playing = false;                         // he holds the pose he is in until the orange touches his hand
            var l = figure.Bone(HumanBodyBones.LeftHand); var r = figure.Bone(HumanBodyBones.RightHand);
            o.hand = l == null ? r : r == null ? l : Vector3.Distance(l.position, o.giveFrom) < Vector3.Distance(r.position, o.giveFrom) ? l : r;
        }
        Log($"{o.tr.name} offered to the old man at {y:0.00} m ({(clip != null ? clip.name : "no receive clip")} plays when it touches his hand)");
    }

    // lit only where the player looks (an orange in a hand is always lit)
    void Light(float dt)
    {
        if (figure != null) figure.brightness = Mathf.MoveTowards(figure.brightness, offering || given ? 1f : Lit(oldManPos + Vector3.up * 0.3f), 3f * dt);   // while he takes an orange he stays lit, so it is not an orange floating in the dark
        if (bagTr.gameObject.activeSelf)
        {
            bagLit = Mathf.MoveTowards(bagLit, Lit(bagTr.position), 3f * dt);
            Show(bagR, bag, 1.2f * bagLit, bagLit);
        }
        foreach (var o in list)
        {
            if (!o.tr.gameObject.activeSelf) continue;
            o.lit = Mathf.MoveTowards(o.lit, o.holder >= 0 || o.given ? 1f : Lit(o.tr.position), 3f * dt);
            Show(o.r, orange, orangeEmission * o.lit, o.lit);
        }
    }

    void Show(Renderer r, Color c, float scale, float lit)
    {
        r.enabled = lit > 0.01f;
        mpb.SetColor(BaseColor, new Color(c.r * scale, c.g * scale, c.b * scale, 1f));
        r.SetPropertyBlock(mpb);
    }

    float Lit(Vector3 p) => Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(litInner, litOuter, Vector3.Angle(head.forward, p - head.position)));

    void End()
    {
        if (figure != null) Destroy(figure.gameObject);
        if (bagTr != null) Destroy(bagTr.gameObject);
        foreach (var o in list) if (o.tr != null) Destroy(o.tr.gameObject);
        list.Clear(); held = 0; figure = null; bagTr = null; bagLit = 0f;
        if (seq != null) seq.deferBreak = false;
        phase = Phase.Idle; t = 0f;
        Log("scene 2 beat 1 ends");
    }

    static void Prepare(Renderer r, Material m)
    {
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    Vector3 At(float leftDeg, float dist) => origin + Quaternion.AngleAxis(-leftDeg, Vector3.up) * fwd * dist;
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} oldman {msg}");
}
