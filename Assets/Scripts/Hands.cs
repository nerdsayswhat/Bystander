using Oculus.Interaction.Input;
using UnityEngine;

// Where the hands are and whether each one is closed in a fist (index 0 = left, 1 = right). Looked up when first asked, and again if lost.
public static class Hands
{
    static readonly Transform[] anchor = new Transform[2];
    static readonly SyntheticHand[] synth = new SyntheticHand[2];
    static float nextLook;

    public static Transform Anchor(int h)
    {
        if (anchor[h] == null) anchor[h] = GameObject.Find(h == 0 ? "LeftHandAnchor" : "RightHandAnchor")?.transform;
        return anchor[h];
    }

    public static bool Fist(int h, float limit = 0.12f)
    {
        if (synth[h] == null && Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + 1f;
            foreach (var s in Object.FindObjectsByType<SyntheticHand>(FindObjectsSortMode.None)) synth[s.Handedness == Handedness.Left ? 0 : 1] = s;
        }
        return ChainWrap.IsFist(synth[h], limit);
    }
}
