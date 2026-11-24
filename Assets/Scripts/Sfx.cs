using System.Collections.Generic;
using UnityEngine;

// The film's sound effects (author 2026-11-24: sound work starts now; every sound is listed in the Google Doc "Bystander: sound effects to gather").
// A sound is just a file in Assets/Resources/Audio/ with the agreed name, for example Audio/dog_bark.wav. Several takes of one sound are named
// dog_bark, dog_bark_2, dog_bark_3 ... and one is picked at random each time (never the same twice in a row). A file that is not there yet is silently skipped
// (and named once in the log as "sfx: missing"), so the film plays exactly as before until the sounds arrive.
// EVERY sound is directional (distance-based 3D through the Meta XR Audio spatializer) unless the author says otherwise (2026-11-24): pass spatial: false only on request.
//   Sfx.Play("bag_hit", pos)                      one shot at a position
//   var h = Sfx.Loop("woman_breath", pos)         a loop; h.Volume = 0.5f; h.Seek(0.3f); h.Attach(transform); h.Stop()
//   far sounds: Play / Loop take maxDist and minDist (sirens: 150 and 10) so the level falls with distance
public static class Sfx
{
    public class Handle
    {
        public AudioSource src; public bool alive;
        public float Volume { set { if (src != null) src.volume = Mathf.Clamp01(value * Master); } }
        public Vector3 Position { set { if (src != null) src.transform.position = value; } }
        public void Seek(float fraction) { if (src != null && src.clip != null) src.time = Mathf.Clamp01(fraction) * (src.clip.length - 0.05f); }   // start a loop part-way in, so copies are not in step
        public void Attach(Transform parent) { if (src != null) src.transform.SetParent(parent, true); }                                              // the source moves with it (the street root sinks)
        public void Stop() { if (src != null) Object.Destroy(src.gameObject); src = null; alive = false; }
    }

    static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
    static readonly Dictionary<string, int> last = new Dictionary<string, int>();
    public static float Master = 1f;

    static AudioClip Pick(string name)
    {
        if (!cache.TryGetValue(name, out var takes))
        {
            var list = new List<AudioClip>();
            var one = Resources.Load<AudioClip>("Audio/" + name); if (one != null) list.Add(one);
            for (int i = 2; i < 12; i++) { var c = Resources.Load<AudioClip>("Audio/" + name + "_" + i); if (c != null) list.Add(c); }
            takes = list.ToArray(); cache[name] = takes;
            if (takes.Length == 0) Debug.Log("BYSTANDER EVENT sfx: missing Audio/" + name);
        }
        if (takes.Length == 0) return null;
        int k = Random.Range(0, takes.Length);
        if (takes.Length > 1 && last.TryGetValue(name, out var l) && k == l) k = (k + 1) % takes.Length;
        last[name] = k; return takes[k];
    }

    static AudioSource Make(AudioClip clip, Vector3 pos, float vol, bool spatial, bool loop, float maxDist, float minDist, float delay = 0f)
    {
        var go = new GameObject("Sfx " + clip.name); go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip; s.volume = Mathf.Clamp01(vol * Master); s.loop = loop; s.spatialBlend = spatial ? 1f : 0f; s.spatialize = spatial;   // spatialize: through the Meta XR Audio plugin (Project Settings > Audio)
        s.minDistance = minDist; s.maxDistance = maxDist; s.rolloffMode = AudioRolloffMode.Logarithmic; s.dopplerLevel = 0f;
        if (delay > 0f) s.PlayDelayed(delay); else s.Play();
        return s;
    }

    public static void Play(string name, Vector3 pos, float vol = 1f, bool spatial = true, float maxDist = 40f, float minDist = 1f, float delay = 0f)
    {
        var c = Pick(name); if (c == null) return;
        var s = Make(c, pos, vol, spatial, false, maxDist, minDist, delay); Object.Destroy(s.gameObject, c.length + delay + 0.2f);
    }

    public static Handle Loop(string name, Vector3 pos, float vol = 1f, bool spatial = true, float maxDist = 40f, float minDist = 1f)
    {
        var c = Pick(name); var h = new Handle();
        if (c == null) return h;
        h.src = Make(c, pos, vol, spatial, true, maxDist, minDist); h.alive = true; return h;
    }
}
