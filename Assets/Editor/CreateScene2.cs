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
        else if (File.Exists(Dst))
        {
            var text = File.ReadAllText(Dst);                                                       // an older Scene 2.unity is brought up to date by itself: a part or a street clip list is missing
            if (!text.Contains("cower:") || !text.Contains("idleMan:") || !text.Contains("scaredWalk:") || !text.Contains("walkMix:") || !text.Contains("talkClips:") || !text.Contains("cowerClip:") || !text.Contains("punchClips:")) EditorApplication.delayCall += Create;
        }
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
        var cards = scene.GetRootGameObjects().Select(g => g.GetComponent<CardsScene>()).FirstOrDefault(c => c != null);
        if (cards == null)
        {
            var go4 = new GameObject("CardsScene");
            SceneManager.MoveGameObjectToScene(go4, scene);
            cards = go4.AddComponent<CardsScene>();
        }
        cards.idleMan = ClipAt("Assets/Animations/Mixamo/standing_idle_1.fbx"); cards.idleWoman = ClipAt("Assets/Animations/Mixamo/standing_idle_2.fbx");
        EditorUtility.SetDirty(cards);
        var street = scene.GetRootGameObjects().Select(g => g.GetComponent<StreetScene>()).FirstOrDefault(c => c != null);
        if (street == null)
        {
            var go5 = new GameObject("StreetScene");
            SceneManager.MoveGameObjectToScene(go5, scene);
            street = go5.AddComponent<StreetScene>();
        }
        street.idle = ClipAt("Assets/Animations/Mixamo/standing_idle_1.fbx"); street.fallen = ClipAt("Assets/Animations/Mixamo/lying_in_pain.fbx");
        street.cowerClip = beat3.cower; street.idle2 = ClipAt("Assets/Animations/Mixamo/standing_idle_2.fbx"); street.idle3 = ClipAt("Assets/Animations/Mixamo/standing_idle_3.fbx"); street.lookUp = ClipAt("Assets/Animations/Mixamo/looking_up.fbx");
        street.pickUp = ClipAt("Assets/Animations/Mixamo/pick_up.fbx"); street.walk = ClipAt("Assets/Animations/Mixamo/walking_medium_pace.fbx"); street.walkOld = ClipAt("Assets/Animations/Mixamo/old_man_walk.fbx"); street.gather = Clip("oldman_gather_loop");
        const string M = "Assets/Animations/Mixamo/";
        street.talkClips = new[] { ClipAt(M + "Talking.fbx"), ClipAt(M + "Talking (1).fbx"), ClipAt(M + "Talking (2).fbx"), ClipAt(M + "Talking (3).fbx") }; street.textClips = new[] { ClipAt(M + "Texting While Standing.fbx") };
        street.runClips = new[] { ClipAt(M + "Running.fbx"), ClipAt(M + "Running (1).fbx"), ClipAt(M + "Look Behind Run.fbx") }; street.punchClips = new[] { ClipAt(M + "Punching.fbx"), ClipAt(M + "Punching (1).fbx"), ClipAt(M + "Punching (2).fbx"), ClipAt(M + "Punching (3).fbx") }; street.hitClips = new[] { ClipAt(M + "Taking Punch.fbx"), ClipAt(M + "Receiving An Uppercut.fbx") }; street.reactClips = new[] { ClipAt(M + "Reacting.fbx"), ClipAt(M + "Surprised.fbx") }; street.sitClips = new[] { ClipAt(M + "Sitting Talking Looking left.fbx"), ClipAt(M + "Sitting Talking looking right.fbx") };
        street.walkMix = new[] { street.walk, ClipAt(M + "Walking While Texting.fbx"), ClipAt(M + "Walking With Shopping Bag.fbx") }; street.arguing = ClipAt(M + "Standing Arguing.fbx");
        street.kneel = ClipAt("Assets/Animations/Mixamo/kneeling.fbx"); street.stabbed = Clip("stabbed_ground_loop"); street.scaredWalk = Clip("Woman_scared_walk_loop");
        EditorUtility.SetDirty(street);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        EditorPrefs.SetBool(Key, true);
        Debug.Log($"Create Scene 2: {Dst} is ready (fall {Name(comp.fall)}, reach {Name(comp.reach)}, dismiss {Name(comp.dismiss)}, receive {Name(comp.receiveLow)} / {Name(comp.receiveMid)} / {Name(comp.receiveHigh)}; woman {Name(beat3.cower)} / {Name(beat3.getUp)} / {Name(beat3.walkAway)}). Open it and press Play.");
    }

    static AnimationClip Clip(string fbx) =>
        AssetDatabase.LoadAllAssetsAtPath(Dir + fbx + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    static AnimationClip ClipAt(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    static string Name(AnimationClip c) => c != null ? c.name : "MISSING";
}
