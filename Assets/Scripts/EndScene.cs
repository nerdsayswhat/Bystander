using UnityEngine;
using UnityEngine.UI;

// The End and Credits steps of the Event List. Both are light text on black.
// End (15 s): one closing line that fades in and out in front of the player, chosen by what they did in scene 5: if they still did nothing with the chains gone
// (no orange given, no can binned, the dog not calmed) it asks WHAT WAS HOLDING YOU BACK THIS TIME? (the same look as WERE THE CHAINS REALLY BINDING YOU?
// in the cards); if they acted and had never broken the chains it asks WAS IT EVER THE CHAINS?; if they had broken them in scene 2 and acted again it shows
// `brokeLine`, a placeholder until the author decides the words.
// Credits (45 s): the lines rise slowly past the player and fade; the experience ends in the dark. The credit lines below are placeholders in [brackets].
// Self-spawns in the scene "Scene 2" and follows SceneSequencer by step name ("End", "Credits"). No sound yet.
public class EndScene : MonoBehaviour
{
    public string didNothingLine = "WHAT WAS HOLDING YOU\nBACK THIS TIME?";
    public string oneActionLine = "WHAT ABOUT\nTHE REST?";                                    // exactly one of the three things done: a placeholder until the author writes the words (2026-11-23)
    public string actedLine = "WAS IT EVER\nTHE CHAINS?";                                      // acted in scene 5 and never broke the chains in scene 2 (the author, 2026-11-21, for now)
    public string brokeLine = "YOU BROKE FREE,\nWHAT ABOUT THE OTHERS?";                       // broke the chains and did 1 to 3 of the things (author, 2026-11-24)
    // the Credits step opens with this block centred in front of the player (author 2026-11-24): the question, the title that completes it, then the byline; then the rest of the credits rise from below
    public string introQuestion = "WILL YOU ACT?\nOR WILL YOU REMAIN A";
    public string introTitle = "<color=#ffffff55>B Y </color>S T A N D<color=#ffffff55> E R</color>";
    public string introByline = "A narrative piece by Ashwin Fulfagar";
    public float[] introAt = { 0f, 3.5f, 6.5f };   // s at which the question, the title and the byline each start to fade in
    public float introHold = 11f;                             // s the block stays before it fades and the credits start to rise
    public string[] credits =
    {
        "<b>Motion capture</b>", "Performer: Ashwin Fulfagar", "Technician: Shrey Bansal", "",
        "<b>Sound</b>", "Sound Effect by DRAGON-STUDIO from Pixabay", "Sound Effect by freesound_community from Pixabay", "Sound Effect by floraphonic from Pixabay", "Sound Effect by Koi Roylers from Pixabay", "Sound Effect by Official Profile from Pixabay", "Sound Effect by Talha Developer from Pixabay", "",   // the author's list, 2026-11-24 (music credits and the voices still to be written)
        "<b>Made with</b>", "Unity", "Meta XR SDK", "Mixamo animations", "",
        "<b>Special thanks</b>", "Dr. Ana-Desipina Tudor", "Eric Fanghanel", "Klaus Fried", "", "Thank you for watching."
    };
    public float dist = 1.4f;                                // m in front of the player
    public int endFontSize = 56, creditsFontSize = 40;

    enum Mode { None, End, Credits }
    SceneSequencer seq; Transform head; Mode mode; float t, len, findTimer, travel, startY;
    GameObject go, introGo; Text text; CanvasGroup[] introG;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Scene 2" && FindFirstObjectByType<EndScene>() == null)
            new GameObject("EndScene").AddComponent<EndScene>();
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
        var want = Mode.None;
        if (seq != null && head != null && seq.Index >= 0)
        {
            var n = seq.steps[seq.Index].name;
            want = n == "End" ? Mode.End : n == "Credits" ? Mode.Credits : Mode.None;
        }
        if (want != mode) { Clear(); mode = Mode.None; }                                    // the step changed: remove the old text
        if (mode == Mode.None && want != Mode.None && !seq.Busy) { mode = want; Begin(); }  // the new step is visible (its fade-in has finished): show the text
        if (mode == Mode.None || go == null) return;

