// Builds the animations for the player's own body, for both forms (human worker and masked robot).
// The player is a full jointed figure seen in first person, so every hand movement is a fixed
// animation on a rigid arm: nothing can stretch or come loose.
// Menu: Tools > The Last Human > Rebuild Player Body Animations
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static RobotAnimationBuilder;

public static class PlayerBodyAnimationBuilder
{
    const string AnimDir = "Assets/Art/Animations/Player";
    public static readonly (string rig, string model)[] Rigs =
    {
        ("Human", "Assets/Art/Models/Characters/Worker.fbx"),
        ("Robot", "Assets/Art/Models/Robots/Robot_Orange.fbx"),
    };

    public static string ControllerPath(string rig) => AnimDir + "/PlayerBody_" + rig + ".controller";
    public static string ClipPath(string rig, string clip) => AnimDir + "/PlayerBody_" + rig + "_" + clip + ".anim";

    // where in each clip things happen (0..1), shared with PlayerBody at runtime
    public const float SortSeconds = 1.7f, SortGrip = 0.30f, SortRelease = 0.80f;
    public const float GrabSeconds = 1.0f, GrabGrip = 0.42f, GrabAtFace = 0.82f;

    // key poses: time, chest lean, chest twist, left upper arm, left forearm, right upper arm, right forearm,
    //            thigh, shin, forearm turn inward. Angles in degrees; a negative arm angle swings it forward.
    // The hand animations are framed low: the hands work at belt height and are only ever raised to
    // chest height, well out in front, so they sit in the bottom of the picture and never cover its centre.
    static readonly float[][] Sort =
    {
        new[] { 0.00f,  3f,   0f,  -8f, -14f,  -8f, -14f,  0f,  0f,  6f },
        new[] { 0.22f, 24f,   0f, -58f, -16f, -58f, -16f, -5f, 10f, 14f },   // both hands down on the ore
        new[] { 0.36f, 24f,   0f, -58f, -18f, -58f, -18f, -5f, 10f, 15f },   // grip
        new[] { 0.50f, 14f,   0f, -50f, -24f, -50f, -24f, -3f,  6f, 15f },   // a short lift, just clear of the belt
        new[] { 0.70f,  8f, -28f, -50f, -22f, -50f, -22f, -2f,  4f, 15f },   // swing toward the bin: a small twist, standing
        new[] { 0.80f, 10f, -28f, -40f, -12f, -40f, -12f, -3f,  6f, 12f },   //   upright, so the shoulder never crosses the view; let go
        new[] { 1.00f,  3f,   0f,  -8f, -14f,  -8f, -14f,  0f,  0f,  6f },
    };
    static readonly float[][] Grab =
    {
        new[] { 0.00f,  3f,   0f,  -8f, -14f,  -8f, -14f,  0f, 0f, 6f },
        new[] { 0.36f, 20f, -10f, -10f, -18f, -60f, -18f, -4f, 8f, 8f },    // right hand out to the mask
        new[] { 0.50f, 20f, -10f, -10f, -18f, -60f, -20f, -4f, 8f, 8f },    // close on it
        new[] { 0.82f,  6f,   0f, -10f, -16f, -40f, -72f,  0f, 0f, 10f },   // bring it up to chest height, at arm's length
        new[] { 1.00f,  6f,   0f, -10f, -16f, -40f, -72f,  0f, 0f, 10f },
    };
    static readonly float[][] Present =      // both forearms held out at chest height, apart (shown during the change)
    {
        new[] { 0f, 4f, 0f, -42f, -58f, -42f, -58f, 0f, 0f, 8f },
        new[] { 1f, 4f, 0f, -42f, -58f, -42f, -58f, 0f, 0f, 8f },
    };
    static readonly float[][] Crouch =       // deep squat, feet flat
    {
        new[] { 0f, 26f, 0f, -22f, -42f, -22f, -42f, -85f, 120f, 8f },
        new[] { 1f, 26f, 0f, -22f, -42f, -22f, -42f, -85f, 120f, 8f },
    };

    [MenuItem("Tools/The Last Human/Rebuild Player Body Animations")]
    public static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        Directory.CreateDirectory(AnimDir);
        var log = "";
        foreach (var (rig, modelPath) in Rigs)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { log += rig + ": model missing\n"; continue; }
            var r = new Rig(model);
            var gait = new Spec { name = rig, walkT = 0.95f, thigh = 28, knee = 46, arm = 18, elbow = 14, lean = 3, yaw = 4, roll = 2,
                                  idleT = 4f, headLook = 0, headTilt = 0, idleElbow = 12, idleSway = 1.2f };
            var idle = Clip("PlayerBody_" + rig + "_Idle", AnimDir);
            FillIdle(idle, gait, r);
            var walk = Clip("PlayerBody_" + rig + "_Walk", AnimDir);
            FillWalk(walk, gait, r);
            var crouch = Poses("PlayerBody_" + rig + "_Crouch", r, Crouch, 1f, true);
            var present = Poses("PlayerBody_" + rig + "_Present", r, Present, 1f, true);
            var sort = Poses("PlayerBody_" + rig + "_Sort", r, Sort, SortSeconds, false);
            var grab = Poses("PlayerBody_" + rig + "_Grab", r, Grab, GrabSeconds, false);

