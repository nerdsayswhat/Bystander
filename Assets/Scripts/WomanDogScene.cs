using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using UnityEngine;

// Scene 2 / Beat 3 of the Event List: near you, on your right, a faceless woman (purple particles) who is scared of a barking dog (an orange-brown
// shiba made of particles); a green ball lies between them, and the dog keeps barking. Starts `beat3At` seconds into the step (after the can).
// Lit only where the player looks, everything within arm's reach. The ball is a BreakTarget (reaching for it strains and breaks the chains).
// After the break the ball can be grabbed with the Meta hand grab: the moment it is in the player's hand the dog goes quiet and calms down, the woman
// stands up and thanks the player (woman_getup_thanks), then walks away (woman_walkaway_loop, in place, moved across the floor), and `afterCalm`
// seconds later the film goes to the cards. Let go of the ball anywhere and it drops to the floor.
// Created in Scene 2.unity by Bystander > Create Scene 2 (which assigns the three woman clips). No sound yet (barking, her breathing, footsteps).
public class WomanDogScene : MonoBehaviour
{
    public AnimationClip cower, getUp, walkAway;
    public float scene5At = 40f;                            // scene 5: seconds into the step (author 2026-11-20: scene 2 was 50, a little faster)
    public float beat3At = 50f;                             // seconds after the step is visible
    public float womanAt = -25f, womanAway = 1.1f;         // degrees left of where the head pointed at the start (negative = right), metres
    public float dogAt = -115f, dogAway = 1.25f;           // renamed so stale saved values are dropped: about 1.2 m between them
    public float womanFar = 3.6f, dogGap = 1.2f, dogRun = 1.4f, dogStop = 1.0f;   // she is 3.6 m out (new fields: the saved scene overrides womanAway); the dog is only 1.2 m from her; he runs to the player at 3.5 m/s once the ball is taken. The ball stays at the old midpoint (womanAway/dogAway)
    public float ballDist = 1.0f;                           // m: how far the ball lies from the player on the line to the woman and the dog (new field)
    public float seatedBall = 0.5f;                        // seated player: the ball lies this fraction of its usual distance (0.8 m) from them
    public float womanTweak = 0f;                           // fine-tune: extra turn in degrees on top of "right hand towards the dog" (negative = anticlockwise seen from above); renamed value kept 0
    public float ballSize = 0.04f, ballRest = 0.04f, litFull = 35f, litFade = 75f;
    public Color woman = new Color(0.06f, 0.012f, 0.16f);   // x emission 10 = (0.6, 0.12, 1.6): purple, blue clips to full so it stays purple
    public Color ball = new Color(0.05f, 0.9f, 0.1f);       // x ballEmission: green
    public float ballEmission = 1.6f;
    public float walkSpeed = 0.7f;                          // m/s while she walks away
    public float afterCalm = 9f;                            // seconds from the ball being grabbed to the cards

