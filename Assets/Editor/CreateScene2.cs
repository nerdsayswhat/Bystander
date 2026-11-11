using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds Assets/Scenes/Scene 2.unity: a copy of Scene 1 (camera rig, chain, global volume) with an OldManScene object whose clips are
// assigned from the mocap FBXs. Runs by itself once, the first time the project compiles with this file and Scene 2.unity is missing;
// Bystander > Create Scene 2 runs it again (an existing Scene 2.unity is kept and only the clips are re-assigned). The scene open in the
// editor is not touched: the copy is opened additively, saved and closed.
public static class CreateScene2
{
    const string Src = "Assets/Scenes/Scene 1.unity", Dst = "Assets/Scenes/Scene 2.unity", Dir = "Assets/Animations/MoCap_Animations/Ashwin/", Key = "Bystander.Scene2Created";

    [InitializeOnLoadMethod]
    static void Auto()
    {
        if (!EditorPrefs.GetBool(Key) && !File.Exists(Dst)) EditorApplication.delayCall += Create;
        // a scene made before a part existed is brought up to date by itself (the same operation as the menu): Beat 3 is missing from an older Scene 2.unity
        else if (File.Exists(Dst) && !File.ReadAllText(Dst).Contains("cower:")) EditorApplication.delayCall += Create;
    }

    [MenuItem("Bystander/Create Scene 2")]
    static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Create Scene 2: stop Play mode first"); return; }
        if (!File.Exists(Dst) && !AssetDatabase.CopyAsset(Src, Dst)) { Debug.LogError("Create Scene 2: could not copy " + Src); return; }
        var scene = SceneManager.GetSceneByPath(Dst);
        bool wasOpen = scene.IsValid() && scene.isLoaded;       // Scene 2 may already be open in the editor: then it is edited in place and left open
        if (!wasOpen) scene = EditorSceneManager.OpenScene(Dst, OpenSceneMode.Additive);
        var comp = scene.GetRootGameObjects().Select(g => g.GetComponent<OldManScene>()).FirstOrDefault(c => c != null);
        if (comp == null)
        {
            var go = new GameObject("OldManScene");
            SceneManager.MoveGameObjectToScene(go, scene);
            comp = go.AddComponent<OldManScene>();
        }
        comp.fall = Clip("Oldman_Fall"); comp.reach = Clip("Oldman_ground_reach_loop"); comp.dismiss = Clip("Oldman_dismiss");
        comp.receiveLow = Clip("oldman_receive_low"); comp.receiveMid = Clip("oldman_receive_mid"); comp.receiveHigh = Clip("oldman_receive_high");
        EditorUtility.SetDirty(comp);
        var beat3 = scene.GetRootGameObjects().Select(g => g.GetComponent<WomanDogScene>()).FirstOrDefault(c => c != null);
        if (beat3 == null)
        {
            var go3 = new GameObject("WomanDogScene");
            SceneManager.MoveGameObjectToScene(go3, scene);
            beat3 = go3.AddComponent<WomanDogScene>();
        }
        beat3.cower = Clip("woman_cower_loop"); beat3.getUp = Clip("woman_getup_thanks"); beat3.walkAway = Clip("woman_walkaway_loop");
        EditorUtility.SetDirty(beat3);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        EditorPrefs.SetBool(Key, true);
        Debug.Log($"Create Scene 2: {Dst} is ready (fall {Name(comp.fall)}, reach {Name(comp.reach)}, dismiss {Name(comp.dismiss)}, receive {Name(comp.receiveLow)} / {Name(comp.receiveMid)} / {Name(comp.receiveHigh)}; woman {Name(beat3.cower)} / {Name(beat3.getUp)} / {Name(beat3.walkAway)}). Open it and press Play.");
    }

    static AnimationClip Clip(string fbx) =>
        AssetDatabase.LoadAllAssetsAtPath(Dir + fbx + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    static string Name(AnimationClip c) => c != null ? c.name : "MISSING";
}
