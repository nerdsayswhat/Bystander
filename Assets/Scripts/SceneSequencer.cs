using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
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

    // lengths in seconds from the Bystander Event List (2026-10-26): the sum of each scene's beats, 13:35 in all on the longest path
    public Step[] steps =
    {
        new Step { name = "Warning", autoAdvance = 10f },
        new Step { name = "Tutorial: phrases to your heart", autoAdvance = 75f },          // 25 + 25 + 25
        new Step { name = "1 Darkness: chained", autoAdvance = 34f },                      // author 2026-10-27: first flash at 10 s, scene 2 at 35 s (34 + the 1 s fade); the Event List said 25 + 45
        new Step { name = "2 Darkness: the three events", autoAdvance = 100f, breakWindow = true },   // 25 + 25 + 50 (author 2026-11-09: the old man and the can sooner; was 45 + 40 + 50 = 135)
        new Step { name = "3 Street: darkness lifts", autoAdvance = 110f, breakWindow = true },       // 50 + 60
        new Step { name = "4 Cards", autoAdvance = 80f },                                  // 25 + 40 + 15
        new Step { name = "5 Darkness: no chains", autoAdvance = 160f },                   // 15 + 50 + 40 + 55
        new Step { name = "6 Street again", autoAdvance = 115f },                          // 45 + 70
        new Step { name = "End", autoAdvance = 15f },
        new Step { name = "Credits", autoAdvance = 45f },
    };
    public int cardsStep = 5, noChainsStep = 6;   // indices into steps
    public int chainsFromStep = 2;                // the warning and the tutorial (steps 0 and 1) have no chains
    public int firstPlaceholder = 4;              // steps from here on are not built yet: their name shows low in the view (step 3, scene 2, has the old man beat so far)
    public float fadeTime = 1f;
    public static readonly bool TestBeat3Only = false;   // build for testing the woman and the dog only: starts at the Scene 2 step (editor or build), no old man, no can, the woman appears after 3 s. Set false for the real film.
    public static readonly bool TestCardsOnly = true;   // build for testing the cards only: starts at the "4 Cards" step (editor or build). Set false for the real film.
    public bool showLabel = true;                // placeholder labels on or off

    // what the player did in scene 5, for scene 6 (the scene content sets these)
    public bool gaveOrange, binnedCan, calmedDog;

    public int Index { get; private set; } = -1;
    public bool Busy => busy;                      // a fade is running: the step has been entered but is not yet visible
    public bool ChainsBrokenByPlayer { get; private set; }
    public bool ShowChainsLine => !ChainsBrokenByPlayer;   // scene 4 shows "WERE THE CHAINS REALLY BINDING YOU?" only if they were never broken

    ChainWrap chains;
    Image fade; Text label;
    Transform fadeRoot, labelRoot;
    float timer; bool busy; int pending = -1;   // pending: a jump asked for mid-transition

    const bool AutoSpawn = true;                  // on 2026-10-26 for the opening (warning, tutorial, scene 1); set to false to test the figures and chains without scene changes

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

        // Scene 2.unity holds everything (it is a copy of Scene 1 plus the old man and the can): pressing Play in the editor begins at "2 Darkness: the three events" to save the wait, a build begins at the warning
        int first = 0;
        if (TestBeat3Only || Application.isEditor && SceneManager.GetActiveScene().name == "Scene 2")   // only in the editor: a build of Scene 2 runs the whole film from the warning
            for (int i = 0; i < steps.Length; i++) if (steps[i].name.StartsWith("2")) { first = i; break; }
        if (TestCardsOnly)
            for (int i = 0; i < steps.Length; i++) if (steps[i].name.StartsWith("4")) { first = i; break; }
        GoTo(first);
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
        if (s.autoAdvance > 0f && Index + 1 < steps.Length && (timer += Time.deltaTime) >= s.autoAdvance) Next();   // the last step (Credits) stays
    }

    public void Next() => GoTo(Index + 1);
    public void GoToCards() { if (Index != cardsStep) GoTo(cardsStep); }   // two scene parts may both ask (the orange given and the can binned): the second is ignored
    public bool deferBreak;                       // scene content sets this while it wants the player to act after the chains break (give the orange to the old man); it then calls GoToCards() itself

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
            chains.Hidden = i < chainsFromStep;
            chains.armed = s.breakWindow;
            if (i == noChainsStep) chains.Release();      // never broken: they disperse quietly as scene 5 begins (no-op if already broken)
        }
        label.text = showLabel && i >= firstPlaceholder && !s.name.StartsWith("4") ? s.name : "";   // the cards (step "4") are built
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
        if (deferBreak) return;                      // the scene lets the player act first and calls GoToCards() when it is done
        if (busy) { pending = cardsStep; return; }   // a transition is running: jump when it ends
        if (Index >= 0 && steps[Index].breakWindow) GoTo(cardsStep);
    }
}