            var path = ControllerPath(rig);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var old in sm.states) sm.RemoveState(old.state);
            foreach (var old in sm.anyStateTransitions) sm.RemoveAnyStateTransition(old);
            while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(0);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("WalkRate", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Present", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Sort", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Grab", AnimatorControllerParameterType.Trigger);

            var sIdle = State(sm, "Idle", idle);
            var sWalk = State(sm, "Walk", walk);
            sWalk.speedParameter = "WalkRate";
            sWalk.speedParameterActive = true;
            var sCrouch = State(sm, "Crouch", crouch);
            var sPresent = State(sm, "Present", present);
            var sSort = State(sm, "Sort", sort);
            var sGrab = State(sm, "Grab", grab);
            sm.defaultState = sIdle;

            Link(sIdle.AddTransition(sWalk), 0.15f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            Link(sWalk.AddTransition(sIdle), 0.2f).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            Link(sm.AddAnyStateTransition(sCrouch), 0.2f).AddCondition(AnimatorConditionMode.If, 0, "Crouch");
            Link(sCrouch.AddTransition(sIdle), 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");
            Link(sm.AddAnyStateTransition(sPresent), 0.2f).AddCondition(AnimatorConditionMode.If, 0, "Present");
            Link(sPresent.AddTransition(sIdle), 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0, "Present");
            Link(sm.AddAnyStateTransition(sSort), 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Sort");
            Exit(sSort.AddTransition(sIdle));
            Link(sm.AddAnyStateTransition(sGrab), 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Grab");
            Exit(sGrab.AddTransition(sIdle));
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            log += $"PlayerBody {rig}: idle, walk, crouch, present, sort ({sort.length:F2}s), grab ({grab.length:F2}s)\n";
        }
        return log.TrimEnd();
    }

    static AnimatorState State(AnimatorStateMachine sm, string name, Motion motion)
    {
        var s = sm.AddState(name);
        s.motion = motion;
        return s;
    }

    static AnimatorStateTransition Link(AnimatorStateTransition t, float blend)
    {
        t.hasExitTime = false;
        t.duration = blend;
        t.canTransitionToSelf = false;
        return t;
    }

    static void Exit(AnimatorStateTransition t)
    {
        t.hasExitTime = true;
        t.exitTime = 0.98f;
        t.duration = 0.1f;
    }

    /// <summary>A clip that eases through a table of key poses.</summary>
    static AnimationClip Poses(string name, Rig rig, float[][] table, float seconds, bool loop)
    {
        var clip = Clip(name, AnimDir);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var c = new Curves();
        int steps = Mathf.Max(2, Mathf.RoundToInt(seconds * 30f));
        for (int i = 0; i <= steps; i++)
        {
            float u = (float)i / steps;
            var p = Sample(table, u);
            float t = u * seconds;
            var hips = rig.hipsRest;
            hips.y = rig.HipsY(p[7], p[8], p[7], p[8]);
            c.Pos(Hips, t, hips);
            c.Rot(Hips, t, 0, p[2] * 0.25f, 0);
            c.Rot(Chest, t, p[1], p[2] * 0.75f, 0);
            c.Rot(Head, t, 0, 0, 0);
            c.Rot(Arm("L"), t, p[3], 0, -6f);          // slightly out from the body
            c.Rot(Fore("L"), t, p[4], p[9], 0);       // forearms turn in toward each other
            c.Rot(Arm("R"), t, p[5], 0, 6f);
            c.Rot(Fore("R"), t, p[6], -p[9], 0);
            foreach (var side in new[] { "L", "R" })
            {
                c.Rot(Thigh(side), t, p[7]);
                c.Rot(Shin(side), t, p[8]);
                c.Rot(Foot(side), t, -(p[7] + p[8]));
            }
        }
        c.Apply(clip);
        return clip;
    }

    static float[] Sample(float[][] table, float u)
    {
        int i = 0;
        while (i < table.Length - 2 && u > table[i + 1][0]) i++;
        var a = table[i];
        var b = table[i + 1];
        float k = Mathf.InverseLerp(a[0], b[0], u);
        k = k * k * (3f - 2f * k);
        var r = new float[a.Length];
        for (int j = 0; j < a.Length; j++) r[j] = Mathf.Lerp(a[j], b[j], k);
        return r;
    }
}
