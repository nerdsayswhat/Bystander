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
    public float womanTweak = 0f;                            // fine-tune: extra turn in degrees on top of "right hand towards the dog" (negative = anticlockwise seen from above); renamed value kept 0
    public float ballSize = 0.04f, ballRest = 0.04f, litFull = 35f, litFade = 75f;
    public Color woman = new Color(0.06f, 0.012f, 0.16f);   // x emission 10 = (0.6, 0.12, 1.6): purple, blue clips to full so it stays purple
    public Color ball = new Color(0.05f, 0.9f, 0.1f);       // x ballEmission: green
    public float ballEmission = 1.6f;
    public float walkSpeed = 0.7f;                          // m/s while she walks away
    public float afterCalm = 9f;                            // seconds from the ball being grabbed to the cards

    enum Phase { Idle, Waiting, Shown }
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    SceneSequencer seq; ChainWrap chain; Transform head; Phase phase;
    ParticleFigure womanFig; ParticleDog dog; Transform ballTr; Renderer ballR; HandGrabInteractable grab;
    Vector3 origin, fwd, womanPos, dogPos, awayDir; MaterialPropertyBlock mpb;
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
            if (p.y <= ballSize) { p.y = ballSize; falling = false; }
            ballTr.position = p;
        }
        if (calmed)
        {
            if (!walking && womanFig.ClipTime >= getUp.length - 0.05f && walkAway != null)
            {
                womanFig.Play(walkAway, true); walking = true; stride = walkAway.averageSpeed.magnitude; if (stride < 0.1f) stride = walkSpeed; Log($"the woman walks away at {stride:0.00} m/s (clip speed {walkAway.averageSpeed.magnitude:0.00}, length {walkAway.length:0.0}s)");
            }
            if (walking) womanFig.Skeleton.position += awayDir * stride * dt;
            if (!scene5 && !toCards && t >= calmedAt + afterCalm) { toCards = true; Log("the dog is calm and the woman has gone: to the cards"); seq.GoToCards(); }
        }
        // lit only where the player looks; once calmed they stay lit so the ball is not alone in the dark
        womanLit = Mathf.MoveTowards(womanLit, calmed ? 1f : Lit(womanPos + Vector3.up * 0.9f), 3f * dt);
        dogLit = Mathf.MoveTowards(dogLit, calmed ? 1f : Lit(dogPos + Vector3.up * 0.3f), 3f * dt);
        womanFig.brightness = womanLit; dog.brightness = dogLit;
        ballLit = Mathf.MoveTowards(ballLit, held ? 1f : Lit(ballTr.position), 3f * dt);
        ballR.enabled = ballLit > 0.01f;
        mpb.SetColor(BaseColor, new Color(ball.r * ballEmission * ballLit, ball.g * ballEmission * ballLit, ball.b * ballEmission * ballLit, 1f));
        ballR.SetPropertyBlock(mpb);
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
        t = 0f; womanLit = dogLit = ballLit = 0f; held = falling = calmed = walking = toCards = false;
        fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        origin = new Vector3(head.position.x, 0f, head.position.z);
        womanPos = At(womanAt, womanAway); dogPos = At(dogAt, dogAway); PlacedWoman = womanPos; PlacedDog = dogPos; HasPlaced = true;
        awayDir = (womanPos - origin).normalized;                               // she walks directly away from the player
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
        s.transform.position = (womanPos + dogPos) * 0.5f + Vector3.up * ballRest;
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
        awayDir = face;                                                         // she walks the way she faces
        womanFig.playing = true; dog.barking = true; ballTr.gameObject.SetActive(true);
        phase = Phase.Shown;
        Log("a woman cowers from a barking dog on the right, a ball lies between them");
    }

    void OnGrab(PointerEventType type)
    {
        if (type == PointerEventType.Select && !held)
        {
            held = true; falling = false;
            if (!calmed)                                                         // the dog goes quiet the moment the ball is in the player's hand
            {
                calmed = true; calmedAt = t; dog.barking = false; dog.lookAt = head; seq.calmedDog = true;
                womanFig.Play(getUp, false);
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
        if (womanFig != null) Destroy(womanFig.gameObject);
        if (dog != null) Destroy(dog.gameObject);
        if (ballTr != null) Destroy(ballTr.gameObject);
        if (seq != null) seq.deferBreak = false;
        womanFig = null; dog = null; ballTr = null; phase = Phase.Idle; t = 0f;
        Log("scene 2 beat 3 ends");
    }

    public static bool HasPlaced; public static Vector3 PlacedWoman, PlacedDog;     // for the street scenes that follow: the woman and the dog stay exactly there
    Vector3 At(float leftDeg, float dist) => origin + Quaternion.AngleAxis(-leftDeg, Vector3.up) * fwd * dist;
    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} womandog {msg}");
}
