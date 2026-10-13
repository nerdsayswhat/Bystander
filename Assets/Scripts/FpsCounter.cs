using UnityEngine;
using UnityEngine.UI;

// Frame counter for checking performance in the headset without the OVR tool. Spawns itself in every
// build while testing, head-locked low in the view. Delete this file for the final build.
public class FpsCounter : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, -0.2f, 1f);    // metres in front of the eyes, below the centre
    public float window = 0.5f;                            // seconds per reading

    Text text;
    float time, worst; int frames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        var go = new GameObject("FpsCounter", typeof(RectTransform), typeof(Canvas));
        go.AddComponent<FpsCounter>();
    }

    void Start()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(600f, 100f);
        rect.localScale = Vector3.one * 0.0007f;   // 600 px is about 0.4 m wide

        var t = new GameObject("Text", typeof(RectTransform), typeof(Text));
        t.transform.SetParent(transform, false);
        var tr = (RectTransform)t.transform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        text = t.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 44;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "fps...";
    }

    void LateUpdate()
    {
        // attach to the head as soon as the camera exists (the rig can finish building after the scene loads)
        if (transform.parent == null && Camera.main != null)
        {
            transform.SetParent(Camera.main.transform, false);
            transform.localPosition = offset;
            transform.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        if (text == null) return;
        float dt = Time.unscaledDeltaTime;
        time += dt; frames++; worst = Mathf.Max(worst, dt);
        if (time < window) return;
        float fps = frames / time;
        text.text = $"{fps:0} fps  {1000f * time / frames:0.0} ms  worst {1000f * worst:0} ms";
        text.color = fps >= 70f ? Color.green : fps >= 55f ? Color.yellow : Color.red;   // assumes the 72 Hz target
        time = 0f; frames = 0; worst = 0f;
    }
}
