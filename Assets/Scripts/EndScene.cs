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
    public string brokeLine = "WAS HELPING\nEVER LOCKED?";                                    // broke the chains in scene 2 and acted again in scene 5 (the author's choice, 2026-11-21, from the idea "helping does not require permission")
    public string[] credits =
    {
        "<b>BYSTANDER</b>", "", "A film by Ashwin Fulfagar", "",
        "<b>Motion capture</b>", "[performers and technicians to be written]", "",
        "<b>Sound</b>", "Sound Effect by DRAGON-STUDIO from Pixabay", "Sound Effect by freesound_community from Pixabay", "Sound Effect by floraphonic from Pixabay", "Sound Effect by Koi Roylers from Pixabay", "Sound Effect by Official Profile from Pixabay", "Sound Effect by Talha Developer from Pixabay", "",   // the author's list, 2026-11-24 (music credits and the voices still to be written)
        "<b>Made with</b>", "Unity", "Meta XR SDK", "Mixamo animations", "",
        "[thanks to be written]", "", "Thank you for watching."
    };
    public float dist = 1.4f;                                // m in front of the player
    public int endFontSize = 56, creditsFontSize = 40;

    enum Mode { None, End, Credits }
    SceneSequencer seq; Transform head; Mode mode; float t, len, findTimer, travel, startY;
    GameObject go; Text text;

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
        text.color = new Color(1f, 1f, 1f, a * (mode == Mode.Credits ? 0.92f : 1f));
        if (mode == Mode.Credits) go.transform.position = new Vector3(go.transform.position.x, startY + travel * Mathf.Clamp01(t / len), go.transform.position.z);
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
            s = did == 0 ? didNothingLine : did == 1 ? oneActionLine : broke ? brokeLine : actedLine;
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
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = credits ? creditsFontSize : endFontSize;
        text.fontStyle = credits ? FontStyle.Normal : FontStyle.Bold; text.supportRichText = true;
        text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
        text.text = s; text.color = new Color(1f, 1f, 1f, 0f);
        if (credits) Log("credits roll");
    }

    string[] credits_() => credits != null && credits.Length > 0 ? credits : new[] { "BYSTANDER" };

    void Clear()
    {
        if (go != null) Destroy(go);
        go = null; text = null;
    }

    static void Log(string msg) => Debug.Log($"BYSTANDER EVENT t={Time.time:0.00} end {msg}");
}
