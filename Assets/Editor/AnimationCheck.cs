using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Menu: Bystander > Animation Check. Enters Play mode by itself (the animation graph does not evaluate in edit mode), plays
// every clip under Assets/Animations on the figure's skeleton, stops Play mode and writes to <project>/AnimationChecks/:
//   <clip>.png       contact sheet: 8 moments across the clip, top row front view, bottom row side view, centred on where
//                    the hips start; left side cyan, right side orange, floor line grey, figure pinned to the floor like ParticleFigure
//   <clip>_curve.csv time, hips x/y/z, head y, lowest bone y (30 samples a second)
//   report.md        one row per clip plus warnings (loop pop, travel, wrong rig)
// Delete the file when the animations are signed off.
public static class AnimationCheck
{
    const int Samples = 8, CellW = 200, CellH = 300;
    const float Fps = 30f, ViewW = 2.4f, ViewH = 2.0f, Floor = 0.1f, Pin = 0.05f;

    static readonly HumanBodyBones[][] Links =
    {
        new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head },
        new[] { HumanBodyBones.Chest, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
        new[] { HumanBodyBones.Chest, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
        new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes },
        new[] { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes },
    };
    static readonly Color32[] LinkColor = { new Color32(235, 235, 235, 255), new Color32(60, 200, 255, 255), new Color32(255, 150, 40, 255), new Color32(60, 200, 255, 255), new Color32(255, 150, 40, 255) };

    class Result { public string name, path; public float length; public bool loopFlag, human; public List<string> notes = new List<string>(); public float travelEnd, travelMax, hipsStart, hipsMin, hipsMax, hipsEnd, headMin, lowStart, loopGap; }

    // the animation graph only evaluates in Play mode: the menu item enters Play mode, the check runs on the third frame, then Play mode stops
    const string Flag = "AnimationCheck.run";

    [MenuItem("Bystander/Animation Check")]
    static void Start() { EditorPrefs.SetBool(Flag, true); EditorApplication.isPlaying = true; }

