using System.Collections;
using Oculus.Interaction.Input;
using Oculus.Interaction.Locomotion;
using UnityEngine;

// Chains binding the player: a fixed helix around the torso plus a chain from a wrist cuff on each
// hand to a belt anchor. The chain has a limited reach: past it the rendered hand is held at the
// limit (SyntheticHand wrist lock) while the real hand keeps pulling, and the chain strains.
// Add to an empty GameObject in the scene (rig at world origin).
public class ChainWrap : MonoBehaviour
{
    public Material linkMaterial;                  // flat emissive; falls back to Unlit/Color
    public Color linkColor = new Color(0.34591186f, 0.34591186f, 0.34591186f), strainColor = new Color(0.67295593f, 0.67295593f, 0.67295593f);
    public float torsoRadius = 0.2f, torsoHeight = 0.6f, torsoTurns = 3f, linkSpacing = 0.07f;
    public Vector3 linkSize = new Vector3(0.05f, 0.035f, 0.1f);   // chunky: thin links read as noodles
    public int handLinks = 14;                                    // max; extra links hide when the chain is short
    public Vector3 beltAnchor = new Vector3(0.28f, 0.6f, 0.12f);  // cuff chain end: side, height below head, forward (mirrored for left)
    public float sag = 0.06f;                                     // near taut: a slack chain reads as a rope
    public float reach = 0.35f;                                  // max anchor -> hand distance before the hand is held
    public float pullRange = 0.25f;                               // extra real-hand pull (m) for full strain
    public float tremble = 0.0009f;                               // shake (m) at the highest pull; grows with distance from the body
    public float tremorStart = 0.75f;                             // fraction of reach where shaking and colour start
    public float awayStart = 0.3f, awayFull = 0.45f;              // hand distance from the body axis (m) where resistance fades in; nothing inside awayStart
    public float bodyBack = 0.08f;                                // chest sits this far behind the head
    public float follow = 2f;                                     // how fast the wrap re-centres on the player (1/s)

    public float LeftStrain { get; private set; }                 // 0..1, for the break logic later
    public float RightStrain { get; private set; }

    class Side { public Transform hand, cuff; public Transform[] links; public Vector3 anchor; public SyntheticHand synth; public bool left; public float resist; }
    Side left = new Side { left = true }, right = new Side();
    MaterialPropertyBlock mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");   // URP Unlit colour (Assets/Materials/ChainLink.mat)
    Transform[] torso; Renderer[] torsoR; Vector3[] torsoOff;   // torso links and their offsets from the body centre
    Vector3 body; float topY;                                    // smoothed body centre (XZ) and head height at start

    IEnumerator Start()
    {
        // no locomotion of any kind (teleport, turn, slide, step), hands or controllers: the player stays put
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb is ILocomotionEventBroadcaster || mb is ILocomotionEventHandler) mb.enabled = false;

        var head = Camera.main.transform;
        // wait for tracking so head height is real (ponytail: 3 s cap, then use whatever we have)
        for (float t = 0; head.position.y < 0.3f && t < 3f; t += Time.deltaTime) yield return null;

        left.hand = GameObject.Find("LeftHandAnchor")?.transform;
        right.hand = GameObject.Find("RightHandAnchor")?.transform;
        foreach (var h in FindObjectsByType<SyntheticHand>(FindObjectsSortMode.None))
            (h.Handedness == Handedness.Left ? left : right).synth = h;
        if (left.synth == null || right.synth == null)
            Debug.LogWarning("ChainWrap: no SyntheticHand found; the rendered hand will not be held at the chain limit");

