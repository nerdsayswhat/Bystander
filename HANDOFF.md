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
- Done 2026-10-11: Scene Support and Anchor Support set to Disabled in `Assets/Oculus/OculusProjectConfig.asset` (hands-only stays on). `Assets/#Meta/` is now git-ignored and untracked.
- **Android Meta XR feature risk: fixed 2026-10-11.** The Android settings had no `MetaXRFeature Android` object and five null entries in their feature list (likely the source of the startup NullReferenceExceptions at `FeatureHelpers.cs:243`). Ticking "Meta XR" on the Android tab and saving created `MetaXRFeature Android` (enabled) in `OpenXR Package Settings.asset` and removed the nulls. Still to confirm: restart Unity and check `Editor.log` that the three startup exceptions are gone.
- Still pending: first Android (Development) build, and check the generated manifest has the hand-tracking entries; test on the real headset.
- **Chains (in progress, 2026-10-11):** `Assets/Scripts/ChainWrap.cs`. Add it to an empty GameObject in `Test_Scene`. Builds a fixed-in-world helix of link ellipsoids around the torso, plus a near-taut chain from a wrist cuff on each hand to a belt anchor at the hips (anchors fixed in world; links follow the `LeftHandAnchor`/`RightHandAnchor` transforms every frame). First version felt like "noodle arms" (thin links, floor anchors, no cuffs), so links are now chunky (`linkSize`), cuffs added and anchors moved to the hips. Serialized values on an existing component override new defaults: use Reset on the component. Not yet verified on the headset. Gaps: no glow or rim light, falls back to `Unlit/Color` via `Shader.Find` (assign a real material before the Android build).
- **Strain (written 2026-10-11, untested):** `ChainWrap` now has a chain `reach` (script default 0.35 m, anchor to hand; the saved scene component holds its own value, so set Reach in the Inspector when changing it. Torso radius 0.2). The whole chain (hand links, cuffs, torso wrap) shakes and shifts colour with a 0..1 "resist" value that grows with the hand's distance from the body, starting at `tremorStart` (75%) of `reach` and peaking at `reach + pullRange`; `tremble` is the peak shake. Current working values (script defaults and scene component agree): tremble 0.0009 m, tremorStart 0.75, reach 0.35, pullRange 0.25, bodyBack 0.08, follow 2, torsoRadius 0.2, grey link colour 0.346 and strain colour 0.673. Colours are the user's own grey `linkColor`/`strainColor`. The torso wrap and belt anchors now follow the player's chest (head XZ pulled back `bodyBack` 0.08 m along the facing direction, smoothed by `follow`), because the one-time capture at start did not feel centred. Confirmed on the headset 2026-10-11: the hands stop at the limit. Past it the rendered hand is held at the limit via `SyntheticHand.LockWristPosition` (found with `FindObjectsByType<SyntheticHand>`, wrist lock is position only) while the real hand keeps pulling. The chain links tremble and shift towards `strainColor`, and `LeftStrain`/`RightStrain` (0..1, full at `pullRange` extra metres) are exposed for the break logic. The cuff now follows the held hand. Assumptions to check on the headset: the Comprehensive rig's visible hand is driven by that `SyntheticHand` (a warning is logged if none is found), the OVR hand anchor is close to the wrist, and `reach` feels right. Next: break rule (deliberate grab, about 1 s, near an object) driven by the strain.
- **No locomotion (2026-10-11, untested):** the rig nests Meta's `Locomotor` prefab. `ChainWrap.Start` now disables every `ILocomotionEventBroadcaster` and `ILocomotionEventHandler` in the scene (teleport, turn, slide, step; hands or controllers), so the player can never move. Decision: the film is standing-only, no locomotion at all.
- **Resistance only when reaching away (2026-10-11, untested):** shake and colour were driven by distance from the belt anchor, so bringing the wrists together in front also trembled. `resist` is now multiplied by how far the real hand is from the body axis (horizontal): 0 inside `awayStart` (0.3 m), 1 at `awayFull` (0.45 m). The hand hold at `reach` is unchanged.
- **Trello board "Bystander"** (https://trello.com/b/FzvAEN3U/bystander) has To Do, Doing and Done lists mirroring this file. Workflow: ask before creating a card for new work, then keep Doing updated.
- **Standing instruction:** update this file after every major step.

## Testing with the XR Operator
Tools appear as `mcp__metavr__xr_openxr_*`, in sessions started after the registration. Unity must be in Play mode with the Meta XR Simulator active (otherwise Play mode fails with `XR_ERROR_FORM_FACTOR_UNAVAILABLE`). `metavr` is registered at local scope through `.claude/metavr-proxy.mjs`, a stopgap for a protocol mismatch between metavr 1.8 and Claude Code 2.1.292. To undo: `claude mcp remove metavr -s local`, then `metavr mcp install claude-code --execute -y`. The Unity console can be read through `Editor.log` (`%LOCALAPPDATA%\Unity\Editor\Editor.log`). The `unity` CLI bridge cannot reach the editor because the Pipeline package is not installed.

## Gotcha
The Claude desktop app redirects writes under `%APPDATA%` into its own private folder, so tools installed there from inside the app are invisible to Unity. Install such things from a normal terminal.

## Open story questions
- What does the old man do when handed the orange? Not a second thank-you; the woman already thanks the player. Idea: he walks off toward the hospital.
- The can has no card. Write one, or make the gap deliberate.
- Scene 6 only punishes inaction with litter and a scared woman. Idea: reuse the cards at the end and finish Stephen's and Julia's sentences if the player helped.
- The woman "gets up" in scene 5, but is never shown down in scene 2.
- Scene 5 says "pick up" the ball, scene 6 says "distract" the dog. Is the ball thrown or just held?

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
