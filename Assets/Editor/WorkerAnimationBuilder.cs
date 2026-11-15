// Builds the background worker: a looping "sort ore" animation, its Animator Controller and a prefab.
// Menu: Tools > The Last Human > Rebuild Worker Animation
// The loop: reach to the belt with both hands, lift a chunk, twist to the bin on the left, drop it, return.
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static RobotAnimationBuilder;

public static class WorkerAnimationBuilder
{
    const string ModelPath = "Assets/Art/Models/Characters/Worker.fbx";
    const string AnimDir = "Assets/Art/Animations/Workers";
    public const string PrefabPath = "Assets/Prefabs/Workers/Worker.prefab";

    // one row per key pose: time (0..1), chest lean, chest twist, head pitch, head turn, upper arm, forearm, knee dip
    static readonly float[][] Poses =
    {
        new[] { 0.00f,  6f,   0f,  8f,   0f, -25f, -55f, 0f },   // ready, looking at the belt
        new[] { 0.28f, 20f,   0f,  4f,   0f, -62f, -22f, 5f },   // reach
        new[] { 0.42f, 12f,   0f,  6f,   0f, -45f, -60f, 3f },   // lift
        new[] { 0.60f, 10f, -42f,  6f, -12f, -48f, -45f, 2f },   // twist to the bin
        new[] { 0.72f, 16f, -42f,  8f, -12f, -38f, -15f, 4f },   // drop
        new[] { 0.88f,  8f,  -8f,  8f,   0f, -28f, -50f, 1f },   // back
        new[] { 1.00f,  6f,   0f,  8f,   0f, -25f, -55f, 0f },
    };

    [MenuItem("Tools/The Last Human/Rebuild Worker Animation")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        var imp = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (imp == null) return "Worker model not found";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var rig = new Rig(model);

        const float seconds = 2.6f;
        var clip = Clip("Worker_Sort", AnimDir);
        var c = new Curves();
        // sample the key poses densely so the motion eases between them
        const int steps = 78;
        for (int i = 0; i <= steps; i++)
        {
            float u = (float)i / steps;
            var p = Sample(u);
            float t = u * seconds;
            var hips = rig.hipsRest;
            hips.y = rig.HipsY(-p[7], 2 * p[7], -p[7], 2 * p[7]);
            c.Pos(Hips, t, hips);
            c.Rot(Hips, t, 0, p[2] * 0.25f, 0);
            c.Rot(Chest, t, p[1], p[2] * 0.75f, 0);
            c.Rot(Head, t, p[3], p[4], 0);
            foreach (var (side, sign) in new[] { ("L", 1f), ("R", -1f) })
            {
                c.Rot(Arm(side), t, p[5], 0, sign * -6f);
                c.Rot(Fore(side), t, p[6], sign * 14f, 0);
                c.Rot(Thigh(side), t, -p[7]);
                c.Rot(Shin(side), t, 2 * p[7]);
                c.Rot(Foot(side), t, -p[7]);
            }
        }
        c.Apply(clip);

        Directory.CreateDirectory(AnimDir);
        var ctrlPath = AnimDir + "/Worker.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var old in sm.states) sm.RemoveState(old.state);
        var state = sm.AddState("Sort");
        state.motion = clip;
        sm.defaultState = state;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            Object.DestroyImmediate(inst);
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var an = root.GetComponent<Animator>();
        if (an == null) an = root.AddComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.applyRootMotion = false;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.ImportAsset(ctrlPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        return $"Worker: sort loop {clip.length:F2}s, prefab saved";
    }

    /// <summary>Key poses blended with an ease in and out between neighbours.</summary>
    static float[] Sample(float u)
    {
        int i = 0;
        while (i < Poses.Length - 2 && u > Poses[i + 1][0]) i++;
        var a = Poses[i];
        var b = Poses[i + 1];
        float k = Mathf.InverseLerp(a[0], b[0], u);
        k = k * k * (3f - 2f * k);
        var r = new float[a.Length];
        for (int j = 0; j < a.Length; j++) r[j] = Mathf.Lerp(a[j], b[j], k);
        return r;
    }
}
