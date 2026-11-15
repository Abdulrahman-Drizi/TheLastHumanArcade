// Builds the two prototype scenes (MainMenu, Workplace) from code so the level stays easy to change.
// Menu: Tools > The Last Human > Build Prototype Scenes
// This file: shared helpers, materials, UI helpers and the main menu. The level is in PrototypeBuilder.Workplace.cs.
using System.Collections.Generic;
using System.IO;
using TheLastHuman;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.Video;

public static partial class PrototypeBuilder
{
    const string SceneDir = "Assets/Scenes";
    const string MatDir = "Assets/Art/Materials";
    const string CharDir = "Assets/Art/Models/Characters";
    const string PropDir = "Assets/Art/Models/Props";
    const string EnvDir = "Assets/Art/Models/Environment";
    const string FontPath = "Assets/Art/Fonts/Orbitron/Orbitron.ttf";

    // The arcade game's palette. Seven colours, each with one job:
    //   charcoal steel  frames, beams, dark trim            petrol steel   the gate, containers, machinery
    //   warm concrete   floors and wall inserts             oxide orange   robot armour and the colossus (paint, not light)
    //   work-light amber  lamps, robot eye slits, the disguise      signal cyan  battery cores, usable machines, the powered gate
    //   alert red       detection and the boss's wind-up: off until something happens
    static readonly Color32 Charcoal = new Color32(0x1A, 0x22, 0x26, 255);
    static readonly Color32 Petrol = new Color32(0x31, 0x46, 0x4A, 255);
    static readonly Color32 Concrete = new Color32(0x93, 0x8B, 0x7E, 255);
    static readonly Color32 ConcreteFloor = new Color32(0x81, 0x77, 0x6B, 255);
    static readonly Color32 Oxide = new Color32(0xA6, 0x50, 0x32, 255);
    static readonly Color32 Fog = new Color32(0x1A, 0x22, 0x26, 255);
    static readonly Color32 Cream = new Color32(244, 236, 214, 255);
    static readonly Color32 Amber = new Color32(0xF1, 0xAA, 0x48, 255);
    static readonly Color32 Alert = new Color32(0xE5, 0x57, 0x4B, 255);
    static readonly Color32 Panel = new Color32(0x12, 0x18, 0x1B, 215);
    static readonly Color32 Cyan = new Color32(0x6A, 0xCA, 0xC5, 255);
    static readonly Color32 Mint = new Color32(0x6A, 0xCA, 0xC5, 255);          // (the old game's second signal colour: here it is the same cyan)

    static Font font;
    static readonly List<string> log = new List<string>();

    [MenuItem("Tools/The Last Human/Build Prototype Scenes")]
    public static void BuildMenu() => Debug.Log(BuildAll());

    public static string BuildAll()
    {
        log.Clear();
        EnsureLayers(Layers.Minimap, Layers.FirstPerson, Layers.Player, Layers.Robot, Layers.Vehicle, Layers.Hud3D, Layers.Curb, Layers.WallRun);
        TuneRenderPipeline();
        font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null) return "Orbitron font missing at " + FontPath;
        RemoveLegacyMaterials();
        MakeMaterials();            // before the models are imported, so they can pick these materials up by name
        MakeCityMaterials();
        ImportCharacterModels();
        PlayerSettings.productName = "The Last Human";