        if (linkMaterial == null)
        {
            linkMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = linkColor };
            // fallback only; Assets/Materials/ChainLink.mat is assigned in the scene so the shader ships in builds
        }
        mpb = new MaterialPropertyBlock();

        topY = head.position.y;
        body = BodyTarget(head);
        BuildTorso();
        foreach (var s in new[] { left, right })
        {
            s.links = new Transform[handLinks];
            for (int i = 0; i < handLinks; i++) s.links[i] = MakeLink(i);
            s.cuff = MakeCuff();
        }
    }

    // chest centre: the head XZ pulled back along the facing direction (the head sits in front of the spine)
    Vector3 BodyTarget(Transform head)
    {
        var f = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        var p = head.position - f * bodyBack;
        return new Vector3(p.x, 0f, p.z);
    }

    void BuildTorso()
    {
        // chest-height spiral around the body centre; offsets are relative so the whole wrap can follow the player
        float top = topY - 0.25f, circ = 2f * Mathf.PI * torsoRadius * torsoTurns;
        int n = Mathf.CeilToInt(circ / linkSpacing);
        torso = new Transform[n]; torsoR = new Renderer[n]; torsoOff = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float u = (float)i / n, a = u * torsoTurns * 2f * Mathf.PI;
            var pos = new Vector3(Mathf.Cos(a) * torsoRadius, top - u * torsoHeight, Mathf.Sin(a) * torsoRadius);
            var tangent = new Vector3(-Mathf.Sin(a), -torsoHeight / circ, Mathf.Cos(a));
            torso[i] = MakeLink(i); torsoR[i] = torso[i].GetComponent<Renderer>(); torsoOff[i] = pos;
            PlaceLink(torso[i], body + pos, tangent, i);
        }
    }

    Transform MakeLink(int i)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);   // ellipsoid reads as a link
        Destroy(g.GetComponent<Collider>());                        // must not block the grab cube
        g.GetComponent<Renderer>().sharedMaterial = linkMaterial;
        g.transform.SetParent(transform, false);
        g.transform.localScale = i % 2 == 0 ? linkSize : new Vector3(linkSize.y, linkSize.x, linkSize.z);
        return g.transform;
    }

    // thick band at the wrist; follows the rendered (held) hand, not the real one
    Transform MakeCuff()
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = linkMaterial;
        g.transform.SetParent(transform, false);
        g.transform.localScale = new Vector3(0.09f, 0.03f, 0.09f);
        return g.transform;
    }

    static void PlaceLink(Transform link, Vector3 pos, Vector3 tangent, int i)
    {
        link.position = pos;
        link.rotation = Quaternion.LookRotation(tangent.normalized) * Quaternion.Euler(0, 0, i % 2 == 0 ? 0 : 90);
    }

    void Update()
    {
        if (left.links == null) return;
        // the wrap and belt anchors drift after the player, slowly, so they stay centred but still hold the hands
        body = Vector3.Lerp(body, BodyTarget(Camera.main.transform), 1f - Mathf.Exp(-follow * Time.deltaTime));
        left.anchor = body + new Vector3(-beltAnchor.x, topY - beltAnchor.y, beltAnchor.z);
        right.anchor = body + new Vector3(beltAnchor.x, topY - beltAnchor.y, beltAnchor.z);
        LeftStrain = UpdateSide(left);
        RightStrain = UpdateSide(right);
        float r = Mathf.Max(left.resist, right.resist);   // the whole chain shakes and lightens when either hand pulls
        mpb.SetColor(BaseColor, Color.Lerp(linkColor, strainColor, r));
        for (int i = 0; i < torso.Length; i++)
        {
            torso[i].position = body + torsoOff[i] + Random.insideUnitSphere * (tremble * r);
            torsoR[i].SetPropertyBlock(mpb);
        }
    }

    // returns strain 0..1. Chain is a quadratic bezier anchor -> hand with a small sag; links follow every frame.
    float UpdateSide(Side s)
    {
        if (s.hand == null) return 0f;
        var real = s.hand.position;
        var d = real - s.anchor;
        float dist = d.magnitude, strain = 0f;
        var end = real;
        if (dist > reach)
        {
            end = s.anchor + d / dist * reach;                       // rendered hand stops at the chain limit
            strain = Mathf.Clamp01((dist - reach) / pullRange);
            // ponytail: assumes the OVR hand anchor ~ the wrist; tune reach if the held hand looks off
            if (s.synth != null)
                s.synth.LockWristPosition(s.synth.TrackingToWorldTransformer.ToTrackingPose(new Pose(end, Quaternion.identity)).position);
        }
        else if (s.synth != null) s.synth.FreeWrist(SyntheticHand.WristLockMode.Position);

        // resist: pull on the chain, but only while the hand is out away from the body, so bringing the wrists together stays quiet
        var off = real - body; off.y = 0f;
        s.resist = Mathf.InverseLerp(reach * tremorStart, reach + pullRange, dist) * Mathf.InverseLerp(awayStart, awayFull, off.magnitude);
        mpb.SetColor(BaseColor, Color.Lerp(linkColor, strainColor, s.resist));
        var mid = (s.anchor + end) * 0.5f + Vector3.down * sag * (1f - strain);
        float len = Mathf.Max((end - s.anchor).magnitude, 0.01f);   // ponytail: straight-line length approximates the arc
        for (int i = 0; i < s.links.Length; i++)
        {
            float t = (i + 0.5f) * linkSpacing / len, u = 1f - t;
            s.links[i].gameObject.SetActive(t <= 1f);
            if (t > 1f) continue;
            var pos = u * u * s.anchor + 2f * u * t * mid + t * t * end;
            var tangent = 2f * u * (mid - s.anchor) + 2f * t * (end - mid);
            PlaceLink(s.links[i], pos + Random.insideUnitSphere * (tremble * s.resist), tangent, i);
            s.links[i].GetComponent<Renderer>().SetPropertyBlock(mpb);
        }
        var dir = end - s.anchor;
        s.cuff.position = end + Random.insideUnitSphere * (tremble * s.resist);
        if (dir.sqrMagnitude > 1e-4f) s.cuff.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
        s.cuff.GetComponent<Renderer>().SetPropertyBlock(mpb);
        return strain;
    }
}
