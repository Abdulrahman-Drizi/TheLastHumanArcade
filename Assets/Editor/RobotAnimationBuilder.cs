// Builds idle and walk clips, an Animator Controller and a ready-to-use prefab for each robot.
// Menu: Tools > The Last Human > Rebuild Robot Animations
// The clips are generated from a few numbers per robot (tempo, stride, arm swing...), so retuning
// a robot's movement is a matter of editing its Spec below and running the menu item again.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RobotAnimationBuilder
{
    const string ModelDir = "Assets/Art/Models/Robots";
    const string AnimDir = "Assets/Art/Animations/Robots";
    const string PrefabDir = "Assets/Prefabs/Robots";
    const float Fps = 30f;

    internal class Spec
    {
        public string name;
        // walk (angles in degrees, time in seconds)
        public float walkT, thigh, knee, arm, elbow, lean, yaw, roll;
        // idle
        public float idleT, headLook, headTilt, idleElbow, idleSway;
    }

    static readonly Spec[] Specs =
    {
        // heavy unit: slow, short stride, stiff arms
        new Spec { name = "Robot_Orange", walkT = 1.2f, thigh = 23, knee = 47, arm = 13, elbow = 10, lean = 3.5f, yaw = 5, roll = 2.5f,
                   idleT = 5.0f, headLook = 7, headTilt = 0, idleElbow = 6, idleSway = 1.0f },
        // scout: quick, long stride, loose arms, curious head
        new Spec { name = "Robot_Purple", walkT = 0.8f, thigh = 30, knee = 50, arm = 27, elbow = 22, lean = 6.5f, yaw = 8, roll = 3.0f,
                   idleT = 3.2f, headLook = 14, headTilt = 6, idleElbow = 10, idleSway = 2.0f },
        // bruiser: medium pace, heavy forearms swing wide
        new Spec { name = "Robot_Blue", walkT = 0.95f, thigh = 26, knee = 50, arm = 21, elbow = 16, lean = 5, yaw = 6.5f, roll = 3.5f,
                   idleT = 4.0f, headLook = 10, headTilt = 0, idleElbow = 16, idleSway = 1.5f },
    };

    internal const string Hips = "Hips", Chest = "Hips/Chest", Head = "Hips/Chest/Head";
    internal static string Arm(string s) => "Hips/Chest/UpperArm." + s;
    internal static string Fore(string s) => Arm(s) + "/Forearm." + s;
    internal static string Thigh(string s) => "Hips/Thigh." + s;
    internal static string Shin(string s) => Thigh(s) + "/Shin." + s;
    internal static string Foot(string s) => Shin(s) + "/Foot." + s;

    [MenuItem("Tools/The Last Human/Rebuild Robot Animations")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        Directory.CreateDirectory(AnimDir);
        Directory.CreateDirectory(PrefabDir);
        AssetDatabase.Refresh();
        var log = new List<string>();
        foreach (var sp in Specs)
        {
            var modelPath = ModelDir + "/" + sp.name + ".fbx";
            var imp = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (imp == null) { log.Add(sp.name + ": model not found"); continue; }
            if (imp.animationType != ModelImporterAnimationType.None)
            {
                imp.animationType = ModelImporterAnimationType.None;   // rigid parts, no avatar needed
                imp.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var rig = new Rig(model);

            var idle = Clip(sp.name + "_Idle");
            FillIdle(idle, sp, rig);
            var walk = Clip(sp.name + "_Walk");
            FillWalk(walk, sp, rig);

            var ctrlPath = AnimDir + "/" + sp.name + ".controller";
            // reuse the existing controller so references to it (prefabs, scenes) stay valid
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var old in sm.states) sm.RemoveState(old.state);
            while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(0);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var sIdle = sm.AddState("Idle");
            sIdle.motion = idle;
            var sWalk = sm.AddState("Walk");
            sWalk.motion = walk;
            sm.defaultState = sIdle;
            var a = sIdle.AddTransition(sWalk);
            a.hasExitTime = false; a.duration = 0.18f;
            a.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var b = sWalk.AddTransition(sIdle);
            b.hasExitTime = false; b.duration = 0.22f;
            b.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // prefab: created once as a variant of the model, then only its Animator is updated
            var prefabPath = PrefabDir + "/" + sp.name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
                Object.DestroyImmediate(inst);
            }
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var an = root.GetComponent<Animator>();
            if (an == null) an = root.AddComponent<Animator>();
            an.runtimeAnimatorController = ctrl;
            an.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            // without this the imported prefab can keep a stale (empty) controller reference
            AssetDatabase.ImportAsset(ctrlPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            log.Add($"{sp.name}: idle {idle.length:F2}s, walk {walk.length:F2}s, leg {rig.thighLen:F2}+{rig.shinLen:F2} m");
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    // ---------------------------------------------------------------- rig measurements
    internal class Rig
    {
        public Vector3 hipsRest, thighLocal;
        public float thighLen, shinLen, ankleHeight;
        public Rig(GameObject model)
        {
            var t = model.transform;
            hipsRest = t.Find(Hips).localPosition;
            thighLocal = t.Find(Thigh("L")).localPosition;
            thighLen = t.Find(Shin("L")).localPosition.magnitude;
            shinLen = t.Find(Foot("L")).localPosition.magnitude;
            ankleHeight = hipsRest.y + thighLocal.y - thighLen - shinLen;
        }

        // Hips height that keeps the lower foot on the ground for the given leg angles
        // (angles from vertical in degrees, positive = toward the back).
        public float HipsY(float thighL, float shinL, float thighR, float shinR)
        {
            float a = Reach(thighL, shinL), b = Reach(thighR, shinR);
            float reach = 0.5f * (a + b + Mathf.Sqrt((a - b) * (a - b) + 1e-4f));   // smooth max
            return ankleHeight - thighLocal.y + reach;
        }

        float Reach(float thigh, float shin) =>
            thighLen * Mathf.Cos(thigh * Mathf.Deg2Rad) + shinLen * Mathf.Cos((thigh + shin) * Mathf.Deg2Rad);
    }

    // ---------------------------------------------------------------- curve plumbing
    internal class Curves
    {
        readonly Dictionary<(string, string), List<Keyframe>> keys = new Dictionary<(string, string), List<Keyframe>>();
        void Add(string path, string prop, float t, float v)
        {
            if (!keys.TryGetValue((path, prop), out var l)) keys[(path, prop)] = l = new List<Keyframe>();
            l.Add(new Keyframe(t, v));
        }
        public void Rot(string path, float t, float x, float y = 0, float z = 0)
        {
            Add(path, "localEulerAnglesRaw.x", t, x);
            Add(path, "localEulerAnglesRaw.y", t, y);
            Add(path, "localEulerAnglesRaw.z", t, z);
        }
        public void Pos(string path, float t, Vector3 p)
        {
            Add(path, "m_LocalPosition.x", t, p.x);
            Add(path, "m_LocalPosition.y", t, p.y);
            Add(path, "m_LocalPosition.z", t, p.z);
        }
        public void Apply(AnimationClip clip)
        {
            foreach (var kv in keys)
            {
                var c = new AnimationCurve(kv.Value.ToArray());
                for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(kv.Key.Item1, typeof(Transform), kv.Key.Item2), c);
            }
        }
    }

    internal static AnimationClip Clip(string name, string dir = AnimDir)
    {
        Directory.CreateDirectory(dir);
        var path = dir + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name, frameRate = Fps };
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        return clip;
    }

    // ---------------------------------------------------------------- walk (in place; move the object from code)
    // Sign convention after import: rotating a limb by a negative X angle swings it forward.
    internal static void FillWalk(AnimationClip clip, Spec s, Rig rig)
    {
        var c = new Curves();
        int n = Mathf.RoundToInt(s.walkT * Fps);
        for (int i = 0; i <= n; i++)
        {
            float t = s.walkT * i / n;
            float ph = 2 * Mathf.PI * i / n;
            float sn = Mathf.Sin(ph);
            var leg = new float[2, 3];
            for (int k = 0; k < 2; k++)
            {
                float p = ph + k * Mathf.PI;                       // right leg is half a cycle behind
                float swing = Mathf.Pow(Mathf.Max(0, Mathf.Cos(p)), 1.3f);   // 1 at mid-swing, 0 in stance
                float thigh = -s.thigh * Mathf.Sin(p) - 0.3f * s.knee * swing;
                float shin = 4 + s.knee * swing;
                float foot = -(thigh + shin) * (1 - 0.35f * swing) + 8 * swing * Mathf.Max(0, -Mathf.Sin(p));
                leg[k, 0] = thigh; leg[k, 1] = shin; leg[k, 2] = foot;
            }
            var hips = rig.hipsRest;
            hips.y = rig.HipsY(leg[0, 0], leg[0, 1], leg[1, 0], leg[1, 1]);
            c.Pos(Hips, t, hips);
            c.Rot(Hips, t, 0, s.yaw * sn, s.roll * Mathf.Cos(ph));
            c.Rot(Chest, t, s.lean + 1.2f * Mathf.Cos(2 * ph), -1.6f * s.yaw * sn, -0.6f * s.roll * Mathf.Cos(ph));
            c.Rot(Head, t, -0.6f * s.lean - 1.0f * Mathf.Cos(2 * ph), 0.6f * s.yaw * sn, 0);
            string[] side = { "L", "R" };
            for (int k = 0; k < 2; k++)
            {
                float sg = k == 0 ? 1 : -1;
                c.Rot(Thigh(side[k]), t, leg[k, 0]);
                c.Rot(Shin(side[k]), t, leg[k, 1]);
                c.Rot(Foot(side[k]), t, leg[k, 2]);
                float armSwing = sg * sn;                          // arm goes back when its own leg goes forward
                c.Rot(Arm(side[k]), t, s.arm * armSwing, 0, sg * -3);
                c.Rot(Fore(side[k]), t, -(s.elbow * 0.5f + s.elbow * Mathf.Max(0, -armSwing)));
            }
        }
        c.Apply(clip);
    }

    // ---------------------------------------------------------------- idle (feet planted, small servo movement)
    internal static void FillIdle(AnimationClip clip, Spec s, Rig rig)
    {
        var c = new Curves();
        int n = Mathf.RoundToInt(s.idleT * Fps);
        for (int i = 0; i <= n; i++)
        {
            float t = s.idleT * i / n;
            float ph = 2 * Mathf.PI * i / n;
            float dip = 1.6f * (0.5f - 0.5f * Mathf.Cos(ph));      // tiny knee bend so the body settles and rises
            var hips = rig.hipsRest;
            hips.y = rig.HipsY(-dip, 2 * dip, -dip, 2 * dip);
            c.Pos(Hips, t, hips);
            c.Rot(Hips, t, 0, 0.6f * s.idleSway * Mathf.Sin(ph), 0);
            c.Rot(Chest, t, 1.0f * Mathf.Sin(ph), 1.5f * s.idleSway * Mathf.Sin(ph + 0.5f), 0);
            c.Rot(Head, t, 1.5f * Mathf.Sin(2 * ph), s.headLook * Mathf.Sin(ph + 0.9f), s.headTilt * Mathf.Sin(2 * ph + 0.4f));
            string[] side = { "L", "R" };
            for (int k = 0; k < 2; k++)
            {
                float sg = k == 0 ? 1 : -1;
                c.Rot(Thigh(side[k]), t, -dip);
                c.Rot(Shin(side[k]), t, 2 * dip);
                c.Rot(Foot(side[k]), t, -dip);
                c.Rot(Arm(side[k]), t, s.idleSway * Mathf.Sin(ph + k * 1.3f), 0, sg * -3);
                c.Rot(Fore(side[k]), t, -(s.idleElbow + 0.25f * s.idleElbow * Mathf.Sin(ph + 1.0f + k * 0.8f)));
            }
        }
        c.Apply(clip);
    }

    // ---------------------------------------------------------------- self-check
    // Samples every clip and reports how far the lowest foot strays from the ground, plus loop continuity.
    public static string Verify()
    {
        var log = new List<string>();
        foreach (var sp in Specs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + sp.name + ".prefab");
            if (prefab == null) { log.Add(sp.name + ": prefab missing"); continue; }
            var go = Object.Instantiate(prefab);
            var feet = new[] { go.transform.Find(Foot("L")).GetComponent<Renderer>(), go.transform.Find(Foot("R")).GetComponent<Renderer>() };
            foreach (var kind in new[] { "Idle", "Walk" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimDir + "/" + sp.name + "_" + kind + ".anim");
                float lo = 99, hi = -99, lift = 0;
                int steps = Mathf.RoundToInt(clip.length * Fps);
                for (int i = 0; i <= steps; i++)
                {
                    clip.SampleAnimation(go, clip.length * i / steps);
                    float l = feet[0].bounds.min.y, r = feet[1].bounds.min.y;
                    float m = Mathf.Min(l, r);
                    lo = Mathf.Min(lo, m); hi = Mathf.Max(hi, m);
                    lift = Mathf.Max(lift, Mathf.Max(l, r));
                }
                clip.SampleAnimation(go, 0);
                var p0 = go.transform.Find(Hips).localPosition; var r0 = go.transform.Find(Thigh("L")).localRotation;
                clip.SampleAnimation(go, clip.length);
                float seam = (go.transform.Find(Hips).localPosition - p0).magnitude + Quaternion.Angle(go.transform.Find(Thigh("L")).localRotation, r0);
                log.Add($"{sp.name} {kind}: length {clip.length:F2}s loop={clip.isLooping} lowest foot {lo * 100:F1}..{hi * 100:F1} cm, highest foot lift {lift * 100:F1} cm, seam {seam:F4}");
            }
            Object.DestroyImmediate(go);
        }
        return string.Join("\n", log);
    }
}
