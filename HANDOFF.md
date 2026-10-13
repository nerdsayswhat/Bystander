# Bystander: handoff notes

Interactive VR film for Meta Quest 3. Unity 6000.3.8f1, Meta XR SDK 207, OpenXR. Hands tracking only, standing, no voice-over. Work happens on the `Test` branch (never `main`). The author commits and pushes themselves in GitHub Desktop; Claude never commits and never adds a Co-Authored-By line.

The script is the Google Doc named "Bystander" (in the author's Drive). Its text is color-coded: orange = left, blue = right, purple = top, green = bottom, magenta = behind, yellow highlight = player action. "In front" has no color.

## Story structure
1. Darkness, player is chained. Flashes and sound above (the "spectacle").
2. Three events, found by looking: an old man's oranges spill (left, at feet), a can is kicked (behind, lands at feet next to a bin), a woman is scared of a barking dog (right) with a ball between them. The narrator lies: "You cannot do anything but look at it." The old man says "Don't bother."
3. Darkness lifts: a street full of problems, all out of reach. A few people help, most just stand. A big spectacle above.
4. Darkness again, cards appear (photos with a name and a one-line story, for example Stephen and his wife who loves oranges, and Julia on her way to a job interview), then "WERE THE CHAINS REALLY BINDING YOU?"
5. Darkness again, no chains. The player can pick up the orange and give it to the old man, put the can in the bin, pick up the ball so the dog calms and the woman walks away. Or do nothing.
6. The street again. Unhelped things have visibly got worse (squished fruit, more trash, the woman walking scared). If the player acted, the area around them is clean. The spectacle continues.

## Decisions made
- **Chain break:** if the player reaches out and grabs any object before the cards, the chains strain, then break. The scene goes straight to the cards (scene 4) without the "WERE THE CHAINS..." line, then pass two (scene 5). The break needs a deliberate grab (hand closed near the object for about a second), and the chains should strain visibly first because hands get no force feedback. Idea: keep the rendered hand at the chain limit while the real hand pulls on, then snap.
- **Cards play for everyone.**
- **Player stays isolated in the dark** during the choices. That is deliberate: it stands for our blindness to the world around us.
- **Scale gap is intentional:** a stabbing and an abandoned baby on one side, a soda can on the other. Only the vicinity is reachable.
- **Visual rule:** living things are particles, objects are solid. People, the dog and the baby are faceless particle clouds. The orange, can, ball, bin, bicycle and stroller are solid simple shapes, no textures, flat self-lit color with a rim light. Chains are solid glowing links. The player's hands are NOT particles for now (decided 2026-10-11): they stay Meta's normal rendered hands. Objects are only lit while the player looks at them. The area the player helps becomes a lit, clean circle.
- **Pose carries the meaning** since there are no faces: fallen = pooled low, stabbed = a thin trail from one point, baby = tiny tight cluster plus crying, scared woman = flinching jitter, bystanders = still columns.
- **Cards** are the only place faces appear. Idea: the particles that were the person assemble into a portrait. Use portraits with clear rights.
- **Mocap** may be used for the old man, the woman and the stabbed person (about 12 to 15 short clips in total, mostly loops). The dog, baby, stroller, chains and the player's hands are not mocap. Freeze the story beats before any capture session.
- **Photosensitivity:** the opening flashes need to stay under 3 per second, small, below full brightness, with a warning screen.

## Gaze (Quest 3 has no eye tracking, so gaze means head direction)
Use empty GameObjects as targets and an angle check, not a physical cone. One empty per yellow "look" in the script: "look up" is an empty high above, "look behind" an empty behind, and so on. The same empty carries the spatial sound source. Tune the angles on the headset.

```csharp
using UnityEngine;
using UnityEngine.Events;

public class GazeTarget : MonoBehaviour
{
    public float innerAngle = 12f, outerAngle = 30f, dwell = 0.5f;   // tune in the headset
    public UnityEvent onGazed;                  // fires once after the dwell
    public float Weight { get; private set; }   // 0..1, drives the "lit by attention" glow
    float t; bool fired;

    void Update()
    {
        var head = Camera.main.transform;
        float a = Vector3.Angle(head.forward, transform.position - head.position);
        Weight = Mathf.InverseLerp(outerAngle, innerAngle, a);   // 1 inside inner, 0 outside outer
        t = a < innerAngle ? t + Time.deltaTime : 0f;
        if (!fired && t >= dwell) { fired = true; onGazed.Invoke(); }
    }
}
```

Still needed: a fallback if the player never looks (sound repeats louder, target flickers, then the scene moves on after a few seconds).

## Project state (2026-10-11)
- On `Test`. `Test_Scene` now has the Meta Camera Rig, the Comprehensive Interaction Rig (hands) and one grabbable cube at about `0, 1.0, 0.45` (Unity world, forward is +Z). Hand grab verified working with real hands over Quest Link on 2026-10-11.
- Packages: Meta XR All-in-One SDK 207.0.0, OpenXR 1.18.0. XR Hands 1.7.3 is present only as a dependency of the Meta SDK. XR Interaction Toolkit was removed.
- Git LFS tracks models, textures, audio and video. `.gitignore` excludes keystores, `Assets/Resources/DevAgentSettings.asset` (holds a personal token and LAN address), `Assets/XR/APILayers~/` and `TempAssembly.dll`.
- Known harmless noise: three NullReferenceExceptions at editor startup from Unity's OpenXR feature refresh. They don't affect Play mode or the XR Operator. Decided to leave them alone.
- Done in Unity: Camera Rig, Hand Tracking and Hand Grab blocks added, hand tracking set to Hands Only (saved in `OculusProjectConfig`), Meta XR OpenXR feature group enabled.
- Next for Claude: chain-break logic (reach for an object before the cards breaks the chains), particle hands on the hand bones, the scene sequencer, then GazeTarget. Test with the XR Operator in a fresh session.
- Done 2026-10-11: Scene Support and Anchor Support set to Disabled in `Assets/Oculus/OculusProjectConfig.asset` (hands-only stays on).
- **RESOLVED 2026-10-13 (second rebuild, 01:01):** the packaged manifest no longer has `USE_SCENE`, `USE_ANCHOR_API` or `RECORD_AUDIO`. Remaining permissions: `INTERNET`, `ACCESS_NETWORK_STATE`, `MODIFY_AUDIO_SETTINGS`, `BLUETOOTH` and `HAND_TRACKING`, and `oculus.software.handtracking` is still required. `OculusProjectConfig.asset` stayed at 0 for both support values, and `OculusProjectSetupSettings.asset` now stores the Ignore for the MR Utility Kit task (new key `dd7fa12a...`, Android and Standalone). Do not press Fix All in the Project Setup Tool. The notes below explain the history.
- **Rebuild result 2026-10-13 (earlier):** `RECORD_AUDIO` is gone (the manifest edit survived the build) and hand tracking is intact, but `USE_SCENE` and `USE_ANCHOR_API` are still requested because `OculusProjectConfig.asset` flipped back to `anchorSupport: 1`, `sceneSupport: 2`. **Cause:** the MR Utility Kit package (part of the All-in-One SDK) adds a Required Project Setup Tool task, "MR Utility Kit recommends Scene Support to be set to Required" (`OVRProjectSetupMRUK.cs`), that is not satisfied whenever the scene has an `OVRCameraRig`, and its fix sets scene support to Required and anchor support to Enabled. Something applies that fix (the Fix button, Fix All, or a build prompt). **Fix:** set both values to 0 with Unity closed, then in Unity open the Project Setup Tool (Edit, Project Settings, Meta XR) and use Ignore on that one task, on both the Android and PC tabs, so it is stored in `Assets/Oculus/OculusProjectSetupSettings.asset` and never auto-applied. Never press Fix All.
- **Fixed 2026-10-13 (needs a rebuild to confirm):** `anchorSupport` and `sceneSupport` set back to 0 in `OculusProjectConfig.asset`, and `RECORD_AUDIO` removal added to `Assets/Plugins/Android/AndroidManifest.xml` (Meta's manifest preprocessor may rewrite that file on build, so re-read the packaged manifest after the next build). If the support values flip back, find what flips them. Original finding follows.
- **Regression found 2026-10-13:** `OculusProjectConfig.asset` has `anchorSupport: 1` and `sceneSupport: 2` again (changed in commit `b77da4e`, from 0 and 0 in `9e8379d`), so the Android manifest requests `USE_SCENE` and `USE_ANCHOR_API` and the headset may show a spatial-data prompt. Something re-enables them (likely a Meta SDK component or the build step). Fix: set both to 0 with Unity closed, then check whether they flip back and what flips them. The manifest also requests `RECORD_AUDIO` (Meta SDK default, no voice input in the film), which can be removed with a `tools:node="remove"` entry in `Assets/Plugins/Android/AndroidManifest.xml`. `Assets/#Meta/` is now git-ignored and untracked.
- **Android Meta XR feature risk: fixed 2026-10-11.** The Android settings had no `MetaXRFeature Android` object and five null entries in their feature list (likely the source of the startup NullReferenceExceptions at `FeatureHelpers.cs:243`). Ticking "Meta XR" on the Android tab and saving created `MetaXRFeature Android` (enabled) in `OpenXR Package Settings.asset` and removed the nulls. Still to confirm: restart Unity and check `Editor.log` that the three startup exceptions are gone.
- **First Android (Development) build: done 2026-10-13.** The author reports everything works on the Quest 3 (hands, chains, strain, chain material). The generated manifest's hand-tracking entries were not inspected separately, but hand tracking works on the device. Next device checks: the particle budget once figures exist, and the break rule.
- **Chains (in progress, 2026-10-11):** `Assets/Scripts/ChainWrap.cs`. Add it to an empty GameObject in `Test_Scene`. Builds a fixed-in-world helix of link ellipsoids around the torso, plus a near-taut chain from a wrist cuff on each hand to a belt anchor at the hips (anchors fixed in world; links follow the `LeftHandAnchor`/`RightHandAnchor` transforms every frame). First version felt like "noodle arms" (thin links, floor anchors, no cuffs), so links are now chunky (`linkSize`), cuffs added and anchors moved to the hips. Serialized values on an existing component override new defaults: use Reset on the component. Not yet verified on the headset. Gaps: no glow or rim light, falls back to `Unlit/Color` via `Shader.Find` (assign a real material before the Android build).
- **Strain (written 2026-10-11, untested):** `ChainWrap` now has a chain `reach` (script default 0.35 m, anchor to hand; the saved scene component holds its own value, so set Reach in the Inspector when changing it. Torso radius 0.2). The whole chain (hand links, cuffs, torso wrap) shakes and shifts colour with a 0..1 "resist" value that grows with the hand's distance from the body, starting at `tremorStart` (75%) of `reach` and peaking at `reach + pullRange`; `tremble` is the peak shake. Current working values (script defaults and scene component agree): tremble 0.0009 m, tremorStart 0.75, reach 0.35, pullRange 0.25, bodyBack 0.08, follow 2, torsoRadius 0.2, grey link colour 0.346 and strain colour 0.673. Colours are the user's own grey `linkColor`/`strainColor`. The torso wrap and belt anchors now follow the player's chest (head XZ pulled back `bodyBack` 0.08 m along the facing direction, smoothed by `follow`), because the one-time capture at start did not feel centred. Confirmed on the headset 2026-10-11: the hands stop at the limit. Past it the rendered hand is held at the limit via `SyntheticHand.LockWristPosition` (found with `FindObjectsByType<SyntheticHand>`, wrist lock is position only) while the real hand keeps pulling. The chain links tremble and shift towards `strainColor`, and `LeftStrain`/`RightStrain` (0..1, full at `pullRange` extra metres) are exposed for the break logic. The cuff now follows the held hand. Assumptions to check on the headset: the Comprehensive rig's visible hand is driven by that `SyntheticHand` (a warning is logged if none is found), the OVR hand anchor is close to the wrist, and `reach` feels right. Next: break rule (deliberate grab, about 1 s, near an object) driven by the strain.
- **No locomotion (2026-10-11, untested):** the rig nests Meta's `Locomotor` prefab. `ChainWrap.Start` now disables every `ILocomotionEventBroadcaster` and `ILocomotionEventHandler` in the scene (teleport, turn, slide, step; hands or controllers), so the player can never move. Decision: the film is standing-only, no locomotion at all.
- **Resistance only when reaching away (2026-10-11, untested):** shake and colour were driven by distance from the belt anchor, so bringing the wrists together in front also trembled. `resist` is now multiplied by how far the real hand is from the body axis (horizontal): 0 inside `awayStart` (0.3 m), 1 at `awayFull` (0.45 m). The hand hold at `reach` is unchanged.
- **Trello board "Bystander"** (https://trello.com/b/FzvAEN3U/bystander) has To Do, Doing and Done lists mirroring this file. Workflow: ask before creating a card for new work, then keep Doing updated.
- **Standing instruction:** update this file after every major step.

## Review 2026-10-11 (script vs build vs Trello, for a November start)
- **Break rule conflict:** the doc has the objects at the player's feet, and the chain reach is 0.35 m from a belt anchor about 1 m above the floor, so a hand can never get "near" an object. Trigger the break from strain plus a closed hand held for about 1 s, not from proximity. Decide how that picks "an object" (any strain plus grab is simplest).
- **Chain material on Android: fixed 2026-10-11, confirmed working on the Quest 3 build 2026-10-13.** `Assets/Materials/ChainLink.mat` (URP Unlit, grey) is assigned to `ChainWrap.linkMaterial` in `Test_Scene`, so the shader ships in builds. The script now sets the URP `_BaseColor` through the property block (the old `_Color` is ignored by URP Unlit) and its fallback uses `Universal Render Pipeline/Unlit`. Check that Unity imports the material without errors.
- **Not on Trello yet:** the film's content. Scenes 2 to 6 assets (orange, can, ball, bin as simple shapes; particle people, dog, baby, stroller, bicycle), the cards scene and portraits, spatial audio, the darkness-lift lighting, scene 5 pick-up and hand-over triggers, the acted/not-acted state for scene 6, the ending line (the doc has an open comment about it), a Quest 3 performance budget for the street scenes, and mocap planning. Nothing has a due date.
- **"You cannot do anything but look at it"** in the doc is for the author only, not shown to the player. Ignore it. There is no voice-over.
- **Schedule (set 2026-10-11):** submission deadline 26 Nov; the whole app should be finished by the second week of November (card due 13 Nov) so thesis work can start. Every Trello card has a due date, working back from that: Android build 14 Oct, mocap decision 15 Oct, chain break 16 Oct, story decisions 17 Oct, sequencer 18 Oct, gaze and portraits 20 Oct, photosensitivity 22 Oct, scene 1 on 23 Oct, particle people 25 Oct, scene 2 on 28 Oct, cards scene 30 Oct, scene 5 on 2 Nov, audio 4 Nov, mocap 5 Nov, street scene 3 on 6 Nov, scene 6 on 9 Nov, performance 11 Nov, playthrough 13 Nov, submission upload 24 Nov (two-day buffer).
- **Pace:** the hard mechanic (chains, strain, hand hold, no locomotion) is done in 5 days, but nothing has been built or run on the headset itself (all testing was over Link). The two street scenes and the mocap clips are the long pole; the darkness scenes (1, 2, 4, 5) are cheap and make a good first slice.

## Thesis (state on 2026-10-12)
- **Where it is:** Claude Doc "Bystander: Thesis (rough draft)": https://claude.ai/code/artifact/5fa69431-52fd-4fa4-97b6-9f0969ac19d3. Edit that doc, never recreate it. A Word copy is exported to `Bystander_Thesis.docx` in the project root (git-ignored; 12 pt, 1.5 spacing, page numbers, A4). Re-export after any change. Method: the docs `export` tool (docx, a4) saves its base64 result to a file; decode it, then patch the zip: 1.5 line spacing (`w:line="360"`) on every paragraph, 12 pt (`sz 24`) in table cells and in the Normal style (reuse `styles.xml` and `footer1.xml` from the previous docx), a PAGE-number footer, A4 page size. Last re-exported 2026-10-12 from doc rev 404.
- **Structure:** Abstract, acknowledgements, lists, 1 Introduction, 2 State of the art, 3 Experience, 4 Creative process, 5 Technical choices (kept shallow on purpose), 6 Critical discussion, 7 Conclusions, numbered references, AI Declaration (the author's own text). Two tables (scenes, versions) and Figure 1 (scene flow diagram). About 6,200 words of main text without tables; the brief is 6,000 to 8,000, excluding appendices and bibliography. Appendices and the UX studies section were removed.
- **Rules the author set:** write in the style of their own reports (Birthday, Design Immersive Experience, Critical Practice and Exploration, Project Report), never the lit review's style. No bracketed notes or questions in the doc. Only references from the author's own documents (25, numbered; in-text citations are numbered [n]); do not add references, sources or co-authors without asking. Never mention audience testing, user testing or participant studies. Placeholders allowed: `[DOI link]` is mostly filled by the author already.
- **Story history** (author's Downloads folder): `FMP Pitch_Bystander.pdf`, `Proposal.pdf` (May 2026), `BYSTANDER_Revised_2026.docx` (June 2026, the Cylinder), the Google Doc "Bystander" (current script). The lit review is a fact source only, and some of its quotes and dates did not check out.
- **State of the claims:** gaze targets, the break rule and the scene sequencer are not built, and section 5.2 says so. Update it when they exist. The thesis frames viewer effects as intentions, not results.
- **Open for the author:** the AI Declaration only mentions code, while the report text was drafted with AI help, so it needs to say so and follow UAL's rules. The state of the art has no XR, media or performance art section. Check the facts and DOIs of references written from memory. The critical arguments in 2.2, 2.3, 2.4, 6.1 and 6.3 were drafted by Claude, so the author must agree with them. Headset captures for figures are still to add.
- A recurring risk: a reviewer's AI-pattern audit flagged repeated central claims, "not X but Y" contrasts, tidy closing sentences and a theory-to-feature rhythm. Do not chase detector scores; fix substance.

## Mocap plan (drafted 2026-10-12, not frozen)
- **Capture (13 clips):** old man (fall and drop bag, reach for oranges on the ground, "Don't bother" dismissal, receive the orange, gather oranges when ignored), woman (cower from dog, get up and thank, walk away scared, scared walk for scene 6, pull back from the barking dog), stabbed person (stagger holding side, collapse, weak movement on the ground).
- **Found online (about 10 generic loops):** idle and standing variants, looking up at the spectacle, phone use, hurried walk, normal walk, elderly walk, crouch and pick up, throw or drop litter, fallen-person idle, kneeling beside someone.
- **Not mocap:** dog, baby, stroller, bicycle, chains, the player's hands, the spectacle.
- Hand-over of the orange must meet the player's real hand, so plan IK or a few reach heights. The full list lives in the 2026-10-12 chat; the Trello "Animation: mocap capture and import (if used)" card is the home for it.

## Testing with the XR Operator
Tools appear as `mcp__metavr__xr_openxr_*`, in sessions started after the registration. Unity must be in Play mode with the Meta XR Simulator active (otherwise Play mode fails with `XR_ERROR_FORM_FACTOR_UNAVAILABLE`). `metavr` is registered at local scope through `.claude/metavr-proxy.mjs`, a stopgap for a protocol mismatch between metavr 1.8 and Claude Code 2.1.292. To undo: `claude mcp remove metavr -s local`, then `metavr mcp install claude-code --execute -y`. The Unity console can be read through `Editor.log` (`%LOCALAPPDATA%\Unity\Editor\Editor.log`). The `unity` CLI bridge cannot reach the editor because the Pipeline package is not installed.

## Gotcha
The Claude desktop app redirects writes under `%APPDATA%` into its own private folder, so tools installed there from inside the app are invisible to Unity. Install such things from a normal terminal.

## Story questions, answered 2026-10-12
- The old man takes the orange and walks back into the darkness (tentative, not important).
- The can has no card. Not important.
- Scene 6 keeps showing consequences for inaction on purpose. The first drafts wanted inaction to cause no visible harm, but the author wants to show that inaction has consequences too. Reusing the cards at the end (finishing Stephen's and Julia's sentences) is only an idea; the thesis lists it as future work.
- The woman is not shown down in scene 2.
- The ball is picked up, not thrown (for now).
- Running time: about 15 minutes at most.
- The people are faceless because they are strangers to the player. The darkness shows our own blindness to everything around us.
- The Cylinder ending was dropped: it was too vague and gave no context or build-up. Its infinite principle became the expanding crowd (the people in the street who go on as far as the player can see, and the cards that keep appearing without end).

## Hard-won findings (2026-10-11)
- **The fix that made everything work: enable the "Meta XR" feature group** in Project Settings, XR Plug-in Management, OpenXR, on both the PC (Windows) tab and the Android tab. Without it Meta's OVRManager never initializes (`OVRManager.OVRManagerinitialized` stays false), the OVRCameraRig stays frozen, and no hands or controllers reach the app. Symptoms were a head-locked grey image, no hands, input "None". Unity's own XR input still tracked the head, which is how it was diagnosed. Meta's SDK shows a one-time "Enable Meta XR Feature Set" dialog that is easy to miss.
- **Testing over Quest Link or the simulator:** the Game view must be the focused window while testing. Typing in the Claude app steals focus. Workaround: the author presses Alt+Tab once after putting the headset on (or clicks the Game view), then Claude reads the log. Claude cannot see the headset under Link (capture and set-pose tools are removed there), so Unity's log is the window.
- **Reading Unity's output:** `%LOCALAPPDATA%\Unity\Editor\Editor.log`. Debug.Log lines appear there. `GrabLogger.cs` (Assets/Scripts) logs grab and release only; delete it when no longer wanted.
- **XR Operator caveats:** its MCP connection drops when the app session changes (for example after switching between the simulator and Link); only a new Claude session reconnects it. It needs Play mode with the Meta XR Simulator active. A skybox-only view looks identical for any yaw, so "identical screenshots" proved nothing; put a visible object in view when testing.
- **Cube placement:** the original building-block cube sat at the origin, which is under the player's feet (floor-level tracking origin). A cube at head height at the origin ends up inside the player's face.
- **Files kept out of git:** `Assets/#Meta/` (machine and per-run IDs), `Assets/Resources/DevAgentSettings.asset` (personal token and LAN address), `Assets/XR/APILayers~/`, `TempAssembly.dll`, keystores. All are in `.gitignore`.
- **The three startup NullReferenceExceptions** from Unity's OpenXR feature refresh are still there and still harmless. A cleanup attempt of stale XR Hands entries did not fix them and was reverted.

## Starting a fresh session
1. Commit and push the current work first (GitHub Desktop, branch `Test`, leave `Assets/#Meta` unticked).
2. New session in `C:\Ashwin\Unity\Bystander`. Memory notes and this file load automatically.
3. For XR Operator work: open Unity, switch the Meta XR Simulator on, press Play, click the Game view, then ask Claude to check the operator tools.
