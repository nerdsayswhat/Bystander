using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// A faceless person made of particles, driven by a humanoid skeleton. The model is only a skeleton: its meshes
// are hidden and every particle sits at a fixed offset from a bone, so any humanoid clip (Mixamo, mocap) moves it.
// Add to an empty GameObject, drag the humanoid model and a clip in, and press Play.
public class ParticleFigure : MonoBehaviour
{
    public GameObject model;                       // the humanoid FBX (e.g. T_Pose); only its skeleton is used
    public AnimationClip clip;                     // the clip to play; looped by this script
    public bool loop = true;                       // untick for one-shots (kneel down, lie in pain, pick up): plays once and holds the last pose
    public int particleCount = 2500;              // total for the figure; raise or lower to find the Quest budget
    public float particleSize = 0.014f;  
    public float sizeMin = 0.008f, sizeMax = 0.025f;   // each particle gets a random size in this range (m), so the figure looks organic, not uniform; particleSize is only used to rest the feet on the floor
    public float emission = 10f;                   // at 10 the colour below gives a pale, bright blue, like the chain at its maximum
    public Color color = new Color(0.004f, 0.05f, 0.16f);   // x emission 10 = (0.04, 0.5, 1.6): a saturated azure blue, strong in blue and green, almost no red, so it stays blue when it blooms (the old grey-blue (0.065, 0.088, 0.1) x 10 read as white)
    public Material particleMaterial;              // falls back to Resources/ChainParticle (flat discs, alpha clipped)
    public Vector3 position = new Vector3(0f, 0f, 1.5f);   // where the figure stands (world), facing the origin
    public float groundY = 0f;                      // world height of the floor (the tracking origin is the floor); the lowest particle is placed on it
    public bool playing = true;                     // false: holds the first pose of the clip until it is set to true (a figure that must be in place but not yet moving)
    public float blendTime = 0.4f;                  // seconds the particles take to ease from the old pose to the new one when Play switches clips (0 = a hard cut)
    public float brightness = 1f;                   // 0..1 scales the glow; 0 hides the figure (scenes where things are lit only where the player looks)

    struct Seg { public HumanBodyBones a, b; public float radius, weight; }
    // limb segments (bone a to bone b) with a thickness; weight shares the particles out by volume
    static readonly Seg[] Body =
    {
        new Seg { a = HumanBodyBones.Hips,          b = HumanBodyBones.Chest,         radius = 0.15f,  weight = 3.0f },
        new Seg { a = HumanBodyBones.LeftUpperLeg,  b = HumanBodyBones.RightUpperLeg, radius = 0.12f,  weight = 1.6f },   // pelvis: a band across both hip joints, so the torso and the thighs join instead of leaving a gap
        new Seg { a = HumanBodyBones.Chest,         b = HumanBodyBones.Neck,          radius = 0.16f,  weight = 3.0f },
        new Seg { a = HumanBodyBones.Neck,          b = HumanBodyBones.Head,          radius = 0.05f,  weight = 0.5f },
        new Seg { a = HumanBodyBones.LeftShoulder,  b = HumanBodyBones.LeftUpperArm,  radius = 0.05f,  weight = 0.6f },   // collar bones: fill the gap between the neck and the shoulder joints
        new Seg { a = HumanBodyBones.RightShoulder, b = HumanBodyBones.RightUpperArm, radius = 0.05f,  weight = 0.6f },
        new Seg { a = HumanBodyBones.LeftUpperArm,  b = HumanBodyBones.LeftLowerArm,  radius = 0.045f, weight = 1.0f },
        new Seg { a = HumanBodyBones.LeftLowerArm,  b = HumanBodyBones.LeftHand,      radius = 0.04f,  weight = 1.0f },
        new Seg { a = HumanBodyBones.RightUpperArm, b = HumanBodyBones.RightLowerArm, radius = 0.045f, weight = 1.0f },
        new Seg { a = HumanBodyBones.RightLowerArm, b = HumanBodyBones.RightHand,     radius = 0.04f,  weight = 1.0f },
        new Seg { a = HumanBodyBones.LeftUpperLeg,  b = HumanBodyBones.LeftLowerLeg,  radius = 0.07f,  weight = 1.6f },
        new Seg { a = HumanBodyBones.LeftLowerLeg,  b = HumanBodyBones.LeftFoot,      radius = 0.05f,  weight = 1.4f },
        new Seg { a = HumanBodyBones.RightUpperLeg, b = HumanBodyBones.RightLowerLeg, radius = 0.07f,  weight = 1.6f },
        new Seg { a = HumanBodyBones.RightLowerLeg, b = HumanBodyBones.RightFoot,     radius = 0.05f,  weight = 1.4f },
        new Seg { a = HumanBodyBones.LeftFoot,      b = HumanBodyBones.LeftToes,      radius = 0.045f, weight = 0.8f },   // feet: without them the legs stop at the ankle, which reads as floating
        new Seg { a = HumanBodyBones.RightFoot,     b = HumanBodyBones.RightToes,     radius = 0.045f, weight = 0.8f },
        new Seg { a = HumanBodyBones.LeftHand,      b = HumanBodyBones.LeftMiddleProximal,  radius = 0.035f, weight = 0.4f },   // hands
        new Seg { a = HumanBodyBones.RightHand,     b = HumanBodyBones.RightMiddleProximal, radius = 0.035f, weight = 0.4f },
    };

