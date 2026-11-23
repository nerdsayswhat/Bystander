using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Bakes the real body surface of the characters in Assets/Characters into Assets/Resources/Bodies/*.asset (BodySurface): about 4000 random points over the skinned meshes
// (weighted by triangle area, in the bind pose), each with its two strongest humanoid bones and its offset in their space. Runs by itself once when an asset is missing
// and the FBX is already a Humanoid; Bystander > Bake Bodies runs it again (after a change of character).
public static class BodyBaker
{
    const string Dir = "Assets/Characters/", Out = "Assets/Resources/Bodies/"; const int Samples = 4000;

    [InitializeOnLoadMethod]
    static void Auto()
    {
        bool missing = (File.Exists(Dir + "Slim-Man.fbx") && !File.Exists(Out + "Body_SlimMan.asset")) || (File.Exists(Dir + "Slim-woman.fbx") && !File.Exists(Out + "Body_SlimWoman.asset"));
        if (missing && !EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.delayCall += Bake;
    }

    [MenuItem("Bystander/Bake Bodies")]
    static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Bake Bodies: stop Play mode first"); return; }
        Directory.CreateDirectory(Out);
        BakeOne("Slim-Man", "Body_SlimMan"); BakeOne("Slim-woman", "Body_SlimWoman");
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }

    static void BakeOne(string fbx, string asset)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + fbx + ".fbx");
        if (model == null) { Debug.LogWarning("Bake Bodies: " + Dir + fbx + ".fbx is not imported yet"); return; }
        var go = Object.Instantiate(model); go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var anim = go.GetComponent<Animator>();
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman) { Debug.LogWarning("Bake Bodies: " + fbx + " is not a Humanoid yet (Rig > Animation Type); reimport it and run Bystander > Bake Bodies"); return; }
            var human = new Dictionary<Transform, int>();
            for (int b = 0; b < (int)HumanBodyBones.LastBone; b++) { var t = anim.GetBoneTransform((HumanBodyBones)b); if (t != null) human[t] = b; }
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            // every triangle of every skinned mesh (but the eyelashes and eyebrows), in world space, with its area
            var tw = new List<Vector3[]>(); var tidx = new List<(SkinnedMeshRenderer smr, int[] v)>(); var area = new List<float>(); float total = 0f;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                string n = smr.name.ToLowerInvariant(); if (n.Contains("lash") || n.Contains("brow")) continue;
                var m = smr.sharedMesh; if (m == null) continue; var v = m.vertices; var tri = m.triangles; var l2w = smr.transform.localToWorldMatrix;
                for (int i = 0; i + 2 < tri.Length; i += 3)
                {
                    Vector3 a = l2w.MultiplyPoint3x4(v[tri[i]]), b = l2w.MultiplyPoint3x4(v[tri[i + 1]]), c = l2w.MultiplyPoint3x4(v[tri[i + 2]]);
                    float ar = Vector3.Cross(b - a, c - a).magnitude * 0.5f; if (ar < 1e-9f) continue;
                    tw.Add(new[] { a, b, c }); tidx.Add((smr, new[] { tri[i], tri[i + 1], tri[i + 2] })); total += ar; area.Add(total);
                }
            }
            if (area.Count == 0) { Debug.LogWarning("Bake Bodies: no skinned mesh in " + fbx); return; }
            var rnd = new System.Random(7);
            var s = ScriptableObject.CreateInstance<BodySurface>(); s.model = model;
            s.boneA = new int[Samples]; s.boneB = new int[Samples]; s.offA = new Vector3[Samples]; s.offB = new Vector3[Samples]; s.wB = new float[Samples];
            var acc = new Dictionary<int, float>(); var boneCache = new Dictionary<(SkinnedMeshRenderer, int), int>();
            int Humanoid(SkinnedMeshRenderer smr, int bi)                                            // a mesh bone as a humanoid bone (a non-humanoid helper bone goes to its nearest humanoid parent)
            {
                if (boneCache.TryGetValue((smr, bi), out int r)) return r;
                var t = smr.bones[bi]; r = (int)HumanBodyBones.Hips;
                while (t != null) { if (human.TryGetValue(t, out int h)) { r = h; break; } t = t.parent; }
                boneCache[(smr, bi)] = r; return r;
            }
            for (int k = 0; k < Samples; k++)
            {
                float pick = (float)rnd.NextDouble() * total; int lo = 0, hi = area.Count - 1;
                while (lo < hi) { int mid = (lo + hi) / 2; if (area[mid] < pick) lo = mid + 1; else hi = mid; }
                var tri3 = tw[lo]; var (smr, vi) = tidx[lo];
                float u = (float)rnd.NextDouble(), w = (float)rnd.NextDouble(); if (u + w > 1f) { u = 1f - u; w = 1f - w; }
                var p = tri3[0] + u * (tri3[1] - tri3[0]) + w * (tri3[2] - tri3[0]);
                var bw = smr.sharedMesh.boneWeights; acc.Clear();
                float[] bary = { 1f - u - w, u, w };
                for (int c = 0; c < 3; c++)
                {
                    var b = bw[vi[c]];
                    void Add(int bi, float wt) { if (wt <= 0f) return; int h = Humanoid(smr, bi); acc[h] = (acc.TryGetValue(h, out float o) ? o : 0f) + wt * bary[c]; }
                    Add(b.boneIndex0, b.weight0); Add(b.boneIndex1, b.weight1); Add(b.boneIndex2, b.weight2); Add(b.boneIndex3, b.weight3);
                }
                int ba = (int)HumanBodyBones.Hips, bb = -1; float wa = -1f, wbb = 0f;
                foreach (var kv in acc) if (kv.Value > wa) { wa = kv.Value; ba = kv.Key; }
                foreach (var kv in acc) if (kv.Key != ba && kv.Value > wbb) { wbb = kv.Value; bb = kv.Key; }
                var ta = anim.GetBoneTransform((HumanBodyBones)ba) ?? hips; s.boneA[k] = ba; s.offA[k] = ta.InverseTransformPoint(p);
                if (bb >= 0 && wbb / (wa + wbb) > 0.08f) { var tb = anim.GetBoneTransform((HumanBodyBones)bb) ?? hips; s.boneB[k] = bb; s.offB[k] = tb.InverseTransformPoint(p); s.wB[k] = wbb / (wa + wbb); }
                else { s.boneB[k] = ba; s.offB[k] = s.offA[k]; s.wB[k] = 0f; }
            }
            AssetDatabase.CreateAsset(s, Out + asset + ".asset");
            Debug.Log($"Bake Bodies: {asset} written ({Samples} points on {tidx.Count} triangles)");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
