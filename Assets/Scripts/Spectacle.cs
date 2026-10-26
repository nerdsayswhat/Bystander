using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

// Scene 1 / Beat 2 of the Event List: the spectacle high above. Small far-off flashes (bursts of glowing discs, never more than
// maxPerSecond a second and capped below full brightness). Each flash plays one of boomClips from the same spot; with no clips it is
// silent (no placeholder sound). The player finds the flashes by looking up; if they never do, the sound repeats louder (Volume
// steps up every nudgeEvery seconds).
// It follows the SceneSequencer by step name: it runs in scenes 1 (after scene1Delay, the length of beat 1), 2, 3 and 6.
// Without a sequencer (Test_Scene today) it starts testDelay seconds after the scene loads. Spawns itself; AutoSpawn = false turns it off.
public class Spectacle : MonoBehaviour
{
    const bool AutoSpawn = true;
    const int Max = 600;                                    // particles alive at once
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly Color[] Palette =                       // no pure red: saturated red flashes are the riskiest colour for photosensitive viewers
    {
        new Color(0.35f, 0.9f, 1f),    // cyan
        new Color(0.4f, 0.55f, 1f),    // electric blue
        new Color(0.7f, 0.45f, 1f),    // violet
        new Color(1f, 0.35f, 0.85f),   // magenta
        new Color(1f, 0.8f, 0.3f),     // gold
        new Color(1f, 0.55f, 0.2f),    // orange
        new Color(0.45f, 1f, 0.55f),   // green
        new Color(1f, 1f, 1f),         // white
    };

    public Vector3 center = new Vector3(0f, 32f, 12f);      // world: high above and a little ahead, about 70 degrees up and 34 m away (the same distance as before, so the size looks the same)
    public Vector3 spread = new Vector3(22f, 8f, 12f);      // half-sizes of the box around it where the flashes land
    public float maxPerSecond = 2.5f, maxGap = 1.3f;        // photosensitivity: the Event List says under 3 flashes a second
    public float maxScale = 3f;                             // each flash is a random 1 to maxScale times the base size
    public float peak = 0.7f, emission = 6f;               // vertex colour at the brightest (below full) x material brightness (still above the bloom threshold)
    public float scene1Delay = 24f, testDelay = 4f;         // NOT FINAL (author, 2026-10-26): a second earlier than the 25 s beat 1 and the 5 s test start; judge again once the tutorial is in front of it
    public float lookAngle = 35f, lookDwell = 0.4f;        // "looked up": the head points within lookAngle of the spectacle for lookDwell seconds
    public float nudgeEvery = 6f;                           // not looked yet: the boom gets louder every nudgeEvery seconds
    public AudioClip[] boomClips;                           // the sound for each flash; empty = silent (the synthesized placeholder boom was removed: the same sound every flash got irritating)
    public UnityEvent onLookedUp;

    public bool IsOn => on;
    public int Flashes { get; private set; }
    public bool LookedUp { get; private set; }
    public float GazeAngle { get; private set; } = -1f;     // degrees between where the head points and the spectacle
    public float Volume => LookedUp ? 0.8f : Mathf.Min(1f, 0.5f + 0.1f * Mathf.Floor(onTime / Mathf.Max(nudgeEvery, 0.1f)));

