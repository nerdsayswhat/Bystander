using System.Collections;
using UnityEngine;

// Chains binding the player: a fixed helix around the torso plus a sagging chain from each hand
// to a floor anchor. Add to an empty GameObject in the scene (rig at world origin).
public class ChainWrap : MonoBehaviour
{
    public Material linkMaterial;                  // flat emissive; falls back to Unlit/Color
    public Color linkColor = new Color(1f, 0.55f, 0.2f);
    public float torsoRadius = 0.3f, torsoHeight = 0.6f, torsoTurns = 3f, linkSpacing = 0.07f;
    public Vector3 linkSize = new Vector3(0.05f, 0.035f, 0.1f);   // chunky: thin links read as noodles
    public int handLinks = 14;                                    // max; extra links hide when the chain is short
    public Vector3 beltAnchor = new Vector3(0.28f, 0.6f, 0.12f);  // cuff chain end: side, height below head, forward (mirrored for left)
    public float sag = 0.06f;                                     // near taut: a slack chain reads as a rope

    Transform leftHand, rightHand;
    Transform[] leftChain, rightChain;
    Vector3 leftAnchor, rightAnchor;

    IEnumerator Start()
    {
        var head = Camera.main.transform;
        // wait for tracking so head height is real (ponytail: 3 s cap, then use whatever we have)
        for (float t = 0; head.position.y < 0.3f && t < 3f; t += Time.deltaTime) yield return null;

        leftHand = GameObject.Find("LeftHandAnchor")?.transform;
        rightHand = GameObject.Find("RightHandAnchor")?.transform;
        if (linkMaterial == null)
        {
            linkMaterial = new Material(Shader.Find("Unlit/Color")) { color = linkColor };
            // ponytail: Shader.Find may be stripped in builds; assign a real material before the Android build
        }

        var p = head.position;   // anchors fixed in the world, so the hands are held, not dragged along
        leftAnchor = new Vector3(p.x - beltAnchor.x, p.y - beltAnchor.y, p.z + beltAnchor.z);
        rightAnchor = new Vector3(p.x + beltAnchor.x, p.y - beltAnchor.y, p.z + beltAnchor.z);
        BuildTorso(p);
        leftChain = MakeChain(handLinks);
        rightChain = MakeChain(handLinks);
        MakeCuff(leftHand);
        MakeCuff(rightHand);
    }

    // thick band at the wrist, so the chain visibly locks onto the hand
    void MakeCuff(Transform hand)
    {
        if (hand == null) return;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = linkMaterial;
        g.transform.SetParent(hand, false);
        g.transform.localRotation = Quaternion.Euler(90, 0, 0);          // cylinder axis along the forearm (+Z)
        g.transform.localScale = new Vector3(0.09f, 0.03f, 0.09f);
        g.transform.localPosition = new Vector3(0, 0, -0.05f);           // ponytail: guess; tune against the real wrist
    }

    void BuildTorso(Vector3 headPos)
    {
        // chest-height spiral, fixed in the world around where the player stood at start
        float top = headPos.y - 0.25f, circ = 2f * Mathf.PI * torsoRadius * torsoTurns;
        int n = Mathf.CeilToInt(circ / linkSpacing);
        var center = new Vector3(headPos.x, 0f, headPos.z);
        for (int i = 0; i < n; i++)
        {
            float u = (float)i / n, a = u * torsoTurns * 2f * Mathf.PI;
            var pos = center + new Vector3(Mathf.Cos(a) * torsoRadius, top - u * torsoHeight, Mathf.Sin(a) * torsoRadius);
            var tangent = new Vector3(-Mathf.Sin(a), -torsoHeight / circ, Mathf.Cos(a));
            PlaceLink(MakeLink(i), pos, tangent, i);
        }
    }

    Transform[] MakeChain(int count)
    {
        var links = new Transform[count];
        for (int i = 0; i < count; i++) links[i] = MakeLink(i);
        return links;
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

    static void PlaceLink(Transform link, Vector3 pos, Vector3 tangent, int i)
    {
        link.position = pos;
        link.rotation = Quaternion.LookRotation(tangent.normalized) * Quaternion.Euler(0, 0, i % 2 == 0 ? 0 : 90);
    }

    void Update()
    {
        if (leftChain == null) return;
        UpdateHand(leftChain, leftHand, leftAnchor);
        UpdateHand(rightChain, rightHand, rightAnchor);
    }

    // quadratic bezier floor-anchor -> hand with a sagging midpoint; links follow every frame
    void UpdateHand(Transform[] links, Transform hand, Vector3 anchor)
    {
        if (hand == null) return;
        var end = hand.position;
        var mid = (anchor + end) * 0.5f + Vector3.down * sag;
        float len = Vector3.Distance(anchor, end);   // ponytail: straight-line length approximates the arc
        for (int i = 0; i < links.Length; i++)
        {
            float t = (i + 0.5f) * linkSpacing / Mathf.Max(len, 0.01f), u = 1f - t;
            links[i].gameObject.SetActive(t <= 1f);
            if (t > 1f) continue;
            var pos = u * u * anchor + 2f * u * t * mid + t * t * end;
            var tangent = 2f * u * (mid - anchor) + 2f * t * (end - mid);
            PlaceLink(links[i], pos, tangent, i);
        }
    }
}
