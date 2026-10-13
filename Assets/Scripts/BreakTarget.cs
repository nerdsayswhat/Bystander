using System.Collections.Generic;
using UnityEngine;

// Marks an object whose reach breaks the player's chains: ChainWrap breaks when a hand is pulled towards one.
public class BreakTarget : MonoBehaviour
{
    public static readonly List<BreakTarget> All = new List<BreakTarget>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);
}