    // rounded blobs on a bone: offset and radii are in the figure's own axes (x right, y up, z forward, so z is the way it faces)
    struct Blob { public HumanBodyBones bone; public Vector3 offset, radii; public float weight; }
    static Blob Ball(HumanBodyBones b, float r, float w) => new Blob { bone = b, radii = new Vector3(r, r, r), weight = w };
    static readonly Blob[] Blobs =
    {
        new Blob { bone = HumanBodyBones.Head, offset = new Vector3(0f, 0.105f, -0.005f), radii = new Vector3(0.075f, 0.10f, 0.09f),  weight = 2.0f },   // skull: taller than wide, not a sphere
        new Blob { bone = HumanBodyBones.Head, offset = new Vector3(0f, 0.04f, 0.04f),    radii = new Vector3(0.055f, 0.05f, 0.05f),  weight = 0.9f },   // jaw and chin, pushed forward and down
        Ball(HumanBodyBones.Neck, 0.065f, 0.4f),
        Ball(HumanBodyBones.LeftUpperArm, 0.065f, 0.6f),  Ball(HumanBodyBones.RightUpperArm, 0.065f, 0.6f),    // shoulders
        Ball(HumanBodyBones.LeftLowerArm, 0.045f, 0.35f), Ball(HumanBodyBones.RightLowerArm, 0.045f, 0.35f),   // elbows
        Ball(HumanBodyBones.LeftHand, 0.04f, 0.3f),       Ball(HumanBodyBones.RightHand, 0.04f, 0.3f),         // wrists
        Ball(HumanBodyBones.LeftUpperLeg, 0.085f, 0.6f),  Ball(HumanBodyBones.RightUpperLeg, 0.085f, 0.6f),    // hips
        Ball(HumanBodyBones.LeftLowerLeg, 0.065f, 0.45f), Ball(HumanBodyBones.RightLowerLeg, 0.065f, 0.45f),   // knees
        Ball(HumanBodyBones.LeftFoot, 0.05f, 0.35f),      Ball(HumanBodyBones.RightFoot, 0.05f, 0.35f),        // ankles
    };

