// The arcade game: one scene, about a minute and a half.
// Layout (top view, +z is north):
//   south: the yard. The player starts at its south edge looking straight up the middle lane at
//          the security gate. Walls of stacked crates make the lanes; four batteries stand in them,
//          each watched in a different way; the work pen (where the people are kept) is the
//          south-east corner. Amber work lamps.
//   north: the gate, and behind it the colossus's hall with the plug at its far end. Cool light.
// The level is laid out on the model kits' 4 m grid (Assets/Kits, Quaternius, CC0): their wall,
// floor, door, crate and lamp modules stand where plain boxes used to. Every surface is painted
// from the seven-colour palette at the top of PrototypeBuilder.cs.
using System.Collections.Generic;
using TheLastHuman;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static partial class PrototypeBuilder
{
    const float Yard = 16f;                         // the yard is 32 m square (8 kit cells each way)
    const float GateZ = 16f;
    const float ArenaHalf = 14f, ArenaZ1 = 44f;     // the hall behind the gate: 28 m wide, 28 m deep
    const float ArenaHeight = 10f;
    const string KitDir = "Assets/Kits";
    static readonly Vector3 ArcadeSpawn = new Vector3(0f, 0.05f, -13f);

    static Material aCharcoal, aPetrol, aConcrete, aPaintAmber, aPaintCream, aDead, gAmber, gCool, gCyan,
                    kTrim1, kTrim2, kTrim3, kTrim3Dark, kPadded, kFloorDark, kProps, kGlass, kDecal, kGate;
    static Transform arcadeLampsWarm, arcadeLampsCool;
    static readonly List<Light> machineLights = new List<Light>();
    static readonly List<Renderer> machineGlow = new List<Renderer>();

    // ================================================================ materials
    static Texture2D KitTexture(string path, bool normal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.maxTextureSize != 1024)
        {
            importer.textureType = type;
            importer.maxTextureSize = 1024;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>
    /// A kit material: the kit's own grey panel sheet (and its normal map) painted with one of the
    /// palette colours. 'paint' is the colour the sheet's mid-grey should come out as.
    /// </summary>
    static Material KitLit(string name, Color paint, float sheetGrey, string sheet, float smoothness, float metallic, bool useSheet = true)
    {
        // the sheet multiplies the paint, so the paint is lifted by however dark the sheet is (worked out in linear light)
        float grey = Mathf.GammaToLinearSpace(sheetGrey);
        var lifted = useSheet
            ? new Color(Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(paint.r) / grey), Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(paint.g) / grey),
                        Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(paint.b) / grey), 1f)
            : paint;
        var m = Lit(name, lifted, smoothness, metallic, MatDir + "/Arcade");
        string folder = sheet.StartsWith("T_Props") ? KitDir + "/Essentials/Textures/" : KitDir + "/MegaKit/Textures/";
        m.SetTexture("_BaseMap", useSheet ? KitTexture(folder + sheet + "_BaseColor.png", false) : null);
        var normal = KitTexture(folder + sheet + "_Normal.png", true);
        m.SetTexture("_BumpMap", normal);
        if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
        m.SetFloat("_BumpScale", 1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Recolour(string path, Color colour, Color? emission = null, float strength = 0f)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) return;
        m.SetColor("_BaseColor", colour);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value.linear * strength);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
    }

    static void MakeArcadeMaterials()
    {
        string dir = MatDir + "/Arcade";
        aCharcoal = Lit("A_Charcoal", Charcoal, 0.3f, 0.3f, dir);
        aPetrol = Lit("A_Petrol", Petrol, 0.3f, 0.35f, dir);
        aConcrete = Lit("A_Concrete", Concrete, 0.12f, 0f, dir);
        aPaintAmber = Lit("A_PaintAmber", new Color32(0xD8, 0x98, 0x40, 255), 0.08f, 0f, dir);       // paint on the floor: amber, but not lit
        aPaintCream = Lit("A_PaintCream", new Color32(214, 206, 186, 255), 0.08f, 0f, dir);
        aDead = Lit("A_Dead", new Color32(14, 18, 20, 255), 0.4f, 0.2f, dir);
        gAmber = Lit("A_GlowAmber", new Color32(40, 28, 12, 255), 0.3f, 0f, dir, new Color32(0xF1, 0x98, 0x38, 255), 2.0f);
        gCool = Lit("A_GlowCool", new Color32(30, 36, 38, 255), 0.3f, 0f, dir, new Color32(206, 232, 238, 255), 1.9f);
        gCyan = Lit("A_GlowCyan", new Color32(12, 30, 30, 255), 0.3f, 0f, dir, Cyan, 2.6f);
        // the kit's sheets, painted
        kTrim1 = KitLit("K_Trim1_Petrol", Petrol, 0.42f, "T_Trim_01", 0.32f, 0.35f);
        kTrim2 = KitLit("K_Trim2_Charcoal", new Color32(0x26, 0x30, 0x35, 255), 0.42f, "T_Trim_02", 0.3f, 0.3f);
        kTrim3 = KitLit("K_Trim3_Concrete", Concrete, 0.66f, "T_Trim_03", 0.14f, 0f);
        kTrim3Dark = KitLit("K_Trim3_Dark", Charcoal, 0.66f, "T_Trim_03", 0.2f, 0.1f);
        kPadded = KitLit("K_Padded_Concrete", ConcreteFloor, 0.5f, "T_PaddedWall", 0.14f, 0f);
        kFloorDark = KitLit("K_Floor_Petrol", new Color32(0x3A, 0x4C, 0x50, 255), 0.42f, "T_Trim_02", 0.25f, 0.2f);
        // (the crates are the yard's cover: painted a step lighter than the machinery so they stand clear of the dark walls)
        kProps = KitLit("K_Props_Petrol", new Color32(0x46, 0x62, 0x68, 255), 0.5f, "T_Props_Crates", 0.1f, 0f, false);
        kGate = KitLit("K_Gate_Petrol", new Color32(0x44, 0x60, 0x66, 255), 0.42f, "T_Trim_01", 0.3f, 0.35f);
        kGlass = Lit("K_Glass", new Color32(18, 30, 34, 255), 0.85f, 0.2f, dir);
        // painted markings (the kit's decal sheet, cut out): amber paint, not light
        kDecal = Lit("K_Decal_Paint", new Color32(0xD8, 0x98, 0x40, 255), 0.08f, 0f, dir);
        kDecal.SetTexture("_BaseMap", KitTexture(KitDir + "/MegaKit/Textures/T_Decals.png", false));
        kDecal.SetFloat("_AlphaClip", 1f);
        kDecal.SetFloat("_Cutoff", 0.4f);
        kDecal.EnableKeyword("_ALPHATEST_ON");
        kDecal.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(kDecal);

        // the characters and the old kit, repainted from the same palette (their own colours are reset by the shared builders)
        string robots = "Assets/Art/Models/Robots/Materials/";
        Recolour(robots + "Robot_Orange.mat", Oxide);
        Recolour(robots + "Robot_Blue.mat", new Color32(0x8A, 0x40, 0x28, 255));        // the police units: the same paint, a shade deeper
        Recolour(robots + "Robot_Purple.mat", Oxide);
        Recolour(robots + "Robot_Slate.mat", new Color32(0x3C, 0x48, 0x4C, 255));
        Recolour(robots + "Robot_SlateDark.mat", Charcoal);
        Recolour(robots + "Robot_Glow.mat", new Color32(40, 28, 12, 255), Amber, 3f);
        string city = MatDir + "/City/";
        Recolour(city + "Boss_Bronze.mat", Oxide);
        Recolour(city + "Boss_Iron.mat", new Color32(0x3C, 0x48, 0x4C, 255));
        Recolour(city + "Boss_Dark.mat", Charcoal);
        Recolour(city + "Boss_Glow.mat", new Color32(40, 28, 12, 255), Amber, 2.4f);
        Recolour(city + "Mono_GlowBattery.mat", new Color32(12, 30, 30, 255), Cyan, 3f);
        Recolour(city + "Mono_Plate.mat", Petrol);
        Recolour(city + "Mono_Hull.mat", Charcoal);
        Recolour(city + "Mono_Trim.mat", new Color32(0x4A, 0x5E, 0x62, 255));
        Recolour(city + "Hud_Arrow.mat", Amber, Amber, 0.85f);
        string old = MatDir + "/Environment/";
        Recolour(old + "Env_Wall.mat", Petrol);
        Recolour(old + "Env_WallInset.mat", Charcoal);
        Recolour(old + "Env_Trim.mat", new Color32(0x4A, 0x5E, 0x62, 255));
        Recolour(old + "Env_Metal.mat", new Color32(0x2A, 0x36, 0x3A, 255));
        Recolour(old + "Env_Dark.mat", Charcoal);
        Recolour(old + "Env_Crate.mat", Petrol);
        Recolour(old + "Env_CrateDark.mat", Charcoal);
        Recolour(old + "Env_Container.mat", Petrol);
        Recolour(old + "Env_ContainerDark.mat", new Color32(0x26, 0x38, 0x3C, 255));
        Recolour(old + "Env_Belt.mat", Charcoal);
        Recolour(old + "Env_HideGlow.mat", new Color32(214, 206, 186, 255));              // the hiding mark is cream paint now, not a light
        Recolour(old + "Env_ScreenGlow.mat", new Color32(12, 30, 30, 255), Cyan, 1.4f);
        Recolour(MatDir + "/Minimap/FX_SlamZone.mat", new Color(Alert.r / 255f, Alert.g / 255f, Alert.b / 255f, 0.42f));
        AssetDatabase.SaveAssets();
    }

    /// <summary>Which of our materials a kit model's material slot gets. 'lamp' is what its light panels become here.</summary>
    static Material KitSlot(string slot, Material lamp)
    {
        if (slot.StartsWith("M_LightFade")) return aDead;
        if (slot.StartsWith("M_Light")) return lamp != null ? lamp : gAmber;
        if (slot.StartsWith("M_Glass")) return kGlass;
        if (slot.StartsWith("M_Black")) return aDead;
        if (slot.StartsWith("M_Decal_Screen")) return gCyan;
        if (slot.Contains("Decal")) return kDecal;
        if (slot.StartsWith("MI_Trim_01")) return kTrim1;
        if (slot.StartsWith("MI_Trim_02_Floor")) return kFloorDark;
        if (slot.StartsWith("MI_Trim_02")) return kTrim2;
        if (slot.StartsWith("MI_Trim_03_Dark") || slot.StartsWith("MI_Trim_03_Cables")) return kTrim3Dark;
        if (slot.StartsWith("MI_Trim_03")) return kTrim3;
        if (slot.StartsWith("MI_PaddedWall")) return kPadded;
        if (slot.StartsWith("MI_Props")) return kProps;
        return aCharcoal;
    }

    /// <summary>
    /// A model from the kits ("MegaKit/Models/Walls/WallBand_Straight", "Essentials/Models/Prop_Crate"...),
    /// painted from the palette. 'solid' gives it a box collider of that size standing on its origin.
    /// </summary>
    static GameObject Kit(string model, Vector3 position, float yaw = 0f, float scale = 1f, Transform parent = null, Vector3? solid = null,
                          Material lamp = null, bool shadows = true)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitDir + "/" + model + ".fbx");
        if (prefab == null) { log.Add("missing kit model " + model); return new GameObject(model); }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = model.Substring(model.LastIndexOf('/') + 1);
        go.transform.SetParent(parent != null ? parent : env, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale *= scale;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = KitSlot(mats[i] != null ? mats[i].name : "", lamp);
            r.sharedMaterials = mats;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        }
        if (solid.HasValue)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = solid.Value / scale;
            box.center = new Vector3(0f, solid.Value.y / 2f / scale, 0f);
        }
        if (parent == null) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    /// <summary>
    /// A kit wall module (they stand on one edge of their 4 m cell and face its middle) placed so
    /// that it stands at 'edge' and faces along 'inward'.
    /// </summary>
    static GameObject KitWall(string model, Vector3 edge, Vector3 inward, Material lamp = null)
    {
        inward.y = 0f;
        inward.Normalize();
        float yaw = Mathf.Atan2(inward.z, -inward.x) * Mathf.Rad2Deg;
        return Kit("MegaKit/Models/Walls/" + model, edge + inward * 2f, yaw, 1f, null, null, lamp);
    }

    /// <summary>A straight run of wall: panels to 3 m, a top course to 5 m, a buttress every other bay.</summary>
    static void WallRunKit(Vector3 from, Vector3 to, Vector3 inward, Material lamp, bool lampsOnWall)
    {
        int bays = Mathf.RoundToInt(Vector3.Distance(from, to) / 4f);
        var step = (to - from) / bays;
        for (int i = 0; i < bays; i++)
        {
            var mid = from + step * (i + 0.5f);
            KitWall(i % 2 == 0 ? "WallBand_Straight" : "WallAstra_Straight_Flat", mid, inward);
            KitWall("TopSimple_Straight", mid, inward);
            if (i % 2 == 1) Kit("MegaKit/Models/Columns/Column_Simple", from + step * i + inward * 0.26f, Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg + 90f);
            if (lampsOnWall && i % 2 == 0)
            {
                // a lamp panel high on the wall, and the light it gives
                var at = mid + Vector3.up * 3.6f + inward * 0.12f;
                Kit("MegaKit/Models/Props/Prop_Light_Wide", at, Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg, 1f, null, null, lamp, false);
                bool warm = lamp == gAmber;
                var light = PointLight("WallLamp", at + inward * 1.4f + Vector3.down * 0.5f, warm ? (Color)Amber : (Color)new Color32(206, 232, 238, 255),
                                       warm ? 6.5f : 7f, 11f, warm ? arcadeLampsWarm : arcadeLampsCool);
                if (!warm) machineLights.Add(light);
            }
        }
    }

    /// <summary>Stacked kit crates as one tall block of cover, 'length' m long (along x unless turned), about 3 m high and 1.6 m thick.</summary>
    static void CrateWall(string name, Vector3 centre, float length, bool alongZ)
    {
        var root = new GameObject(name).transform;
        root.SetParent(env, false);
        root.position = centre;
        root.rotation = Quaternion.Euler(0f, alongZ ? 90f : 0f, 0f);
        int n = Mathf.Max(1, Mathf.RoundToInt(length / 3.5f));
        float each = length / n;
        for (int i = 0; i < n; i++)
        {
            float x = -length / 2f + each * (i + 0.5f);
            var low = Kit("Essentials/Models/Prop_Crate_Large", Vector3.zero, 0f, 1f, root);
            low.transform.localPosition = new Vector3(x, 0f, 0f);
            low.transform.localRotation = Quaternion.identity;
            // the upper row: a second crate, or one under a tarpaulin, a hand's width off square so it reads as stacked
            var high = Kit(i % 2 == 0 ? "Essentials/Models/Prop_Crate_Tarp_Large" : "Essentials/Models/Prop_Crate_Large", Vector3.zero, 0f, 1f, root);
            high.transform.localPosition = new Vector3(x + (i % 2 == 0 ? 0.08f : -0.06f), 1.5f, i % 2 == 0 ? 0.04f : -0.05f);
            high.transform.localRotation = Quaternion.Euler(0f, i % 2 == 0 ? 2f : -3f, 0f);
        }
        var solid = root.gameObject.AddComponent<BoxCollider>();
        solid.center = new Vector3(0f, 1.5f, 0f);
        solid.size = new Vector3(length, 3f, 1.6f);
        GameObjectUtility.SetStaticEditorFlags(root.gameObject, StaticEditorFlags.BatchingStatic);
        foreach (Transform child in root) GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);
    }

    // ================================================================ the scene
    static void BuildArcade()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeArcadeMaterials();
        env = new GameObject("Environment").transform;
        mapRoot = new GameObject("MinimapDrawing").transform;       // the shared robot and hide-spot builders draw on it; it is thrown away below
        mapScale = 1f;
        machineLights.Clear();
        machineGlow.Clear();
        hideSpots.Clear();
        arcadeLampsWarm = new GameObject("Lamps_WorkSide").transform;
        arcadeLampsCool = new GameObject("Lamps_MachineSide").transform;

        ArcadeLighting();
        ArcadeYard();
        var gate = ArcadeGate(out var gateStand);
        ArcadeHall();

        // ---- the player and the HUD
        var player = BuildPlayer(out var pivot, out var body, out var flash, out var playerCameras);
        player.transform.SetPositionAndRotation(ArcadeSpawn, Quaternion.identity);
        var controller = player.GetComponent<FirstPersonController>();
        controller.walkSpeed = 4.1f;
        controller.sprintSpeed = 5.8f;
        var identity = player.GetComponent<PlayerIdentity>();
        var hud = BuildHud(out _, out var story, playerCameras[0].transform);
        story.gameObject.SetActive(false);
        hudMinimap.SetActive(false);
        hudStatus.SetActive(false);
        var arcadeHud = ArcadeHudWidgets(hud);
        NewEventSystem();

        var systems = new GameObject("GameSystems");
        systems.AddComponent<Sfx>();
        var detection = systems.AddComponent<DetectionSystem>();
        detection.player = player.transform;
        detection.playerHead = pivot;
        detection.identity = identity;
        detection.workZone = new Bounds(new Vector3(0f, -500f, 0f), Vector3.one);      // nowhere is safe for a human here
        var fx = systems.AddComponent<TransitionFx>();
        fx.volume = ArcadePost();
        fx.hud = hud;
        fx.flash = flash;
        body.fx = fx;
        var interactor = player.GetComponent<PlayerInteractor>();
        interactor.hud = hud;
        interactor.body = body;
        var disguise = player.AddComponent<Disguise>();
        disguise.identity = identity;
        disguise.body = body;
        disguise.player = controller;
        disguise.hud = arcadeHud;
        disguise.fx = fx;

        var flow = systems.AddComponent<ArcadeFlow>();
        flow.hud = hud;
        flow.arcadeHud = arcadeHud;
        flow.player = controller;
        flow.identity = identity;
        flow.detection = detection;
        flow.disguise = disguise;
        flow.gate = gate;
        gate.flow = flow;
        hudPause.player = controller;
        hudPause.flow = flow;

        // ---- hiding places, batteries, patrols
        var spots = new GameObject("HideSpots").transform;
        Closet(spots, new Vector3(-Yard + 0.62f, 0f, 3.5f), 90f, controller, identity, detection, hud);
        Closet(spots, new Vector3(Yard - 0.62f, 0f, 2.6f), -90f, controller, identity, detection, hud);
        HideBox(spots, new Vector3(-14.9f, 0f, 8.3f), controller, identity, detection, hud);
        HideBox(spots, new Vector3(14.9f, 0f, -2.6f), controller, identity, detection, hud);
        foreach (var spot in hideSpots)
        {
            // the cue for "you can hide here" is a painted mark, the same on every one, instead of a coloured light
            foreach (var l in spot.GetComponentsInChildren<Light>()) Object.DestroyImmediate(l.gameObject);
            var pulse = spot.GetComponent<GlowPulse>();
            if (pulse != null) Object.DestroyImmediate(pulse);
            HideMark(spot);
        }

        var batteryRoot = new GameObject("Batteries").transform;
        var batteries = new List<BatteryPickup>
        {
            Battery(batteryRoot, flow, new Vector3(-13.5f, 0f, -5.5f)),
            Battery(batteryRoot, flow, new Vector3(9f, 0f, 4f)),
            Battery(batteryRoot, flow, new Vector3(-13.5f, 0f, 13f)),
            Battery(batteryRoot, flow, new Vector3(5.2f, 0f, 13.6f)),
        };
        flow.batteries = batteries.ToArray();

        var robotRoot = new GameObject("Patrols").transform;
        RobotCount = 0;
        // the middle lane: the first thing the player sees is this one walking toward them
        // (a patrol's sight is a triangle 8 m deep, which reaches 10.4 m along its edges: it turns back 11.5 m short
        // of the start, so a player who has not moved is never seen, and one who has stepped out is)
        Robot(robotRoot, "Patrol_Lane", "Orange", 1.7f, 180f, At(0f, 6f, 0.2f), At(0f, -1.5f, 1.4f, 180f), At(0f, 11f, 1.2f, 0f));
        // the east side: round the crate wall and through the gap where a battery stands (its southern leg keeps the same distance)
        Robot(robotRoot, "Patrol_East", "Orange", 1.7f, 0f, At(13.5f, -2f, 0.8f), At(13.5f, 4f, 0.6f), At(3.2f, 4f, 1.2f, 180f), At(3.2f, -2f, 0.6f));
        // two sentries that only turn: one over the north-west battery, one beside the gate
        Robot(robotRoot, "Sentry_West", "Blue", 1.4f, 270f, At(-7.5f, 12.6f, 3.2f, 270f), At(-7.5f, 12.6f, 2.6f, 180f));
        Robot(robotRoot, "Sentry_Gate", "Blue", 1.4f, 270f, At(9.6f, 13.2f, 3.4f, 270f), At(9.6f, 13.2f, 2.2f, 180f));
        var patrols = robotRoot.GetComponentsInChildren<RobotPatrol>();
        foreach (var patrol in patrols)
        {
            patrol.suspectSeconds = 0.6f;               // "?" is brief, but long enough to read
            patrol.closeSuspectSeconds = 0.3f;
            patrol.chaseSpeed = 6.9f;                   // faster than the player can run
            patrol.searchSeconds = 2f;
            var vision = patrol.GetComponent<RobotVision>();
            vision.viewDistance = 8f;
            vision.viewAngle = 80f;
        }

        // ---- the colossus, the plug, the ending
        var fight = ArcadeBoss(systems, flow, hud, arcadeHud, controller, fx, out var boss);
        flow.fight = fight;
        var plug = ArcadePlug(flow);
        flow.plug = plug;
        flow.arenaEntry = new GameObject("ArenaEntry").transform;
        flow.arenaEntry.SetPositionAndRotation(new Vector3(0f, 0.05f, GateZ + 3.4f), Quaternion.identity);
        flow.arenaLine = GateZ + 2.2f;

        var guide = systems.AddComponent<Guide>();
        guide.viewer = player.transform;
        guide.marker = new GameObject("GuideMarker").transform;
        var corners = new List<Transform>();
        var cornerRoot = new GameObject("GuideCorners").transform;
        foreach (float x in new[] { -13.5f, -7.5f, 0f, 7.5f, 13.5f })
            foreach (float z in new[] { -12f, -5f, 4f, 12f })
            {
                if (x > 6f && z < -8f) continue;        // that corner is the work pen
                var corner = new GameObject("Corner").transform;
                corner.SetParent(cornerRoot, false);
                corner.position = new Vector3(x, 0f, z);
                corners.Add(corner);
            }
        guide.corners = corners.ToArray();
        flow.guide = guide;

        flow.finale = ArcadeFinale(systems, hud, arcadeHud, controller, playerCameras, patrols);

        // the RETRY and REPLAY buttons
        UnityEventTools.AddPersistentListener(hud.transform.Find("Loss/RetryButton").GetComponent<Button>().onClick, new UnityAction(flow.Retry));
        UnityEventTools.AddPersistentListener(hud.transform.Find("Win/ReplayButton").GetComponent<Button>().onClick, new UnityAction(flow.Replay));

        Object.DestroyImmediate(mapRoot.gameObject);
        mapRoot = null;
        System.IO.Directory.CreateDirectory(SceneDir);
        if (!EditorSceneManager.SaveScene(scene, SceneDir + "/Arcade.unity")) log.Add("THE SCENE WAS NOT SAVED");
        log.Add("Arcade scene saved: " + env.childCount + " environment pieces, " + patrols.Length + " patrols, " + batteries.Count + " batteries, "
                + hideSpots.Count + " hiding places, " + guide.corners.Length + " guide corners");
    }

    // ================================================================ light
    static void ArcadeLighting()
    {
        // dusk: one cool key light from the north-west that throws every shadow, a dim sky, a little haze
        var key = new GameObject("KeyLight").AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color32(204, 214, 222, 255);
        key.intensity = 0.9f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.85f;
        key.transform.rotation = Quaternion.Euler(48f, 152f, 0f);

        var sky = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Arcade/A_Sky.mat");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, MatDir + "/Arcade/A_Sky.mat");
        }
        sky.shader = Shader.Find("Skybox/Procedural");
        sky.SetFloat("_SunSize", 0f);
        // (an overcast evening: grey with a little steel in it, not a blue night)
        sky.SetFloat("_AtmosphereThickness", 0.35f);
        sky.SetColor("_SkyTint", new Color32(150, 150, 146, 255));
        sky.SetColor("_GroundColor", new Color32(40, 42, 42, 255));
        sky.SetFloat("_Exposure", 0.42f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.sun = key;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(96, 104, 110, 255);
        RenderSettings.ambientEquatorColor = new Color32(84, 84, 80, 255);
        RenderSettings.ambientGroundColor = new Color32(46, 44, 40, 255);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color32(52, 58, 60, 255);
        RenderSettings.fogStartDistance = 26f;
        RenderSettings.fogEndDistance = 110f;
    }

    /// <summary>The picture's grade (a little contrast, restrained bloom, a soft edge) and the brief tear used when the disguise fails.</summary>
    static Volume ArcadePost()
    {
        var look = NewProfile("ArcadeLook");
        var bloom = AddOverride<Bloom>(look);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.55f);
        bloom.scatter.Override(0.6f);
        AddOverride<Tonemapping>(look).mode.Override(TonemappingMode.Neutral);
        var vignette = AddOverride<Vignette>(look);
        vignette.intensity.Override(0.26f);
        vignette.smoothness.Override(0.45f);
        var grade = AddOverride<ColorAdjustments>(look);
        grade.contrast.Override(10f);
        grade.saturation.Override(4f);
        var baseVolume = new GameObject("PostFX").AddComponent<Volume>();
        baseVolume.isGlobal = true;
        baseVolume.sharedProfile = look;

        var glitch = NewProfile("ArcadeGlitch");
        AddOverride<ChromaticAberration>(glitch).intensity.Override(0.9f);
        AddOverride<LensDistortion>(glitch).intensity.Override(-0.3f);
        AddOverride<FilmGrain>(glitch).intensity.Override(0.5f);
        var glitchVolume = new GameObject("PostFX_Tear").AddComponent<Volume>();
        glitchVolume.isGlobal = true;
        glitchVolume.priority = 10;
        glitchVolume.weight = 0f;
        glitchVolume.sharedProfile = glitch;
        EditorUtility.SetDirty(look);
        EditorUtility.SetDirty(glitch);
        return glitchVolume;
    }

    // ================================================================ the yard
    static void ArcadeYard()
    {
        // ground: one solid slab under everything, and the kit's floor plates over it
        Box("Ground", new Vector3(0f, -0.5f, 14f), new Vector3(60f, 1f, 76f), aConcrete);
        for (float x = -14f; x <= 14f; x += 4f)
            for (float z = -14f; z <= 14f; z += 4f)
            {
                bool lane = Mathf.Abs(x) < 3f;                              // the middle lane, straight to the gate
                bool machine = z > 9f;                                      // the strip in front of the gate
                Kit("MegaKit/Models/Platforms/" + (machine ? "Platform_DarkPlates" : lane ? "Platform_3Plates" : "Platform_Simple"),
                    new Vector3(x, 0.012f, z), ((int)(x + z) / 4 % 2) * 90f, 1f, null, null, null, false);
            }

        // walls: amber-lit on the three sides of the work yard, cool at the machine end (the gate itself is ArcadeGate's)
        WallRunKit(new Vector3(-Yard, 0f, -Yard), new Vector3(Yard, 0f, -Yard), Vector3.forward, gAmber, true);
        WallRunKit(new Vector3(-Yard, 0f, -Yard), new Vector3(-Yard, 0f, Yard), Vector3.right, gAmber, true);
        WallRunKit(new Vector3(Yard, 0f, -Yard), new Vector3(Yard, 0f, Yard), Vector3.left, gAmber, true);
        WallRunKit(new Vector3(-Yard, 0f, GateZ), new Vector3(-4f, 0f, GateZ), Vector3.back, gCool, false);
        WallRunKit(new Vector3(4f, 0f, GateZ), new Vector3(Yard, 0f, GateZ), Vector3.back, gCool, false);
        foreach (var (centre, size) in new[]
        {
            (new Vector3(0f, 3f, -Yard - 0.5f), new Vector3(34f, 6f, 1f)), (new Vector3(-Yard - 0.5f, 3f, 0f), new Vector3(1f, 6f, 34f)),
            (new Vector3(Yard + 0.5f, 3f, 0f), new Vector3(1f, 6f, 34f)),
            (new Vector3(-9.9f, 3f, GateZ + 0.5f), new Vector3(12.2f, 6f, 1f)), (new Vector3(9.9f, 3f, GateZ + 0.5f), new Vector3(12.2f, 6f, 1f)),
        })
        {
            var wall = new GameObject("WallSolid");
            wall.transform.SetParent(env, false);
            wall.transform.position = centre;
            wall.AddComponent<BoxCollider>().size = size;
        }
        // (the amber lamps in the far north of the side walls would warm the machine end: those two bays are cool instead)
        foreach (Transform lamp in arcadeLampsWarm)
            if (lamp.position.z > 9f) lamp.GetComponent<Light>().color = new Color32(206, 232, 238, 255);

        // cover: four walls of stacked crates that make the lanes, and one stack by the start to step behind
        CrateWall("Cover_SouthWest", new Vector3(-7.5f, 0f, 0f), 7f, false);
        CrateWall("Cover_SouthEast", new Vector3(7.5f, 0f, 0f), 7f, false);
        CrateWall("Cover_NorthWest", new Vector3(-8.5f, 0f, 7.7f), 7f, false);
        CrateWall("Cover_NorthEast", new Vector3(8.5f, 0f, 7.7f), 7f, false);
        CrateWall("Cover_Start", new Vector3(-7.4f, 0f, -7.2f), 3.5f, false);
        // a few single pieces where a lane would otherwise be a bare corridor (none of them tall enough to hide behind standing)
        Kit("Essentials/Models/Prop_Barrel1", new Vector3(-11.2f, 0f, -1.4f), 20f, 1f, null, new Vector3(0.7f, 1.1f, 0.7f));
        Kit("Essentials/Models/Prop_Barrel1", new Vector3(-10.4f, 0f, -1.7f), 70f, 1f, null, new Vector3(0.7f, 1.1f, 0.7f));
        Kit("Essentials/Models/Prop_Crate", new Vector3(11.6f, 0f, 9.6f), 12f, 1f, null, new Vector3(1.5f, 1.5f, 1.5f));
        Kit("Essentials/Models/Prop_Shelves_WideTall", new Vector3(-15.5f, 0f, -9f), 90f, 1f, null, new Vector3(0.6f, 2.7f, 2f));
        Kit("Essentials/Models/Prop_Shelves_WideTall", new Vector3(-15.5f, 0f, -11.1f), 90f, 1f, null, new Vector3(0.6f, 2.7f, 2f));

        // painted on the floor (paint, not light): the lane's two edges, and a stop line with chevrons in front of the gate
        foreach (float s in new[] { -1f, 1f })
            Box("LaneEdge", new Vector3(s * 3.6f, 0.02f, -1.5f), new Vector3(0.14f, 0.012f, 21f), aPaintCream, false);
        Box("GateStopLine", new Vector3(0f, 0.02f, 12.6f), new Vector3(7.2f, 0.012f, 0.3f), aPaintAmber, false);
        for (int i = -4; i <= 4; i++)
            Box("GateHatch", new Vector3(i * 0.8f, 0.02f, 13.5f), new Vector3(0.28f, 0.012f, 1.3f), aPaintAmber, false, null, 35f);

        ArcadePen();
    }

    /// <summary>The work pen in the south-east corner: bars, a sliding gate, a conveyor, and the people kept at it.</summary>
    static void ArcadePen()
    {
        const float x0 = 8f, z1 = -9f;                      // its two open sides (the yard's own walls close the others)
        void Bars(string name, Vector3 from, Vector3 to, Transform parent)
        {
            float length = Vector3.Distance(from, to);
            var direction = (to - from) / length;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var mid = (from + to) / 2f;
            Box(name + "RailTop", mid + Vector3.up * 2.7f, new Vector3(0.1f, 0.1f, length), aCharcoal, false, null, yaw, parent);
            Box(name + "RailLow", mid + Vector3.up * 0.25f, new Vector3(0.1f, 0.1f, length), aCharcoal, false, null, yaw, parent);
            for (float d = 0.2f; d < length; d += 0.42f)
                Box(name + "Bar", from + direction * d + Vector3.up * 1.45f, new Vector3(0.06f, 2.5f, 0.06f), aCharcoal, false, null, yaw, parent);
        }
        var pen = new GameObject("WorkPen").transform;
        pen.SetParent(env, false);
        Bars("PenWest", new Vector3(x0, 0f, -Yard), new Vector3(x0, 0f, z1), pen);
        Bars("PenNorthA", new Vector3(x0, 0f, z1), new Vector3(10.4f, 0f, z1), pen);
        Bars("PenNorthB", new Vector3(13.6f, 0f, z1), new Vector3(Yard, 0f, z1), pen);
        foreach (var (centre, size) in new[]
        {
            (new Vector3(x0, 1.4f, (-Yard + z1) / 2f), new Vector3(0.2f, 2.8f, Yard + z1)),
            (new Vector3(9.2f, 1.4f, z1), new Vector3(2.4f, 2.8f, 0.2f)), (new Vector3(14.8f, 1.4f, z1), new Vector3(2.4f, 2.8f, 0.2f)),
        })
        {
            var solid = new GameObject("PenSolid");
            solid.transform.SetParent(pen, false);
            solid.transform.position = centre;
            solid.AddComponent<BoxCollider>().size = size;
        }
        foreach (float x in new[] { 10.4f, 13.6f })
            Box("PenGatePost", new Vector3(x, 1.5f, z1), new Vector3(0.24f, 3f, 0.24f), aPetrol, false, null, 0f, pen);
        // the gate: shut and solid until the ending slides it back
        var gate = new GameObject("PenGate").transform;
        gate.SetParent(pen, false);
        gate.position = new Vector3(12f, 0f, z1);
        Bars("PenGate", new Vector3(10.5f, 0f, z1), new Vector3(13.5f, 0f, z1), gate);
        var gateSolid = gate.gameObject.AddComponent<BoxCollider>();
        gateSolid.center = new Vector3(0f, 1.4f, 0f);
        gateSolid.size = new Vector3(3f, 2.8f, 0.2f);

        // painted on the yard floor just outside the pen's gate: the line the people in it do not cross
        Box("PenLine", new Vector3(12f, 0.02f, z1 + 0.55f), new Vector3(3.6f, 0.012f, 0.24f), aPaintAmber, false);

        // inside: the belt along the south wall, and a lamp over it
        for (int i = 0; i < 3; i++)
            Prop("Conveyor", new Vector3(10f + i * 2f, 0f, -14.9f), 0f, null, new Vector3(2f, 0.98f, 1.0f), pen);
        PointLight("PenLamp", new Vector3(12f, 3.2f, -12.5f), Amber, 8f, 9f, arcadeLampsWarm);
    }

    // ================================================================ the gate
    static SecurityGate ArcadeGate(out Transform stand)
    {
        const float s = 1.3f;                                   // the kit's door, a third larger: 5.5 m of opening, 6.5 m tall
        float leaf = 2.11f * s;
        var root = new GameObject("SecurityGate").transform;
        root.position = new Vector3(0f, 0f, GateZ);
        var gate = root.gameObject.AddComponent<SecurityGate>();
        gate.range = 3.2f;

        Kit("MegaKit/Models/Platforms/Door_Frame_SquareTall", root.position, 0f, s, root);
        // two great columns carry it, a header ties them, and the wall closes in to the frame on either side
        foreach (float side in new[] { -1f, 1f })
        {
            Kit("MegaKit/Models/Columns/Column_Large_Straight", root.position + new Vector3(side * 3.95f, 0f, -0.2f), side > 0f ? 0f : 180f, 1f, root);
            var fill = new GameObject("GateFill");
            fill.transform.SetParent(root, false);
            fill.transform.position = root.position + new Vector3(side * 3.6f, 3.25f, 0.4f);
            fill.AddComponent<BoxCollider>().size = new Vector3(1.9f, 6.5f, 1.4f);
        }
        Box("GateHeader", root.position + new Vector3(0f, 7.3f, 0.1f), new Vector3(9.4f, 1.5f, 1.3f), aPetrol, false, null, 0f, root);
        Box("GateHeaderLip", root.position + new Vector3(0f, 6.5f, -0.5f), new Vector3(7.2f, 0.14f, 0.3f), aCharcoal, false, null, 0f, root);
        // status lamps in the header: amber while it is locked, cyan the moment the fourth battery is in
        var status = new List<Renderer>();
        foreach (float x in new[] { -2.4f, 0f, 2.4f })
            status.Add(Box("GateStatus", root.position + new Vector3(x, 7.3f, -0.57f), new Vector3(1.5f, 0.22f, 0.06f), gAmber, false, null, 0f, root).GetComponent<Renderer>());
        gate.statusLamps = status.ToArray();
        gate.statusOn = gCyan;
        var lights = new List<Light>();
        foreach (float x in new[] { -2.4f, 2.4f })
            lights.Add(PointLight("GateLight", root.position + new Vector3(x, 3.4f, -2.6f), new Color32(206, 232, 238, 255), 11f, 9f, root));
        gate.gateLights = lights.ToArray();
        machineLights.AddRange(lights);
        // cool work light over the approach, and one square on the doors: the gate is the brightest thing at this end of the yard
        machineLights.Add(SpotLight("GateSpot", root.position + new Vector3(0f, 8.5f, -6f), root.position + new Vector3(0f, 0f, -2.5f),
                                    new Color32(206, 232, 238, 255), 34f, 16f, 74f, arcadeLampsCool));
        machineLights.Add(SpotLight("GateDoorSpot", root.position + new Vector3(0f, 6.2f, -6.5f), root.position + new Vector3(0f, 2.2f, 0f),
                                    new Color32(206, 232, 238, 255), 30f, 12f, 58f, arcadeLampsCool));

        // the two leaves, each with two sockets across it at chest height
        var sockets = new List<Transform>();
        var seated = new List<GameObject>();
        var lamps = new List<Renderer>();
        foreach (float side in new[] { -1f, 1f })
        {
            var holder = new GameObject(side < 0f ? "LeafLeft" : "LeafRight").transform;
            holder.SetParent(root, false);
            var door = Kit("MegaKit/Models/Platforms/Door_DarkMetal", root.position + new Vector3(side < 0f ? -leaf : 0f, 0f, 0.12f), 0f, s, holder);
            foreach (var r in door.GetComponentsInChildren<Renderer>()) r.sharedMaterial = kGate;
            Box("LeafBand", root.position + new Vector3(side * leaf / 2f, 1.35f, -0.06f), new Vector3(leaf - 0.2f, 1.1f, 0.1f), aCharcoal, false, null, 0f, holder);
            foreach (float along in new[] { 0.28f, 0.74f })
            {
                // a socket: a dark ring that lights when it is filled, a cream-painted frame, and the hole the battery goes in
                var at = root.position + new Vector3(side * leaf * along, 1.35f, -0.12f);
                var socket = new GameObject("Socket").transform;
                socket.SetParent(holder, false);
                socket.SetPositionAndRotation(at, Quaternion.Euler(0f, 180f, 0f));
                lamps.Add(Box("SocketRing", at + new Vector3(0f, 0f, 0.02f), new Vector3(0.86f, 0.86f, 0.04f), aDead, false, null, 0f, socket).GetComponent<Renderer>());
                Box("SocketFrame", at, new Vector3(0.68f, 0.68f, 0.06f), aPaintCream, false, null, 0f, socket);
                Box("SocketHole", at + new Vector3(0f, 0f, -0.012f), new Vector3(0.44f, 0.44f, 0.05f), aDead, false, null, 0f, socket);
                // the battery that will sit in it: lying on its side, its base buried in the door
                var cell = CityProp("Mono_Battery", at + new Vector3(0f, 0f, 0.1f), 0f, 0.24f, false, socket);
                cell.name = "SeatedBattery";
                cell.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                seated.Add(cell);
                sockets.Add(socket);
            }
            if (side < 0f) gate.leafLeft = holder; else gate.leafRight = holder;
        }
        // (left to right as the player sees them)
        gate.sockets = new[] { sockets[1], sockets[0], sockets[2], sockets[3] };
        gate.seated = new[] { seated[1], seated[0], seated[2], seated[3] };
        gate.socketLamps = new[] { lamps[1], lamps[0], lamps[2], lamps[3] };
        gate.lampOff = aDead;
        gate.lampOn = gCyan;
        gate.leafTravel = leaf - 0.1f;
        var barrier = new GameObject("GateBarrier");
        barrier.transform.SetParent(root, false);
        barrier.transform.position = root.position + new Vector3(0f, 2.6f, 0.1f);
        var block = barrier.AddComponent<BoxCollider>();
        block.size = new Vector3(leaf * 2f, 5.2f, 0.5f);
        gate.barrier = block;
        stand = new GameObject("GateStand").transform;
        stand.SetParent(root, false);
        stand.position = root.position + new Vector3(0f, 0f, -1.5f);
        gate.standPoint = stand;
        return gate;
    }

    /// <summary>A battery on its stand: the core lit cyan, turning slowly. Walking into it takes it.</summary>
    static BatteryPickup Battery(Transform root, ArcadeFlow flow, Vector3 at)
    {
        var go = new GameObject("Battery");
        go.transform.SetParent(root, false);
        go.transform.position = at;
        Kit("MegaKit/Models/Props/Prop_ItemHolder", at, 0f, 1f, go.transform);
        var cell = CityProp("Mono_Battery", at + Vector3.up * 0.62f, 0f, 0.42f, false, go.transform);
        var pickup = go.AddComponent<BatteryPickup>();
        pickup.flow = flow;
        pickup.cell = cell.transform;
        pickup.glow = PointLight("BatteryGlow", at + Vector3.up * 1.2f, Cyan, 5f, 5f, go.transform);
        return pickup;
    }

    /// <summary>The mark that says "you can hide here": a cream square with an eye struck through, painted on the spot and on the floor in front of it.</summary>
    static void HideMark(HideSpot spot)
    {
        var material = Flat("A_HideMark", Color.white);
        material.mainTexture = HideIcon();
        EditorUtility.SetDirty(material);
        bool closet = spot.kind == HideKind.Closet;
        var t = spot.transform;
        void Mark(string name, Vector3 local, Quaternion rotation, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(t, false);
            quad.transform.localPosition = local;
            quad.transform.localRotation = rotation;
            quad.transform.localScale = Vector3.one * size;
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
        // (a quad faces along its own -z: the closet's front is its +z, the box is marked on all four sides)
        if (closet) Mark("HideMarkDoor", new Vector3(0f, 2.6f, 0.56f), Quaternion.Euler(0f, 180f, 0f), 0.56f);
        else
            for (int i = 0; i < 4; i++)
                Mark("HideMarkSide", Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 1.3f, 0.77f), Quaternion.Euler(0f, i * 90f + 180f, 0f), 0.56f);
    }

    /// <summary>The hiding sign: a dark plate with a cream border, and on it an eye with a line struck through.</summary>
    static Texture2D HideIcon()
    {
        const string path = "Assets/Art/UI/HideMark.png";
        const int n = 128;
        Color plate = new Color32(26, 34, 38, 240), paint = new Color32(226, 218, 198, 255);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float box = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                float inside = 1f - Mathf.SmoothStep(0.94f, 0.98f, box);
                float border = Mathf.SmoothStep(0.81f, 0.84f, box);
                // the eye is where two discs overlap; its outline is the rim of that shape
                float almond = Mathf.Max(Mathf.Sqrt(u * u + (v - 0.45f) * (v - 0.45f)), Mathf.Sqrt(u * u + (v + 0.45f) * (v + 0.45f)));
                float outline = Mathf.SmoothStep(0.72f, 0.75f, almond) * (1f - Mathf.SmoothStep(0.83f, 0.86f, almond));
                float pupil = 1f - Mathf.SmoothStep(0.17f, 0.2f, Mathf.Sqrt(u * u + v * v));
                // the stroke through it, with a clear margin either side so it reads as struck out
                float off = Mathf.Abs(u - v) / 1.4142f;
                bool along = Mathf.Abs(u + v) < 1.12f;
                float stroke = along ? 1f - Mathf.SmoothStep(0.055f, 0.085f, off) : 0f;
                float margin = along ? 1f - Mathf.SmoothStep(0.15f, 0.18f, off) : 0f;
                float ink = Mathf.Clamp01(Mathf.Max(border, Mathf.Max(stroke, Mathf.Max(outline, pupil) * (1f - margin))));
                var c = Color.Lerp(plate, paint, ink);
                c.a = inside * Mathf.Lerp(plate.a, 1f, ink);
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        System.IO.Directory.CreateDirectory("Assets/Art/UI");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ================================================================ the colossus's hall
    static void ArcadeHall()
    {
        float z0 = GateZ, mid = (GateZ + ArenaZ1) / 2f;
        for (float x = -12f; x <= 12f; x += 4f)
            for (float z = z0 + 2f; z < ArenaZ1; z += 4f)
                Kit("MegaKit/Models/Platforms/" + (Mathf.Abs(x) < 1f || Mathf.Abs(z - mid) < 1f ? "Platform_CenterPlate" : "Platform_Squares"),
                    new Vector3(x, 0.012f, z), 0f, 1f, null, null, null, false);
        // walls: the kit to 5 m, plain dark steel above to the roof
        WallRunKit(new Vector3(-ArenaHalf, 0f, z0), new Vector3(-ArenaHalf, 0f, ArenaZ1), Vector3.right, gCool, true);
        WallRunKit(new Vector3(ArenaHalf, 0f, z0), new Vector3(ArenaHalf, 0f, ArenaZ1), Vector3.left, gCool, true);
        WallRunKit(new Vector3(-ArenaHalf, 0f, ArenaZ1), new Vector3(ArenaHalf, 0f, ArenaZ1), Vector3.back, gCool, false);
        WallRunKit(new Vector3(-ArenaHalf + 2f, 0f, z0 + 1f), new Vector3(-6f, 0f, z0 + 1f), Vector3.forward, gCool, false);
        WallRunKit(new Vector3(6f, 0f, z0 + 1f), new Vector3(ArenaHalf - 2f, 0f, z0 + 1f), Vector3.forward, gCool, false);
        float upper = (ArenaHeight + 5f) / 2f, tall = ArenaHeight - 5f;
        Box("HallUpperWest", new Vector3(-ArenaHalf - 0.3f, upper, mid), new Vector3(0.6f, tall, ArenaZ1 - z0), aCharcoal, false);
        Box("HallUpperEast", new Vector3(ArenaHalf + 0.3f, upper, mid), new Vector3(0.6f, tall, ArenaZ1 - z0), aCharcoal, false);
        Box("HallUpperNorth", new Vector3(0f, upper, ArenaZ1 + 0.3f), new Vector3(ArenaHalf * 2f, tall, 0.6f), aCharcoal, false);
        Box("HallUpperSouth", new Vector3(0f, upper + 1.6f, z0 + 0.7f), new Vector3(ArenaHalf * 2f, tall - 3.2f, 0.6f), aCharcoal, false);
        Box("HallRoof", new Vector3(0f, ArenaHeight + 0.2f, mid), new Vector3(ArenaHalf * 2f + 1.2f, 0.4f, ArenaZ1 - z0 + 1.2f), aCharcoal, false);
        foreach (var (centre, size) in new[]
        {
            (new Vector3(-ArenaHalf - 0.5f, 5f, mid), new Vector3(1f, 10f, ArenaZ1 - z0)), (new Vector3(ArenaHalf + 0.5f, 5f, mid), new Vector3(1f, 10f, ArenaZ1 - z0)),
            (new Vector3(0f, 5f, ArenaZ1 + 0.5f), new Vector3(ArenaHalf * 2f, 10f, 1f)),
        })
        {
            var wall = new GameObject("HallSolid");
            wall.transform.SetParent(env, false);
            wall.transform.position = centre;
            wall.AddComponent<BoxCollider>().size = size;
        }
        // four columns and the roof beams they carry, with the lamps hung from the beams: cool, from overhead
        foreach (float z in new[] { z0 + 7f, ArenaZ1 - 7f })
        {
            foreach (float x in new[] { -ArenaHalf + 1.2f, ArenaHalf - 1.2f })
                Kit("MegaKit/Models/Columns/Column_Large_Straight", new Vector3(x, 0f, z), x < 0f ? 90f : -90f, 1f);
            Box("RoofBeam", new Vector3(0f, ArenaHeight - 0.5f, z), new Vector3(ArenaHalf * 2f, 0.7f, 0.9f), aPetrol, false);
            foreach (float x in new[] { -6f, 6f })
            {
                machineGlow.Add(Box("RoofLamp", new Vector3(x, ArenaHeight - 0.9f, z), new Vector3(3f, 0.1f, 0.5f), gCool, false).GetComponent<Renderer>());
                machineLights.Add(SpotLight("RoofSpot", new Vector3(x, ArenaHeight - 1f, z), new Vector3(x * 0.6f, 0f, z), new Color32(206, 232, 238, 255),
                                            34f, 16f, 78f, arcadeLampsCool));
            }
        }
        // a painted ring where it sits (paint, not light)
        Vector3 home = new Vector3(0f, 0f, mid);
        for (int i = 0; i < 24; i++)
        {
            float a = i / 24f * Mathf.PI * 2f;
            if (i % 2 == 0) Box("HomeRing", home + new Vector3(Mathf.Sin(a) * 4.6f, 0.02f, Mathf.Cos(a) * 4.6f), new Vector3(0.9f, 0.012f, 0.22f), aPaintAmber, false, null, a * Mathf.Rad2Deg + 90f);
        }
    }

    static TokenFight ArcadeBoss(GameObject systems, ArcadeFlow flow, HudController hud, ArcadeHud arcadeHud, FirstPersonController controller,
                                 TransitionFx fx, out BossController boss)
    {
        float mid = (GateZ + ArenaZ1) / 2f;
        var home = new GameObject("BossHome").transform;
        home.SetPositionAndRotation(new Vector3(0f, 0f, mid), Quaternion.Euler(0f, 180f, 0f));
        var bossGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CityDir + "/Robot_Boss.fbx"));
        bossGo.name = "Colossus";
        bossGo.transform.SetPositionAndRotation(home.position, home.rotation);
        SetLayer(bossGo, Layers.Robot);
        var capsule = bossGo.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 2.9f, 0f);
        capsule.height = 5.8f;
        capsule.radius = 1.25f;
        Solidify(bossGo);
        boss = bossGo.AddComponent<BossController>();
        Transform Bone(string path) => bossGo.transform.Find(path);
        boss.hips = Bone("Hips");
        boss.chest = Bone("Hips/Chest");
        boss.head = Bone("Hips/Chest/Head");
        boss.upperArmL = Bone("Hips/Chest/UpperArm.L");
        boss.forearmL = Bone("Hips/Chest/UpperArm.L/Forearm.L");
        boss.upperArmR = Bone("Hips/Chest/UpperArm.R");
        boss.forearmR = Bone("Hips/Chest/UpperArm.R/Forearm.R");
        boss.thighL = Bone("Hips/Thigh.L");
        boss.shinL = Bone("Hips/Thigh.L/Shin.L");
        boss.footL = Bone("Hips/Thigh.L/Shin.L/Foot.L");
        boss.thighR = Bone("Hips/Thigh.R");
        boss.shinR = Bone("Hips/Thigh.R/Shin.R");
        boss.footR = Bone("Hips/Thigh.R/Shin.R/Foot.R");
        boss.glowRenderers = bossGo.GetComponentsInChildren<MeshRenderer>();
        boss.coreLight = PointLight("CoreLight", Vector3.zero, Amber, 5f, 9f, boss.chest);
        boss.coreLight.transform.localPosition = new Vector3(0f, 0.3f, 1.4f);
        boss.calm = Amber;
        boss.furious = Alert;
        boss.player = controller.transform;
        boss.arena = new Bounds(new Vector3(0f, ArenaHeight / 2f, mid), new Vector3(ArenaHalf * 2f - 8f, ArenaHeight, ArenaZ1 - GateZ - 10f));
        boss.slamsOnly = true;
        boss.redOnlyInWindUp = true;
        boss.walkSpeed = 3.0f;
        boss.restSeconds = 1.0f;
        boss.windUpSeconds = 1.5f;

        var fight = systems.AddComponent<TokenFight>();
        fight.flow = flow;
        fight.hud = hud;
        fight.arcadeHud = arcadeHud;
        fight.player = controller;
        fight.boss = boss;
        fight.voice = bossGo.AddComponent<BossAudio>();
        fight.dodge = hudDodge;
        fight.bossHome = home;
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.name = "SlamMark";
        var discRenderer = disc.GetComponent<MeshRenderer>();
        discRenderer.sharedMaterial = cTelegraph;
        discRenderer.shadowCastingMode = ShadowCastingMode.Off;
        discRenderer.receiveShadows = false;
        fight.telegraph = disc.transform;
        var feet = Object.Instantiate(disc);
        feet.name = "SlamMarkFeet";
        fight.telegraphBody = feet.transform;
        disc.SetActive(false);
        feet.SetActive(false);
        fight.waves = BuildShockwaves();
        fight.waves[0].radius = boss.slamRadius + 0.4f;
        fight.waves[0].colour = Amber;
        fight.waves[1].radius = boss.bodyRadius;
        fight.waves[1].colour = Alert;
        return fight;
    }

    /// <summary>The power terminal on the hall's far wall: the socket, the plug in it, its cage, and the data centre it feeds.</summary>
    static PlugTerminal ArcadePlug(ArcadeFlow flow)
    {
        float wall = ArenaZ1;
        var root = new GameObject("PowerTerminal").transform;
        root.position = new Vector3(0f, 0f, wall - 0.5f);
        var terminal = root.gameObject.AddComponent<PlugTerminal>();
        terminal.flow = flow;
        terminal.range = 2.6f;
        var live = new List<Renderer>();
        // the socket housing on the wall, ringed in cyan while it has power
        Box("SocketHousing", root.position + new Vector3(0f, 1.5f, 0.1f), new Vector3(2.2f, 2.4f, 0.8f), aPetrol, true, null, 0f, root);
        live.Add(Box("SocketRing", root.position + new Vector3(0f, 1.35f, -0.31f), new Vector3(1.0f, 1.0f, 0.04f), gCyan, false, null, 0f, root).GetComponent<Renderer>());
        Box("SocketFace", root.position + new Vector3(0f, 1.35f, -0.33f), new Vector3(0.8f, 0.8f, 0.04f), aCharcoal, false, null, 0f, root);
        foreach (float x in new[] { -0.85f, 0.85f })
            live.Add(Box("SocketStrip", root.position + new Vector3(x, 1.5f, -0.31f), new Vector3(0.1f, 2.0f, 0.04f), gCyan, false, null, 0f, root).GetComponent<Renderer>());
        // the plug: a fat barrel with a grab bar, seated in the socket, its cable looping away along the floor
        var plug = new GameObject("Plug").transform;
        plug.SetParent(root, false);
        plug.position = root.position + new Vector3(0f, 1.35f, -0.36f);
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(barrel.GetComponent<Collider>());
        barrel.name = "PlugBarrel";
        barrel.transform.SetParent(plug, false);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localPosition = new Vector3(0f, 0f, -0.2f);
        barrel.transform.localScale = new Vector3(0.5f, 0.24f, 0.5f);
        barrel.GetComponent<MeshRenderer>().sharedMaterial = aCharcoal;
        Box("PlugBand", plug.position + new Vector3(0f, 0f, -0.2f), new Vector3(0.54f, 0.54f, 0.1f), aPaintAmber, false, null, 0f, plug);
        Box("PlugGrip", plug.position + new Vector3(0f, 0f, -0.56f), new Vector3(0.7f, 0.1f, 0.1f), aPetrol, false, null, 0f, plug);
        foreach (float x in new[] { -0.3f, 0.3f })
            Box("PlugGripArm", plug.position + new Vector3(x, 0f, -0.5f), new Vector3(0.08f, 0.08f, 0.16f), aPetrol, false, null, 0f, plug);
        Box("PlugCable", plug.position + new Vector3(0f, -0.62f, -0.36f), new Vector3(0.12f, 1.1f, 0.12f), aDead, false, null, 0f, plug);
        Kit("MegaKit/Models/Props/Prop_Cable_3", root.position + new Vector3(-1.2f, 0.02f, -2.2f), 20f, 1f, root, null, null, false);
        terminal.plug = plug;
        terminal.lamp = PointLight("TerminalLight", root.position + new Vector3(0f, 2.2f, -1.6f), Cyan, 6f, 7f, root);
        // the name it goes by, once, on a plate over it
        Box("NamePlate", root.position + new Vector3(0f, 3.25f, 0.05f), new Vector3(5.2f, 0.62f, 0.7f), aCharcoal, false, null, 0f, root);
        WorldText("PlugName", "PLUG OF MALICIOUS DATA CENTER", root.position + new Vector3(0f, 3.25f, -0.32f), Quaternion.identity, 0.0036f, 96, Cream, root);
        // the data centre: cabinets either side, their screens alive until the plug comes out
        for (int i = 0; i < 4; i++)
            foreach (float side in new[] { -1f, 1f })
            {
                var cabinet = Kit("MegaKit/Models/Props/Prop_Computer", new Vector3(side * (3.2f + i * 1.9f), 0f, wall - 0.75f), 180f, 1.5f, root,
                                  new Vector3(1.1f, 2.4f, 0.9f));
                live.AddRange(cabinet.GetComponentsInChildren<Renderer>());
            }
        terminal.liveParts = live.ToArray();
        terminal.liveMaterials = new[] { gCyan };
        terminal.deadMaterial = aDead;
        // the cage in front of it, until the colossus is down
        var cage = new GameObject("PlugCage").transform;
        cage.SetParent(root, false);
        cage.position = root.position + new Vector3(0f, 0f, -1.9f);
        for (float x = -2.2f; x <= 2.21f; x += 0.4f)
            Box("CageBar", cage.position + new Vector3(x, 1.5f, 0f), new Vector3(0.08f, 3f, 0.08f), aCharcoal, false, null, 0f, cage);
        Box("CageTop", cage.position + new Vector3(0f, 3f, 0f), new Vector3(4.6f, 0.14f, 0.14f), aCharcoal, false, null, 0f, cage);
        var cageSolid = cage.gameObject.AddComponent<BoxCollider>();
        cageSolid.center = new Vector3(0f, 1.5f, 0f);
        cageSolid.size = new Vector3(4.6f, 3f, 0.3f);
        terminal.cage = cage.gameObject;
        terminal.standPoint = new GameObject("PlugStand").transform;
        terminal.standPoint.SetParent(root, false);
        terminal.standPoint.position = root.position + new Vector3(0f, 0f, -1.4f);
        machineGlow.AddRange(live);
        machineLights.Insert(0, terminal.lamp);
        return terminal;
    }

    // ================================================================ the ending
    static Finale ArcadeFinale(GameObject systems, HudController hud, ArcadeHud arcadeHud, FirstPersonController controller, Camera[] playerCameras,
                               RobotPatrol[] patrols)
    {
        var finale = systems.AddComponent<Finale>();
        finale.hud = hud;
        finale.arcadeHud = arcadeHud;
        finale.player = controller;
        finale.flightCamera = BuildFlightCamera();
        finale.playerCameras = playerCameras;
        finale.machineLights = machineLights.ToArray();
        foreach (var patrol in patrols) machineGlow.AddRange(patrol.GetComponentsInChildren<Renderer>());
        var gate = GameObject.Find("SecurityGate");
        machineGlow.AddRange(gate.GetComponentsInChildren<Renderer>());
        finale.machineGlow = machineGlow.ToArray();
        finale.liveMaterials = new[]
        {
            gCyan, gCool, AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Models/Robots/Materials/Robot_Glow.mat"),
        };
        finale.deadMaterial = aDead;
        finale.patrols = patrols;
        finale.penGate = GameObject.Find("PenGate").transform;
        finale.penGateSlide = new Vector3(3.1f, 0f, 0f);

        // the people in the pen: at the belt until the ending, then out through the gate
        var people = new GameObject("Workers").transform;
        var workers = new List<Transform>();
        var animators = new List<Animator>();
        var gestures = new List<RobotGesture>();
        var hops = new List<Hover>();
        var goals = new List<Vector3>();
        var places = new (float x, float z, float gx, float gz)[]
        {
            (10.0f, -13.9f, 9.2f, -6.6f), (11.4f, -13.9f, 10.8f, -5.4f), (12.8f, -13.9f, 12.4f, -6.4f), (14.2f, -13.9f, 13.8f, -5.2f),
            (11.0f, -11.4f, 10.0f, -4.0f), (13.2f, -11.2f, 12.0f, -4.2f),
        };
        for (int i = 0; i < places.Length; i++)
        {
            var p = places[i];
            var person = Figure("Human", people, 0.95f + 0.02f * (i % 4));
            person.name = "Worker_" + i;
            SetLayer(person, Layers.Robot);
            person.transform.SetPositionAndRotation(new Vector3(p.x, 0.05f, p.z), Quaternion.Euler(0f, i < 4 ? 180f : 160f + 40f * (i % 2), 0f));
            foreach (var r in person.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            var gesture = WireGesture(person);
            gesture.gesture = i < 4 ? Gesture.Typing : Gesture.Carrying;       // hands busy at the belt
            var hop = person.AddComponent<Hover>();
            hop.spin = 0f;
            hop.amplitude = 0.06f;
            hop.period = 0.46f + 0.05f * (i % 4);
            hop.enabled = false;
            workers.Add(person.transform);
            animators.Add(person.GetComponent<Animator>());
            gestures.Add(gesture);
            hops.Add(hop);
            goals.Add(new Vector3(p.gx, 0.05f, p.gz));
        }
        finale.workers = workers.ToArray();
        finale.workerAnimators = animators.ToArray();
        finale.workerGestures = gestures.ToArray();
        finale.workerHops = hops.ToArray();
        finale.workerGoals = goals.ToArray();
        finale.cameraFrom = new Vector3(3.2f, 2.3f, -2.2f);
        finale.cameraTo = new Vector3(5.6f, 1.7f, -1.0f);
        finale.lookFrom = new Vector3(12f, 1.3f, -9f);
        finale.lookTo = new Vector3(11.2f, 1.25f, -5.4f);
        finale.faceToward = finale.cameraTo;
        var dawn = new GameObject("DawnLight").AddComponent<Light>();
        dawn.type = LightType.Directional;
        dawn.color = new Color32(255, 204, 150, 255);
        dawn.intensity = 0f;
        dawn.shadows = LightShadows.None;
        dawn.transform.rotation = Quaternion.Euler(24f, 125f, 0f);
        dawn.enabled = false;
        finale.dawn = dawn;
        return finale;
    }

    // ================================================================ HUD
    /// <summary>Trims the shared HUD down to what this game uses and adds the arcade's own few readouts.</summary>
    static ArcadeHud ArcadeHudWidgets(HudController hud)
    {
        Vector2 tl = new Vector2(0, 1), bl = new Vector2(0, 0), mid = new Vector2(0.5f, 0.5f), bottom = new Vector2(0.5f, 0), top = new Vector2(0.5f, 1);
        var canvas = hud.transform;
        var play = hud.gameplayGroup.transform;
        var arcade = canvas.gameObject.AddComponent<ArcadeHud>();
        arcade.amber = Amber;
        arcade.cyan = Cyan;
        arcade.red = Alert;
        arcade.cream = Cream;

        // the objective box: one line under a small header
        var comms = (RectTransform)play.Find("Comms");
        comms.sizeDelta = new Vector2(540f, 92f);
        ((RectTransform)hud.commsBody.transform).sizeDelta = new Vector2(500f, 34f);
        hud.commsBody.horizontalOverflow = HorizontalWrapMode.Overflow;
        hud.commsHeader.text = "OBJECTIVE";
        // everything is used with F here: E belongs to the disguise
        var promptKey = play.Find("Prompt/Key_E/Cap/Label");
        if (promptKey != null) promptKey.GetComponent<Text>().text = "F";
        hudPause.items[3].text = "START OVER";

        // batteries, under the objective
        var batteries = UiObject("Batteries", play);
        Place(batteries.transform, tl, tl, tl, new Vector2(42, -140), new Vector2(300, 34));
        arcade.batteryGroup = batteries.AddComponent<CanvasGroup>();
        var pips = new List<Image>();
        var pipRects = new List<RectTransform>();
        for (int i = 0; i < 4; i++)
        {
            var holder = UiObject("Pip" + i, batteries.transform);
            var rect = Place(holder.transform, tl, tl, mid, new Vector2(14 + i * 36, -17), new Vector2(24, 32));
            var cap = Img("Cap", holder.transform, new Color32(244, 236, 214, 60));
            Place(cap, top, top, top, new Vector2(0, 0), new Vector2(10, 5));
            var body = Img("Body", holder.transform, new Color32(244, 236, 214, 60));
            Place(body, bottom, bottom, bottom, Vector2.zero, new Vector2(22, 27));
            // (the body is the pip that changes colour; the cap follows it)
            var follow = holder.AddComponent<PipCap>();
            follow.body = body;
            follow.cap = cap;
            pips.Add(body);
            pipRects.Add(rect);
        }
        arcade.pips = pips.ToArray();
        arcade.pipRects = pipRects.ToArray();

        // the disguise: its key, one word, one bar
        var disguise = Img("Disguise", play, Panel);
        Place(disguise, bottom, bottom, bottom, new Vector2(0, 34), new Vector2(330, 58));
        arcade.disguiseGroup = disguise.gameObject.AddComponent<CanvasGroup>();
        var key = KeyCap(disguise.transform, "E", 34f);
        Place(key, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), key.sizeDelta);
        arcade.disguiseLabel = Label("Label", disguise.transform, "DISGUISE", 19, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(arcade.disguiseLabel, tl, tl, tl, new Vector2(62, -8), new Vector2(250, 24));
        arcade.disguiseFill = Bar(disguise.transform, bl, new Vector2(62, 12), new Vector2(252, 8), Amber);
        // four brackets round the picture while it is on
        var corners = UiObject("DisguiseFrame", play);
        Stretch(corners.transform);
        corners.transform.SetAsFirstSibling();
        arcade.visorCorners = corners.AddComponent<CanvasGroup>();
        arcade.visorCorners.blocksRaycasts = false;
        foreach (var (cx, cy) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
        {
            var corner = new Vector2(cx, cy);
            var inset = new Vector2(cx == 0f ? 18 : -18, cy == 0f ? 18 : -18);
            Place(Img("Bracket", corners.transform, Amber), corner, corner, corner, inset, new Vector2(110, 4));
            Place(Img("Bracket", corners.transform, Amber), corner, corner, corner, inset, new Vector2(4, 110));
        }

        // danger: one chip that says what is happening and what to do about it (no overlay on the picture: the patrol's
        // own "?" and "!" and their sounds carry the rest)
        var danger = Img("Danger", play, Panel);
        Place(danger, top, top, top, new Vector2(0, -182), new Vector2(640, 44));
        arcade.dangerGroup = danger.gameObject.AddComponent<CanvasGroup>();
        arcade.dangerText = Label("Text", danger.transform, "", 19, TextAnchor.MiddleCenter, Alert, FontStyle.Bold);
        Stretch(arcade.dangerText);

        // the colossus's tokens
        var tokens = Img("Tokens", play, Panel);
        Place(tokens, top, top, top, new Vector2(0, -34), new Vector2(620, 62));
        arcade.tokenGroup = tokens.gameObject.AddComponent<CanvasGroup>();
        var tokenLabel = Label("Label", tokens.transform, "COLOSSUS TOKENS", 17, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(tokenLabel, tl, tl, tl, new Vector2(18, -8), new Vector2(400, 24));
        arcade.tokenValue = Label("Value", tokens.transform, "100%", 19, TextAnchor.MiddleRight, Amber, FontStyle.Bold);
        Place(arcade.tokenValue, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -8), new Vector2(120, 24));
        arcade.tokenFill = Bar(tokens.transform, bl, new Vector2(18, 12), new Vector2(584, 10), Amber);

        // one line of teaching
        var hint = Img("Hint", play, new Color32(0x12, 0x18, 0x1B, 170));
        Place(hint, bottom, bottom, bottom, new Vector2(0, 190), new Vector2(860, 40));
        arcade.hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
        arcade.hintText = Label("Text", hint.transform, "", 18, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Stretch(arcade.hintText);

        // the title, for the first three seconds of a run
        var title = UiObject("Title", play);
        Place(title.transform, bl, bl, bl, new Vector2(60, 150), new Vector2(700, 110));
        arcade.titleGroup = title.AddComponent<CanvasGroup>();
        var titleText = Label("Text", title.transform, "THE LAST HUMAN", 60, TextAnchor.UpperLeft, Cream, FontStyle.Bold);
        Place(titleText, tl, tl, tl, Vector2.zero, new Vector2(700, 76));
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        Place(Img("Rule", title.transform, Amber), bl, bl, bl, new Vector2(2, 18), new Vector2(430, 4));

        // the loss card: what happened, and the one key that undoes it
        var loss = Img("Loss", canvas, new Color32(0x12, 0x18, 0x1B, 215));
        loss.raycastTarget = true;
        Stretch(loss);
        arcade.lossGroup = loss.gameObject.AddComponent<CanvasGroup>();
        arcade.lossTitle = Label("Title", loss.transform, "CAUGHT", 120, TextAnchor.MiddleCenter, Alert, FontStyle.Bold);
        Place(arcade.lossTitle, mid, mid, mid, new Vector2(0, 90), new Vector2(1500, 150));
        arcade.lossBody = Label("Body", loss.transform, "", 28, TextAnchor.MiddleCenter, Cream);
        Place(arcade.lossBody, mid, mid, mid, new Vector2(0, -6), new Vector2(1300, 40));
        var retry = TextButton("RetryButton", loss.transform, "RETRY", 30, out var retryLabel);
        Place(retry, mid, mid, mid, new Vector2(0, -104), new Vector2(300, 70));
        retry.GetComponent<Image>().color = Panel;
        Border(retry.transform, Amber, 2f);
        retry.gameObject.AddComponent<MenuItemHighlight>().label = retryLabel;
        KeyRow(loss.transform, mid, new Vector2(0, -178), 18, Cream, 32f, ("R", "RETRY"));

        // the victory card: a band across the bottom, so the people stay in the picture
        var win = Img("Win", canvas, new Color32(0x12, 0x18, 0x1B, 205));
        win.raycastTarget = true;
        Place(win, new Vector2(0, 0), new Vector2(1, 0), bottom, Vector2.zero, new Vector2(0, 330));
        arcade.winGroup = win.gameObject.AddComponent<CanvasGroup>();
        Place(Img("Rule", win.transform, Amber), new Vector2(0, 1), new Vector2(1, 1), top, Vector2.zero, new Vector2(0, 4));
        var hero = Label("Title", win.transform, "THE LAST HUMAN <color=#F1AA48>HERO</color>", 86, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Place(hero, top, top, top, new Vector2(0, -34), new Vector2(1700, 110));
        hero.horizontalOverflow = HorizontalWrapMode.Overflow;
        var sub = Label("Body", win.transform, "The AI models are unplugged. Humanity is free.", 26, TextAnchor.MiddleCenter, Cream);
        Place(sub, top, top, top, new Vector2(0, -150), new Vector2(1500, 36));
        var replay = TextButton("ReplayButton", win.transform, "REPLAY", 32, out var replayLabel);
        Place(replay, bottom, bottom, bottom, new Vector2(0, 34), new Vector2(320, 76));
        replay.GetComponent<Image>().color = Panel;
        Border(replay.transform, Amber, 2f);
        replay.gameObject.AddComponent<MenuItemHighlight>().label = replayLabel;

        // the cards sit under the fade and the pause menu
        int fadeIndex = hud.fade.transform.GetSiblingIndex();
        loss.transform.SetSiblingIndex(fadeIndex);
        win.transform.SetSiblingIndex(fadeIndex);
        return arcade;
    }

    /// <summary>A soft frame: clear in the middle, solid at the edges of the screen.</summary>
    static Texture2D EdgeTexture()
    {
        const string path = "Assets/Art/UI/DangerEdge.png";
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs(x / (size - 1f) - 0.5f) * 2f, v = Mathf.Abs(y / (size - 1f) - 0.5f) * 2f;
                float d = Mathf.Max(u, v);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.SmoothStep(0.72f, 1f, d), 1.6f) * 0.75f));
            }
        tex.Apply();
        System.IO.Directory.CreateDirectory("Assets/Art/UI");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
