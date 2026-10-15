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
    public int particleCount = 1500;              // total for the figure; raise or lower to find the Quest budget
    public float particleSize = 0.014f;
    public float emission = 10f;                   // at 10 the colour below gives a pale, bright blue, like the chain at its maximum
    public Color color = new Color(0.065f, 0.088f, 0.1f);   // x emission 10 = a pale blue of luminance about 0.84, above the bloom threshold so it glows
    public Material particleMaterial;              // falls back to Resources/ChainParticle (flat discs, alpha clipped)
    public Vector3 position = new Vector3(0f, 0f, 1.5f);   // where the figure stands (world), facing the origin
    public float groundY = 0f;                      // world height of the floor (the tracking origin is the floor); the lowest particle is placed on it

    struct Seg { public HumanBodyBones a, b; public float radius, weight; }
    // limb segments (bone a to bone b) with a thickness; weight shares the particles out by volume
    static readonly Seg[] Body =
    {
        new Seg { a = HumanBodyBones.Hips,          b = HumanBodyBones.Chest,         radius = 0.15f,  weight = 3.0f },
        new Seg { a = HumanBodyBones.Chest,         b = HumanBodyBones.Neck,          radius = 0.16f,  weight = 3.0f },
        new Seg { a = HumanBodyBones.Neck,          b = HumanBodyBones.Head,          radius = 0.05f,  weight = 0.5f },
        new Seg { a = HumanBodyBones.Head,          b = HumanBodyBones.Head,          radius = 0.11f,  weight = 2.0f },   // ball
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

    public Transform Skeleton { get; private set; }                // the instantiated skeleton (for ExperienceLog; Body is the segment table)
    public int ParticleCount => parts != null ? parts.Length : 0;
    public float ClipTime => graph.IsValid() ? clipTime : -1f;
    public float LowestY { get; private set; }                      // height of the lowest particle (for ExperienceLog: 0 means the feet are on the floor)
    float clipTime;
    PlayableGraph graph; AnimationClipPlayable playable;
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

        if (clip == null) Debug.LogWarning("ParticleFigure: no clip assigned, the figure will stay in the T-pose");
        else
        {
            Debug.Log($"ParticleFigure: playing '{clip.name}' ({clip.length:0.0}s), humanoid clip: {clip.isHumanMotion}, avatar human: {anim.avatar.isHuman}");
            if (!clip.isHumanMotion) Debug.LogWarning("ParticleFigure: the clip is not a humanoid clip (set its FBX Rig > Animation Type to Humanoid), so it cannot move this skeleton");
            graph = PlayableGraph.Create("ParticleFigure");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);   // Update sets the time and evaluates the graph itself, so the pose is certain before the particles read the bones
            playable = AnimationClipPlayable.Create(graph, clip);
            var output = AnimationPlayableOutput.Create(graph, "anim", anim);
            output.SetSourcePlayable(playable);
            graph.Evaluate(0f);                             // the bones take the clip's first pose before the particles are bound
        }

        Bind(anim);
        BuildSystem();
    }

    // scatter the particles over the limb segments, each stored in the space of the bone it belongs to
    void Bind(Animator anim)
    {
        float total = 0f;
        foreach (var s in Body) total += s.weight;
        var bl = new System.Collections.Generic.List<Transform>(); var ll = new System.Collections.Generic.List<Vector3>();
        foreach (var s in Body)
        {
            var ta = anim.GetBoneTransform(s.a); var tb = anim.GetBoneTransform(s.b);
            if (ta == null || tb == null) continue;
            int n = Mathf.Max(1, Mathf.RoundToInt(particleCount * s.weight / total));
            var axisW = tb.position - ta.position;
            bool ball = s.a == s.b;
            float len = ball ? 0f : axisW.magnitude;
            var axisDir = ball ? Vector3.up : axisW.normalized;
            var side = Vector3.Cross(axisDir, Vector3.right).sqrMagnitude > 0.01f ? Vector3.Cross(axisDir, Vector3.right).normalized : Vector3.Cross(axisDir, Vector3.forward).normalized;
            var up = Vector3.Cross(axisDir, side);
            for (int i = 0; i < n; i++)
            {
                Vector3 p;
                if (ball) p = ta.position + Vector3.up * s.radius + Random.onUnitSphere * s.radius;               // a head above the head bone
                else
                {
                    float t = Random.value, ang = Random.value * 2f * Mathf.PI;
                    p = ta.position + axisW * t + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * s.radius;
                }
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
        for (int i = 0; i < parts.Length; i++) { parts[i].startLifetime = 1000f; parts[i].remainingLifetime = 1000f; parts[i].startSize = particleSize; parts[i].startColor = color; }
        var go = new GameObject("FigureParticles");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = parts.Length;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = particleMaterial != null ? particleMaterial : Resources.Load<Material>("ChainParticle");
        var mat = rend.material;
        mat.SetColor(BaseColor, Color.white * emission);
        mat.SetTexture("_BaseMap", DiscTexture());
        ps.Play();
    }

    void Update()
    {
        if (clip == null || !graph.IsValid()) return;
        float len = Mathf.Max(clip.length, 0.01f);
        clipTime = loop ? (clipTime + Time.deltaTime) % len : Mathf.Min(clipTime + Time.deltaTime, len);   // loops, or plays once and holds the last pose
        playable.SetTime(clipTime);
        graph.Evaluate(0f);
    }

    void LateUpdate()
    {
        if (ps == null) return;
        float low = float.MaxValue;
        for (int i = 0; i < parts.Length; i++) { parts[i].position = bone[i].TransformPoint(local[i]); low = Mathf.Min(low, parts[i].position.y); }
        LowestY = low;
        ps.SetParticles(parts, parts.Length);
    }

    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

    // same flat white disc as the chain: round through the alpha, no shading or shadow side
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
}
