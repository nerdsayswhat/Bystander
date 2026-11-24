using UnityEngine;

// A shiba inu made of particles, built in code (there is no dog model or clip): about 2300 glowing discs in ellipsoids (body, chest, hips, four legs,
// neck, head, muzzle, jaw, two triangular ears, a thick curled tail), in the same flat azure-style particle look as ParticleFigure but orange-brown.
// Faces along the transform's forward, stands on the floor (y = 0 at the feet), about 0.55 m long and 0.65 m to the top of the head.
// Barking: every `barkEvery` seconds the head snaps up and back, the jaw drops, the chest pushes out, the body recoils and the whole dog flares
// brighter; between barks it trembles and the tail wags. barking = false: head low, no flare (he has calmed down). Flare is 1 on a bark (for sound later).
public class ParticleDog : MonoBehaviour
{
    public int particleCount = 2300;                         // the shares below add up to 2315; they scale with this
    public Color color = new Color(0.09f, 0.03f, 0.004f);   // x emission 10 = (0.9, 0.3, 0.04): orange-brown, clips to orange and blooms like the oranges
    public float emission = 10f;
    public float brightness = 1f;                            // 0..1, 0 hides him (lit only where the player looks)
    public bool barking = true;
    public Transform lookAt;                                 // when not barking he turns to face this (the player's head)
    public float barkEvery = 0.9f;
    public float Flare { get; private set; }

    enum Part : byte { Body, Chest, Neck, Head, Jaw, Ear, Tail }
    const float K = 0.0042f;                                 // metres per sketch unit: the body is about 130 units long
    static readonly Vector3 HeadPivot = new Vector3(218f, 112f, 0f), JawPivot = new Vector3(250f, 95f, 0f), TailPivot = new Vector3(98f, 100f, 0f);

