using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.UI;

// The opening, before the chains (Event List: Warning and Tutorial / Beats 1 to 3). Follows the SceneSequencer by step name.
//   Warning:  the photosensitivity warning on a panel in front of the player (the sequencer fades it in and out).
//   Tutorial: three rounds of floating phrases, all within reach from the start and none moving towards the player.
//             Round 1 is a single phrase with the instruction under it; round 2 has three, one at knee height; round 3 has four,
//             one at knee height and two at the ankles, as if picking something up from the ground. Close a fist around a phrase and carry it to your heart (the chest): it dissolves
//             into you with a warm pulse. A held phrase goes in by itself after nudgeAfter seconds and whatever is left when a
//             round times out dissolves where it floats, so a slow player cannot stall the film. After the last one the light
//             goes out and the film moves on.
// The sequencer hides the chains for these two steps (ChainWrap.Hidden), so the hands are free.
// The wording below and the tones are placeholders for the author's own. Spawns itself; AutoSpawn = false turns it off.
public class Opening : MonoBehaviour
{
    const bool AutoSpawn = true;
    const string WarningText =
        "<b>PHOTOSENSITIVITY WARNING</b>\n\n" +
        "Later in this experience there are bright, colourful flashes of light. " +
        "If you are sensitive to flashing lights, or you start to feel unwell, take off the headset and stop.";
    const string HintText = "GRAB IT AND PULL IT TO YOUR HEART";

    // where a phrase floats, relative to the head and its yaw: side (right +), ahead, and y (below the eyes; or above the floor when feet)
    struct Slot
    {
        public string text; public float side, ahead, y; public bool feet;
        public Slot(string text, float side, float ahead, float y, bool feet = false) { this.text = text; this.side = side; this.ahead = ahead; this.y = y; this.feet = feet; }
    }
    const float Knee = 0.5f, Ankle = 0.12f;   // heights above the floor (m) for the low phrases
    static readonly Slot[][] Rounds =       // placeholder phrases: the author writes the real ones
    {
        new[] { new Slot("YOU ARE HERE", 0f, 0.45f, -0.15f) },
        new[] { new Slot("YOU CAN SEE", -0.34f, 0.45f, -0.08f), new Slot("YOU CAN REACH", 0.34f, 0.45f, -0.08f),
                new Slot("YOU CAN HEAR", 0f, 0.35f, Knee, true) },                                        // one at the knees: reach down for it
        new[] { new Slot("YOU CAN SPEAK", 0f, 0.42f, -0.2f), new Slot("YOU CAN HELP", 0.3f, 0.35f, Knee, true),
                new Slot("YOU CAN KNEEL", -0.16f, 0.35f, Ankle, true), new Slot("YOU CAN ACT", 0.16f, 0.35f, Ankle, true) },   // two at the ankles: as if picking something up from the ground
    };
    const int MaxPhrases = 4;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    enum Phase { Idle, Settle, Play, Done }

    class Phrase { public Transform root; public Text text; public Vector3 pos, offset; public int state, hand = -1; public float t, age, pop, seed; public bool toHeart; }   // state: 0 floats, 1 held, 2 dissolving, 3 gone

    public float settle = 3f, roundGap = 1.5f, absorbTime = 1.5f, finishPause = 3f;   // seconds: dark before the first round, between rounds, dissolve, dark after the last
    public float nudgeAfter = 10f, roundCap = 20f;           // seconds before a held phrase goes in by itself; before a round dissolves whatever is left
    public float grabRadius = 0.3f, heartRadius = 0.2f;     // metres: the fist must close this close to the phrase; this close to the heart it dissolves
    public float fistLimit = 0.12f;                          // mean fingertip distance from the wrist (m) below which the hand counts as closed
    public float heartDrop = 0.3f, heartAhead = 0.1f;        // the heart: this far below the head and ahead of it (head yaw only)
    public Color phraseColor = new Color(0.75f, 0.62f, 0.35f), hotColor = new Color(1f, 0.85f, 0.5f);   // soft gold, brighter near the heart

    public string Status
    {
        get
        {
            if (phase == Phase.Idle) return null;
            int left = 0;
            for (int i = 0; i < count; i++) if (phr[i].state != 3) left++;
            return $"round={round + 1}/{Rounds.Length} phase={phase} left={left} L={(holder[0] != null ? 1 : 0)} R={(holder[1] != null ? 1 : 0)}";
        }
    }

