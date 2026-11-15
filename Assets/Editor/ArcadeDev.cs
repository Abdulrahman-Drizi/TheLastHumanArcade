// Command-line entry points for working on this project without the editor window, e.g.
//   Unity.exe -batchmode -projectPath <project> -executeMethod ArcadeDev.Build -quit -logFile <file>
// Build      rebuilds the game's scene from code (log in Logs/build.txt), then takes the stills.
// Stills     renders the scene from a list of player-height viewpoints to Docs/Stills (no HUD).
// DumpKits   lists the two model kits (sizes, pivots, material slots) to Logs/kits.txt.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArcadeDev
{
    static string Root => Directory.GetParent(Application.dataPath).FullName;
    static string LogDir => Path.Combine(Root, "Logs");

    public static void Build()
    {
        Directory.CreateDirectory(LogDir);
        string result = "";
        try
        {
            result = PrototypeBuilder.BuildAll();
            result += "\n" + TakeStills();
        }
        catch (System.Exception e) { result += "\nBUILD FAILED\n" + e; }
        File.WriteAllText(Path.Combine(LogDir, "build.txt"), result);
    }

    // ---------------------------------------------------------------- the automated play-test
    static string Flag => Path.Combine(LogDir, "bot.flag");
    static string Done => Path.Combine(LogDir, "bot.done");

    /// <summary>
    /// Plays the game from the command line (batch mode, without -quit): opens the scene, enters
    /// Play Mode with the test bot asked for (see ArcadeBot), and closes the editor when the bot
    /// says it has finished, or after five minutes whatever happens.
    /// </summary>
    public static void PlayTest()
    {
        if (!Application.isBatchMode) { Debug.Log("ArcadeDev.PlayTest is for batch mode."); return; }
        Directory.CreateDirectory(LogDir);
        if (File.Exists(Done)) File.Delete(Done);
        File.WriteAllText(Flag, "run");
        string play = Path.Combine(Root, "Docs", "Play");
        if (Directory.Exists(play)) foreach (var old in Directory.GetFiles(play, "*.png")) File.Delete(old);
        EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
        SessionState.SetFloat("ArcadeBotStarted", (float)EditorApplication.timeSinceStartup);
        EditorApplication.update += Watch;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void ResumeWatch()
    {
        // (entering Play Mode may reload the scripts: the watch is put back)
        if (!Application.isBatchMode || !File.Exists(Flag)) return;
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
    }

    static void Watch()
    {
        float started = SessionState.GetFloat("ArcadeBotStarted", 0f);
        bool late = EditorApplication.timeSinceStartup - started > 300.0;
        if (!File.Exists(Done) && !late) return;
        EditorApplication.update -= Watch;
        if (late) File.AppendAllText(Path.Combine(LogDir, "bot.txt"), "EDITOR TIME LIMIT\n");
        if (File.Exists(Flag)) File.Delete(Flag);
        if (File.Exists(Done)) File.Delete(Done);
        EditorApplication.Exit(0);
    }

    public static void Stills()
    {
        Directory.CreateDirectory(LogDir);
        string result;
        try { result = TakeStills(); }
        catch (System.Exception e) { result = "STILLS FAILED\n" + e; }
        File.WriteAllText(Path.Combine(LogDir, "stills.txt"), result);
    }

    /// <summary>The scene as it stands when the game starts, seen from the player's eye height at a few places.</summary>
    static string TakeStills()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
        var camGo = GameObject.Find("Main Camera");
        if (camGo == null) return "no Main Camera";
        var cam = camGo.GetComponent<Camera>();
        string dir = Path.Combine(Root, "Docs", "Stills");
        Directory.CreateDirectory(dir);
        var views = new (string name, Vector3 from, Vector3 at, float fov)[]
        {
            ("01_gate_approach_from_the_start", new Vector3(0f, 1.65f, -13f), new Vector3(0f, 2.2f, 16f), 68f),
            ("02_gate_approach_midway", new Vector3(-1.2f, 1.65f, 1.5f), new Vector3(1.5f, 2.0f, 16f), 68f),
            ("03_west_lane_hide_and_battery", new Vector3(-12.8f, 1.65f, -1f), new Vector3(-14f, 1.0f, 12f), 68f),
            ("04_east_gap_battery", new Vector3(2.6f, 1.65f, 3.2f), new Vector3(12f, 1.0f, 4.4f), 68f),
            ("05_gate_close", new Vector3(1.4f, 1.65f, 11.2f), new Vector3(0.4f, 2.0f, 16f), 68f),
            ("06_boss_entrance", new Vector3(0f, 1.65f, 19.4f), new Vector3(0f, 3.2f, 30f), 68f),
            ("07_plug", new Vector3(1.5f, 1.65f, 38.5f), new Vector3(0f, 1.7f, 44f), 68f),
            ("08_work_pen", new Vector3(4.5f, 1.65f, -5.5f), new Vector3(12f, 1.2f, -12f), 68f),
            ("10_closet_close", new Vector3(-11.5f, 1.65f, 1.2f), new Vector3(-15.4f, 1.3f, 3.5f), 60f),
            ("11_guard_close", new Vector3(1.8f, 1.65f, 2.2f), new Vector3(0f, 1.5f, 6f), 60f),
            ("09_plan", new Vector3(0f, 62f, 10f), new Vector3(0f, 0f, 10.01f), 60f),
        };
        // (the player's own body is drawn by a second camera stacked on this one; outside Play Mode it would fill the picture)
        var bodyCamera = GameObject.Find("BodyCamera");
        if (bodyCamera != null) bodyCamera.SetActive(false);
        var keepPosition = cam.transform.position;
        var keepRotation = cam.transform.rotation;
        float keepFov = cam.fieldOfView;
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        var shot = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var sb = new StringBuilder();
        // (the hall has a roof: the plan view lifts it off for the picture)
        var roof = GameObject.Find("HallRoof");
        foreach (var view in views)
        {
            if (roof != null) roof.SetActive(view.name != "09_plan");
            cam.transform.SetPositionAndRotation(view.from, Quaternion.LookRotation(view.at - view.from, view.name == "09_plan" ? Vector3.forward : Vector3.up));
            cam.fieldOfView = view.fov;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, view.name + ".png"), shot.EncodeToPNG());
            sb.AppendLine("still " + view.name);
        }
        if (roof != null) roof.SetActive(true);
        cam.transform.SetPositionAndRotation(keepPosition, keepRotation);
        cam.fieldOfView = keepFov;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(shot);
        return sb.ToString();
    }

    public static void DumpKits()
    {
        Directory.CreateDirectory(LogDir);
        var sb = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Kits" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) continue;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { sb.AppendLine(path + "  (no renderers)"); continue; }
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var slots = new System.Collections.Generic.List<string>();
            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials) if (m != null && !slots.Contains(m.name)) slots.Add(m.name);
            sb.AppendLine(string.Format("{0} | size {1:0.00} {2:0.00} {3:0.00} | min {4:0.00} {5:0.00} {6:0.00} | scale {7} | mats {8}",
                path.Substring("Assets/Kits/".Length), bounds.size.x, bounds.size.y, bounds.size.z, bounds.min.x, bounds.min.y, bounds.min.z,
                model.transform.localScale.x, string.Join(",", slots)));
        }
        File.WriteAllText(Path.Combine(LogDir, "kits.txt"), sb.ToString());
    }
}