    enum Phase { Idle, Waiting, Shown }
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    SceneSequencer seq; ChainWrap chain; Transform head; Phase phase;
    float diag, diagTick; Sfx.Handle breath, steps; ParticleFigure womanFig; ParticleDog dog; Transform ballTr; Renderer ballR; HandGrabInteractable grab;
    Vector3 origin, fwd, womanPos, dogPos, awayDir, ballPos; MaterialPropertyBlock mpb;
    float stride, t, findTimer, womanLit, dogLit, ballLit, vy, calmedAt; bool scene5, held, falling, calmed, walking, toCards, warned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        // an older Scene 2.unity has no WomanDogScene object: make one so that the missing clips are reported (Bystander > Create Scene 2 adds the real one)
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Scene 2" && FindFirstObjectByType<WomanDogScene>() == null)
            new GameObject("WomanDogScene").AddComponent<WomanDogScene>();
    }

    public static AnimationClip PlacedClip; public static bool PlacedLoop; public static float PlacedTime, PlacedBright = 1f;   // what she was doing last frame, so the street can carry on from that pose
    void Update()
    {
        float dt = Time.deltaTime;
        if (womanFig != null && womanFig.ClipTime >= 0f) { PlacedClip = womanFig.clip; PlacedLoop = womanFig.loop; PlacedTime = womanFig.ClipTime; PlacedBright = womanFig.brightness; }
        if ((findTimer -= dt) <= 0f)
        {
            findTimer = 1f;
            if (seq == null) seq = FindFirstObjectByType<SceneSequencer>();
            if (chain == null) chain = FindFirstObjectByType<ChainWrap>();
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }
        bool want = seq != null && head != null && seq.Index >= 0 && (seq.steps[seq.Index].name.StartsWith("2") || seq.steps[seq.Index].name.StartsWith("5"));
        if (want && phase == Phase.Idle && !seq.Busy) Begin();
        if (!want && phase != Phase.Idle) { End(); return; }
        if (phase == Phase.Idle) return;

        t += dt;
        if (phase == Phase.Waiting && t >= (SceneSequencer.TestBeat3Only ? 3f : scene5 ? scene5At : beat3At)) Appear();
        if (phase != Phase.Shown) return;

        if (!calmed && ((chain != null && chain.Broken) || scene5)) grab.enabled = true;          // the hands are free: the ball can be grabbed
        if (falling && !held)
        {
            vy -= 9.8f * dt;
            var p = ballTr.position; p.y += vy * dt;
            if (p.y <= ballSize + Accessibility.Lift) { p.y = ballSize + Accessibility.Lift; falling = false; }
            ballTr.position = p;
        }
        if (calmed)
        {
            if (!walking && womanFig.ClipTime >= getUp.length - 0.05f && walkAway != null)
            {
                womanFig.Play(walkAway, true); walking = true; steps = Sfx.Loop("woman_footsteps", womanPos + Vector3.up * 0.1f, 0.8f); stride = walkAway.averageSpeed.magnitude; if (stride < 0.1f) stride = walkSpeed; Log($"the woman walks away at {stride:0.00} m/s (clip speed {walkAway.averageSpeed.magnitude:0.00}, length {walkAway.length:0.0}s)");
            }
            if (walking) { womanFig.Skeleton.position += awayDir * stride * dt; if (steps != null && steps.alive) steps.Position = womanFig.Skeleton.position; }
            if (dog != null)                                                     // the dog comes running to the player and stops about 0.6 m away, with a small hop in his stride
            {
                var me = new Vector3(head.position.x, 0f, head.position.z);
                var from = dog.transform.position; from.y = 0f;
                var stop = me + (from - me).normalized * dogStop;
                bool running = (stop - from).sqrMagnitude > 0.0025f;
                var np = Vector3.MoveTowards(from, stop, dogRun * dt);
                np.y = running ? 0.04f * Mathf.Abs(Mathf.Sin(t * 16f)) : 0f;
                dog.transform.position = np;
            }
            if (!scene5 && !toCards && t >= calmedAt + afterCalm) { toCards = true; Log("the dog is calm and the woman has gone: to the cards"); seq.GoToCards(); }
        }
        // lit only where the player looks; once calmed they stay lit so the ball is not alone in the dark
        womanLit = Mathf.MoveTowards(womanLit, calmed ? 1f : Lit(womanPos + Vector3.up * 0.9f), 3f * dt);
        dogLit = Mathf.MoveTowards(dogLit, calmed ? 1f : Lit(dogPos + Vector3.up * 0.3f), 3f * dt);
        womanFig.brightness = womanLit; dog.brightness = dogLit;
        ballLit = Mathf.MoveTowards(ballLit, 1f, 3f * dt);   // always lit once it appears: it was gaze-lit, and with the woman and dog 3.6 m out the ball at your feet was outside the lit cone (author: no ball near me)
        ballR.enabled = ballLit > 0.01f;
        mpb.SetColor(BaseColor, new Color(ball.r * ballEmission * ballLit, ball.g * ballEmission * ballLit, ball.b * ballEmission * ballLit, 1f));
        ballR.SetPropertyBlock(mpb);
        if (diag > 0f && (diagTick -= dt) <= 0f)                                    // for 20 s after the ball appears, once a second: is it really drawn, and where is it compared with the gaze (so a log answers "is the ball there")
        {
            diag -= 1f; diagTick = 1f; var to = ballTr.position - head.position;
            Log($"ball check: active {ballTr.gameObject.activeInHierarchy} renderer on {ballR.enabled} seen by a camera {ballR.isVisible} lit {ballLit:0.00} at ({ballTr.position.x:0.00},{ballTr.position.y:0.00},{ballTr.position.z:0.00}) {new Vector2(to.x, to.z).magnitude:0.00} m away, {Vector3.Angle(head.forward, to):0} degrees from the gaze, head pitch {-Mathf.Asin(Mathf.Clamp(head.forward.y, -1f, 1f)) * Mathf.Rad2Deg * -1f:0}");
        }
    }

    // the woman, the dog and the ball are made in the dark and kept hidden until the beat starts
    void Begin()
    {
        if (cower == null || getUp == null)
        {
            if (!warned) { warned = true; Debug.LogWarning("WomanDogScene: the woman clips are not assigned (use Bystander > Create Scene 2)"); }
            return;
        }
        scene5 = seq.steps[seq.Index].name.StartsWith("5");
        t = 0f; womanLit = dogLit = ballLit = 0f; held = falling = calmed = walking = toCards = false; HasYaw = false;
        fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        origin = new Vector3(head.position.x, 0f, head.position.z);
        womanPos = At(womanAt, womanFar);
        var toOldDog = At(dogAt, dogAway) - At(womanAt, womanAway); toOldDog.y = 0f;
        dogPos = womanPos + toOldDog.normalized * dogGap; PlacedWoman = womanPos; PlacedDog = dogPos; HasPlaced = true;
        awayDir = (womanPos - origin).normalized;                               // she walks directly away from the player
        // author 2026-11-24: the ball lies ON THE LINE THAT JOINS THE PLAYER AND THEM (the middle of the woman and the dog), closer to the player: same bearing, 1.0 m out on the floor (0.65 m when seated); no rolling
        var mid = (womanPos + dogPos) * 0.5f - origin; mid.y = 0f; ballPos = origin + mid.normalized * (ballDist * (Accessibility.Seated ? seatedBall : 1f));
        mpb = new MaterialPropertyBlock();

        var prefab = Resources.Load<GameObject>("Prefab/ParticleFigure");
        var go = Instantiate(prefab);
        go.name = "Woman";
        womanFig = go.GetComponent<ParticleFigure>();
        womanFig.clip = cower; womanFig.loop = true; womanFig.position = womanPos; womanFig.groundY = 0f;
        womanFig.color = woman; womanFig.playing = false; womanFig.brightness = 0f;   // set before its Start runs next frame
        BodyLibrary.Apply(womanFig, true);                                              // the woman on a real female body surface (if the bodies are baked)

        var dg = new GameObject("Dog");
        dg.transform.position = dogPos;
        var toWoman = womanPos - dogPos; toWoman.y = 0f;
        dg.transform.rotation = Quaternion.LookRotation(toWoman);                // he faces the woman
        dog = dg.AddComponent<ParticleDog>();
        dog.brightness = 0f; dog.barking = false;

        var mat = Resources.Load<Material>("OrangeSphere");
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.SetActive(false);                                                      // components are added while inactive so the SDK injections land before Awake and Start
        s.name = "GreenBall";
        s.transform.localScale = Vector3.one * ballSize * 2f;

        s.transform.position = ballPos + Vector3.up * (ballRest + Accessibility.Lift);
        ballTr = s.transform; ballR = s.GetComponent<Renderer>();
        Prepare(ballR, mat);
        s.AddComponent<BreakTarget>();
        s.GetComponent<SphereCollider>().radius = 1.8f;                         // about 0.08 m: easy to catch
        var rb = s.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var g = s.AddComponent<Grabbable>(); g.InjectOptionalRigidbody(rb); g.InjectOptionalThrowWhenUnselected(false);
        grab = s.AddComponent<HandGrabInteractable>();
        grab.InjectAllHandGrabInteractable(GrabTypeFlags.All, rb, GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
        grab.InjectOptionalPointableElement(g);
        grab.enabled = false;                                                    // opens when the chains break
        g.WhenPointerEventRaised += e => OnGrab(e.Type);
        if (!scene5) seq.deferBreak = true;                                      // breaking the chains must not jump to the cards: the ball comes first
        phase = Phase.Waiting;
        Log("scene 2 beat 3 waits");
    }

    void Appear()
    {
        var toDog = dogPos - womanPos; toDog.y = 0f;                            // her right hand points at the dog: she faces the dog direction turned anticlockwise by 90
        var face = Quaternion.AngleAxis(-90f, Vector3.up) * toDog.normalized;
        var sk = womanFig.Skeleton;
        sk.RotateAround(womanFig.Bone(HumanBodyBones.Hips).position, Vector3.up, Vector3.SignedAngle(Vector3.ProjectOnPlane(sk.forward, Vector3.up), face, Vector3.up) + womanTweak);
        awayDir = face; PlacedYaw = sk.eulerAngles.y; HasYaw = true;            // she walks the way she faces; the street copies this exact turn
        womanFig.playing = true; dog.barking = true; Sfx.Play("woman_scream", womanPos + Vector3.up * 1.3f); breath = Sfx.Loop("woman_breath", womanPos + Vector3.up * 0.5f, 0.7f); ballTr.gameObject.SetActive(true);   // she screams as the beat starts and the recording runs on (no dialogue yet); the ball appears with her
        phase = Phase.Shown;
        Log("a woman cowers from a barking dog on the right, a ball lies on the floor in front of you");
        diag = 20f; diagTick = 0f;
        { var to = ballTr.position - head.position; Log($"ball at ({ballTr.position.x:0.00},{ballTr.position.y:0.00},{ballTr.position.z:0.00}), {new Vector2(to.x, to.z).magnitude:0.00} m from you, {Vector3.Angle(head.forward, to):0} degrees from where you look, seated {Accessibility.Seated}"); }
    }

    void OnGrab(PointerEventType type)
    {
        if (type == PointerEventType.Select && !held)
        {
            held = true; falling = false;
            if (!calmed)                                                         // the dog goes quiet the moment the ball is in the player's hand
            {
                calmed = true; calmedAt = t; breath?.Stop(); Sfx.Play("ball_pickup", ballTr.position); Sfx.Play("dog_whine", dogPos + Vector3.up * 0.3f); dog.barking = false; dog.lookAt = head; seq.calmedDog = true;
                womanFig.Play(getUp, false); { var wh = womanFig.Bone(HumanBodyBones.Head); Sfx.Play("woman_thanks", wh != null ? wh.position : womanPos + Vector3.up * 1.4f, 0.9f, true, 25f, 1.5f); }   // from her head, directional
                Log("ball grabbed: the dog goes quiet, the woman gets up");
            }
        }
        else if ((type == PointerEventType.Unselect || type == PointerEventType.Cancel) && held)
        {
            held = false; falling = true; vy = 0f;
            Log($"ball let go at {ballTr.position.y:0.00} m high");
        }
    }

    float Lit(Vector3 p) => Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(litFull, litFade, Vector3.Angle(head.forward, p - head.position)));

    static void Prepare(Renderer r, Material m)
    {
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
    }

    void End()
    {
        breath?.Stop(); steps?.Stop();   // the woman's breathing and footsteps end with the beat
        // scene 2 into the street (no break, the step after is "3 ..."): the lady is handed over alive, the same object, and the street adopts her
        bool toStreet = womanFig != null && !scene5 && phase == Phase.Shown && seq != null && seq.Index >= 0 && seq.steps[seq.Index].name.StartsWith("3");
        if (womanFig != null) { if (toStreet) HandOver = womanFig; else Destroy(womanFig.gameObject); }
        if (dog != null) Destroy(dog.gameObject);
        if (ballTr != null) Destroy(ballTr.gameObject);
        if (seq != null) seq.deferBreak = false;
        womanFig = null; dog = null; ballTr = null; phase = Phase.Idle; t = 0f;
        Log("scene 2 beat 3 ends");
    }

    public static ParticleFigure HandOver;                                          // the lady, alive, for the street to adopt (set in End, cleared by StreetScene)
    public static bool HasYaw; public static float PlacedYaw;                      // the turn her skeleton ended with in Appear (world yaw, degrees)
    public static bool HasPlaced; public static Vector3 PlacedWoman, PlacedDog;     // for the street scenes that follow: the woman and the dog stay exactly there
    Vector3 At(float leftDeg, float dist) => origin + Quaternion.AngleAxis(-leftDeg, Vector3.up) * fwd * dist;
    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} womandog {msg}");
}