    SceneSequencer seq; Transform head, leftT, rightT; SyntheticHand leftS, rightS;
    Text warnText, hintText; Transform warnRoot, hintRoot;
    readonly Phrase[] phr = new Phrase[MaxPhrases]; readonly Phrase[] holder = new Phrase[2];
    ParticleSystem ps; AudioSource[] voices; AudioSource swell; int voice;
    AudioClip arrive, chime, pulse, last;
    Phase phase; int round = -1, count; float phaseT, roundT, warnT, findTimer, startedAt, swellT; bool nexted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (AutoSpawn && FindFirstObjectByType<Opening>() == null) new GameObject("Opening").AddComponent<Opening>();
    }

    void Start()
    {
        warnText = MakeText("WarningPanel", new Vector2(1000f, 700f), 0.0018f, 44, out warnRoot);
        warnText.horizontalOverflow = HorizontalWrapMode.Wrap;
        warnText.color = Color.white; warnText.text = WarningText;
        warnRoot.gameObject.SetActive(false);
        hintText = MakeText("PhraseHint", new Vector2(1000f, 100f), 0.0004f, 40, out hintRoot);
        hintText.text = HintText;
        hintRoot.gameObject.SetActive(false);
        for (int i = 0; i < MaxPhrases; i++)
        {
            var q = new Phrase { seed = i * 1.7f };
            q.text = MakeText("Phrase" + i, new Vector2(800f, 200f), 0.0004f, 64, out q.root);
            q.root.gameObject.SetActive(false);
            phr[i] = q;
        }

        var go = new GameObject("OpeningParticles");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.maxParticles = 400; main.startSpeed = 0f;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));   // every particle shrinks away
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = Resources.Load<Material>("ChainParticle");
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rend.receiveShadows = false;
        rend.maxParticleSize = 1f;
        var mat = rend.material;
        mat.SetColor(BaseColor, Color.white * 3f);
        mat.SetTexture("_BaseMap", ParticleFigure.DiscTexture());
        ps.Play();

        voices = new AudioSource[4];
        for (int i = 0; i < voices.Length; i++)
        {
            var v = new GameObject("OpeningVoice" + i).AddComponent<AudioSource>();
            v.transform.SetParent(transform, false);
            v.playOnAwake = false; v.spatialBlend = 1f; v.spatialize = true; v.minDistance = 1f; v.maxDistance = 20f;
            voices[i] = v;
        }
        swell = gameObject.AddComponent<AudioSource>();      // head-locked tone that swells as a held phrase nears the chest
        swell.clip = Tone(440f, 1f, 0.5f, true); swell.loop = true; swell.spatialBlend = 0f; swell.volume = 0f; swell.playOnAwake = false;
        arrive = Tone(392f, 1.4f, 0.12f); chime = Tone(880f, 1f, 0.2f); pulse = Tone(196f, 1.8f, 0.4f); last = Tone(196f, 4f, 0.4f);   // placeholder tones
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if ((findTimer -= dt) <= 0f) { findTimer = 1f; Find(); }
        if (seq == null || head == null || seq.Index < 0) return;
        string n = seq.steps[seq.Index].name;
        bool inWarning = n.StartsWith("Warning"), inTutorial = n.StartsWith("Tutorial");

        warnRoot.gameObject.SetActive(inWarning);
        if (inWarning)
        {
            if (warnT == 0f) Log("warning shown");
            if ((warnT += dt) < 1.5f)      // placed in front of wherever the head is looking as the step starts; tracking may still be settling
            {
                var f = Flat();
                warnRoot.position = head.position + f * 2f;
                warnRoot.rotation = Quaternion.LookRotation(f);
            }
        }
        else warnT = 0f;

        if (inTutorial) { if (!seq.Busy || phase != Phase.Idle) Tutorial(dt); }
        else if (phase != Phase.Idle) Stop();
        swell.volume = Mathf.MoveTowards(swell.volume, swellT * 0.15f, dt);
        swell.pitch = 1f + swell.volume * 3f;
        if (swell.volume > 0f && !swell.isPlaying) swell.Play();
    }

    void Tutorial(float dt)
    {
        phaseT += dt;
        switch (phase)
        {
            case Phase.Idle: startedAt = Time.time; round = -1; Log("tutorial started"); Go(Phase.Settle); break;
            case Phase.Settle: if (phaseT >= (round < 0 ? settle : roundGap)) Appear(); break;
            case Phase.Play: Play(dt); break;
            case Phase.Done: if (!nexted && phaseT >= finishPause && !seq.Busy) { nexted = true; seq.Next(); } break;
        }
    }

    void Go(Phase to) { phase = to; phaseT = 0f; }

    // a round: all its phrases float in place at once (fading in), where the player can reach them
    void Appear()
    {
        round++; roundT = 0f;
        var slots = Rounds[round];
        var f = Flat(); var right = Vector3.Cross(Vector3.up, f);
        count = slots.Length;
        for (int i = 0; i < count; i++)
        {
            var s = slots[i]; var q = phr[i];
            q.pos = head.position + f * s.ahead + right * s.side;
            q.pos.y = s.feet ? Mathf.Max(s.y, Accessibility.Lift) : head.position.y + s.y;   // seated: nothing lower than lap height
            q.text.text = s.text; q.state = 0; q.hand = -1; q.t = q.age = q.pop = 0f;
            q.root.gameObject.SetActive(true);
        }
        hintRoot.gameObject.SetActive(round == 0);
        Voice(arrive, phr[0].pos);
        Log($"round {round + 1} appears: {count} phrase{(count == 1 ? "" : "s")}");
        Go(Phase.Play);
    }

    void Play(float dt)
    {
        roundT += dt; swellT = 0f;
        // each free hand takes the nearest floating phrase in reach when it closes into a fist
        for (int h = 0; h < 2; h++)
        {
            var t = h == 0 ? leftT : rightT;
            if (holder[h] != null || t == null || !ChainWrap.IsFist(h == 0 ? leftS : rightS, fistLimit)) continue;
            Phrase best = null; float bd = grabRadius;
            for (int i = 0; i < count; i++)
            {
                float d = Vector3.Distance(t.position, phr[i].pos);
                if (phr[i].state == 0 && d < bd) { bd = d; best = phr[i]; }
            }
            if (best == null) continue;
            best.state = 1; best.hand = h; best.offset = best.pos - t.position; best.t = 0f; best.pop = 1f; holder[h] = best;
            Voice(chime, best.pos);
            Log($"\"{best.text.text}\" grabbed by {(h == 0 ? "L" : "R")} after {roundT:0.0}s");
        }

        int open = 0;
        for (int i = 0; i < count; i++)
        {
            var q = phr[i];
            if (q.state == 3) continue;
            q.t += dt; q.age += dt;
            if (q.state == 0 && roundT > roundCap) Absorb(q, false);          // the round timed out: it dissolves where it floats
            else if (q.state == 1)
            {
                var hand = q.hand == 0 ? leftT : rightT;
                bool closed = hand != null && ChainWrap.IsFist(q.hand == 0 ? leftS : rightS, fistLimit * 1.25f);   // a little looser to stay closed than to close
                if (!closed)
                {
                    Log($"\"{q.text.text}\" released at {Vector3.Distance(q.pos, Heart()):0.00} m from the heart");
                    holder[q.hand] = null; q.hand = -1; q.state = 0;
                }
                else
                {
                    q.offset = Vector3.Lerp(q.offset, Vector3.zero, 6f * dt);
                    q.pos = hand.position + q.offset;
                    float d = Vector3.Distance(q.pos, Heart());
                    swellT = Mathf.Max(swellT, Mathf.Clamp01(1f - (d - heartRadius) / 0.4f));
                    if (d < heartRadius || q.t > nudgeAfter) Absorb(q, true);
                }
            }
            else if (q.state == 2)
            {
                if (q.toHeart) q.pos = Vector3.Lerp(q.pos, Heart(), 1f - Mathf.Exp(-6f * dt));
                if (q.t >= absorbTime) { q.state = 3; q.root.gameObject.SetActive(false); continue; }
            }
            Show(q, dt);
            if (q.state != 3 && i == 0 && round == 0) ShowHint(q);
            if (q.state != 3) open++;
        }

        if (open > 0) return;
        hintRoot.gameObject.SetActive(false);
        Log($"round {round + 1} cleared after {roundT:0.0}s");
        if (round + 1 < Rounds.Length) Go(Phase.Settle);
        else { Log($"tutorial done after {Time.time - startedAt:0.0}s"); Go(Phase.Done); }
    }

    // a phrase dissolves: warm particles and a pulse at the heart (or where it floats, when the round timed out)
    void Absorb(Phrase q, bool toHeart)
    {
        Log($"\"{q.text.text}\" {(toHeart ? "reached the heart" : "timed out")} after {q.t:0.0}s");
        var c = toHeart ? Heart() : q.pos;
        for (int i = 0; i < 36; i++)
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = c + Random.insideUnitSphere * 0.06f, velocity = Random.onUnitSphere * Random.Range(0.15f, 0.5f),
                startSize = Random.Range(0.012f, 0.03f), startLifetime = Random.Range(1f, 1.6f),
                startColor = Color.Lerp(new Color(1f, 0.7f, 0.3f), new Color(1f, 0.95f, 0.7f), Random.value)
            }, 1);
        ps.Emit(new ParticleSystem.EmitParams { position = c, velocity = Vector3.zero, startSize = 0.32f, startLifetime = 0.5f, startColor = new Color(1f, 0.6f, 0.25f) * 0.8f }, 1);   // the warm pulse
        bool finale = round == Rounds.Length - 1;
        for (int i = 0; i < count; i++) if (phr[i] != q && phr[i].state < 2) finale = false;
        Voice(finale ? last : pulse, c);
        if (q.hand >= 0) holder[q.hand] = null;
        q.hand = -1; q.state = 2; q.t = 0f; q.toHeart = toHeart;
    }

    // phrase text: faces the head, fades in, brighter near the heart, pops when grabbed, shrinks and fades while it dissolves
    void Show(Phrase q, float dt)
    {
        q.pop = Mathf.MoveTowards(q.pop, 0f, dt * 3f);
        float near = Mathf.Clamp01(1f - (Vector3.Distance(q.pos, Heart()) - heartRadius) / 0.5f);
        float fade = q.state == 2 ? Mathf.Clamp01(1f - q.t / absorbTime) : 1f;
        var col = Color.Lerp(phraseColor, hotColor, near);
        col.a = fade * Mathf.Clamp01(q.age);
        q.text.color = col;
        float bob = q.state == 0 ? Mathf.Sin(Time.time * 1.3f + q.seed) * 0.008f : 0f;
        q.root.position = q.pos + Vector3.up * bob;
        q.root.rotation = Quaternion.LookRotation(q.pos - head.position);
        q.root.localScale = Vector3.one * 0.0004f * (1f + 0.15f * q.pop) * Mathf.Lerp(0.3f, 1f, fade);
    }

    // the instruction sits just above the first phrase and goes with it
    void ShowHint(Phrase q)
    {
        var p = q.pos + Vector3.up * 0.07f;
        hintRoot.position = p;
        hintRoot.rotation = Quaternion.LookRotation(p - head.position);
        var col = new Color(0.8f, 0.8f, 0.8f, 0.8f * Mathf.Clamp01(q.age - 1f) * (q.state == 2 ? Mathf.Clamp01(1f - q.t / absorbTime) : 1f));   // appears a second after the phrase
        hintText.color = col;
    }

    void Stop()
    {
        phase = Phase.Idle; round = -1; nexted = false; phaseT = 0f; swellT = 0f;
        holder[0] = holder[1] = null;
        hintRoot.gameObject.SetActive(false);
        foreach (var q in phr) q.root.gameObject.SetActive(false);
    }

    Vector3 Flat() => Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
    Vector3 Heart() => head.position + Vector3.down * heartDrop + Flat() * heartAhead;

    void Find()
    {
        if (seq == null) seq = FindFirstObjectByType<SceneSequencer>();
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (leftT == null) leftT = GameObject.Find("LeftHandAnchor")?.transform;
        if (rightT == null) rightT = GameObject.Find("RightHandAnchor")?.transform;
        if (leftS == null || rightS == null)
            foreach (var h in FindObjectsByType<SyntheticHand>(FindObjectsSortMode.None))
                if (h.Handedness == Handedness.Left) leftS = h; else rightS = h;
    }

    void Voice(AudioClip clip, Vector3 at)
    {
        var v = voices[voice++ % voices.Length];
        v.transform.position = at; v.clip = clip; v.volume = 1f; v.Play();
    }

    static Text MakeText(string name, Vector2 size, float scale, int fontSize, out Transform root)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = size;
        go.transform.localScale = Vector3.one * scale;
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(go.transform, false);
        var r = (RectTransform)t.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        root = go.transform;
        return t;
    }

    // a soft two-partial tone (placeholder sound); loop = a steady sine that loops without a click
    static AudioClip Tone(float hz, float seconds, float volume, bool loop = false)
    {
        const int rate = 24000;
        int n = (int)(rate * seconds);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate, s = Mathf.Sin(2f * Mathf.PI * hz * t);
            if (!loop) s = (s + 0.5f * Mathf.Sin(3f * Mathf.PI * hz * t)) / 1.5f * (1f - Mathf.Exp(-t * 30f)) * Mathf.Exp(-t * 4f / seconds) * (1f - (float)i / n);   // quick attack, slow fade
            d[i] = s * volume;
        }
        var c = AudioClip.Create("OpeningTone", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} opening {msg}");
}