        // the arcade game is one scene
        BuildArcade();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SceneDir + "/Arcade.unity", true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.Add("build settings: Arcade");
        return string.Join("\n", log);
    }

    /// <summary>Rebuild one scene without re-importing the models (quick, for tuning a level). 'which': Workplace, City or Escape.</summary>
    public static string BuildOne(string which)
    {
        log.Clear();
        font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        MakeMaterials();
        MakeCityMaterials();
        if (which == "Workplace") BuildWorkplace();
        else if (which == "City") BuildCity();
        else if (which == "Escape") BuildEscape();
        else return "unknown scene " + which;
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    // ================================================================ project setup
    static void EnsureLayers(params string[] names)
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        foreach (var name in names)
        {
            bool found = false;
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) found = true;
            if (found) continue;
            for (int i = 6; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                break;
            }
        }
        tags.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    static void TuneRenderPipeline()
    {
        var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (rp == null) return;
        rp.renderScale = 1f;
        rp.msaaSampleCount = 2;
        rp.supportsHDR = true;                 // needed for the bloom on amber lights
        rp.maxAdditionalLightsCount = 8;
        rp.shadowDistance = 70f;               // long enough to keep the sun out of the far end of the facility hall
        EditorUtility.SetDirty(rp);
        log.Add("render pipeline: " + rp.name + " tuned (scale 1, MSAA 2, HDR, 8 lights per object)");
    }

    static void ImportCharacterModels()
    {
        Lit("Human_Skin", new Color32(202, 138, 96, 255), 0.25f, 0f, CharDir + "/Materials");
        Lit("Human_Glove", new Color32(112, 70, 50, 255), 0.2f, 0f, CharDir + "/Materials");
        Lit("Human_GloveDark", new Color32(84, 52, 38, 255), 0.2f, 0f, CharDir + "/Materials");
        Lit("Human_Sleeve", new Color32(46, 82, 80, 255), 0.15f, 0f, CharDir + "/Materials");
        // background worker (same palette as the miner in the menu art)
        var workerColors = new (string name, Color32 color)[]
        {
            ("Worker_Shirt", new Color32(46, 82, 80, 255)), ("Worker_ShirtLight", new Color32(60, 100, 96, 255)),
            ("Worker_Pants", new Color32(60, 72, 80, 255)), ("Worker_PantsLight", new Color32(74, 86, 92, 255)),
            ("Worker_Vest", new Color32(186, 148, 96, 255)), ("Worker_VestDark", new Color32(156, 120, 76, 255)),
            ("Worker_Stripe", new Color32(240, 120, 38, 255)), ("Worker_Belt", new Color32(98, 62, 42, 255)),
            ("Worker_SkinDark", new Color32(150, 100, 72, 255)), ("Worker_Hair", new Color32(50, 36, 28, 255)),
            ("Worker_Hat", new Color32(220, 112, 44, 255)), ("Worker_Black", new Color32(10, 11, 14, 255)),
            ("Worker_Boot", new Color32(70, 46, 34, 255)),
        };
        foreach (var w in workerColors) Lit(w.name, w.color, 0.15f, 0f, CharDir + "/Materials");
        // ore: dark faceted rock with a few glowing crystal faces
        Lit("Ore_Rock", new Color32(62, 58, 72, 255), 0.35f, 0.1f, PropDir + "/Materials");
        Lit("Ore_Crystal", new Color32(20, 12, 6, 255), 0.5f, 0f, PropDir + "/Materials", new Color32(255, 140, 40, 255), 2.2f);
        AssetDatabase.SaveAssets();
        var models = new List<string> { CharDir + "/FP_Arms_Human", CharDir + "/FP_Arms_Robot", CharDir + "/Mask", CharDir + "/Worker" };
        foreach (var ore in new[] { "Ore_A", "Ore_B", "Ore_C", "Ore_D", "Ore_Pile" }) models.Add(PropDir + "/" + ore);
        foreach (var guid in AssetDatabase.FindAssets("Env_ t:Model", new[] { EnvDir }))      // the environment kit
            models.Add(AssetDatabase.GUIDToAssetPath(guid).Replace(".fbx", ""));
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CityDir }))               // Robots City, the Monolith, the car, the waypoint arrow
            models.Add(AssetDatabase.GUIDToAssetPath(guid).Replace(".fbx", ""));
        foreach (var name in models)
        {
            var imp = AssetImporter.GetAtPath(name + ".fbx") as ModelImporter;
            if (imp == null) { log.Add("missing model " + name); continue; }
            imp.bakeAxisConversion = true;
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
            imp.SaveAndReimport();
        }
        log.Add(WorkerAnimationBuilder.Build());
        log.Add(RobotAnimationBuilder.Build());          // also makes sure the robot models carry no avatar
        log.Add(PlayerBodyAnimationBuilder.Build());
    }

    /// <summary>Materials from the first version of the level, replaced by the Env_ set.</summary>
    static void RemoveLegacyMaterials()
    {
        foreach (var name in new[] { "Floor_A", "Floor_B", "Wall", "WallTrim", "Metal", "Rail", "Sleeper", "HazardYellow", "HazardBlack",
                                     "Crate", "CrateDark", "Container", "Belt", "Ore", "LampGlow", "WindowGlow", "ScreenGlow", "AlarmGlow",
                                     "PortalGlow", "PortalBar", "Tower", "City" })
            AssetDatabase.DeleteAsset(MatDir + "/Environment/" + name + ".mat");
        // post-processing profiles are now made per scene (WorkplaceGlitch, CityVisor, ...)
        AssetDatabase.DeleteAsset("Assets/Settings/Prototype/IdentityGlitch.asset");
        AssetDatabase.DeleteAsset("Assets/Settings/Prototype/VisorLook.asset");
    }

    // ================================================================ materials
    static Material Lit(string name, Color color, float smoothness = 0.2f, float metallic = 0f, string dir = null,
                        Color? emission = null, float emissionStrength = 0f)
    {
        dir = dir ?? MatDir + "/Environment";
        Directory.CreateDirectory(dir);
        var path = dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            m.SetColor("_EmissionColor", emission.Value.linear * emissionStrength);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Glow(string name, Color32 color, float strength) =>
        Lit(name, new Color32(10, 10, 10, 255), 0.3f, 0f, null, color, strength);

    /// <summary>Unlit, optionally see-through colour. Used for minimap shapes.</summary>
    static Material Flat(string name, Color color)
    {
        var dir = MatDir + "/Minimap";
        Directory.CreateDirectory(dir);
        var path = dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Sprites/Default");
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }

    static VolumeProfile NewProfile(string name)
    {
        Directory.CreateDirectory("Assets/Settings/Prototype");
        var path = "Assets/Settings/Prototype/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
        return p;
    }

    static T AddOverride<T>(VolumeProfile p) where T : VolumeComponent
    {
        var c = p.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    // ================================================================ UI helpers
    static RectTransform Place(Component c, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(Component c)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static GameObject UiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image Img(string name, Transform parent, Color color)
    {
        var img = UiObject(name, parent).AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text Label(string name, Transform parent, string text, int size, TextAnchor align, Color color,
                      FontStyle style = FontStyle.Normal)
    {
        var t = UiObject(name, parent).AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Button TextButton(string name, Transform parent, string text, int size, out Text label)
    {
        var bg = Img(name, parent, new Color(0, 0, 0, 0));
        bg.raycastTarget = true;
        var b = bg.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        label = Label("Label", bg.transform, text, size, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Stretch(label);
        return b;
    }

    /// <summary>
    /// A keyboard key: a pale cap sitting on a darker lip, with the key's name on it. Every control
    /// hint in the game uses these, so it is obvious at a glance that they mean keys to press.
    /// </summary>
    static RectTransform KeyCap(Transform parent, string key, float height = 34f)
    {
        float width = Mathf.Max(height, height * 0.5f + key.Length * height * 0.44f);
        var lip = Img("Key_" + key, parent, new Color32(118, 110, 94, 255));
        lip.rectTransform.sizeDelta = new Vector2(width, height);
        var cap = Img("Cap", lip.transform, new Color32(240, 234, 216, 255));
        var capRt = Stretch(cap);
        capRt.offsetMin = new Vector2(1.5f, height * 0.14f);
        capRt.offsetMax = new Vector2(-1.5f, -1.5f);
        var label = Label("Label", cap.transform, key, Mathf.RoundToInt(height * 0.48f), TextAnchor.MiddleCenter, new Color32(18, 24, 30, 255), FontStyle.Bold);
        Stretch(label);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        var size = lip.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = width;
        size.minHeight = size.preferredHeight = height;
        return lip.rectTransform;
    }

    /// <summary>
    /// A row of key caps and short captions, e.g. [W][S] DRIVE  [A][D] STEER. It sizes itself to
    /// its contents and grows away from the given anchor.
    /// </summary>
    static GameObject KeyRow(Transform parent, Vector2 anchor, Vector2 position, int textSize, Color textColour, float keyHeight,
                             params (string key, string caption)[] items)
    {
        var row = UiObject("KeyRow", parent);
        Place(row.transform, anchor, anchor, anchor, position, new Vector2(10, keyHeight));
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = row.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var (key, caption) in items)
        {
            if (key != null) KeyCap(row.transform, key, keyHeight);
            if (caption == null) continue;
            var text = Label("Caption", row.transform, caption + "   ", textSize, TextAnchor.MiddleLeft, textColour, FontStyle.Bold);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
        return row;
    }

    /// <summary>Thin frame around a panel.</summary>
    static void Border(Transform panel, Color color, float thickness)
    {
        Vector2 a = Vector2.zero, b = Vector2.one;
        Place(Img("BorderTop", panel, color), new Vector2(0, 1), b, new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, thickness));
        Place(Img("BorderBottom", panel, color), a, new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, thickness));
        Place(Img("BorderLeft", panel, color), a, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(thickness, 0));
        Place(Img("BorderRight", panel, color), new Vector2(1, 0), b, new Vector2(1, 0.5f), Vector2.zero, new Vector2(thickness, 0));
    }

    static Canvas NewCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void NewEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    static void SetLayer(GameObject go, string layer)
    {
        int id = LayerMask.NameToLayer(layer);
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = id;
    }

    // ================================================================ main menu scene
    static void BuildMainMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(6, 18, 20, 255);
        camGo.AddComponent<AudioListener>();
        NewEventSystem();

        var canvas = NewCanvas("MenuCanvas");
        var root = canvas.transform;

        // still frame first (always visible), the video is shown over it once it is ready
        var still = UiObject("StillFrame", root).AddComponent<RawImage>();
        still.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/MenuBackground.png");
        still.raycastTarget = false;
        Cover(still);
        var videoImage = UiObject("Video", root).AddComponent<RawImage>();
        videoImage.raycastTarget = false;
        Cover(videoImage);

        var shade = Img("RightShade", root, new Color(0.02f, 0.07f, 0.08f, 0.38f));
        Place(shade, new Vector2(0.62f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);

        // title block, right side, as in the key art: THE LAST, HUMAN and the amber rule under them are one column,
        // each exactly as wide as the others (the small line is letter-spaced out to the full width)
        const float titleWidth = 620f;
        Vector2 tr = new Vector2(1, 1);
        var block = UiObject("TitleBlock", root);
        var blockRect = Place(block.transform, tr, tr, tr, new Vector2(-70, -70), new Vector2(titleWidth, 300));
        float humanHeight = JustifiedLine(blockRect, "TitleBottom", "HUMAN", titleWidth, 0.09f, 1f, out int humanSize);
        float topHeight = JustifiedLine(blockRect, "TitleTop", "THE LAST", titleWidth, 0f, 2.3f, out _, Mathf.RoundToInt(humanSize * 0.52f));
        float gap = humanHeight * 0.4f;
        blockRect.Find("TitleTop").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
        blockRect.Find("TitleBottom").GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -(topHeight + gap));
        float ruleY = -(topHeight + gap + humanHeight + humanHeight * 0.23f);
        var rule = Img("TitleRule", blockRect, Amber);
        Place(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, ruleY), new Vector2(titleWidth, 4));
        float menuTop = -70f + ruleY - 4f;          // (canvas units from the top of the screen) where the title block ends

        // menu entries. The amber crosshair marker is only there while the mouse is over an entry.
        var marker = UiObject("Marker", root).GetComponent<RectTransform>();
        Place(marker, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-560, menuTop - 130f), new Vector2(60, 60));
        foreach (var (pos, size) in new[] { (new Vector2(-19, 0), new Vector2(20, 5)), (new Vector2(19, 0), new Vector2(20, 5)),
                                            (new Vector2(0, -19), new Vector2(5, 20)), (new Vector2(0, 19), new Vector2(5, 20)) })
            Place(Img("Bar", marker, Amber), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        var play = TextButton("PlayButton", root, "PLAY", 50, out var playLabel);
        Place(play, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-380, menuTop - 130f), new Vector2(300, 80));
        var quit = TextButton("QuitButton", root, "QUIT", 40, out var quitLabel);
        Place(quit, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-380, menuTop - 230f), new Vector2(300, 70));
        foreach (var (button, label) in new[] { (play, playLabel), (quit, quitLabel) })
        {
            var h = button.gameObject.AddComponent<MenuItemHighlight>();
            h.label = label;
            h.marker = marker;
        }

        var fade = Img("Fade", root, Color.black);
        Stretch(fade);

        var menuGo = new GameObject("MainMenu");
        var player = menuGo.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        var menu = menuGo.AddComponent<MainMenuController>();
        menu.video = player;
        menu.videoImage = videoImage;
        menu.playButton = play;
        menu.quitButton = quit;
        menu.fade = fade;
        UnityEventTools.AddPersistentListener(play.onClick, new UnityAction(menu.Play));
        UnityEventTools.AddPersistentListener(quit.onClick, new UnityAction(menu.Quit));

        EditorSceneManager.SaveScene(scene, SceneDir + "/MainMenu.unity");
        log.Add("MainMenu scene saved");
    }

    /// <summary>
    /// One line of the title, set letter by letter so that its ink runs from exactly x = 0 to
    /// x = 'width' inside its holder (whose top-left corner is the top-left of the ink). The font
    /// size is chosen so the letters fill the width at a natural gap of 'gap' x the cap height
    /// (or is given, in which case the letters are spread out to the width); a space is
    /// 'wordGap' letter gaps wide. Returns the height of the capitals; 'size' is the font size used.
    /// </summary>
    static float JustifiedLine(Transform parent, string name, string text, float width, float gap, float wordGap, out int size, int fixedSize = 0)
    {
        var holder = UiObject(name, parent);
        Place(holder.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 10));

        // the ink of one capital at a given size, relative to the left-middle of a box of (2, 1.5) x size
        (float minX, float maxX, float minY, float maxY) Ink(char c, int fontSize)
        {
            var generator = new TextGenerator();
            var settings = new TextGenerationSettings
            {
                font = font, fontSize = fontSize, fontStyle = FontStyle.Bold, color = Color.white, scaleFactor = 1f, lineSpacing = 1f,
                textAnchor = TextAnchor.MiddleLeft, horizontalOverflow = HorizontalWrapMode.Overflow, verticalOverflow = VerticalWrapMode.Overflow,
                generationExtents = new Vector2(fontSize * 2f, fontSize * 1.5f), pivot = new Vector2(0f, 0.5f), generateOutOfBounds = true,
            };
            generator.Populate(c.ToString(), settings);
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var v in generator.verts)
            {
                x0 = Mathf.Min(x0, v.position.x); x1 = Mathf.Max(x1, v.position.x);
                y0 = Mathf.Min(y0, v.position.y); y1 = Mathf.Max(y1, v.position.y);
            }
            if (generator.vertexCount == 0) return (0f, fontSize * 0.8f, -fontSize * 0.36f, fontSize * 0.36f);      // (the font gave nothing: a fair guess)
            return (x0, x1, y0, y1);
        }
        float InkWidth(int fontSize)
        {
            float total = 0f;
            foreach (char c in text) if (c != ' ') { var ink = Ink(c, fontSize); total += ink.maxX - ink.minX; }
            return total;
        }
        int letters = 0, spaces = 0;
        foreach (char c in text) { if (c == ' ') spaces++; else letters++; }
        float gaps = letters - 1 - spaces + spaces * wordGap;             // in letter gaps

        size = fixedSize;
        if (size <= 0)
        {
            // at size 100 the line would be this wide; scale to fit, then take the size just under it
            var h = Ink('H', 100);
            float at100 = InkWidth(100) + gaps * gap * (h.maxY - h.minY);
            size = Mathf.FloorToInt(100f * width / at100);
        }
        var cap = Ink('H', size);
        float capHeight = cap.maxY - cap.minY;
        float each = (width - InkWidth(size)) / Mathf.Max(1f, gaps);     // what is left over is shared out between the letters
        float x = 0f;
        foreach (char c in text)
        {
            if (c == ' ') { x += each * (wordGap - 1f); continue; }
            var ink = Ink(c, size);
            var letter = Label("Letter_" + c, holder.transform, c.ToString(), size, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
            letter.horizontalOverflow = HorizontalWrapMode.Overflow;
            // the box's left-middle is put where the ink's own origin has to be for the ink to start at x and hang from y = 0
            Place(letter, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0f, 0.5f), new Vector2(x - ink.minX, -cap.maxY), new Vector2(size * 2f, size * 1.5f));
            x += ink.maxX - ink.minX + each;
        }
        return capHeight;
    }

    /// <summary>Fill the screen at 16:9 without stretching.</summary>
    static void Cover(RawImage image)
    {
        Stretch(image);
        var fit = image.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = 16f / 9f;
    }

    static void BestFit(Text t, int min, int max)
    {
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = min;
        t.resizeTextMaxSize = max;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
    }
}