    SceneSequencer seq; Transform head; ParticleSystem ps;
    bool on; float onTime, nextFlash, lookTime, findTimer, stepStart; int lastStep = -1;
    readonly Vector3[] pos = new Vector3[Max], vel = new Vector3[Max];
    readonly float[] age = new float[Max], life = new float[Max], size0 = new float[Max];
    readonly Color[] tint = new Color[Max];
    readonly ParticleSystem.Particle[] parts = new ParticleSystem.Particle[Max];
    int used;
    AudioSource[] voices; int voice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (AutoSpawn && FindFirstObjectByType<Spectacle>() == null) new GameObject("Spectacle").AddComponent<Spectacle>();
    }

    void Start()
    {
        var go = new GameObject("SpectacleParticles");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = Max;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = Resources.Load<Material>("ChainParticle");
        rend.shadowCastingMode = ShadowCastingMode.Off; rend.receiveShadows = false;   // no shadows on particles, ever
        rend.maxParticleSize = 1f;                                                      // the big discs must not be clamped to the default half-screen limit
        var mat = rend.material;
        mat.SetColor(BaseColor, Color.white * emission);
        mat.SetTexture("_BaseMap", ParticleFigure.DiscTexture());
        for (int i = 0; i < parts.Length; i++) { parts[i].startLifetime = 1000f; parts[i].remainingLifetime = 1000f; }
        ps.Play();

        voices = new AudioSource[4];
        for (int i = 0; i < voices.Length; i++)
        {
            var v = new GameObject("SpectacleVoice" + i).AddComponent<AudioSource>();
            v.transform.SetParent(transform, false);
            v.playOnAwake = false; v.loop = false; v.spatialBlend = 1f; v.dopplerLevel = 0f;
            v.rolloffMode = AudioRolloffMode.Linear; v.minDistance = 100f; v.maxDistance = 300f;   // full volume out to 100 m: it is far away but loud
            voices[i] = v;
        }
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

        bool want = Wanted();
        if (want != on)
        {
            on = want;
            Log(on ? "started" : "stopped");
            if (on) nextFlash = Time.time + 0.5f;
        }
        if (on)
        {
            onTime += dt;
            Gaze(dt);
            if (Time.time >= nextFlash)
            {
                Flash();
                float min = 1f / Mathf.Max(maxPerSecond, 0.1f);
                nextFlash = Time.time + Random.Range(min, Mathf.Max(min, maxGap));
            }
        }
        Step(dt);
    }

    bool Wanted()
    {
        if (seq == null) return Time.timeSinceLevelLoad >= testDelay;
        if (seq.Index < 0) return false;
        if (seq.Index != lastStep) { lastStep = seq.Index; stepStart = Time.time; }
        string n = seq.steps[seq.Index].name;
        if (string.IsNullOrEmpty(n)) return false;
        if (n[0] == '1') return Time.time - stepStart >= scene1Delay;   // beat 1 (the chains, silence) comes first
        return n[0] == '2' || n[0] == '3' || n[0] == '6';
    }

    void Gaze(float dt)
    {
        if (head == null) return;
        GazeAngle = Vector3.Angle(head.forward, center - head.position);
        if (LookedUp) return;
        lookTime = GazeAngle < lookAngle ? lookTime + dt : 0f;
        if (lookTime < lookDwell) return;
        LookedUp = true;
        Log($"player looked up after {onTime:0.0}s");
        onLookedUp?.Invoke();
    }

    // one flash: big short-lived discs for the core and sparks flying out and shrinking, two palette colours per burst
    void Flash()
    {
        var c = center + new Vector3(Random.Range(-spread.x, spread.x), Random.Range(-spread.y, spread.y), Random.Range(-spread.z, spread.z));
        int ia = Random.Range(0, Palette.Length), ib = (ia + Random.Range(1, Palette.Length)) % Palette.Length;   // two different colours per burst
        Color a = Palette[ia], b = Palette[ib];
        float k = Random.Range(1f, maxScale);                                   // each burst is 1 to maxScale times the base size (discs and spread together)
        float dim = Mathf.Lerp(1f, 0.8f, (k - 1f) / Mathf.Max(maxScale - 1f, 0.01f));   // the biggest bursts are a little dimmer: the area grows, so the brightness gives a bit back
        for (int i = 0; i < 90 && used < Max; i++, used++)
        {
            bool core = i < 12;
            pos[used] = core ? c + Random.insideUnitSphere * 2f * k : c;
            vel[used] = core ? Vector3.zero : Random.onUnitSphere * Random.Range(8f, 20f) * k;
            age[used] = 0f; life[used] = core ? 0.4f : Random.Range(0.6f, 1.2f);
            size0[used] = (core ? Random.Range(4f, 7f) : Random.Range(0.8f, 1.8f)) * k;
            tint[used] = (core ? a : Color.Lerp(Random.value < 0.5f ? a : b, Color.white, Random.value * 0.35f)) * dim;
        }
        Flashes++;
        if (boomClips == null || boomClips.Length == 0) return;   // silent until real clips are assigned
        var v = voices[voice++ % voices.Length];
        v.transform.position = c;
        v.clip = boomClips[Random.Range(0, boomClips.Length)];
        v.volume = Volume; v.pitch = Random.Range(0.9f, 1.1f);
        v.PlayDelayed(Random.Range(0.1f, 0.3f));   // far away: the sound trails the light
    }

    void Step(float dt)
    {
        int k = 0;
        for (int i = 0; i < used; i++)
        {
            age[i] += dt;
            if (age[i] >= life[i]) continue;                                   // dead: dropped as the arrays are compacted
            float t = age[i] / life[i];
            float b = t < 0.15f ? t / 0.15f : Mathf.Pow(1f - (t - 0.15f) / 0.85f, 2f);   // quick rise, slow fade
            if (k != i) { pos[k] = pos[i]; vel[k] = vel[i]; age[k] = age[i]; life[k] = life[i]; size0[k] = size0[i]; tint[k] = tint[i]; }
            pos[k] += vel[k] * dt; vel[k] *= Mathf.Max(0f, 1f - 2f * dt);
            parts[k].position = pos[k];
            parts[k].startSize = size0[k] * (0.4f + 0.6f * b);
            float s = peak * b;                                                // brightness through the colour; the alpha stays 1 (the material clips on it)
            parts[k].startColor = new Color(tint[k].r * s, tint[k].g * s, tint[k].b * s, 1f);
            k++;
        }
        used = k;
        ps.SetParticles(parts, used);
    }

    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} spectacle {msg}");
}