    public Transform Skeleton { get; private set; }                // the instantiated skeleton (for ExperienceLog; Body is the segment table)
    public int ParticleCount => parts != null ? parts.Length : 0;
    public Transform Bone(HumanBodyBones b) => animator != null ? animator.GetBoneTransform(b) : null;   // a bone of the live skeleton (e.g. a hand to hand something to)
    public float ClipTime => graph.IsValid() ? clipTime : -1f;
    public float LowestY { get; private set; }                      // height of the lowest particle (for ExperienceLog: 0 means the feet are on the floor)
    float clipTime;
    PlayableGraph graph; AnimationClipPlayable playable; AnimationPlayableOutput aout; Animator animator;
    ParticleSystemRenderer prend; Material pmat; float lastBrightness = -1f;
    Transform[] bone; Vector3[] local;             // per particle: its bone and its offset in that bone's space
    ParticleSystem ps; ParticleSystem.Particle[] parts;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        if (model == null) { Debug.LogWarning("ParticleFigure: no model assigned"); return; }
        var toOrigin = new Vector3(-position.x, 0f, -position.z);
        var go = Instantiate(model, position, toOrigin.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toOrigin) : Quaternion.identity);   // faces the player
        go.transform.SetParent(transform, true);
        Skeleton = go.transform;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;   // skeleton only

        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        if (anim.avatar == null || !anim.avatar.isHuman) { Debug.LogWarning("ParticleFigure: the model is not a humanoid (set Rig > Animation Type to Humanoid)"); return; }
        anim.applyRootMotion = false;
        anim.enabled = true;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the meshes are hidden, and a culled animator never moves its bones (it stays in the T-pose)
        animator = anim;

        if (clip == null) Debug.LogWarning("ParticleFigure: no clip assigned, the figure will stay in the T-pose");
        else
        {
            Debug.Log($"ParticleFigure: playing '{clip.name}' ({clip.length:0.0}s), humanoid clip: {clip.isHumanMotion}, avatar human: {anim.avatar.isHuman}");
            if (!clip.isHumanMotion) Debug.LogWarning("ParticleFigure: the clip is not a humanoid clip (set its FBX Rig > Animation Type to Humanoid), so it cannot move this skeleton");
            graph = PlayableGraph.Create("ParticleFigure");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);   // Update sets the time and evaluates the graph itself, so the pose is certain before the particles read the bones
            playable = AnimationClipPlayable.Create(graph, clip);
            aout = AnimationPlayableOutput.Create(graph, "anim", anim);
            aout.SetSourcePlayable(playable);
            graph.Evaluate(0f);                             // the bones take the clip's first pose before the particles are bound
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);   // mocap carries the performer's studio position: start the clip with the hips over the figure's position
            if (hips != null) Skeleton.position += new Vector3(position.x - hips.position.x, 0f, position.z - hips.position.z);
        }

        Bind(anim);
        BuildSystem();
    }

    // scatter the particles over the limb segments, each stored in the space of the bone it belongs to
    void Bind(Animator anim)
    {
        float total = 0f;
        foreach (var s in Body) total += s.weight;
        foreach (var b in Blobs) total += b.weight;
        var bl = new System.Collections.Generic.List<Transform>(); var ll = new System.Collections.Generic.List<Vector3>();
        foreach (var s in Body)
        {
            var ta = anim.GetBoneTransform(s.a); var tb = anim.GetBoneTransform(s.b);
            if (ta == null || tb == null) continue;
            int n = Mathf.Max(1, Mathf.RoundToInt(particleCount * s.weight / total));
            var axisW = tb.position - ta.position;
            var axisDir = axisW.normalized;
            var side = Vector3.Cross(axisDir, Vector3.right).sqrMagnitude > 0.01f ? Vector3.Cross(axisDir, Vector3.right).normalized : Vector3.Cross(axisDir, Vector3.forward).normalized;
            var up = Vector3.Cross(axisDir, side);
            for (int i = 0; i < n; i++)
            {
                float t = Random.value, ang = Random.value * 2f * Mathf.PI;
                var p = ta.position + axisW * t + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * s.radius;
                bl.Add(ta); ll.Add(ta.InverseTransformPoint(p));
            }
        }
        foreach (var b in Blobs)
        {
            var ta = anim.GetBoneTransform(b.bone);
            if (ta == null) continue;
            int n = Mathf.Max(1, Mathf.RoundToInt(particleCount * b.weight / total));
            for (int i = 0; i < n; i++)
            {
                var q = Random.onUnitSphere;
                var p = ta.position + Skeleton.TransformVector(b.offset + Vector3.Scale(q, b.radii));
                bl.Add(ta); ll.Add(ta.InverseTransformPoint(p));
            }
        }
        bone = bl.ToArray(); local = ll.ToArray();

        // stand the figure on the floor: move the whole skeleton so the lowest particle rests on groundY
        float lowest = float.MaxValue;
        for (int i = 0; i < bone.Length; i++) lowest = Mathf.Min(lowest, bone[i].TransformPoint(local[i]).y);
        Skeleton.position += Vector3.up * (groundY + particleSize * 0.5f - lowest);
    }

    void BuildSystem()
    {
        parts = new ParticleSystem.Particle[bone.Length];
        for (int i = 0; i < parts.Length; i++) { parts[i].startLifetime = 1000f; parts[i].remainingLifetime = 1000f; parts[i].startSize = Random.Range(sizeMin, sizeMax); parts[i].startColor = color; }
        var go = new GameObject("FigureParticles");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = parts.Length;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        prend = go.GetComponent<ParticleSystemRenderer>();
        prend.sharedMaterial = particleMaterial != null ? particleMaterial : Resources.Load<Material>("ChainParticle");
        pmat = prend.material;
        pmat.SetColor(BaseColor, Color.white * emission * brightness);
        pmat.SetTexture("_BaseMap", DiscTexture());
        ps.Play();
    }

    // switch to another clip (a fall, then reaching on the ground, then a dismissive wave): the new clip starts where the figure is,
    // with its hips over the hips of the pose it replaces. A hard cut, no blend.
    Vector3[] blendFrom; float blend = 1f;
    public void Play(AnimationClip next, bool loopIt = true)
    {
        if (next == null || !graph.IsValid()) return;
        if (blendTime > 0f && parts != null)               // remember where every particle is now; LateUpdate eases from there
        {
            if (blendFrom == null || blendFrom.Length != parts.Length) blendFrom = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++) blendFrom[i] = parts[i].position;
            blend = 0f;
        }
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var keep = hips.position;
        var old = playable;
        playable = AnimationClipPlayable.Create(graph, next);
        aout.SetSourcePlayable(playable);
        old.Destroy();
        clip = next; loop = loopIt; clipTime = 0f; playing = true;
        playable.SetTime(0.0);
        graph.Evaluate(0f);
        Skeleton.position += new Vector3(keep.x - hips.position.x, 0f, keep.z - hips.position.z);
    }

    void Update()
    {
        if (clip == null || !graph.IsValid() || !playing) return;
        float len = Mathf.Max(clip.length, 0.01f);
        float prev = clipTime;
        clipTime = loop ? (clipTime + Time.deltaTime) % len : Mathf.Min(clipTime + Time.deltaTime, len);   // loops, or plays once and holds the last pose
        if (loop && clipTime < prev && blendTime > 0f && parts != null)   // the loop seam: ease from the last pose into the first so the restart does not jerk
        {
            if (blendFrom == null || blendFrom.Length != parts.Length) blendFrom = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++) blendFrom[i] = parts[i].position;
            blend = 0f;
        }
        playable.SetTime(clipTime);
        graph.Evaluate(0f);
    }

    void LateUpdate()
    {
        if (ps == null) return;
        float low = float.MaxValue;
        for (int i = 0; i < parts.Length; i++) { parts[i].position = bone[i].TransformPoint(local[i]); low = Mathf.Min(low, parts[i].position.y); }
        float lift = groundY + particleSize * 0.5f - low;   // keep the lowest particle on the floor every frame: kneeling and lying clips touch the ground instead of floating
        for (int i = 0; i < parts.Length; i++) parts[i].position += Vector3.up * lift;
        if (blend < 1f)                                     // a clip switch: ease every particle from where it was to where the new pose puts it
        {
            blend = Mathf.Min(1f, blend + Time.deltaTime / Mathf.Max(blendTime, 0.01f));
            float k = Mathf.SmoothStep(0f, 1f, blend);
            for (int i = 0; i < parts.Length; i++) parts[i].position = Vector3.Lerp(blendFrom[i], parts[i].position, k);
        }
        LowestY = -lift;                                    // how far the clip itself had lifted the figure (0 = feet on the floor)
        ps.SetParticles(parts, parts.Length);
        if (!Mathf.Approximately(brightness, lastBrightness))
        {
            lastBrightness = brightness;
            prend.enabled = brightness > 0.002f;
            pmat.SetColor(BaseColor, Color.white * emission * brightness);
        }
    }

    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

    // same flat white disc as the chain: round through the alpha, no shading or shadow side
    public static Texture2D DiscTexture()
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
}