    [InitializeOnLoadMethod]
    static void Arm()
    {
        if (!EditorPrefs.GetBool(Flag) || !EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorPrefs.DeleteKey(Flag);
        int frames = 0;
        void Tick()
        {
            if (!EditorApplication.isPlaying || ++frames < 3) return;
            EditorApplication.update -= Tick;
            try { Run(); } finally { EditorApplication.isPlaying = false; }
        }
        EditorApplication.update += Tick;
    }

    static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefab/ParticleFigure.prefab");
        var model = prefab != null ? prefab.GetComponent<ParticleFigure>().model : null;
        if (model == null) { Debug.LogError("AnimationCheck: the ParticleFigure prefab has no model"); return; }

        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "AnimationChecks"));
        Directory.CreateDirectory(dir);
        var results = new List<Result>();
        var paths = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Animations" });
        for (int i = 0; i < paths.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(paths[i]);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            EditorUtility.DisplayProgressBar("Animation Check", path, i / (float)paths.Length);
            int n = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                string label = Path.GetFileNameWithoutExtension(path) + (n++ > 0 ? "_" + n : "");
                try { results.Add(Check(model, clip, label, path, dir)); }
                catch (System.Exception e) { Debug.LogError("AnimationCheck: " + label + ": " + e); }
            }
        }
        EditorUtility.ClearProgressBar();
        WriteReport(results, dir);
        Debug.Log("AnimationCheck: " + results.Count + " clips written to " + dir);
    }

    static Result Check(GameObject model, AnimationClip clip, string label, string assetPath, string dir)
    {
        var r = new Result { name = label, path = assetPath, length = clip.length, human = clip.isHumanMotion, loopFlag = AnimationUtility.GetAnimationClipSettings(clip).loopTime };
        if (!r.human) { r.notes.Add("not a humanoid clip: it cannot move the figure"); return r; }

        var go = Object.Instantiate(model, Vector3.zero, Quaternion.identity);
        go.hideFlags = HideFlags.HideAndDontSave;
        var graph = default(PlayableGraph);
        try
        {
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            if (anim.avatar == null || !anim.avatar.isHuman) { r.notes.Add("the model is not humanoid"); return r; }
            anim.applyRootMotion = false;
            graph = PlayableGraph.Create("AnimationCheck");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var pl = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput.Create(graph, "anim", anim).SetSourcePlayable(pl);

            var bones = new List<HumanBodyBones>();
            foreach (var l in Links) foreach (var b in l) if (!bones.Contains(b) && anim.GetBoneTransform(b) != null) bones.Add(b);
            var tr = bones.ConvertAll(b => anim.GetBoneTransform(b));
            int hipsI = bones.IndexOf(HumanBodyBones.Hips), headI = bones.IndexOf(HumanBodyBones.Head);

            int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * Fps) + 1);
            var pos = new Vector3[frames][];
            var csv = new StringBuilder("time,hipsX,hipsY,hipsZ,headY,lowestBoneY\n");
            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(clip.length, f / Fps);
                pl.SetTime(t); graph.Evaluate(0f);
                pos[f] = new Vector3[tr.Count];
                float low = float.MaxValue;
                for (int k = 0; k < tr.Count; k++) { pos[f][k] = tr[k].position; low = Mathf.Min(low, pos[f][k].y); }
                var h = pos[f][hipsI];
                csv.Append(t.ToString("0.000")).Append(',').Append(h.x.ToString("0.000")).Append(',').Append(h.y.ToString("0.000")).Append(',').Append(h.z.ToString("0.000")).Append(',').Append(pos[f][headI].y.ToString("0.000")).Append(',').Append(low.ToString("0.000")).Append('\n');
            }
            File.WriteAllText(Path.Combine(dir, label + "_curve.csv"), csv.ToString());

            // numbers
            var h0 = pos[0][hipsI];
            r.hipsStart = h0.y; r.hipsMin = r.hipsMax = h0.y; r.headMin = pos[0][headI].y; r.lowStart = Lowest(pos[0]);
            for (int f = 0; f < frames; f++)
            {
                var h = pos[f][hipsI];
                float d = new Vector2(h.x - h0.x, h.z - h0.z).magnitude;
                r.travelMax = Mathf.Max(r.travelMax, d); r.travelEnd = d;
                r.hipsMin = Mathf.Min(r.hipsMin, h.y); r.hipsMax = Mathf.Max(r.hipsMax, h.y); r.hipsEnd = h.y;
                r.headMin = Mathf.Min(r.headMin, pos[f][headI].y);
            }
            // loop pop: how far the last pose is from the first, with the hips taken out (a travelling walk is fine)
            var a = pos[0]; var z = pos[frames - 1]; float sum = 0f;
            for (int k = 0; k < a.Length; k++) sum += ((a[k] - a[hipsI]) - (z[k] - z[hipsI])).magnitude;
            r.loopGap = sum / a.Length;

            if (r.loopFlag && r.loopGap > 0.12f) r.notes.Add("loop pop: the last pose differs from the first by " + r.loopGap.ToString("0.00") + " m on average");
            if (r.loopFlag && r.travelEnd > 0.3f) r.notes.Add("loops but travels " + r.travelEnd.ToString("0.00") + " m: it will walk away and snap back");
            if (!r.loopFlag && r.name.ToLower().Contains("loop")) r.notes.Add("named loop but Loop Time is off");
            if (r.travelMax > 0.5f) r.notes.Add("travels up to " + r.travelMax.ToString("0.00") + " m from where it starts");
            if (r.hipsMax - r.hipsMin < 0.02f && r.travelMax < 0.02f && r.loopGap < 0.02f) r.notes.Add("almost no motion: check the rig mapping");

            DrawSheet(label, dir, pos, hipsI, tr.Count, bones, frames);
        }
        finally { if (graph.IsValid()) graph.Destroy(); Object.DestroyImmediate(go); }
        return r;
    }

    static float Lowest(Vector3[] p) { float m = float.MaxValue; foreach (var v in p) m = Mathf.Min(m, v.y); return m; }

    static void DrawSheet(string label, string dir, Vector3[][] pos, int hipsI, int count, List<HumanBodyBones> bones, int frames)
    {
        var tex = new Texture2D(Samples * CellW, 2 * CellH, TextureFormat.RGBA32, false);
        var px = new Color32[tex.width * tex.height];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(8, 8, 12, 255);
        var c0 = pos[0][hipsI];
        for (int s = 0; s < Samples; s++)
            for (int view = 0; view < 2; view++)
            {
                var p = pos[Mathf.RoundToInt(s / (float)(Samples - 1) * (frames - 1))];
                int ox = s * CellW, oy = (1 - view) * CellH;   // top row = front view
                float pin = Pin - Lowest(p), cx = view == 0 ? c0.x : c0.z;
                Vector2Int P(int k) => new Vector2Int(ox + CellW / 2 + Mathf.RoundToInt(((view == 0 ? p[k].x : p[k].z) - cx) / ViewW * CellW), oy + Mathf.RoundToInt((p[k].y + pin + Floor) / ViewH * CellH));
                Line(px, ox, oy + Mathf.RoundToInt(Floor / ViewH * CellH), ox + CellW - 1, oy + Mathf.RoundToInt(Floor / ViewH * CellH), new Color32(90, 90, 100, 255), ox, oy);
                for (int l = 0; l < Links.Length; l++)
                    for (int b = 0; b + 1 < Links[l].Length; b++)
                    {
                        int ia = bones.IndexOf(Links[l][b]), ib = bones.IndexOf(Links[l][b + 1]);
                        if (ia < 0 || ib < 0) continue;
                        var A = P(ia); var B = P(ib);
                        Line(px, A.x, A.y, B.x, B.y, LinkColor[l], ox, oy);
                        Dot(px, B.x, B.y, LinkColor[l], ox, oy);
                    }
                Line(px, ox, oy, ox, oy + CellH - 1, new Color32(40, 40, 50, 255), ox, oy);   // cell border
            }
        tex.SetPixels32(px);
        File.WriteAllBytes(Path.Combine(dir, label + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // clipped to the cell (ox, oy)
    static void Put(Color32[] px, int x, int y, Color32 c, int ox, int oy)
    {
        if (x < ox || x >= ox + CellW || y < oy || y >= oy + CellH) return;
        px[y * (Samples * CellW) + x] = c;
    }

    static void Dot(Color32[] px, int x, int y, Color32 c, int ox, int oy)
    {
        for (int i = -2; i <= 2; i++) for (int j = -2; j <= 2; j++) Put(px, x + i, y + j, c, ox, oy);
    }

    static void Line(Color32[] px, int x0, int y0, int x1, int y1, Color32 c, int ox, int oy)
    {
        int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
        for (int guard = 0; guard < 4000; guard++)
        {
            Put(px, x0, y0, c, ox, oy); Put(px, x0 + 1, y0, c, ox, oy);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    static void WriteReport(List<Result> rs, string dir)
    {
        var sb = new StringBuilder("# Animation check\n\nHeights in metres (hips and head are bone positions; the figure is pinned to the floor at runtime, so lift above the floor is not an error). travel = hips distance from the first frame on the floor plane. loopGap = mean bone offset between last and first pose with the hips removed.\n\n");
        sb.Append("| clip | length s | loop flag | hips start/min/max/end | head min | travel max/end | loopGap | notes |\n|---|---|---|---|---|---|---|---|\n");
        foreach (var r in rs)
            sb.Append("| ").Append(r.name).Append(" | ").Append(r.length.ToString("0.0")).Append(" | ").Append(r.loopFlag ? "loop" : "once").Append(" | ")
              .Append(r.hipsStart.ToString("0.00")).Append('/').Append(r.hipsMin.ToString("0.00")).Append('/').Append(r.hipsMax.ToString("0.00")).Append('/').Append(r.hipsEnd.ToString("0.00")).Append(" | ")
              .Append(r.headMin.ToString("0.00")).Append(" | ").Append(r.travelMax.ToString("0.00")).Append('/').Append(r.travelEnd.ToString("0.00")).Append(" | ").Append(r.loopGap.ToString("0.00")).Append(" | ")
              .Append(r.human ? string.Join("; ", r.notes) : "NOT HUMANOID").Append(" |\n");
        File.WriteAllText(Path.Combine(dir, "report.md"), sb.ToString());
    }
}
