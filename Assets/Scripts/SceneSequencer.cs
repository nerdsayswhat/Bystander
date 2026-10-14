using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Runs the film: the six scenes in order, a fade to black between them, and the chain-break shortcut.
// Spawns itself; add one to the scene to edit the steps. Scene content hooks into each step's onEnter/onExit
// and calls Next() when its scene is done (autoAdvance moves on by itself until the scenes are built).
public class SceneSequencer : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        public string name;
        public float autoAdvance;     // seconds before moving on by itself; 0 = wait for Next()
        public bool breakWindow;      // the chains can be broken during this step ("from scene 2 until the cards")
        public UnityEvent onEnter, onExit;
    }

    public Step[] steps =
    {
        new Step { name = "1 Darkness: chained", autoAdvance = 10f },
        new Step { name = "2 Darkness: the three events", autoAdvance = 15f, breakWindow = true },
        new Step { name = "3 Street: darkness lifts", autoAdvance = 12f, breakWindow = true },
        new Step { name = "4 Cards", autoAdvance = 12f },
        new Step { name = "5 Darkness: no chains", autoAdvance = 15f },
        new Step { name = "6 Street again", autoAdvance = 12f },
        new Step { name = "End" },
    };
    public int cardsStep = 3, noChainsStep = 4;   // indices into steps
    public float fadeTime = 1f;
    public bool showLabel = true;                 // placeholder: the step name low in the view while the scenes are not built

    // what the player did in scene 5, for scene 6 (the scene content sets these)
    public bool gaveOrange, binnedCan, calmedDog;

    public int Index { get; private set; } = -1;
    public bool ChainsBrokenByPlayer { get; private set; }
    public bool ShowChainsLine => !ChainsBrokenByPlayer;   // scene 4 shows "WERE THE CHAINS REALLY BINDING YOU?" only if they were never broken

    ChainWrap chains;
    Image fade; Text label;
    Transform fadeRoot, labelRoot;
    float timer; bool busy; int pending = -1;   // pending: a jump asked for mid-transition

    const bool AutoSpawn = false;                 // switched off for now: no scene changes while the figures and chains are being tested; set to true to run the film

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (!AutoSpawn) return;
        if (FindFirstObjectByType<SceneSequencer>() == null) new GameObject("SceneSequencer").AddComponent<SceneSequencer>();
    }

    void Start()
    {
        chains = FindFirstObjectByType<ChainWrap>();
        if (chains != null) chains.onBroken.AddListener(OnBroken);
        else Debug.LogWarning("SceneSequencer: no ChainWrap in the scene; the chain-break shortcut is off");

        // fade: a black image just in front of the eyes, drawn over everything (ZTest Always)
        var f = NewCanvas("SequencerFade", 1600f, 100);
        fadeRoot = f.transform;
        fade = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fade.transform.SetParent(f.transform, false);
        var r = (RectTransform)fade.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        var mat = new Material(Canvas.GetDefaultCanvasMaterial());
        mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        fade.material = mat;
        fade.raycastTarget = false;
        fade.color = Color.black;                 // starts black and fades in

        var l = NewCanvas("SequencerLabel", 600f, 0);
        labelRoot = l.transform;
        label = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(l.transform, false);
        var lr = (RectTransform)label.transform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 44;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        GoTo(0);
    }

    static Canvas NewCanvas(string name, float size, int sorting)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.overrideSorting = true; c.sortingOrder = sorting;
        ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
        go.transform.localScale = Vector3.one * 0.001f;
        return c;
    }

    void LateUpdate()
    {
        // attach to the head as soon as the camera exists
        var cam = Camera.main;
        if (cam == null || fadeRoot == null) return;
        if (fadeRoot.parent != cam.transform)
        {
            fadeRoot.SetParent(cam.transform, false); fadeRoot.localPosition = new Vector3(0f, 0f, 0.35f); fadeRoot.localRotation = Quaternion.identity;
            labelRoot.SetParent(cam.transform, false); labelRoot.localPosition = new Vector3(0f, -0.3f, 1f); labelRoot.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        if (busy || Index < 0) return;
        var s = steps[Index];
        if (s.autoAdvance > 0f && (timer += Time.deltaTime) >= s.autoAdvance) Next();
    }

    public void Next() => GoTo(Index + 1);

    public void GoTo(int i)
    {
        if (busy || i < 0 || i >= steps.Length) return;
        StartCoroutine(Go(i));
    }

    IEnumerator Go(int i)
    {
        busy = true;
        if (Index >= 0)
        {
            yield return FadeTo(1f);
            steps[Index].onExit?.Invoke();               // null when the steps come from the code defaults, not the Inspector
        }
        Index = i; timer = 0f;
        var s = steps[i];
        if (chains != null)
        {
            chains.armed = s.breakWindow;
            if (i == noChainsStep) chains.Release();      // never broken: they disperse quietly as scene 5 begins (no-op if already broken)
        }
        label.text = showLabel ? s.name : "";
        s.onEnter?.Invoke();
        yield return FadeTo(0f);
        busy = false;
        if (pending >= 0) { int j = pending; pending = -1; GoTo(j); }
    }

    IEnumerator FadeTo(float alpha)
    {
        float from = fade.color.a;
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, alpha, t / fadeTime));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, alpha);
    }

    // the player broke the chains: inside the break window the darkness closes in and the film jumps to the cards,
    // without the "WERE THE CHAINS REALLY BINDING YOU?" line (ShowChainsLine is false)
    void OnBroken()
    {
        ChainsBrokenByPlayer = true;
        if (busy) { pending = cardsStep; return; }   // a transition is running: jump when it ends
        if (Index >= 0 && steps[Index].breakWindow) GoTo(cardsStep);
    }
}
