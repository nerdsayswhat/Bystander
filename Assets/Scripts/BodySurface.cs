using UnityEngine;

// The real surface of a character body, baked by Bystander > Bake Bodies (Editor/BodyBaker.cs): about 4000 points spread over the character's skinned meshes (area weighted),
// each stored in the space of the one or two humanoid bones that move it (offA in bone boneA, and a second bone boneB blended in by wB, 0 = bone A only).
// ParticleFigure draws a figure's particles on these points instead of on tubes, so the particles follow the real limbs, chest, hips, hands and head of the body.
public class BodySurface : ScriptableObject
{
    public GameObject model;                 // the character FBX: its skeleton is what the figure animates
    public int[] boneA, boneB;               // HumanBodyBones values
    public Vector3[] offA, offB;
    public float[] wB;
    public int Count => boneA != null ? boneA.Length : 0;
}

// The two bodies in Resources/Bodies (loaded on first use). Apply() gives a figure a body: the character's skeleton and its surface; the particles get smaller because there are
// more of them on a real surface. Without the assets (not baked yet) a figure keeps its tube body.
public static class BodyLibrary
{
    static BodySurface man, woman; static bool loaded;
    static void Load()
    {
        if (loaded) return; loaded = true;
        man = Resources.Load<BodySurface>("Bodies/Body_SlimMan"); woman = Resources.Load<BodySurface>("Bodies/Body_SlimWoman");
    }
    public static bool Available { get { Load(); return man != null || woman != null; } }
    public static void Apply(ParticleFigure f, bool female)
    {
        Load(); var b = female ? woman : man; if (b == null) b = female ? man : woman; if (b == null || b.model == null || b.Count == 0) return;
        f.body = b; f.model = b.model; f.sizeMin *= 0.6f; f.sizeMax *= 0.6f;
    }
}