    Vector3[] loc; Part[] part; float[] size;
    ParticleSystem ps; ParticleSystem.Particle[] parts; Material pmat;
    ParticleSystemRenderer prend; float barkT, lastBrightness = -1f, lastFlare = -1f;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        var l = new System.Collections.Generic.List<Vector3>(); var p = new System.Collections.Generic.List<Part>();
        // sketch units: x forward, y down from the top, z sideways; floor at y = 214
        void Blob(Part pt, float cx, float cy, float cz, float rx, float ry, float rz, float tilt, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var q = Random.insideUnitSphere; var v = new Vector3(q.x * rx, q.y * ry, q.z * rz);
                v = Quaternion.Euler(0f, 0f, tilt) * v;
                Add(pt, new Vector3(cx + v.x, cy + v.y, cz + v.z));
            }
        }
        void Add(Part pt, Vector3 s) { l.Add(To(s)); p.Add(pt); }
        void Tri(Part pt, Vector2 a, Vector2 b, Vector2 c, float z, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float u = Random.value, v = Random.value; if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                var t = a + u * (b - a) + v * (c - a);
                Add(pt, new Vector3(t.x, t.y, z + Random.Range(-3f, 3f)));
            }
        }
        float w = particleCount / 2315f;
        int N(int n) => Mathf.Max(4, Mathf.RoundToInt(n * w));
        Blob(Part.Body, 148, 130, 0, 52, 30, 26, 0, N(430));
        Blob(Part.Chest, 196, 126, 0, 32, 36, 22, 0, N(300));
        Blob(Part.Body, 102, 134, 0, 28, 28, 24, 0, N(150));
        foreach (var lg in new[] { new Vector2(200, 14), new Vector2(200, -14), new Vector2(104, 14), new Vector2(104, -14) })
            Blob(Part.Body, lg.x, 175, lg.y, 9, 38, 9, 0, N(95));
        Blob(Part.Neck, 218, 104, 0, 26, 28, 18, 34, N(230));
        Blob(Part.Head, 240, 86, 0, 26, 22, 20, 0, N(260));
        Blob(Part.Head, 266, 92, 0, 17, 7, 9, 0, N(75));      // upper muzzle
        Blob(Part.Jaw, 262, 97, 0, 14, 5, 8, 0, N(40));       // lower jaw
        Tri(Part.Ear, new Vector2(220, 70), new Vector2(234, 40), new Vector2(246, 70), 11, N(85));
        Tri(Part.Ear, new Vector2(238, 70), new Vector2(254, 42), new Vector2(262, 74), -11, N(85));
        for (int i = 0, n = N(230); i < n; i++)               // the curled tail over the back
        {
            float t = -0.5f + i / (float)n * 5.3f, r = 19f + (Random.value - 0.5f) * 14f;
            Add(Part.Tail, new Vector3(98f + Mathf.Cos(t) * r * 0.95f - 6f, 92f - Mathf.Sin(t) * r * 0.95f + 8f * Mathf.Sin(t * 0.5f), Random.Range(-5f, 5f)));
        }
        Blob(Part.Tail, 100, 108, 0, 10, 14, 8, -17, N(50));
        loc = l.ToArray(); part = p.ToArray();

        parts = new ParticleSystem.Particle[loc.Length]; size = new float[loc.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            size[i] = Random.Range(0.008f, 0.02f);
            parts[i].startLifetime = 1000f; parts[i].remainingLifetime = 1000f; parts[i].startSize = size[i];
            { var c = color * Random.Range(0.8f, 1.15f); c.a = 1f; parts[i].startColor = c; }
        }
        var go = new GameObject("DogParticles");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = parts.Length;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        prend = go.GetComponent<ParticleSystemRenderer>();
        prend.sharedMaterial = Resources.Load<Material>("ChainParticle");
        pmat = prend.material;
        pmat.SetTexture("_BaseMap", ParticleFigure.DiscTexture());
        ps.Play();
    }

    // sketch units to dog space: +z forward, +y up, +x sideways, feet at y = 0
    static Vector3 To(Vector3 s) => new Vector3(s.z * K, (214f - s.y) * K, (s.x - 150f) * K);

    void LateUpdate()
    {
        if (ps == null) return;
        float dt = Time.deltaTime;
        barkT += dt;
        if (barking && barkT >= barkEvery) { barkT = 0f; Sfx.Play("dog_bark", transform.position + Vector3.up * 0.3f); }
        float e = !barking ? 0f : barkT < 0.06f ? barkT / 0.06f : Mathf.Exp(-5f * (barkT - 0.06f));   // 0..1, sharp rise, quick decay
        Flare = e;
        if (!barking && lookAt != null)                                                                 // calm: turn to face the player
        {
            var d = lookAt.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 120f * dt);
        }
        float time = Time.time, tremble = (barking ? 3f : 0.3f) * 0.0015f;
        var head = Quaternion.Euler(barking ? 14f - 6f * e : -6f, 0f, 0f);                            // aggressive: head held low and thrust at her, snapping lower on a bark; raised and looking up at the player when calm
        var jaw = Quaternion.Euler(30f * e, 0f, 0f);
        var tail = Quaternion.Euler(0f, barking ? Mathf.Sin(time * 25f) * 2f : Mathf.Sin(time * 13f) * 30f, 0f);   // stiff, quivering tail when aggressive; a happy wag when calm
        var hp = To(HeadPivot); var jp = To(JawPivot); var tp = To(TailPivot);
        var push = new Vector3(0f, 0f, 0.06f * e); var recoil = new Vector3(0f, 0f, barking ? 0.05f * e : 0f);   // every bark lunges the whole body forward at the woman
        var rot = transform.rotation; var pos = transform.position;
        for (int i = 0; i < loc.Length; i++)
        {
            var v = loc[i];
            switch (part[i])
            {
                case Part.Chest: v += push; break;
                case Part.Neck: v = hp + Quaternion.Slerp(Quaternion.identity, head, 0.5f) * (v - hp); break;
                case Part.Head: case Part.Ear: v = hp + head * (v - hp); break;
                case Part.Jaw: v = jp + jaw * (v - jp); v = hp + head * (v - hp); break;
                case Part.Tail: v = tp + tail * (v - tp); break;
            }
            v += recoil;
            v.y += Mathf.Sin(time * 31f + i) * tremble;
            var wp = pos + rot * v;
            parts[i].position = wp; parts[i].startSize = size[i] * (1f + 0.35f * e);
        }
        ps.SetParticles(parts, parts.Length);
        if (!Mathf.Approximately(brightness, lastBrightness) || !Mathf.Approximately(e, lastFlare))
        {
            lastBrightness = brightness; lastFlare = e;
            prend.enabled = brightness > 0.002f;
            pmat.SetColor(BaseColor, Color.white * emission * brightness * (0.8f + 0.6f * e));          // the whole dog flares on each bark
        }
    }

    void OnDestroy() { if (pmat != null) Destroy(pmat); }
}
