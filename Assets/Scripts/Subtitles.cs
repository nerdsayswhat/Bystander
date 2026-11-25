using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Subtitles for the spoken dialogue only (author 2026-11-25): the old man's "Don't bother" and "Thank you" and the woman's "Thank you". Not for any other sound.
// Subtitles.Say(clipName, text, delay) is called next to the Sfx call of a line: the text shows for the length of that clip (plus a moment), a little below
// where the player looks (it follows the head slowly, so it is always readable and never in the way), white with a dark outline, fading in and out.
// Self-spawns in the scene "Scene 2".
public class Subtitles : MonoBehaviour
{
    public float dist = 1.5f, below = 0.32f, follow = 4f;     // m in front of the head, m below eye level, how quickly it follows the head (per second)
    public int fontSize = 56;

    struct Line { public string text; public float start, end; }
    static Subtitles inst;
    readonly List<Line> lines = new List<Line>();
    Transform head; GameObject go; Text text; CanvasGroup group; float findTimer; Vector3 pos; bool placed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Scene 2" && FindFirstObjectByType<Subtitles>() == null)
            inst = new GameObject("Subtitles").AddComponent<Subtitles>();
    }

    // the line starts `delay` seconds from now and stays for as long as the clip lasts
    public static void Say(string clipName, string text, float delay = 0f)
    {
        if (inst == null) inst = FindFirstObjectByType<Subtitles>();
        if (inst == null) return;
        var clip = Resources.Load<AudioClip>("Audio/" + clipName);
        float len = clip != null ? clip.length : 2.5f, s = Time.time + delay;
        inst.lines.Add(new Line { text = text, start = s, end = s + len + 0.4f });
    }

    void Update()
    {
        if ((findTimer -= Time.deltaTime) <= 0f) { findTimer = 1f; if (head == null && Camera.main != null) head = Camera.main.transform; }
        if (head == null) return;
        Line? cur = null;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].end < Time.time) { lines.RemoveAt(i); continue; }
            if (lines[i].start <= Time.time) cur = lines[i];
        }
        if (cur == null) { if (go != null) group.alpha = 0f; return; }
        if (go == null) Build();
        var c = cur.Value;
        text.text = c.text;
        group.alpha = Mathf.Clamp01((Time.time - c.start) / 0.2f) * Mathf.Clamp01((c.end - Time.time) / 0.3f);
        var f = Vector3.ProjectOnPlane(head.forward, Vector3.up); if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(head.up, Vector3.up); f.Normalize();
        var want = head.position + f * dist + Vector3.down * below;
        pos = placed ? Vector3.Lerp(pos, want, 1f - Mathf.Exp(-follow * Time.deltaTime)) : want; placed = true;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(pos - head.position);
    }

    void Build()
    {
        go = new GameObject("SubtitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        group = go.GetComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
        ((RectTransform)go.transform).sizeDelta = new Vector2(1400f, 160f);
        go.transform.localScale = Vector3.one * 0.001f;
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
        t.transform.SetParent(go.transform, false);
        var r = (RectTransform)t.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        text = t.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false; text.color = Color.white;
        var o = t.GetComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(3f, -3f);
    }
}
