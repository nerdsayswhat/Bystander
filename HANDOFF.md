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
- **Visual rule:** living things are particles, objects are solid. People, the dog and the baby are faceless particle clouds. The orange, can, ball, bin, bicycle and stroller are solid simple shapes, no textures, flat self-lit color with a rim light. Chains are solid glowing links. The player's hands are particles too. Objects are only lit while the player looks at them. The area the player helps becomes a lit, clean circle.
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
- Pushed on `Test`. Scene `Test_Scene` is still empty (camera, light, volume).
- Packages: Meta XR All-in-One SDK 207.0.0, OpenXR 1.18.0. XR Hands 1.7.3 is present only as a dependency of the Meta SDK. XR Interaction Toolkit was removed.
- Git LFS tracks models, textures, audio and video. `.gitignore` excludes keystores, `Assets/Resources/DevAgentSettings.asset` (holds a personal token and LAN address), `Assets/XR/APILayers~/` and `TempAssembly.dll`.
- Known harmless noise: three NullReferenceExceptions at editor startup from Unity's OpenXR feature refresh. They don't affect Play mode or the XR Operator. Decided to leave them alone.
- Next for the author in Unity: add the OVRCameraRig and Hand Tracking Building Blocks, set hand tracking to **Hands Only** on OVRManager (this also adds the Android hand-tracking permission), and accept the Project Setup Tool fixes.
- Next for Claude: GazeTarget and a test scene, the chain-break logic, particle hands on the hand bones, the scene sequencer, then test with the XR Operator.

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