        t += dt;
        float a = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((len - t) / 1.5f);                // in over 1.5 s, out over the last 1.5 s
        if (mode == Mode.Credits) a = Mathf.Clamp01((t - introHold - 1.5f) / 1f) * Mathf.Clamp01((len - t) / 1.5f);   // the credits stay invisible until the intro has faded out, then fade in as they rise
        text.color = new Color(1f, 1f, 1f, a * (mode == Mode.Credits ? 0.92f : 1f));
        if (mode == Mode.Credits)
        {
            go.transform.position = new Vector3(go.transform.position.x, startY + travel * Mathf.Clamp01((t - introHold - 1.5f) / Mathf.Max(1f, len - introHold - 1.5f)), go.transform.position.z);
            if (introG != null) for (int k = 0; k < 3; k++) if (introG[k] != null) introG[k].alpha = Mathf.Clamp01((t - introAt[k]) / 1.5f) * Mathf.Clamp01((introHold + 1.5f - t) / 1.5f);   // CanvasGroups, because <color> tags ignore the Text's own alpha
        }
    }

    void Begin()
    {
        t = 0f; len = Mathf.Max(5f, seq.steps[seq.Index].autoAdvance);
        var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        bool credits = mode == Mode.Credits;
        string s;
        if (credits) s = string.Join("\n", credits_());
        else
        {
            // author 2026-11-23: by how many of the three things the player did (orange given, can binned, dog calmed), in scene 2 after breaking the chains or in scene 5:
            // none = didNothingLine; exactly one = oneActionLine; two or all three = the ending (brokeLine if they broke the chains, else actedLine)
            int did = (seq.gaveOrange ? 1 : 0) + (seq.binnedCan ? 1 : 0) + (seq.calmedDog ? 1 : 0); bool broke = seq.ChainsBrokenByPlayer;
            s = did == 0 ? didNothingLine : broke ? brokeLine : did == 1 ? oneActionLine : actedLine;   // author 2026-11-24: broke the chains and helped (1 to 3 things) = brokeLine
            Log($"the player did {did} of the three things (chains broken: {broke}): {s.Replace("\n", " ")}");
        }
        go = new GameObject(credits ? "Credits" : "EndLine", typeof(RectTransform), typeof(Canvas));
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        int lines = s.Split('\n').Length;
        float h = credits ? lines * creditsFontSize * 1.35f : 200f;
        ((RectTransform)go.transform).sizeDelta = new Vector2(1100f, h);
        go.transform.localScale = Vector3.one * 0.001f;
        var p = head.position + fwd * dist;
        if (credits)
        {
            float H = h * 0.001f;
            startY = head.position.y - 0.7f - H / 2f; travel = H + 1.4f;                       // starts just below the view and rises until it has gone above it
            p.y = startY;
        }
        go.transform.position = p;
        go.transform.rotation = Quaternion.LookRotation(fwd);
        text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(go.transform, false);
        var r = (RectTransform)text.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        var jost = Resources.Load<Font>("Fonts/Jost-Light");                                  // the film's title font (author 2026-11-24); fallback is the built-in one
        text.font = jost != null ? jost : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = credits ? creditsFontSize : endFontSize;
        text.fontStyle = credits ? FontStyle.Normal : FontStyle.Bold; text.supportRichText = true;
        text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
        text.text = s; text.color = new Color(1f, 1f, 1f, 0f);
        if (credits)
        {
            introGo = new GameObject("CreditsIntro", typeof(RectTransform), typeof(Canvas));
            introGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; 
            ((RectTransform)introGo.transform).sizeDelta = new Vector2(1100f, 400f);
            introGo.transform.localScale = Vector3.one * 0.001f;
            introGo.transform.position = head.position + fwd * dist;                            // eye height, centred: it does not rise
            introGo.transform.rotation = Quaternion.LookRotation(fwd);
            introG = new CanvasGroup[3];
            string[] txt = { introQuestion, introTitle, introByline }; int[] sz = { 40, 84, 34 }; float[] yy = { 120f, -10f, -130f };   // three separate lines, each revealed in turn
            for (int k = 0; k < 3; k++)
            {
                var lg = new GameObject("Line" + k, typeof(RectTransform), typeof(CanvasGroup), typeof(Text));
                lg.transform.SetParent(introGo.transform, false);
                var lr = (RectTransform)lg.transform; lr.sizeDelta = new Vector2(1100f, 200f); lr.anchoredPosition = new Vector2(0f, yy[k]);
                var lt = lg.GetComponent<Text>(); lt.font = text.font; lt.fontSize = sz[k]; lt.supportRichText = true; lt.raycastTarget = false; lt.color = Color.white;
                lt.alignment = TextAnchor.MiddleCenter; lt.horizontalOverflow = HorizontalWrapMode.Wrap; lt.verticalOverflow = VerticalWrapMode.Overflow; lt.text = txt[k];
                introG[k] = lg.GetComponent<CanvasGroup>(); introG[k].alpha = 0f;
            }
            Log("credits roll");
        }
    }

    string[] credits_() => credits != null && credits.Length > 0 ? credits : new[] { "BYSTANDER" };

    void Clear()
    {
        if (go != null) Destroy(go);
        if (introGo != null) Destroy(introGo);
        go = introGo = null; text = null; introG = null;
    }

    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} end {msg}");
}
