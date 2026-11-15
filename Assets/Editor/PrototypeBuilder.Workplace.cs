// The Workplace scene: Sector 017, an ore sorting yard under robot watch.
// Layout (top view, +Z is north / up on the minimap):
//   south wall: the feeder and conveyor, where the player and other workers sort ore
//   centre: aisles the robots patrol, containers and crate stacks
//   north wall: the tower gate with the portal, reached along the rails
// Most pieces come from the environment kit (Assets/Art/Models/Environment, built by ArtSource/Environment/build_env.py).
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
    const float MapY = 20f;                       // height of the flat minimap drawing above the level
    // yard limits (inner faces of the walls)
    const float West = -15f, East = 15f, South = -12f, North = 13f;
    const float BeltZ = -10.6f, BeltTop = 0.89f;
    const float StationX = -7f, GateX = 5f;

    static Transform env, mapRoot;
    static Material mFloorA, mFloorB, mSeam, mWall, mTrim, mMetal, mDark, mHazY, mHazK, mLamp, mWindow, mTower, mCity,
                    mPortal, mPortalBar, mRail, mWood, mAlarm, mLine, mWorkLine;
    static Volume visorVolume;
    static LoadingScreen hudLoading;              // the travel card of the HUD built last
    static PauseMenu hudPause;                    // and its pause menu
    static DodgePrompt hudDodge;                  // its dodge bar (the boss fight)
    static TutorialCard hudCard;                  // and its tutorial card (the escape)
    static SpeedLines hudSpeed;                   // and its speed lines (the escape gives them the parkour motor)
    static GameObject hudMinimap, hudStatus;      // parts a level without robots or a map can switch off
    static float mapScale = 1f;                   // minimap markers are drawn this much larger (big levels show more ground)
    static Material mapFloor, mapWall, mapCover, mapPlayer, mapOrange, mapPurple, mapBlue, mapCone, mapConeAlert, mapMask, mapPortal, mapHide;
    static readonly Color32 HideGreen = new Color32(120, 255, 235, 255);      // the palette's mint: safe
    static readonly List<HideSpot> hideSpots = new List<HideSpot>();

    static readonly Vector3 Spawn = new Vector3(StationX, 0.05f, -9.3f);                  // close enough to reach the belt
    static readonly Vector3 WorkPoint = new Vector3(StationX, BeltTop + 0.08f, -10.28f);  // near edge of the belt, in front of the player
    static readonly Vector3 FeedPoint = new Vector3(-12.3f, BeltTop + 0.08f, -10.28f);    // just out of the feeder
    // the conveyor line: a human working here is left alone; anywhere else a robot that sees one gives chase.
    // Its open sides (north and east) are marked on the floor; the walls close the other two.
    static readonly Bounds WorkZone = new Bounds(new Vector3(-6.375f, 1f, -8.6f), new Vector3(16.75f, 4f, 7.2f));
    static readonly Vector3 PortalSpot = new Vector3(GateX, 0f, North + 2.2f);

    static void BuildWorkplace()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeMaterials();
        env = new GameObject("Environment").transform;
        mapRoot = new GameObject("MinimapDrawing").transform;

        BuildLighting();
        BuildYard();
        var station = BuildWorkArea(out var maskVisual, out var maskPickup, out var feederAlarm);
        var portal = BuildPortal(out var beacon);
        BuildSkyline();

        var player = BuildPlayer(out var pivot, out var body, out var flash, out var playerCameras);
        var hud = BuildHud(out var mapImage, out var story, playerCameras[0].transform);
        NewEventSystem();

        // systems
        var systems = new GameObject("GameSystems");
        var identity = player.GetComponent<PlayerIdentity>();
        var controller = player.GetComponent<FirstPersonController>();
        var detection = systems.AddComponent<DetectionSystem>();
        detection.player = player.transform;
        detection.playerHead = pivot;
        detection.identity = identity;
        detection.workZone = WorkZone;
        BuildWorkAreaBoundary();
        var fx = systems.AddComponent<TransitionFx>();
        fx.volume = BuildPostProcessing();
        hud.visor.grade = visorVolume;
        fx.hud = hud;
        fx.flash = flash;
        body.fx = fx;

        var interactor = player.GetComponent<PlayerInteractor>();
        interactor.hud = hud;
        interactor.body = body;
        var maskRules = player.AddComponent<MaskController>();
        maskRules.identity = identity;
        maskRules.body = body;
        maskRules.interactor = interactor;
        maskRules.hud = hud;
        maskRules.fx = fx;

        BuildHideSpots(controller, identity, detection, hud);

        var intro = systems.AddComponent<IntroCinematic>();
        intro.flightCamera = BuildFlightCamera();
        intro.playerCameras = playerCameras;
        intro.playerEyes = pivot;
        intro.body = body;
        // starts in front of the portal, pulls back over the rails, swings round the east side, then comes in low
        // behind the player (who is in full view with the arrow over his head) and moves into his eyes
        var head = new Vector3(StationX, 1.6f, Spawn.z);
        intro.path = new[]
        {
            new Vector3(GateX, 3.4f, 7.6f), new Vector3(6.8f, 6.8f, 1.5f), new Vector3(10.5f, 7.5f, -5.5f),
            new Vector3(0.5f, 4.6f, -4.6f), new Vector3(StationX + 2.2f, 3.1f, -4.6f), new Vector3(StationX, 2.2f, -6.3f),
        };
        intro.focus = new[]
        {
            new Vector3(GateX, 2.4f, North + 3f), new Vector3(4.5f, 1.5f, 10f), new Vector3(0f, 1f, 2f),
            head, head, head,
        };
        intro.seconds = 12f;

        var flow = systems.AddComponent<GameFlow>();
        flow.hud = hud;
        flow.story = story;
        flow.loading = hudLoading;
        flow.capture = BuildCaptureDirector(systems, controller, playerCameras[0], hud);
        hudPause.player = controller;
        hudPause.flow = flow;
        flow.intro = intro;
        flow.player = controller;
        flow.identity = identity;
        flow.body = body;
        flow.maskRules = maskRules;
        flow.detection = detection;
        flow.workStation = station;
        flow.mask = maskPickup;
        flow.feederAlarm = feederAlarm;
        flow.portalBeacon = beacon;
        flow.portalTarget = portal.transform;
        var spawn = new GameObject("StationSpawn").transform;
        spawn.SetPositionAndRotation(Spawn, Quaternion.Euler(0, 180, 0));
        flow.stationSpawn = spawn;

        maskPickup.detection = detection;
        maskPickup.flow = flow;
        maskPickup.hud = hud;
        portal.flow = flow;
        portal.hud = hud;
        foreach (var b in hud.completePanel.GetComponentsInChildren<Button>(true))
            UnityEventTools.AddPersistentListener(b.onClick, b.name == "MenuButton" ? new UnityAction(flow.LoadMenu) : new UnityAction(flow.Replay));

        BuildRobots();
        BuildWorkers();
        BuildMinimap(mapImage, player.transform, maskVisual.transform, portal.transform);
        flow.victory = BuildVictory(systems, hud, intro.flightCamera, playerCameras);

        ApplySurfaceDetail();
        EditorSceneManager.SaveScene(scene, SceneDir + "/Workplace.unity");
        log.Add("Workplace scene saved: " + env.childCount + " environment pieces, " + RobotCount + " robots, " + WorkerCount + " workers");
    }

    // ================================================================ materials
    static void MakeMaterials()
    {
        // shared with the environment kit (the models look these up by name)
        mWall = Lit("Env_Wall", new Color32(48, 90, 94, 255), 0.15f);
        Lit("Env_WallInset", new Color32(36, 70, 74, 255), 0.2f);
        mTrim = Lit("Env_Trim", new Color32(86, 124, 128, 255), 0.25f);
        mMetal = Lit("Env_Metal", new Color32(62, 76, 82, 255), 0.5f, 0.3f);
        mDark = Lit("Env_Dark", new Color32(30, 36, 40, 255), 0.4f, 0.2f);
        mHazY = Lit("Env_HazardYellow", new Color32(214, 170, 40, 255), 0.3f, 0f, null, new Color32(214, 170, 40, 255), 0.25f);
        mHazK = Lit("Env_HazardBlack", new Color32(20, 22, 24, 255), 0.3f);
        Lit("Env_Crate", new Color32(150, 116, 76, 255), 0.1f);
        Lit("Env_CrateDark", new Color32(108, 80, 52, 255), 0.1f);
        Lit("Env_Container", new Color32(52, 104, 106, 255), 0.3f, 0.1f);
        Lit("Env_ContainerDark", new Color32(38, 80, 84, 255), 0.3f, 0.1f);
        Lit("Env_Belt", new Color32(26, 30, 34, 255), 0.4f);
        mTower = Lit("Env_Tower", new Color32(70, 88, 84, 255), 0.1f);
        mLamp = Glow("Env_LampGlow", Amber, 4f);
        Glow("Env_ScreenGlow", new Color32(90, 230, 210, 255), 1.2f);
        mAlarm = Glow("Env_AlarmGlow", new Color32(255, 60, 40, 255), 4f);
        // used only by pieces built here
        mFloorA = Lit("Env_FloorA", new Color32(58, 92, 94, 255), 0.35f);
        mFloorB = Lit("Env_FloorB", new Color32(50, 82, 85, 255), 0.35f);
        mSeam = Lit("Env_FloorSeam", new Color32(30, 54, 58, 255), 0.2f);
        mLine = Lit("Env_FloorLine", new Color32(120, 190, 190, 255), 0.3f);
        mRail = Lit("Env_Rail", new Color32(96, 108, 114, 255), 0.7f, 0.8f);
        mWood = Lit("Env_Sleeper", new Color32(50, 40, 34, 255), 0.1f);
        mWindow = Glow("Env_WindowGlow", Amber, 2.2f);
        mPortal = Glow("Env_PortalGlow", new Color32(120, 255, 235, 255), 3.2f);
        mPortalBar = Glow("Env_PortalBar", Amber, 5f);
        mCity = Lit("Env_City", new Color32(18, 40, 44, 255), 0.05f);
        Glow("Env_HideGlow", HideGreen, 0.8f);            // marker strips on hiding spots: a quiet hint (pulsed at runtime)
        mWorkLine = Glow("Env_WorkLine", Amber, 2.6f);    // the lit boundary of the work area
        Glow("Env_MarkerGlow", Amber, 5f);                // the arrow over the player in the opening flight
        mapHide = Flat("Map_Hide", HideGreen);

        mapFloor = Flat("Map_Floor", new Color32(16, 44, 46, 255));
        mapWall = Flat("Map_Wall", new Color32(96, 170, 190, 255));
        mapCover = Flat("Map_Prop", new Color32(48, 100, 104, 255));
        mapPlayer = Flat("Map_Player", Cream);
        mapOrange = Flat("Map_Orange", new Color32(244, 122, 44, 255));
        mapPurple = Flat("Map_Purple", new Color32(156, 116, 236, 255));
        mapBlue = Flat("Map_Blue", new Color32(64, 146, 240, 255));
        mapCone = Flat("Map_ViewTriangle", new Color32(255, 46, 36, 84));
        mapConeAlert = Flat("Map_ViewTriangleAlert", new Color32(255, 60, 40, 190));
        mapMask = Flat("Map_Mask", Amber);
        mapPortal = Flat("Map_Portal", new Color32(120, 255, 235, 255));
    }

    // ================================================================ geometry helpers
    static GameObject Box(string name, Vector3 center, Vector3 size, Material m, bool solid = true, Material map = null,
                          float yaw = 0f, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent != null ? parent : env, false);
        go.transform.SetPositionAndRotation(center, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (parent == null) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        if (map != null) MapRect(name, center.x, center.z, size.x, size.z, map, MapY + 0.1f, yaw);
        return go;
    }

    /// <summary>
    /// A piece from the environment kit, standing on 'position'. 'solid' is the size of its
    /// collision box in the model's own metres (before scaling); leave it out for decoration.
    /// </summary>
    static GameObject Prop(string model, Vector3 position, float yaw = 0f, Vector3? scale = null, Vector3? solid = null,
                           Transform parent = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvDir + "/Env_" + model + ".fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = model;
        go.transform.SetParent(parent != null ? parent : env, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = scale ?? Vector3.one;
        if (solid.HasValue)
        {
            var c = go.AddComponent<BoxCollider>();
            c.size = solid.Value;
            c.center = new Vector3(0, solid.Value.y / 2f, 0);
        }
        if (parent == null) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static void MapRect(string name, float x, float z, float sizeX, float sizeZ, Material m, float y, float yaw = 0f)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = "Map_" + name;
        q.layer = LayerMask.NameToLayer(Layers.Minimap);
        q.transform.SetParent(mapRoot, false);
        q.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(90, yaw, 0));
        q.transform.localScale = new Vector3(sizeX, sizeZ, 1);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    /// <summary>A straight wall made of kit panels. 'inward' is the yaw that turns a panel's detailed face to the yard.</summary>
    static void WallRun(string name, float x0, float z0, float x1, float z1, float inward)
    {
        bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
        float length = alongX ? Mathf.Abs(x1 - x0) : Mathf.Abs(z1 - z0);
        int n = Mathf.Max(1, Mathf.RoundToInt(length / 6f));
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            var p = new Vector3(Mathf.Lerp(x0, x1, t), 0f, Mathf.Lerp(z0, z1, t));
            Prop("WallPanel", p, inward, new Vector3(length / n / 6f, 1f, 1f), new Vector3(6f, 5.2f, 1f)).name = name;
        }
        MapRect(name, (x0 + x1) / 2f, (z0 + z1) / 2f, alongX ? length : 0.9f, alongX ? 0.9f : length, mapWall, MapY + 0.15f);
    }

    static Light PointLight(string name, Vector3 position, Color color, float intensity, float range, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : env, false);
        go.transform.position = position;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    /// <summary>Lamp post whose arm reaches toward (armX, armZ).</summary>
    static void Lamp(float x, float z, float armX, float armZ)
    {
        float yaw = Mathf.Atan2(-armZ, armX) * Mathf.Rad2Deg;      // the model's arm points along its +X
        Prop("LampPost", new Vector3(x, 0, z), yaw, null, new Vector3(0.24f, 3.2f, 0.24f));
        PointLight("LampLight", new Vector3(x + armX * 0.72f, 2.75f, z + armZ * 0.72f), new Color32(255, 160, 60, 255), 6f, 10f);   // still on at dawn, but fading
    }

    static void Crate(float x, float z, float size, float baseY = 0f, float yaw = 0f) =>
        Prop("Crate", new Vector3(x, baseY, z), yaw, Vector3.one * size, Vector3.one);

    /// <summary>Container sized sizeX by sizeZ on the ground; tall enough to block a robot's view.</summary>
    static void Container(float x, float z, float sizeX, float sizeZ)
    {
        bool turned = sizeX > sizeZ;
        float width = turned ? sizeZ : sizeX, length = turned ? sizeX : sizeZ;
        Prop("Container", new Vector3(x, 0, z), turned ? 90f : 0f, new Vector3(width / 2.5f, 1f, length / 5.6f), new Vector3(2.5f, 2.7f, 5.6f));
        MapRect("Container", x, z, sizeX, sizeZ, mapCover, MapY + 0.1f);
    }

    static int oreCounter;

    /// <summary>A faceted ore rock (or a pile of them) sitting on a surface at the given point.</summary>
    static GameObject Ore(Vector3 surfacePoint, float scale = 1f, Transform parent = null, bool pile = false)
    {
        string model = pile ? "Ore_Pile" : "Ore_" + "ABCD"[oreCounter % 4];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PropDir + "/" + model + ".fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = model;
        go.transform.SetParent(parent != null ? parent : env, false);
        go.transform.SetPositionAndRotation(surfacePoint + Vector3.up * (pile ? 0.07f : 0.075f) * scale, Quaternion.Euler(0, 37f * oreCounter + 11f, 0));
        go.transform.localScale = Vector3.one * scale;
        oreCounter++;
        return go;
    }

    /// <summary>An open sorting bin with some ore already in it.</summary>
    static void Bin(Vector3 at)
    {
        Prop("Bin", at, 0f, null, new Vector3(1.0f, 0.84f, 0.9f));
        Ore(at + new Vector3(0, 0.12f, 0), 0.85f, null, true);
    }

    // ================================================================ lighting and atmosphere
    static void BuildLighting()
    {
        // sunrise: a low warm sun in the east, long shadows, a glowing sky and morning haze
        var sun = new GameObject("Sunrise").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color32(255, 176, 120, 255);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        sun.transform.rotation = Quaternion.Euler(11f, -68f, 0f);      // just above the horizon, shining from the east-south-east

        var sky = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Environment/Env_SunriseSky.mat");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, MatDir + "/Environment/Env_SunriseSky.mat");
        }
        sky.shader = Shader.Find("Skybox/Procedural");
        sky.SetFloat("_SunSize", 0.07f);
        sky.SetFloat("_SunSizeConvergence", 4f);
        sky.SetFloat("_AtmosphereThickness", 1.75f);
        sky.SetColor("_SkyTint", new Color32(214, 150, 132, 255));
        sky.SetColor("_GroundColor", new Color32(70, 60, 66, 255));
        sky.SetFloat("_Exposure", 1.15f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(150, 150, 184, 255);       // cool light from the sky still in shadow
        RenderSettings.ambientEquatorColor = new Color32(206, 150, 128, 255);   // warm bounce near the horizon
        RenderSettings.ambientGroundColor = new Color32(60, 70, 78, 255);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color32(222, 160, 136, 255);             // peach morning haze
        RenderSettings.fogStartDistance = 12f;
        RenderSettings.fogEndDistance = 95f;
    }

    /// <summary>The scene's look, glitch and visor volumes. Each scene gets its own profile assets ('set' names them).</summary>
    static Volume BuildPostProcessing(string set = "Workplace", Color? filter = null)
    {
        var look = NewProfile(set + "Look");
        var bloom = AddOverride<Bloom>(look);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.7f);
        AddOverride<Tonemapping>(look).mode.Override(TonemappingMode.Neutral);
        var vignette = AddOverride<Vignette>(look);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.4f);
        var grade = AddOverride<ColorAdjustments>(look);
        grade.contrast.Override(14f);
        grade.saturation.Override(10f);
        grade.colorFilter.Override(filter ?? new Color(1f, 0.96f, 0.92f));      // by default, a little warmth over everything
        var baseVolume = new GameObject("PostFX").AddComponent<Volume>();
        baseVolume.isGlobal = true;
        baseVolume.sharedProfile = look;

        var glitch = NewProfile(set + "Glitch");
        AddOverride<ChromaticAberration>(glitch).intensity.Override(1f);
        var distortion = AddOverride<LensDistortion>(glitch);
        distortion.intensity.Override(-0.45f);
        var flash = AddOverride<ColorAdjustments>(glitch);
        flash.postExposure.Override(0.7f);
        flash.colorFilter.Override(new Color(1f, 0.78f, 0.55f));
        var grain = AddOverride<FilmGrain>(glitch);
        grain.intensity.Override(0.7f);
        var glitchVolume = new GameObject("PostFX_IdentityGlitch").AddComponent<Volume>();
        glitchVolume.isGlobal = true;
        glitchVolume.priority = 10;
        glitchVolume.weight = 0f;
        glitchVolume.sharedProfile = glitch;

        // the mask's visor: the whole picture goes cold and blue, with a darker blue edge and a little lens fringing
        var visor = NewProfile(set + "Visor");
        var cold = AddOverride<ColorAdjustments>(visor);
        // (VisorHud applies only part of this grade, so the world stays easy to read through the mask)
        cold.colorFilter.Override(new Color(0.84f, 0.95f, 1.07f));
        cold.saturation.Override(-10f);
        cold.contrast.Override(12f);
        var edge = AddOverride<Vignette>(visor);
        edge.color.Override(new Color(0.0f, 0.1f, 0.26f));
        edge.intensity.Override(0.42f);
        edge.smoothness.Override(0.5f);
        AddOverride<ChromaticAberration>(visor).intensity.Override(0.1f);
        AddOverride<FilmGrain>(visor).intensity.Override(0.12f);
        visorVolume = new GameObject("PostFX_Visor").AddComponent<Volume>();
        visorVolume.isGlobal = true;
        visorVolume.priority = 5;
        visorVolume.weight = 0f;
        visorVolume.sharedProfile = visor;

        EditorUtility.SetDirty(look);
        EditorUtility.SetDirty(glitch);
        EditorUtility.SetDirty(visor);
        return glitchVolume;
    }

    // ================================================================ the yard
    static void BuildYard()
    {
        // floor in tiles (each tile then uses its own nearest lights), with seams and painted lines on top
        float w = East - West + 1f, d = North - South + 1f;
        float x0 = West - 0.5f, z0 = South - 0.5f;
        for (int ix = 0; ix < 5; ix++)
            for (int iz = 0; iz < 4; iz++)
                Box("Floor", new Vector3(x0 + (ix + 0.5f) * w / 5f, -0.25f, z0 + (iz + 0.5f) * d / 4f),
                    new Vector3(w / 5f, 0.5f, d / 4f), (ix + iz) % 2 == 0 ? mFloorA : mFloorB);
        for (float x = West + 3f; x < East; x += 3f)
            Box("FloorSeam", new Vector3(x, 0.004f, (South + North) / 2f), new Vector3(0.06f, 0.008f, d - 1.4f), mSeam, false);
        for (float z = South + 3f; z < North; z += 3f)
            Box("FloorSeam", new Vector3(0f, 0.004f, z), new Vector3(w - 1.4f, 0.008f, 0.06f), mSeam, false);
        // dashes down the centre aisle (the work area's own boundary is built separately)
        for (float x = -12f; x <= 2f; x += 2f)
            Box("FloorDash", new Vector3(x, 0.006f, -1.6f), new Vector3(1.0f, 0.01f, 0.1f), mLine, false);
        for (float z = -9f; z <= 11f; z += 2.5f)
            foreach (float dx in new[] { -1.15f, 1.15f })
                Box("RailEdge", new Vector3(GateX + dx, 0.006f, z), new Vector3(0.08f, 0.01f, 1.4f), mHazY, false);
        // drain grates
        foreach (var (gx, gz) in new[] { (-10.5f, 0.6f), (-2f, 9.2f), (9.5f, 5.5f), (11.5f, -7f) })
        {
            Box("Drain", new Vector3(gx, 0.006f, gz), new Vector3(1.2f, 0.012f, 0.7f), mDark, false);
            for (int i = 0; i < 5; i++)
                Box("DrainBar", new Vector3(gx - 0.48f + i * 0.24f, 0.014f, gz), new Vector3(0.06f, 0.012f, 0.62f), mMetal, false);
        }
        MapRect("Floor", 0f, (South + North) / 2f, w, d, mapFloor, MapY);

        // walls from kit panels, posts at the corners
        WallRun("WallSouth", West - 0.5f, South - 0.5f, East + 0.5f, South - 0.5f, 0f);
        WallRun("WallWest", West - 0.5f, South, West - 0.5f, North, 90f);
        WallRun("WallEast", East + 0.5f, South, East + 0.5f, North, -90f);
        WallRun("WallNorthA", West - 0.5f, North + 0.5f, GateX - 3.6f, North + 0.5f, 180f);
        WallRun("WallNorthB", GateX + 3.6f, North + 0.5f, East + 0.5f, North + 0.5f, 180f);
        foreach (var (px, pz) in new[] { (West - 0.5f, South - 0.5f), (East + 0.5f, South - 0.5f), (West - 0.5f, North + 0.5f), (East + 0.5f, North + 0.5f) })
            Prop("WallPost", new Vector3(px, 0, pz), 0f, null, new Vector3(1.4f, 5.6f, 1.4f));

        // rails to the gate, a buffer stop at the south end, bollards, two ore carts
        foreach (float dx in new[] { -0.45f, 0.45f })
        {
            Box("Rail", new Vector3(GateX + dx, 0.06f, 2.75f), new Vector3(0.08f, 0.12f, 27.5f), mRail, false);
            Box("RailFoot", new Vector3(GateX + dx, 0.012f, 2.75f), new Vector3(0.18f, 0.024f, 27.5f), mDark, false);
        }
        for (float z = -10.5f; z < North + 3.2f; z += 1f)
            Box("Sleeper", new Vector3(GateX, 0.03f, z), new Vector3(1.4f, 0.06f, 0.24f), mWood, false);
        Box("BufferBeam", new Vector3(GateX, 0.55f, -11.1f), new Vector3(1.5f, 0.22f, 0.22f), mHazY);
        foreach (float dx in new[] { -0.6f, 0.6f })
            Box("BufferPost", new Vector3(GateX + dx, 0.3f, -11.1f), new Vector3(0.16f, 0.6f, 0.16f), mMetal, false);
        foreach (float z in new[] { -2.2f, 1.2f, 7.2f })
            foreach (float dx in new[] { -1.5f, 1.5f })
                Prop("Bollard", new Vector3(GateX + dx, 0, z));
        foreach (float z in new[] { -5.4f, 4.2f })
        {
            Prop("Cart", new Vector3(GateX, 0.1f, z), 0f, null, new Vector3(1.15f, 1.1f, 1.6f));
            Ore(new Vector3(GateX, 1.02f, z), 1.15f, null, true);
        }

        // cover: containers and crate stacks
        Container(-1.2f, 4.5f, 2.6f, 5.5f);
        Container(10.5f, -1f, 2.4f, 5.5f);
        Container(-9f, 5.2f, 4.6f, 2.4f);
        Crate(3.0f, -9.0f, 1.2f);
        Crate(3.0f, -9.0f, 1.0f, 1.2f, 14f);
        Crate(3.2f, -7.6f, 1.1f, 0f, 8f);
        Crate(0.2f, 8.0f, 1.3f);
        Crate(1.5f, 8.3f, 1.0f, 0f, 20f);
        Crate(0.3f, 8.1f, 0.9f, 1.3f, 33f);
        Crate(9.4f, 11.3f, 1.2f);
        Crate(10.7f, 11.6f, 1.2f, 0f, 12f);
        Crate(-13.7f, -3.4f, 1.1f);
        Crate(7.1f, -10.7f, 1.2f, 0f, 10f);

        // machinery, barrels and pallets along the walls
        Prop("Generator", new Vector3(West + 0.6f, 0, -6.6f), 90f, null, new Vector3(1.5f, 1.8f, 1.0f));
        Prop("Generator", new Vector3(West + 0.6f, 0, 11f), 90f, null, new Vector3(1.5f, 1.8f, 1.0f));
        Prop("Generator", new Vector3(East - 0.6f, 0, 5.5f), -90f, null, new Vector3(1.5f, 1.8f, 1.0f));
        Prop("Generator", new Vector3(-6f, 0, North - 0.6f), 180f, null, new Vector3(1.5f, 1.8f, 1.0f));
        foreach (var (bx, bz) in new[] { (West + 0.6f, -4.4f), (West + 1.3f, -4.6f), (West + 0.7f, -3.8f), (13.9f, 9.6f), (13.3f, 10.3f),
                                         (East - 0.6f, -8.6f), (-12.6f, 12.2f), (3.0f, 11.6f), (-4.4f, 11.9f) })
            Prop("Barrel", new Vector3(bx, 0, bz), bx * 40f, null, new Vector3(0.6f, 0.92f, 0.6f));
        foreach (var (px, pz, yaw) in new[] { (13.6f, -4.8f, 0f), (-12.9f, 0.6f, 90f), (12.9f, 12.2f, 10f) })
        {
            Prop("Pallet", new Vector3(px, 0, pz), yaw, null, new Vector3(1.2f, 0.14f, 1.0f));
            Crate(px, pz, 0.8f, 0.14f, yaw + 12f);
        }

        // pipes on the walls
        for (float x = -12f; x <= 12f; x += 6f)
            Prop("PipeRun", new Vector3(x, 3.5f, South), 0f);
        for (float z = -9f; z <= 9f; z += 6f)
        {
            Prop("PipeRun", new Vector3(West, 3.5f, z), 90f);
            Prop("PipeRun", new Vector3(East, 3.0f, z), -90f);
        }
        foreach (float x in new[] { -11f, -4.5f, 1.5f, 11f })
            Prop("PipeDrop", new Vector3(x, 0, South), 0f);
        foreach (float z in new[] { -2f, 7.5f })
        {
            Prop("PipeDrop", new Vector3(West, 0, z), 90f);
            Prop("PipeDrop", new Vector3(East, 0, z), -90f);
        }

        // amber lamps
        Lamp(-12.3f, -4.6f, 1, 0);
        Lamp(-2.6f, -0.1f, 0, -1);
        Lamp(1.9f, 5.6f, 1, 0);
        Lamp(10.5f, 3.6f, -1, 0);
        Lamp(-8.8f, 3.3f, 0, 1);
        Lamp(7.7f, 10.8f, -1, 0);
        Lamp(-13.6f, 9.4f, 1, 0);
        Lamp(7.0f, -8.4f, -1, 0);
        Lamp(-3.0f, -4.4f, 0, -1);
    }

    // ================================================================ work area: feeder, conveyor, the player's station, the mask
    static WorkStation BuildWorkArea(out GameObject maskVisual, out MaskPickup maskPickup, out GameObject feederAlarm)
    {
        // conveyor along the south wall: seven 2 m kit segments
        for (int i = 0; i < 7; i++)
            Prop("Conveyor", new Vector3(-12.5f + i * 2f, 0, BeltZ), 0f, null, new Vector3(2f, 0.98f, 1.0f));
        // ore never stops coming: a few chunks ride the far lane of the belt, feeder to end stop, all round long
        var flowGo = new GameObject("BeltFlow");
        var flow = flowGo.AddComponent<BeltFlow>();
        var chunks = new List<Transform>();
        for (int i = 0; i < 6; i++)
            chunks.Add(Ore(Vector3.zero, 0.8f + 0.06f * (i % 3), flowGo.transform).transform);
        flow.chunks = chunks.ToArray();
        flow.from = new Vector3(-12.4f, BeltTop + 0.07f, BeltZ - 0.24f);
        flow.to = new Vector3(0.45f, BeltTop + 0.07f, BeltZ - 0.24f);
        flow.tripSeconds = 13f;
        // end stop where the belt finishes
        Box("BeltEnd", new Vector3(0.62f, 0.5f, BeltZ), new Vector3(0.2f, 1.0f, 1.2f), mMetal);
        Box("BeltEndStripe", new Vector3(0.62f, 0.9f, BeltZ), new Vector3(0.22f, 0.14f, 1.22f), mHazY, false);

        // the automated feeder at the west end of the belt: everything on the belt comes out of it
        var feeder = new Vector3(-13.4f, 0f, BeltZ);
        Prop("Feeder", feeder, 0f, null, new Vector3(1.7f, 2.8f, 1.8f));
        MapRect("Feeder", feeder.x, feeder.z, 1.7f, 1.8f, mapCover, MapY + 0.1f);
        feederAlarm = new GameObject("FeederAlarm");
        feederAlarm.transform.position = feeder + new Vector3(0.9f, 2.4f, 0);
        Box("AlarmLamp", feeder + new Vector3(0.84f, 2.34f, 0), new Vector3(0.1f, 0.16f, 0.5f), mAlarm, false, null, 0f, feederAlarm.transform);
        PointLight("AlarmLight", feeder + new Vector3(1.4f, 2.2f, 0.4f), new Color32(255, 60, 40, 255), 7f, 7f, feederAlarm.transform);

        // the player's station: console to the left, bin within reach on the right, the chunk that gets sorted in front
        var binAt = new Vector3(StationX + 0.98f, 0f, -9.45f);
        Bin(binAt);
        Prop("Console", new Vector3(StationX - 1.25f, 0, -9.7f), 0f, null, new Vector3(1.0f, 1.3f, 0.5f));
        PointLight("ConsoleLight", new Vector3(StationX - 0.4f, 1.7f, -9.2f), new Color32(90, 230, 210, 255), 3.6f, 6f);
        Box("WorkMat", new Vector3(StationX, BeltTop + 0.006f, WorkPoint.z), new Vector3(0.7f, 0.012f, 0.42f), mTrim, false);
        Box("StandMat", new Vector3(StationX, 0.008f, -9.3f), new Vector3(1.0f, 0.016f, 0.9f), mDark, false);

        var stationGo = new GameObject("OreSorter");
        stationGo.transform.position = WorkPoint;
        var station = stationGo.AddComponent<WorkStation>();
        station.range = 1.75f;        // close enough that the hands can actually reach the belt
        var work = new GameObject("WorkPoint").transform;
        work.SetParent(stationGo.transform, false);
        work.position = WorkPoint;
        var feed = new GameObject("FeedPoint").transform;
        feed.SetParent(stationGo.transform, false);
        feed.position = FeedPoint;
        var binPoint = new GameObject("BinPoint").transform;
        binPoint.SetParent(stationGo.transform, false);
        binPoint.position = binAt + new Vector3(0, 0.62f, 0);
        var chunk = Ore(WorkPoint - Vector3.up * 0.08f, 1.0f, stationGo.transform);
        chunk.name = "OreChunk";
        station.ore = chunk.transform;
        station.workPoint = work;
        station.feedPoint = feed;
        station.binPoint = binPoint;

        // the mask: hidden at the start, rides down the belt after the third ore
        var pickGo = new GameObject("MaskPickup");
        pickGo.transform.position = WorkPoint + new Vector3(0, 0.05f, 0f);
        maskPickup = pickGo.AddComponent<MaskPickup>();
        maskPickup.range = 1.9f;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + "/Mask.fbx");
        maskVisual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        maskVisual.name = "MaskVisual";
        maskVisual.transform.SetParent(pickGo.transform, false);
        maskVisual.transform.localRotation = Quaternion.Euler(-68, 180, 0);     // lying on the belt, face up toward the player
        maskVisual.transform.localScale = Vector3.one * 1.25f;
        PointLight("MaskGlow", pickGo.transform.position + new Vector3(0, 0.5f, 0.3f), Amber, 2.6f, 3f, maskVisual.transform);
        maskPickup.visual = maskVisual;
        return station;
    }

    // ================================================================ the work area's boundary, marked on the floor
    static void BuildWorkAreaBoundary()
    {
        float north = WorkZone.max.z, east = WorkZone.max.x;        // the two open sides
        const float band = 0.84f, y = 0.012f;                       // width of the lettered strip inside the line
        // outer line: bright, on the boundary itself. Inner line: thin, closing the lettered strip.
        Box("WorkAreaLine", new Vector3((West + east + 0.06f) / 2f, y, north), new Vector3(east + 0.06f - West, 0.02f, 0.12f), mWorkLine, false);
        Box("WorkAreaLine", new Vector3(east, y, (South + north) / 2f), new Vector3(0.12f, 0.02f, north - South), mWorkLine, false);
        Box("WorkAreaInnerLine", new Vector3((West + east - band) / 2f, y, north - band), new Vector3(east - band - West + 0.04f, 0.02f, 0.04f), mWorkLine, false);
        Box("WorkAreaInnerLine", new Vector3(east - band, y, (South + north - band) / 2f), new Vector3(0.04f, 0.02f, north - band - South), mWorkLine, false);
        // the words, repeated along both sides, the right way up for someone standing inside the area
        FloorText("WorkAreaText_North", new Vector3((West + east - band) / 2f, y + 0.012f, north - band / 2f), 0f, east - band - West - 0.8f);
        FloorText("WorkAreaText_East", new Vector3(east - band / 2f, y + 0.012f, (South + north - band) / 2f), 90f, north - band - South - 0.8f);
    }

    /// <summary>
    /// "WORK AREA" lettered flat on the floor, once, in the middle of a strip 'length' metres long.
    /// Yaw 0 reads from the south, 90 from the west. The lettering is unlit, so it glows like the
    /// line beside it.
    /// </summary>
    static void FloorText(string name, Vector3 centre, float yaw, float length)
    {
        const float metresPerPixel = 0.0025f;
        const int size = 96;                                  // capitals about 17 cm tall on the floor
        const string word = "WORK AREA", gap = "   |   ";
        float room = length / metresPerPixel;
        const int count = 1;                                  // said once on each side: the line itself marks the boundary
        var words = new string[count];
        for (int i = 0; i < count; i++) words[i] = word;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(env, false);
        go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(room, size * 1.5f);
        rt.SetPositionAndRotation(centre, Quaternion.Euler(90f, yaw, 0f));
        rt.localScale = Vector3.one * metresPerPixel;
        var text = Label("Text", go.transform, string.Join(gap, words), size, TextAnchor.MiddleCenter, Amber, FontStyle.Bold);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(text);
        log.Add(name + ": \"" + word + "\" x" + count);
    }

    // ================================================================ the gate and portal
    static Portal BuildPortal(out GameObject beacon)
    {
        float x = GateX, wall = North + 0.5f;
        var gate = Prop("GateFrame", new Vector3(x, 0, wall), 0f);
        foreach (float sx in new[] { -1f, 1f })
        {
            var pier = gate.AddComponent<BoxCollider>();
            pier.center = new Vector3(sx * 2.75f, 3.25f, 0);
            pier.size = new Vector3(1.3f, 6.5f, 2.0f);
            MapRect("GatePier", x + sx * 2.9f, wall, 1.4f, 1.6f, mapWall, MapY + 0.15f);
        }
        // recess behind the frame
        Box("GateFloor", new Vector3(x, -0.25f, wall + 2f), new Vector3(6f, 0.5f, 3.2f), mFloorB);
        Box("GateThreshold", new Vector3(x, 0.01f, wall - 0.6f), new Vector3(4.4f, 0.02f, 0.5f), mDark, false);
        for (int i = 0; i < 9; i++)
            Box("GateThresholdStripe", new Vector3(x - 2f + i * 0.5f, 0.016f, wall - 0.6f), new Vector3(0.24f, 0.02f, 0.5f), mHazY, false);
        Box("GateSide", new Vector3(x - 2.6f, 3f, wall + 2.1f), new Vector3(0.6f, 6f, 2.6f), mWall);
        Box("GateSide", new Vector3(x + 2.6f, 3f, wall + 2.1f), new Vector3(0.6f, 6f, 2.6f), mWall);
        foreach (float sx in new[] { -1f, 1f })
            for (int i = 0; i < 3; i++)
                Box("GateRib", new Vector3(x + sx * 2.27f, 2.5f, wall + 1.2f + i * 0.8f), new Vector3(0.08f, 5f, 0.16f), mTrim, false);
        Box("GateBack", new Vector3(x, 3f, wall + 3.6f), new Vector3(6f, 6f, 0.4f), mWall);
        Box("GateRoof", new Vector3(x, 4.9f, wall + 2.1f), new Vector3(5f, 0.4f, 2.6f), mWall, false);
        MapRect("GateFloor", x, wall + 1.8f, 4.6f, 3f, mapFloor, MapY);
        MapRect("GateBack", x, wall + 3.4f, 5.6f, 0.5f, mapWall, MapY + 0.15f);

        // tower above the gate, as in the menu art: stepped shaft with bands, fins and lit windows
        Box("Tower", new Vector3(x, 15.4f, wall + 4.5f), new Vector3(9f, 16.4f, 6f), mTower, false);
        Box("TowerTop", new Vector3(x, 30f, wall + 4.9f), new Vector3(6.4f, 13f, 5f), mTower, false);
        Box("TowerCrown", new Vector3(x, 38f, wall + 4.9f), new Vector3(4.4f, 3f, 3.4f), mTower, false);
        Box("TowerMast", new Vector3(x, 42f, wall + 4.9f), new Vector3(0.3f, 6f, 0.3f), mDark, false);
        Box("TowerMastLight", new Vector3(x, 45.2f, wall + 4.9f), new Vector3(0.5f, 0.5f, 0.5f), mAlarm, false);
        foreach (float y in new[] { 9.2f, 14.4f, 19.6f, 23.4f })
            Box("TowerBand", new Vector3(x, y, wall + 4.5f), new Vector3(9.5f, 0.45f, 6.5f), mTrim, false);
        foreach (float y in new[] { 27f, 32f, 36.3f })
            Box("TowerBand", new Vector3(x, y, wall + 4.9f), new Vector3(6.9f, 0.4f, 5.5f), mTrim, false);
        foreach (float sx in new[] { -1f, 1f })
        {
            Box("TowerFin", new Vector3(x + sx * 4.7f, 12f, wall + 1.7f), new Vector3(0.6f, 11f, 0.6f), mMetal, false);
            Box("TowerFin", new Vector3(x + sx * 3.4f, 29f, wall + 2.5f), new Vector3(0.45f, 12f, 0.45f), mMetal, false);
            Box("TowerShoulder", new Vector3(x + sx * 5.6f, 7.6f, wall + 4.5f), new Vector3(2.2f, 2.4f, 4.4f), mTower, false);
        }
        var rnd = new System.Random(4);
        for (int row = 0; row < 12; row++)
            for (int col = -1; col <= 1; col++)
                if (rnd.NextDouble() < 0.6)
                    Box("TowerWindow", new Vector3(x + col * 1.5f, 8f + row * 1.7f, wall + (row < 9 ? 1.47f : 2.37f)), new Vector3(0.6f, 0.9f, 0.06f), mWindow, false);

        // portal surface, energy bars, light
        var portalGo = new GameObject("Portal");
        portalGo.transform.position = PortalSpot;
        var trigger = portalGo.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        // generous on purpose: the whole gateway and a step in front of it count, so nobody has to find a narrow spot
        trigger.center = new Vector3(0, 2f, -1.0f);
        trigger.size = new Vector3(6.4f, 4f, 5.0f);
        var portal = portalGo.AddComponent<Portal>();
        var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        surface.name = "PortalSurface";
        surface.transform.SetParent(portalGo.transform, false);
        surface.transform.position = new Vector3(x, 2.3f, wall + 3.35f);
        surface.transform.localScale = new Vector3(4.6f, 4.6f, 1f);
        surface.GetComponent<MeshRenderer>().sharedMaterial = mPortal;
        var fxComp = portalGo.AddComponent<PortalFx>();
        var bars = new List<Transform>();
        for (int i = 0; i < 7; i++)
            bars.Add(Box("PortalBar", new Vector3(x - 1.8f + i * 0.6f, 2.3f, wall + 3.1f), new Vector3(0.1f, 2.2f, 0.06f), mPortalBar, false, null, 0f, portalGo.transform).transform);
        fxComp.bars = bars.ToArray();
        fxComp.glow = PointLight("PortalLight", new Vector3(x, 2.4f, wall + 1.1f), new Color32(120, 255, 235, 255), 7f, 12f, portalGo.transform);

        // beacon, switched on once the portal becomes the goal
        beacon = new GameObject("PortalBeacon");
        beacon.transform.position = new Vector3(x, 0, wall);
        Box("BeaconBeam", new Vector3(x, 16f, wall - 0.2f), new Vector3(0.35f, 18f, 0.35f), mPortal, false, null, 0f, beacon.transform);
        PointLight("BeaconLight", new Vector3(x, 3f, wall - 2f), new Color32(120, 255, 235, 255), 9f, 16f, beacon.transform);
        return portal;
    }

    // ================================================================ city silhouettes beyond the walls
    static void BuildSkyline()
    {
        var rnd = new System.Random(23);
        const float s = 0.86f;      // pulled in with the smaller yard
        var blocks = new (float x, float z, float w, float d, float h)[]
        {
            (-30, -8, 12, 14, 20), (-31, 9, 13, 12, 30), (-27, 24, 9, 10, 16), (-44, 2, 14, 30, 42),
            (30, -6, 12, 13, 24), (31, 10, 12, 12, 17), (28, 25, 10, 9, 28), (45, 6, 14, 34, 46),
            (-14, -26, 13, 10, 19), (2, -27, 12, 10, 27), (17, -25, 11, 9, 15), (0, -42, 40, 12, 38),
            (-11, 33, 11, 9, 23), (22, 34, 12, 9, 20), (-24, 42, 16, 10, 36), (36, 44, 18, 10, 40),
        };
        int index = 0;
        foreach (var raw in blocks)
        {
            var b = (x: raw.x * s, z: raw.z * s, raw.w, raw.d, raw.h);
            Box("City", new Vector3(b.x, b.h / 2f, b.z), new Vector3(b.w, b.h, b.d), mCity, false);
            // stepped tops, ledges and rooftop machinery so the skyline is not a row of plain blocks
            Box("CityLedge", new Vector3(b.x, b.h * 0.72f, b.z), new Vector3(b.w + 0.5f, 0.5f, b.d + 0.5f), mCity, false);
            bool stepped = index % 3 != 2;
            if (stepped)
                Box("CityStep", new Vector3(b.x + b.w * 0.12f, b.h + 2.2f, b.z), new Vector3(b.w * 0.62f, 4.4f, b.d * 0.7f), mCity, false);
            if (index % 2 == 0)
                Prop("Rooftop", new Vector3(b.x - b.w * 0.2f, b.h + (stepped ? 4.4f : 0f), b.z), index * 50f, Vector3.one * 1.5f);
            // lit windows on the face that looks at the yard
            bool faceX = Mathf.Abs(b.x) > Mathf.Abs(b.z);
            int count = rnd.Next(5, 10);
            for (int i = 0; i < count; i++)
            {
                float u = (float)rnd.NextDouble() - 0.5f, y = 4f + (float)rnd.NextDouble() * (b.h - 6f);
                var p = faceX
                    ? new Vector3(b.x - Mathf.Sign(b.x) * (b.w / 2f + 0.05f), y, b.z + u * b.d * 0.8f)
                    : new Vector3(b.x + u * b.w * 0.8f, y, b.z - Mathf.Sign(b.z) * (b.d / 2f + 0.05f));
                var size = faceX ? new Vector3(0.08f, 1.1f, 0.7f) : new Vector3(0.7f, 1.1f, 0.08f);
                Box("CityWindow", p, size, mWindow, false);
            }
            index++;
        }
        // gantry over the west wall
        Box("Gantry", new Vector3(West - 4f, 9f, 1f), new Vector3(0.5f, 0.5f, 26f), mCity, false);
        Box("Gantry", new Vector3(West - 4f, 10.6f, 1f), new Vector3(0.4f, 0.3f, 26f), mCity, false);
        for (float z = -11f; z <= 13f; z += 3.4f)
            Box("GantryPost", new Vector3(West - 4f, 9.8f, z), new Vector3(0.2f, 1.6f, 0.2f), mCity, false);
        for (float z = -11f; z <= 13f; z += 12f)
            Box("GantryLeg", new Vector3(West - 4f, 4.5f, z), new Vector3(0.5f, 9f, 0.5f), mCity, false);
    }

    // ================================================================ player
    static GameObject BuildPlayer(out Transform pivot, out PlayerBody body, out Light flash, out Camera[] cameras)
    {
        var player = new GameObject("Player");
        player.layer = LayerMask.NameToLayer(Layers.Player);
        player.transform.SetPositionAndRotation(Spawn, Quaternion.Euler(0, 180, 0));
        var capsule = player.AddComponent<CharacterController>();
        capsule.height = 1.8f;
        capsule.radius = 0.35f;
        capsule.center = new Vector3(0, 0.92f, 0);
        capsule.stepOffset = 0.3f;
        player.AddComponent<PlayerIdentity>();
        var controller = player.AddComponent<FirstPersonController>();

        // eyes sit a little ahead of the neck, so looking down shows the chest, arms and legs from above
        pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0, 1.65f, 0.12f);
        controller.cameraPivot = pivot;

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(pivot, false);
        var cam = WorldCamera(camGo);
        camGo.AddComponent<AudioListener>();

        // the player's own body is drawn by its own camera on top, so it never pokes into walls
        var bodyCamGo = new GameObject("BodyCamera");
        bodyCamGo.transform.SetParent(pivot, false);
        var bodyCam = bodyCamGo.AddComponent<Camera>();
        bodyCam.fieldOfView = 68f;
        bodyCam.nearClipPlane = 0.03f;
        bodyCam.farClipPlane = 6f;
        bodyCam.cullingMask = LayerMask.GetMask(Layers.FirstPerson);
        var bodyData = bodyCam.GetUniversalAdditionalCameraData();
        bodyData.renderType = CameraRenderType.Overlay;
        bodyData.renderPostProcessing = true;
        cam.GetUniversalAdditionalCameraData().cameraStack.Add(bodyCam);
        cameras = new[] { cam, bodyCam };

        // two complete figures standing exactly where the player stands: the human worker and the masked robot.
        // Arms and hands are part of them, so they cannot come apart from the body.
        var human = Figure("Human", player.transform, 1f);
        var robot = Figure("Robot", player.transform, 1f / 1.15f);      // same skeleton as the human at this size
        body = player.AddComponent<PlayerBody>();
        body.human = human;
        body.robot = robot;
        body.humanAnimator = human.GetComponent<Animator>();
        body.robotAnimator = robot.GetComponent<Animator>();
        body.humanHead = human.transform.Find("Hips/Chest/Head");
        body.robotHead = robot.transform.Find("Hips/Chest/Head");
        body.humanRenderers = human.GetComponentsInChildren<Renderer>(true);
        var parts = new List<Transform>();
        foreach (var path in new[] { "Hips/Chest", "Hips/Chest/UpperArm.L", "Hips/Chest/UpperArm.R",
                                     "Hips/Chest/UpperArm.L/Forearm.L", "Hips/Chest/UpperArm.R/Forearm.R",
                                     "Hips/Chest/UpperArm.L/Forearm.L/Hand.L", "Hips/Chest/UpperArm.R/Forearm.R/Hand.R",
                                     "Hips/Thigh.L", "Hips/Thigh.R", "Hips/Thigh.L/Shin.L", "Hips/Thigh.R/Shin.R",
                                     "Hips/Thigh.L/Shin.L/Foot.L", "Hips/Thigh.R/Shin.R/Foot.R" })
            parts.Add(robot.transform.Find(path));
        body.robotParts = parts.ToArray();
        body.controller = controller;
        body.bodyCamera = bodyCam;
        body.sortSeconds = PlayerBodyAnimationBuilder.SortSeconds;
        body.sortGrip = PlayerBodyAnimationBuilder.SortGrip;
        body.sortRelease = PlayerBodyAnimationBuilder.SortRelease;
        body.grabSeconds = PlayerBodyAnimationBuilder.GrabSeconds;
        body.grabGrip = PlayerBodyAnimationBuilder.GrabGrip;
        body.grabAtFace = PlayerBodyAnimationBuilder.GrabAtFace;

        // measure where the fixed animations put the hands, so the player can be lined up with what they reach for
        var handL = human.transform.Find("Hips/Chest/UpperArm.L/Forearm.L/Hand.L");
        var handR = human.transform.Find("Hips/Chest/UpperArm.R/Forearm.R/Hand.R");
        var sort = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerBodyAnimationBuilder.ClipPath("Human", "Sort"));
        sort.SampleAnimation(human, sort.length * PlayerBodyAnimationBuilder.SortGrip);
        var palmL = handL.TransformPoint(new Vector3(0, -0.06f, 0));
        var palmR = handR.TransformPoint(new Vector3(0, -0.06f, 0));
        var between = (palmL + palmR) / 2f;
        body.sortReach = player.transform.InverseTransformPoint(between);
        var carry = new GameObject("CarryPoint").transform;
        carry.SetParent(handL, false);
        carry.position = between;
        body.carryPoint = carry;
        var grab = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerBodyAnimationBuilder.ClipPath("Human", "Grab"));
        grab.SampleAnimation(human, grab.length * PlayerBodyAnimationBuilder.GrabGrip);
        var grip = new GameObject("GripPoint").transform;
        grip.SetParent(handR, false);
        grip.localPosition = new Vector3(0, -0.08f, 0.02f);
        body.gripPoint = grip;
        body.grabReach = player.transform.InverseTransformPoint(grip.position);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerBodyAnimationBuilder.ClipPath("Human", "Idle"));
        idle.SampleAnimation(human, 0f);        // leave the figure standing

        // an arrow floating over the player, shown only during the opening flight
        var marker = Prop("Arrow", Vector3.zero, 0f, Vector3.one * 1.3f, null, player.transform);
        marker.transform.localPosition = new Vector3(0, 2.25f, 0);
        var hover = marker.AddComponent<Hover>();
        hover.spin = 90f;
        hover.amplitude = 0.12f;
        hover.period = 1.4f;
        PointLight("MarkerLight", Vector3.zero, Amber, 3f, 4f, marker.transform).transform.localPosition = new Vector3(0, 0.3f, 0);
        body.marker = marker;

        flash = PointLight("ChangeFlash", Vector3.zero, Amber, 5f, 3f, pivot);
        flash.transform.localPosition = new Vector3(0, -0.2f, 0.4f);
        flash.enabled = false;

        var interactor = player.AddComponent<PlayerInteractor>();
        interactor.viewpoint = pivot;
        interactor.controller = controller;
        return player;
    }

    /// <summary>One of the player's two bodies, standing at the player's feet and driven by its own animator.</summary>
    static GameObject Figure(string rig, Transform player, float scale)
    {
        string modelPath = null;
        foreach (var r in PlayerBodyAnimationBuilder.Rigs)
            if (r.rig == rig) modelPath = r.model;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = "Body_" + rig;
        go.transform.SetParent(player, false);
        go.transform.localScale = Vector3.one * scale;
        SetLayer(go, Layers.FirstPerson);
        var animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerBodyAnimationBuilder.ControllerPath(rig));
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    /// <summary>A camera that draws the world (not the arms or the minimap) with post-processing.</summary>
    static Camera WorldCamera(GameObject go)
    {
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 68f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.Skybox;       // the sunrise sky
        cam.cullingMask = ~LayerMask.GetMask(Layers.Minimap, Layers.FirstPerson, Layers.Hud3D);
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        return cam;
    }

    /// <summary>The camera used for the opening flight; off until the flight starts.</summary>
    static Camera BuildFlightCamera()
    {
        var cam = WorldCamera(new GameObject("IntroFlightCamera"));
        cam.depth = 5;
        cam.enabled = false;
        return cam;
    }

    // ================================================================ hiding spots
    static void BuildHideSpots(FirstPersonController controller, PlayerIdentity identity, DetectionSystem detection, HudController hud)
    {
        hideSpots.Clear();
        var root = new GameObject("HideSpots").transform;
        // closets against the walls: press E to step inside
        Closet(root, new Vector3(West + 0.62f, 0, 4.4f), 90f, controller, identity, detection, hud);
        Closet(root, new Vector3(East - 0.62f, 0, 1.6f), -90f, controller, identity, detection, hud);
        Closet(root, new Vector3(-2.5f, 0, North - 0.62f), 180f, controller, identity, detection, hud);
        Closet(root, new Vector3(East - 0.62f, 0, -7f), -90f, controller, identity, detection, hud);          // a second one on the east wall, for the long walk up that side
        // crates on short legs: press E to slide into the gap underneath
        HideBox(root, new Vector3(-11f, 0, -2f), controller, identity, detection, hud);                           // west side, between the line and the scout's loop
        HideBox(root, new Vector3(-0.2f, 0, -3.9f), controller, identity, detection, hud);
        HideBox(root, new Vector3(1.9f, 0, 1.6f), controller, identity, detection, hud);
        HideBox(root, new Vector3(10.5f, 0, 5.9f), controller, identity, detection, hud);
    }

    static void Closet(Transform root, Vector3 position, float yaw, FirstPersonController controller, PlayerIdentity identity,
                       DetectionSystem detection, HudController hud)
    {
        var go = Prop("Closet", position, yaw, null, null, root);
        go.name = "HideCloset";
        // back and side walls are solid; the front is the doorway
        foreach (var (c, s) in new[] { (new Vector3(0, 1.15f, -0.47f), new Vector3(1.3f, 2.3f, 0.08f)),
                                        (new Vector3(-0.62f, 1.15f, 0), new Vector3(0.08f, 2.3f, 1.0f)),
                                        (new Vector3(0.62f, 1.15f, 0), new Vector3(0.08f, 2.3f, 1.0f)) })
        {
            var wall = go.AddComponent<BoxCollider>();
            wall.center = c;
            wall.size = s;
        }
        var door = Prop("ClosetDoor", Vector3.zero, 0f, null, null, go.transform);
        door.transform.localPosition = new Vector3(-0.55f, 0.08f, 0.5f);     // hinge on the left jamb
        var inside = new GameObject("Inside").transform;
        inside.SetParent(go.transform, false);
        inside.localPosition = new Vector3(0, 0.1f, 0.0f);
        var exit = new GameObject("Exit").transform;
        exit.SetParent(go.transform, false);
        exit.localPosition = new Vector3(0, 0.05f, 1.25f);

        var spot = go.AddComponent<HideSpot>();
        spot.kind = HideKind.Closet;
        spot.range = 2.2f;
        spot.insidePoint = inside;
        spot.exitPoint = exit;
        spot.door = door.transform;
        Wire(spot, go, controller, identity, detection, hud, new Vector3(0, 1.4f, 1.1f));
        MarkerObject("Marker_Closet", go.transform, MarkerShape.Door, 0.95f * Mathf.Min(mapScale, 2.4f), MapY + 0.45f, mapHide, true, false);
    }

    static void HideBox(Transform root, Vector3 position, FirstPersonController controller, PlayerIdentity identity,
                        DetectionSystem detection, HudController hud)
    {
        var go = Prop("HideBox", position, 0f, null, null, root);
        go.name = "HideBox";
        // the crate and its legs are solid; the gap underneath (0.78 m) is too low to walk or crouch into,
        // so the only way in is the hiding move, which lays the player flat
        var crate = go.AddComponent<BoxCollider>();
        crate.center = new Vector3(0, 1.27f, 0);
        crate.size = new Vector3(1.5f, 0.94f, 1.5f);
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var leg = go.AddComponent<BoxCollider>();
                leg.center = new Vector3(sx * 0.62f, 0.39f, sz * 0.62f);
                leg.size = new Vector3(0.14f, 0.78f, 0.14f);
            }
        var spot = go.AddComponent<HideSpot>();
        spot.kind = HideKind.Box;
        spot.range = 2.0f;
        Wire(spot, go, controller, identity, detection, hud, new Vector3(0, 0.4f, 0));
        MarkerObject("Marker_HideBox", go.transform, MarkerShape.Frame, 0.85f * mapScale, MapY + 0.45f, mapHide, false, false);
    }

    static void Wire(HideSpot spot, GameObject go, FirstPersonController controller, PlayerIdentity identity, DetectionSystem detection,
                     HudController hud, Vector3 lightAt)
    {
        spot.controller = controller;
        spot.identity = identity;
        spot.detection = detection;
        spot.hud = hud;
        var glow = PointLight("HideGlowLight", Vector3.zero, HideGreen, 0.45f, 2.2f, go.transform);    // a faint pool of light, no more
        glow.transform.localPosition = lightAt;
        var pulse = go.AddComponent<GlowPulse>();
        pulse.glowLight = glow;
        hideSpots.Add(spot);
    }

    // ================================================================ robots
    static int RobotCount, WorkerCount;

    static RobotPatrol.Stop At(float x, float z, float wait) =>
        new RobotPatrol.Stop { position = new Vector3(x, 0, z), wait = wait };

    static RobotPatrol.Stop At(float x, float z, float wait, float lookYaw) =>
        new RobotPatrol.Stop { position = new Vector3(x, 0, z), wait = wait, faceDirection = true, lookYaw = lookYaw };

    static void BuildRobots()
    {
        RobotCount = 0;
        var root = new GameObject("Robots").transform;
        // orange units are the standard security model and make up most of the patrols
        Robot(root, "Overseer", "Orange", 1.3f, 180f, At(StationX, -3.6f, 6.5f, 180f), At(StationX, -3.6f, 5f, 50f));
        // (four in all: the yard has room to breathe, and each one's beat is easy to read)
        Robot(root, "Patrol_EastLoop", "Orange", 1.3f, 0f, At(8.5f, -9.5f, 1.5f), At(8.5f, 8f, 2f, 270f), At(12.5f, 8f, 1f), At(12.5f, -9.5f, 2f, 270f));
        Robot(root, "Scout_WestLoop", "Purple", 2.1f, 0f, At(-12.8f, 2.2f, 1f), At(-12.8f, 8f, 1f), At(-5.2f, 8f, 1f), At(-5.2f, 2.2f, 1f));
        // the portal's guard no longer stands still: it walks a beat round the gateway, stopping to look about,
        // which leaves moments when the way in is unwatched
        Robot(root, "Guard_Gate", "Blue", 1.5f, 180f,
              At(GateX, 9.6f, 2.5f, 180f), At(8.0f, 8.6f, 1.5f, 200f), At(8.0f, 4.0f, 2f, 270f),
              At(2.4f, 10.4f, 2f, 150f), At(-6.5f, 10.5f, 2f, 180f), At(2.4f, 10.4f, 1f, 90f));
    }

    static void Robot(Transform root, string name, string variant, float speed, float startYaw, params RobotPatrol.Stop[] route)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Robots/Robot_" + variant + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(route[0].position, Quaternion.Euler(0, startYaw, 0));
        SetLayer(go, Layers.Robot);

        float height = variant == "Orange" ? 2.27f : 1.8f;
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0, height / 2f, 0);
        capsule.height = height;
        capsule.radius = 0.42f;

        Solidify(go);
        WireGesture(go);

        var vision = go.AddComponent<RobotVision>();
        vision.head = go.transform.Find("Hips/Chest/Head");
        vision.variant = variant;
        vision.viewAngle = 84f;                 // wide enough to catch movement on its flanks
        vision.viewDistance = variant == "Purple" ? 6.5f : 7.5f;
        var patrol = go.AddComponent<RobotPatrol>();
        patrol.route = route;
        patrol.walkSpeed = speed * 1.15f;       // every patrol steps out a little faster than first tuned
        patrol.icon = AlertMark(go.transform, height + 0.5f);
        patrol.animator = go.GetComponent<Animator>();

        var marker = MarkerObject("Marker_" + name, go.transform, MarkerShape.Dot, (variant == "Orange" ? 0.85f : 0.7f) * mapScale, MapY + 0.6f,
                                  variant == "Orange" ? mapOrange : variant == "Purple" ? mapPurple : mapBlue, false, false);
        marker.vision = vision;
        marker.coneMaterial = mapCone;
        marker.coneAlertMaterial = mapConeAlert;
        RobotCount++;
    }

    static CaptureDirector BuildCaptureDirector(GameObject systems, FirstPersonController controller, Camera view, HudController hud)
    {
        var capture = systems.AddComponent<CaptureDirector>();
        capture.player = controller;
        capture.view = view;
        capture.hud = hud;
        return capture;
    }

    /// <summary>
    /// Makes a walking robot properly solid: its one capsule rides a kinematic body, which is how
    /// the physics engine wants moving obstacles, so the player is stopped by it and cannot pass through.
    /// </summary>
    static void Solidify(GameObject robot)
    {
        var rb = robot.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>The upper-body poses (reaching to grab, typing, writing, carrying) laid over a robot's own animation.</summary>
    static RobotGesture WireGesture(GameObject robot)
    {
        var gesture = robot.AddComponent<RobotGesture>();
        gesture.chest = robot.transform.Find("Hips/Chest");
        gesture.head = robot.transform.Find("Hips/Chest/Head");
        gesture.upperArmL = robot.transform.Find("Hips/Chest/UpperArm.L");
        gesture.forearmL = robot.transform.Find("Hips/Chest/UpperArm.L/Forearm.L");
        gesture.upperArmR = robot.transform.Find("Hips/Chest/UpperArm.R");
        gesture.forearmR = robot.transform.Find("Hips/Chest/UpperArm.R/Forearm.R");
        // the legs too, for the poses that bend the knees (cowering)
        gesture.hips = robot.transform.Find("Hips");
        gesture.thighL = robot.transform.Find("Hips/Thigh.L");
        gesture.shinL = robot.transform.Find("Hips/Thigh.L/Shin.L");
        gesture.footL = robot.transform.Find("Hips/Thigh.L/Shin.L/Foot.L");
        gesture.thighR = robot.transform.Find("Hips/Thigh.R");
        gesture.shinR = robot.transform.Find("Hips/Thigh.R/Shin.R");
        gesture.footR = robot.transform.Find("Hips/Thigh.R/Shin.R/Foot.R");
        return gesture;
    }

    // ================================================================ background workers
    static void BuildWorkers()
    {
        WorkerCount = 0;
        var root = new GameObject("Workers").transform;
        // three more sorters on the same belt as the player, each with a bin on their left
        foreach (var (x, pace, offset) in new[] { (-10.6f, 0.92f, 0.35f), (-3.4f, 1.08f, 0.7f), (-0.6f, 0.86f, 0.1f) })
        {
            Worker(root, new Vector3(x, 0f, -9.3f), 180f, pace, offset);
            Bin(new Vector3(x + 1.15f, 0f, -9.35f));
        }
        // one loading a cart from a pile by the rails
        Worker(root, new Vector3(3.7f, 0f, -5.0f), 180f, 1f, 0.5f);
        Prop("Pallet", new Vector3(3.7f, 0f, -6.1f), 0f, null, new Vector3(1.2f, 0.14f, 1.0f));
        Crate(3.7f, -6.1f, 0.5f, 0.14f, 6f);
        Ore(new Vector3(3.7f, 0.64f, -6.1f), 0.9f, null, true);
    }

    static void Worker(Transform root, Vector3 position, float yaw, float pace, float offset)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerAnimationBuilder.PrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "Worker_" + (WorkerCount + 1);
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        SetLayer(go, Layers.Robot);      // solid to the player, but does not block a robot's line of sight
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0, 0.93f, 0);
        capsule.height = 1.86f;
        capsule.radius = 0.34f;

        var hand = go.transform.Find("Hips/Chest/UpperArm.L/Forearm.L/Hand.L");
        var ore = Ore(Vector3.zero, 0.62f, hand);
        ore.transform.localPosition = new Vector3(0.2f, -0.1f, 0.04f);      // held between both hands
        ore.SetActive(false);

        var npc = go.AddComponent<WorkerNpc>();
        npc.animator = go.GetComponent<Animator>();
        npc.carriedOre = ore;
        npc.pace = pace;
        npc.startOffset = offset;
        WorkerCount++;
    }

    // ================================================================ the ending: the yard celebrates
    /// <summary>
    /// Everything the ending needs, switched off until the hero comes back through the portal:
    /// a banner hung on the tower over the gate (portrait, "THE LAST HUMAN HERO", the number),
    /// the workers gathered under it cheering, confetti, and a small studio where the portrait is filmed.
    /// </summary>
    static VictoryCutscene BuildVictory(GameObject systems, HudController hud, Camera flightCamera, Camera[] playerCameras)
    {
        var victory = systems.AddComponent<VictoryCutscene>();
        victory.flightCamera = flightCamera;
        victory.playerCameras = playerCameras;
        victory.hud = hud;
        var gone = new List<GameObject>();
        foreach (var name in new[] { "Robots", "Workers" })
        {
            var go = GameObject.Find(name);
            if (go != null) gone.Add(go);
        }
        victory.hideForCelebration = gone.ToArray();

        var root = new GameObject("Celebration").transform;
        victory.celebration = root.gameObject;

        // ---- the banner: a world-space canvas, 7 m by 9.6 m, on the front of the tower
        float bx = GateX, by = 16.2f, bz = North + 1.5f;        // just proud of the tower's bands
        Vector2 mid = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f);
        var bannerGo = new GameObject("HeroBanner", typeof(RectTransform));
        bannerGo.layer = LayerMask.NameToLayer("UI");
        bannerGo.transform.SetParent(root, false);
        bannerGo.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var bannerRt = (RectTransform)bannerGo.transform;
        bannerRt.sizeDelta = new Vector2(700, 960);
        bannerRt.SetPositionAndRotation(new Vector3(bx, by, bz), Quaternion.identity);
        bannerRt.localScale = Vector3.one * 0.01f;
        Stretch(Img("Cloth", bannerGo.transform, new Color32(10, 34, 38, 255)));
        Border(bannerGo.transform, Amber, 10f);
        var kicker = Label("Kicker", bannerGo.transform, "FREEDOM RESTORED", 30, TextAnchor.MiddleCenter, new Color32(120, 255, 235, 255), FontStyle.Bold);
        Place(kicker, top, top, top, new Vector2(0, -26), new Vector2(660, 40));
        var frame = Img("PortraitFrame", bannerGo.transform, Amber);
        Place(frame, top, top, top, new Vector2(0, -80), new Vector2(520, 520));
        victory.portrait = UiObject("Portrait", frame.transform).AddComponent<RawImage>();
        victory.portrait.raycastTarget = false;
        Place(victory.portrait, mid, mid, mid, Vector2.zero, new Vector2(500, 500));
        var title1 = Label("TitleTop", bannerGo.transform, "THE LAST HUMAN", 62, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Place(title1, top, top, top, new Vector2(0, -618), new Vector2(680, 76));
        var title2 = Label("TitleBottom", bannerGo.transform, "HERO", 132, TextAnchor.MiddleCenter, Amber, FontStyle.Bold);
        Place(title2, top, top, top, new Vector2(0, -690), new Vector2(680, 150));
        title1.horizontalOverflow = title2.horizontalOverflow = HorizontalWrapMode.Overflow;
        Place(Img("Rule", bannerGo.transform, Amber), top, top, top, new Vector2(0, -846), new Vector2(520, 5));
        var number = Label("Number", bannerGo.transform, "HUMAN 017", 54, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Place(number, top, top, top, new Vector2(0, -862), new Vector2(680, 70));
        // the pole it hangs from, two cords, and lights playing on it
        Box("BannerPole", new Vector3(bx, by + 4.95f, bz - 0.05f), new Vector3(7.6f, 0.18f, 0.18f), mMetal, false, null, 0f, root);
        foreach (float s in new[] { -1f, 1f })
        {
            Box("BannerFinial", new Vector3(bx + s * 3.9f, by + 4.95f, bz - 0.05f), new Vector3(0.3f, 0.3f, 0.3f), mLamp, false, null, 0f, root);
            PointLight("BannerLight", new Vector3(bx + s * 3.2f, by - 1f, bz - 3.2f), new Color32(255, 214, 150, 255), 9f, 12f, root);
        }

        // ---- the portrait studio, far under the yard: the hero's own figure in front of a plain backdrop
        var studio = new GameObject("PortraitStudio").transform;
        studio.SetParent(root, false);
        studio.position = new Vector3(0, -120f, 0);
        var sitter = Figure("Human", studio, 1f);
        sitter.name = "HeroPortraitFigure";
        SetLayer(sitter, "Default");
        sitter.transform.SetPositionAndRotation(studio.position, Quaternion.Euler(0, 180f, 0));      // facing -z, toward the camera
        Box("Backdrop", studio.position + new Vector3(0, 1.6f, 1.1f), new Vector3(4f, 3f, 0.1f), Lit("Env_PortraitBackdrop", new Color32(40, 110, 112, 255), 0.1f), false, null, 0f, studio);
        PointLight("KeyLight", studio.position + new Vector3(0.9f, 2.1f, -1.2f), new Color32(255, 220, 180, 255), 3.2f, 5f, studio);
        PointLight("RimLight", studio.position + new Vector3(-1.1f, 1.9f, 0.5f), new Color32(120, 255, 235, 255), 2.2f, 4f, studio);
        var portraitGo = new GameObject("PortraitCamera");
        portraitGo.transform.SetParent(studio, false);
        var portraitCam = portraitGo.AddComponent<Camera>();
        portraitCam.transform.position = studio.position + new Vector3(0.12f, 1.62f, -1.25f);
        portraitCam.transform.LookAt(studio.position + new Vector3(0, 1.56f, 0));
        portraitCam.fieldOfView = 34f;
        portraitCam.nearClipPlane = 0.1f;
        portraitCam.farClipPlane = 6f;
        portraitCam.clearFlags = CameraClearFlags.SolidColor;
        portraitCam.backgroundColor = new Color32(40, 110, 112, 255);
        portraitCam.cullingMask = 1 << 0;
        var portraitData = portraitCam.GetUniversalAdditionalCameraData();
        portraitData.renderPostProcessing = false;
        portraitData.renderShadows = false;
        victory.portraitCamera = portraitCam;

        // ---- the crowd: every worker in the yard, under the banner, arms up
        var crowd = new List<RobotGesture>();
        var places = new (float x, float z)[]
        {
            (6.6f, 2.5f), (8.0f, 2.1f), (9.3f, 3.0f), (6.9f, 4.4f), (8.4f, 4.7f), (7.4f, 6.5f), (8.9f, 6.7f), (6.6f, 8.5f), (8.2f, 8.7f),
            (9.5f, 9.0f), (3.3f, 2.8f), (3.1f, 7.0f), (2.7f, 9.8f), (3.9f, 10.6f), (5.0f, 0.2f), (4.1f, -1.2f), (6.0f, -1.4f), (5.0f, 6.6f),
            (5.1f, 8.8f), (4.0f, 8.9f), (6.1f, 10.4f), (7.3f, 0.3f),
        };
        var people = new GameObject("Crowd").transform;
        people.SetParent(root, false);
        for (int i = 0; i < places.Length; i++)
        {
            var (x, z) = places[i];
            var person = Figure("Human", people, 0.94f + 0.03f * (i % 5));
            person.name = "Cheering_" + i;
            SetLayer(person, Layers.Robot);
            var facing = new Vector3(bx - x, 0f, bz - z);
            person.transform.SetPositionAndRotation(new Vector3(x, 0.07f, z), Quaternion.LookRotation(facing));
            foreach (var r in person.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            var cheer = WireGesture(person);
            cheer.gesture = Gesture.Cheer;
            crowd.Add(cheer);
            var hop = person.AddComponent<Hover>();          // a little bounce on the spot
            hop.spin = 0f;
            hop.amplitude = 0.06f;
            hop.period = 0.46f + 0.05f * (i % 4);
        }
        victory.crowd = crowd.ToArray();

        // ---- confetti drifting down over them
        var confettiGo = new GameObject("Confetti");
        confettiGo.transform.SetParent(root, false);
        confettiGo.transform.SetPositionAndRotation(new Vector3(bx + 1f, 13f, 5f), Quaternion.Euler(90f, 0f, 0f));
        var confetti = confettiGo.AddComponent<ParticleSystem>();
        var main = confetti.main;
        main.startLifetime = 7f;
        main.startSpeed = 0.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.12f;
        main.maxParticles = 700;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var colours = new Gradient();
        colours.SetKeys(new[]
        {
            new GradientColorKey(Amber, 0f), new GradientColorKey(new Color32(120, 255, 235, 255), 0.35f),
            new GradientColorKey(Cream, 0.65f), new GradientColorKey(new Color32(255, 110, 150, 255), 1f),
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(colours) { mode = ParticleSystemGradientMode.RandomColor };
        var emission = confetti.emission;
        emission.rateOverTime = 90f;
        var shape = confetti.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(16f, 14f, 0.5f);
        var spin = confetti.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var drift = confetti.noise;
        drift.enabled = true;
        drift.strength = 0.5f;
        drift.frequency = 0.4f;
        var confettiRenderer = confettiGo.GetComponent<ParticleSystemRenderer>();
        confettiRenderer.sharedMaterial = Flat("FX_Confetti", Color.white);
        confettiRenderer.shadowCastingMode = ShadowCastingMode.Off;

        // ---- the camera: from among the crowd, up to the banner
        victory.path = new[]
        {
            new Vector3(8.6f, 1.5f, -4.6f), new Vector3(6.4f, 2.4f, -1.5f), new Vector3(5.4f, 7.5f, -0.5f), new Vector3(bx, 15.4f, -1.2f),
        };
        victory.focus = new[]
        {
            new Vector3(bx, 5f, 10f), new Vector3(bx, 9f, bz), new Vector3(bx, 14.5f, bz), new Vector3(bx, by, bz),
        };
        victory.seconds = 13f;
        root.gameObject.SetActive(false);
        return victory;
    }

    // ================================================================ minimap
    static void BuildMinimap(RawImage display, Transform player, Transform mask, Transform portal)
    {
        // cropped to the walls and the gate recess, so nothing on the widget is empty space
        var camGo = new GameObject("MinimapCamera");
        camGo.transform.SetPositionAndRotation(new Vector3(0f, MapY + 20f, 2.2f), Quaternion.Euler(90, 0, 0));
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 15.4f;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(8, 24, 26, 255);
        cam.cullingMask = LayerMask.GetMask(Layers.Minimap);
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;

        var controller = camGo.AddComponent<MinimapController>();
        controller.mapCamera = cam;
        controller.display = display;
        controller.resolution = 384;

        MarkerObject("Marker_Player", player, MarkerShape.Arrow, 1.25f, MapY + 0.9f, mapPlayer, true, false);
        MarkerObject("Marker_Mask", mask, MarkerShape.Diamond, 0.95f, MapY + 0.7f, mapMask, false, true);
        MarkerObject("Marker_Portal", portal, MarkerShape.Ring, 1.5f, MapY + 0.5f, mapPortal, false, false);
    }

    static MinimapMarker MarkerObject(string name, Transform target, MarkerShape shape, float size, float height, Material material,
                                      bool turn, bool hideWithTarget)
    {
        var m = new GameObject(name).AddComponent<MinimapMarker>();
        m.transform.SetParent(mapRoot, false);
        m.gameObject.layer = LayerMask.NameToLayer(Layers.Minimap);
        m.target = target;
        m.shape = shape;
        m.size = size;
        m.height = height;
        m.material = material;
        m.turnWithTarget = turn;
        m.hideWithTarget = hideWithTarget;
        return m;
    }

    // ================================================================ HUD
    static HudController BuildHud(out RawImage mapImage, out StoryScreen story, Transform viewer)
    {
        var canvas = NewCanvas("HUD");
        var hud = canvas.gameObject.AddComponent<HudController>();
        Vector2 tl = new Vector2(0, 1), tr = new Vector2(1, 1), bl = new Vector2(0, 0), mid = new Vector2(0.5f, 0.5f),
                bottom = new Vector2(0.5f, 0), top = new Vector2(0.5f, 1);

        // ---- everything shown during play sits in one group, so it can be hidden for story cards and the intro
        var play = UiObject("Gameplay", canvas.transform);
        Stretch(play.transform);
        hud.gameplayGroup = play.AddComponent<CanvasGroup>();
        hud.gameplayGroup.blocksRaycasts = false;
        var root = play.transform;
        // these two lists are what changes colour between human eyes (amber and cream) and the visor (blue)
        var accents = new List<Graphic>();
        var texts = new List<Graphic>();
        // and these are what changes shape: through the visor every plate turns navy and takes a thin blue frame,
        // like the telemetry panel, and the warm look's own trim steps aside
        var plates = new List<Image>();
        var visorOnly = new List<GameObject>();
        var humanOnly = new List<GameObject>();

        hud.visor = BuildVisor(root);       // first, so the tint and scanlines lie under every other readout
        var crosshair = Img("Crosshair", root, new Color32(244, 236, 214, 150));
        Place(crosshair, mid, mid, mid, Vector2.zero, new Vector2(5, 5));
        texts.Add(crosshair);

        // waypoint, top centre: a 3D arrow, filmed in its own studio, that turns in space toward the current mission target
        var objective = UiObject("Objective", root);
        Place(objective.transform, top, top, top, new Vector2(0, -8), new Vector2(260, 160));
        var arrowComp = objective.AddComponent<ObjectiveArrow>();
        arrowComp.group = objective.AddComponent<CanvasGroup>();
        arrowComp.viewer = viewer;
        arrowComp.display = UiObject("Arrow3D", objective.transform).AddComponent<RawImage>();
        arrowComp.display.raycastTarget = false;
        Place(arrowComp.display, top, top, top, Vector2.zero, new Vector2(138, 138));
        BuildWaypointStudio(arrowComp);
        arrowComp.label = Label("Label", objective.transform, "", 16, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Place(arrowComp.label, bottom, bottom, bottom, new Vector2(0, 0), new Vector2(260, 24));
        hud.objective = arrowComp;
        texts.Add(arrowComp.label);

        // instruction box, top left
        var comms = Img("Comms", root, Panel);
        Place(comms, tl, tl, tl, new Vector2(36, -36), new Vector2(600, 150));
        hud.commsGroup = comms.gameObject.AddComponent<CanvasGroup>();
        hud.commsGroup.blocksRaycasts = false;
        hud.commsAccent = Img("Accent", comms.transform, Amber);
        Place(hud.commsAccent, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 0));
        hud.commsHeader = Label("Header", comms.transform, "SHIFT 017", 18, TextAnchor.UpperLeft, Amber, FontStyle.Bold);
        Place(hud.commsHeader, tl, tl, tl, new Vector2(28, -16), new Vector2(550, 26));
        hud.commsBody = Label("Body", comms.transform, "", 23, TextAnchor.UpperLeft, Cream);
        Place(hud.commsBody, tl, tl, tl, new Vector2(28, -50), new Vector2(550, 90));
        hud.commsBody.lineSpacing = 1.1f;
        accents.Add(hud.commsAccent);
        accents.Add(hud.commsHeader);
        texts.Add(hud.commsBody);
        plates.Add(comms);
        humanOnly.Add(hud.commsAccent.gameObject);
        var commsFrame = VisorFrame(comms.transform, visorOnly);
        Place(Img("Rule", commsFrame.transform, new Color32(70, 180, 255, 110)), tl, tl, tl, new Vector2(28, -43), new Vector2(544, 1));

        // minimap, top right: a compact widget holding only the walled map
        var mapEdge = new Color32(96, 170, 190, 255);
        var frame = Img("Minimap", root, Panel);
        Place(frame, tr, tr, tr, new Vector2(-30, -30), new Vector2(216, 208));
        var mapTrim = UiObject("HumanFrame", frame.transform);
        Stretch(mapTrim.transform);
        Border(mapTrim.transform, mapEdge, 2f);
        humanOnly.Add(mapTrim);
        plates.Add(frame);
        VisorFrame(frame.transform, visorOnly);
        hudMinimap = frame.gameObject;
        mapImage = UiObject("Map", frame.transform).AddComponent<RawImage>();
        mapImage.raycastTarget = false;
        Place(mapImage, mid, mid, mid, Vector2.zero, new Vector2(204, 196));

        // status, bottom left: how the robots are treating the player (through the visor this is a row of the telemetry panel)
        var watch = UiObject("Status", root);
        Place(watch.transform, bl, bl, bl, new Vector2(40, 40), new Vector2(520, 28));
        hud.watchGroup = watch.AddComponent<CanvasGroup>();
        hud.watchGroup.blocksRaycasts = false;
        hud.watchText = Label("Watch", watch.transform, "ROBOTS  NOT LOOKING", 20, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Stretch(hud.watchText);
        hudStatus = watch;
        // mask clock: how long until the mask can be worn again (worn, its battery is the telemetry panel's BATTERY row)
        var maskUi = UiObject("MaskClock", root);
        Place(maskUi.transform, bl, bl, bl, new Vector2(40, 80), new Vector2(420, 44));
        hud.maskGroup = maskUi.AddComponent<CanvasGroup>();
        hud.maskLabel = Label("Label", maskUi.transform, "MASK", 17, TextAnchor.MiddleLeft, Amber, FontStyle.Bold);
        Place(hud.maskLabel, tl, tl, tl, new Vector2(0, 0), new Vector2(520, 22));
        hud.maskFill = Bar(maskUi.transform, bl, Vector2.zero, new Vector2(420, 12), Amber);
        // the Q key, shown beside "MASK READY"
        var maskKey = KeyCap(maskUi.transform, "Q", 26f);
        Place(maskKey, tl, tl, new Vector2(0, 1), new Vector2(152, 4), maskKey.sizeDelta);
        hud.maskKey = maskKey.gameObject;

        // hold E to take the mask off: fills over two seconds
        var holdUi = Img("HoldToRemove", root, Panel);
        Place(holdUi, bottom, bottom, bottom, new Vector2(0, 264), new Vector2(460, 70));
        hud.holdGroup = holdUi.gameObject.AddComponent<CanvasGroup>();
        KeyRow(holdUi.transform, top, new Vector2(0, -6), 17, Cream, 28f, ("E", "HOLD  -  MASK OFF"));
        hud.holdFill = Bar(holdUi.transform, bottom, new Vector2(0, 12), new Vector2(420, 12), Amber);
        accents.Add(hud.holdFill);

        // the prompt: an E key cap and two or three words, just wide enough for them; it sits low, above the bottom edge
        var prompt = Img("Prompt", root, Panel);
        Place(prompt, bottom, bottom, bottom, new Vector2(0, 112), new Vector2(300, 60));
        hud.promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
        var promptLayout = prompt.gameObject.AddComponent<HorizontalLayoutGroup>();
        promptLayout.padding = new RectOffset(18, 24, 11, 13);
        promptLayout.spacing = 14f;
        promptLayout.childAlignment = TextAnchor.MiddleCenter;
        promptLayout.childControlWidth = promptLayout.childControlHeight = true;
        promptLayout.childForceExpandWidth = promptLayout.childForceExpandHeight = false;
        prompt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        KeyCap(prompt.transform, "E", 36f);
        hud.promptText = Label("Text", prompt.transform, "", 24, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        hud.promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var underline = Img("Underline", prompt.transform, Amber);
        underline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Place(underline, new Vector2(0, 0), new Vector2(1, 0), bottom, Vector2.zero, new Vector2(0, 3));
        accents.Add(underline);
        texts.Add(hud.promptText);
        plates.Add(prompt);
        VisorFrame(prompt.transform, visorOnly);

        // warnings: white on near-black inside a red frame, with a red "!" tab: the strongest contrast on the screen,
        // so a refusal ("Access denied: Scan face first") or a failure can never be missed or misread
        var warning = Img("Warning", root, new Color32(14, 4, 4, 246));
        Place(warning, bottom, bottom, bottom, new Vector2(0, 186), new Vector2(960, 72));
        hud.warningGroup = warning.gameObject.AddComponent<CanvasGroup>();
        Border(warning.transform, Alert, 2f);
        var warningTab = Img("Tab", warning.transform, Alert);
        Place(warningTab, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 0));
        Stretch(Label("Mark", warningTab.transform, "!", 40, TextAnchor.MiddleCenter, new Color32(14, 4, 4, 255), FontStyle.Bold));
        hud.warningText = Label("Text", warning.transform, "", 26, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        var warningRect = Stretch(hud.warningText);
        warningRect.offsetMin = new Vector2(76, 6);
        warningRect.offsetMax = new Vector2(-16, -6);
        BestFit(hud.warningText, 17, 26);                                   // a long warning shrinks a little and takes two lines
        hud.warningText.horizontalOverflow = HorizontalWrapMode.Wrap;
        hud.warningText.verticalOverflow = VerticalWrapMode.Truncate;

        hud.captureHint = Label("CaptureHint", root, "Click to look around", 22, TextAnchor.MiddleCenter, Cream);
        Place(hud.captureHint, bottom, bottom, bottom, new Vector2(0, 344), new Vector2(600, 30));
        texts.Add(hud.captureHint);
        // No standing controls list: hints appear only when they apply. The driving keys show for a few
        // seconds on getting into the car; the crouch keys sit on the stance plate, which is only up while crouched.
        var driveKeys = KeyRow(root, new Vector2(1, 0), new Vector2(-36, 34), 15, Cream, 30f,
                               ("W", null), ("S", "DRIVE"), ("A", null), ("D", "STEER"), ("SHIFT", "BOOST"), ("E", "EXIT AT THE ENTRANCE"));
        hud.driveKeys = driveKeys.AddComponent<CanvasGroup>();
        hud.driveKeys.alpha = 0f;
        BuildStancePlate(root, hud);

        // dialogue: who is speaking, and the line
        var say = Img("Dialogue", root, Panel);
        Place(say, bottom, bottom, bottom, new Vector2(0, 262), new Vector2(1040, 104));
        hud.sayGroup = say.gameObject.AddComponent<CanvasGroup>();
        var sayEdge = Img("Edge", say.transform, Amber);
        Place(sayEdge, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(5, 0));
        hud.saySpeaker = Label("Speaker", say.transform, "", 15, TextAnchor.UpperLeft, Amber, FontStyle.Bold);
        Place(hud.saySpeaker, tl, tl, tl, new Vector2(26, -12), new Vector2(980, 20));
        hud.sayLine = Label("Line", say.transform, "", 23, TextAnchor.UpperLeft, Cream);
        Place(hud.sayLine, tl, tl, tl, new Vector2(26, -36), new Vector2(990, 62));
        hud.sayLine.lineSpacing = 1.1f;
        accents.Add(hud.saySpeaker);
        texts.Add(hud.sayLine);
        plates.Add(say);
        humanOnly.Add(sayEdge.gameObject);
        VisorFrame(say.transform, visorOnly);
        hud.accentGraphics = accents.ToArray();
        hud.textGraphics = texts.ToArray();
        hud.plates = plates.ToArray();
        hud.visorOnly = visorOnly.ToArray();
        hud.humanOnly = humanOnly.ToArray();

        // glitch bars (over the gameplay view)
        var glitch = UiObject("Glitch", canvas.transform);
        Stretch(glitch.transform);
        hud.glitchRoot = (RectTransform)glitch.transform;
        for (int i = 0; i < 20; i++)
            Place(Img("Bar", glitch.transform, Color.white), mid, mid, mid, Vector2.zero, new Vector2(100, 4));

        // black bars for the intro flight
        var barTop = Img("LetterboxTop", canvas.transform, Color.black);
        hud.letterboxTop = Place(barTop, new Vector2(0, 1), new Vector2(1, 1), top, Vector2.zero, Vector2.zero);
        var barBottom = Img("LetterboxBottom", canvas.transform, Color.black);
        hud.letterboxBottom = Place(barBottom, new Vector2(0, 0), new Vector2(1, 0), bottom, Vector2.zero, Vector2.zero);
        // a caption for cinematics: one line, sitting in the lower black bar
        var caption = UiObject("Caption", canvas.transform);
        Place(caption.transform, bottom, bottom, bottom, new Vector2(0, 30), new Vector2(1700, 50));
        hud.captionGroup = caption.AddComponent<CanvasGroup>();
        hud.captionGroup.blocksRaycasts = false;
        hud.captionText = Label("Text", caption.transform, "", 28, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Stretch(hud.captionText);
        hud.captionText.horizontalOverflow = HorizontalWrapMode.Overflow;

        story = BuildStoryScreen(canvas.transform);

        // end of round
        var done = Img("Complete", canvas.transform, new Color(0, 0.03f, 0.04f, 0.9f));
        done.raycastTarget = true;
        Stretch(done);
        var doneTitle = Label("Title", done.transform, "ROUND COMPLETE", 76, TextAnchor.MiddleCenter, Amber, FontStyle.Bold);
        Place(doneTitle, mid, mid, mid, new Vector2(0, 130), new Vector2(1400, 100));
        var doneBody = Label("Body", done.transform, "You entered the robot world.", 30, TextAnchor.MiddleCenter, Cream);
        Place(doneBody, mid, mid, mid, new Vector2(0, 40), new Vector2(1200, 44));
        var doneNote = Label("Note", done.transform, "End of the prototype.", 20, TextAnchor.MiddleCenter, new Color32(244, 236, 214, 160));
        Place(doneNote, mid, mid, mid, new Vector2(0, -6), new Vector2(1200, 30));
        foreach (var (name, text, x) in new[] { ("ReplayButton", "PLAY AGAIN", -210f), ("MenuButton", "MAIN MENU", 210f) })
        {
            var b = TextButton(name, done.transform, text, 30, out var label);
            Place(b, mid, mid, mid, new Vector2(x, -110), new Vector2(360, 72));
            b.GetComponent<Image>().color = Panel;
            Border(b.transform, Amber, 2f);
            b.gameObject.AddComponent<MenuItemHighlight>().label = label;
        }
        hud.completePanel = done.gameObject;
        hud.completeTexts = new[] { doneTitle, doneBody, doneNote };

        // hearts (boss fight), top right beside the minimap: pixel hearts, red when full
        var heartsGo = UiObject("Hearts", root);
        Place(heartsGo.transform, tr, tr, tr, new Vector2(-262, -34), new Vector2(150, 40));
        hud.heartsGroup = heartsGo.AddComponent<CanvasGroup>();
        var heartSprite = HeartSprite();
        hud.hearts = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var heart = Img("Heart" + i, heartsGo.transform, Alert);
            heart.sprite = heartSprite;
            Place(heart, tr, tr, tr, new Vector2(-i * 46f, 0), new Vector2(40, 35));
            hud.hearts[2 - i] = heart;              // the last heart to go is the one nearest the minimap
        }

        // a notice of good news (an unlock), top centre under the waypoint
        var notice = Img("Notice", root, new Color32(6, 26, 40, 230));
        Place(notice, top, top, top, new Vector2(0, -226), new Vector2(820, 60));
        hud.noticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
        Border(notice.transform, new Color32(120, 255, 235, 255), 2f);
        hud.noticeText = Label("Text", notice.transform, "", 25, TextAnchor.MiddleCenter, new Color32(160, 255, 240, 255), FontStyle.Bold);
        var noticeRect = Stretch(hud.noticeText);
        noticeRect.offsetMin = new Vector2(22, 6);
        noticeRect.offsetMax = new Vector2(-22, -6);
        BestFit(hud.noticeText, 16, 25);                                    // a long notice shrinks to stay inside its frame

        // a held action (hold E): one quiet line at the bottom, like a subtitle, with a hairline of progress under it.
        // It stands where the E prompt was (which steps aside while the key is held).
        var action = Img("HeldAction", root, new Color32(8, 24, 26, 190));
        Place(action, bottom, bottom, bottom, new Vector2(0, 116), new Vector2(480, 42));
        hud.actionGroup = action.gameObject.AddComponent<CanvasGroup>();
        hud.actionText = Label("Text", action.transform, "", 19, TextAnchor.MiddleCenter, Cream);
        Place(hud.actionText, mid, mid, mid, new Vector2(0, 3), new Vector2(460, 30));
        hud.actionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hud.actionFill = Bar(action.transform, bottom, Vector2.zero, new Vector2(480, 4), Amber);

        // streaks of speed for the parkour run (they only ever show where a parkour motor is given to them)
        hudSpeed = BuildSpeedLines(root);

        // the deletion wave's distance (the escape), bottom centre
        var wave = Img("WaveMeter", root, Panel);
        Place(wave, bottom, bottom, bottom, new Vector2(0, 28), new Vector2(560, 60));
        hud.waveGroup = wave.gameObject.AddComponent<CanvasGroup>();
        hud.waveText = Label("Text", wave.transform, "DELETION WAVE", 20, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Place(hud.waveText, top, top, top, new Vector2(0, -6), new Vector2(540, 28));
        hud.waveFill = Bar(wave.transform, bottom, new Vector2(0, 10), new Vector2(520, 8), Amber);

        hudDodge = BuildDodgePrompt(root);
        hudCard = BuildTutorialCard(root);

        hudLoading = BuildLoadingScreen(canvas.transform);

        hud.fade = Img("Fade", canvas.transform, new Color(0, 0, 0, 0));
        Stretch(hud.fade);
        hudPause = BuildPauseMenu(canvas.transform, hud);      // on top of everything, including the fade
        return hud;
    }

    /// <summary>
    /// The thin blue frame a HUD plate wears through the visor (the look of the telemetry panel).
    /// It is listed in HudController.visorOnly, which shows it only while the mask is on.
    /// </summary>
    static GameObject VisorFrame(Transform plate, List<GameObject> visorOnly)
    {
        var frame = UiObject("VisorFrame", plate);
        frame.AddComponent<LayoutElement>().ignoreLayout = true;       // some plates lay their children out in a row
        Stretch(frame.transform);
        Border(frame.transform, new Color32(70, 180, 255, 170), 2f);
        visorOnly.Add(frame);
        return frame;
    }

    /// <summary>Thin bars flung outward from the middle of the screen at speed; SpeedLines moves them.</summary>
    static SpeedLines BuildSpeedLines(Transform root)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        var go = UiObject("SpeedLines", root);
        Stretch(go.transform);
        go.transform.SetSiblingIndex(1);                // just over the visor's tint, under every readout
        var speed = go.AddComponent<SpeedLines>();
        speed.group = go.AddComponent<CanvasGroup>();
        speed.group.alpha = 0f;
        speed.group.blocksRaycasts = false;
        speed.lines = new RectTransform[38];
        for (int i = 0; i < speed.lines.Length; i++)
        {
            var line = Img("Line", go.transform, i % 3 == 0 ? new Color32(150, 240, 255, 210) : new Color32(255, 255, 255, 170));
            speed.lines[i] = Place(line, mid, mid, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(120, 2));
        }
        return speed;
    }

    /// <summary>The dodge bar of the boss fight: SPACE while the marker is inside the lit window.</summary>
    static DodgePrompt BuildDodgePrompt(Transform root)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f), bottom = new Vector2(0.5f, 0f), top = new Vector2(0.5f, 1f);
        var panel = Img("DodgePrompt", root, new Color32(20, 6, 6, 225));
        Place(panel, bottom, bottom, bottom, new Vector2(0, 300), new Vector2(700, 118));
        Border(panel.transform, Alert, 2f);
        var dodge = panel.gameObject.AddComponent<DodgePrompt>();
        dodge.group = panel.gameObject.AddComponent<CanvasGroup>();
        dodge.group.alpha = 0f;
        dodge.label = Label("Label", panel.transform, "DODGE", 30, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        Place(dodge.label, top, top, top, new Vector2(96, -12), new Vector2(360, 40));
        var cap = KeyCap(panel.transform, "SPACE", 36f);
        Place(cap, top, top, top, new Vector2(-170, -14), cap.sizeDelta);
        var track = Img("Track", panel.transform, new Color32(4, 4, 8, 255));
        Place(track, bottom, bottom, bottom, new Vector2(0, 16), new Vector2(660, 30));
        Border(track.transform, new Color32(255, 255, 255, 90), 2f);
        dodge.windowImage = Img("Window", track.transform, new Color32(120, 255, 235, 140));
        dodge.window = dodge.windowImage.rectTransform;
        dodge.window.anchorMin = new Vector2(0.55f, 0f);
        dodge.window.anchorMax = new Vector2(0.88f, 1f);
        dodge.window.offsetMin = dodge.window.offsetMax = Vector2.zero;
        var marker = Img("Marker", track.transform, Color.white);
        dodge.marker = marker.rectTransform;
        dodge.marker.anchorMin = new Vector2(0f, 0f);
        dodge.marker.anchorMax = new Vector2(0f, 1f);
        dodge.marker.pivot = mid;
        dodge.marker.anchoredPosition = Vector2.zero;
        dodge.marker.sizeDelta = new Vector2(8, 14);
        return dodge;
    }

    /// <summary>
    /// The tutorial card of the escape: the move's name, its key, one line, and a small looping
    /// picture of it (a figure moved by TutorialCard over a few drawn shapes).
    /// </summary>
    static TutorialCard BuildTutorialCard(Transform root)
    {
        Vector2 tl = new Vector2(0, 1), mid = new Vector2(0.5f, 0.5f), right = new Vector2(1f, 0.5f), left = new Vector2(0f, 0.5f);
        var cyan = new Color32(120, 255, 235, 255);
        var ground = new Color32(96, 170, 190, 255);
        var panel = Img("TutorialCard", root, new Color32(6, 22, 32, 235));
        Place(panel, right, right, right, new Vector2(-36, 30), new Vector2(430, 372));
        Border(panel.transform, cyan, 2f);
        var card = panel.gameObject.AddComponent<TutorialCard>();
        card.group = panel.gameObject.AddComponent<CanvasGroup>();
        card.group.alpha = 0f;
        var kicker = Label("Kicker", panel.transform, "NEW ROBOT SKILL", 14, TextAnchor.MiddleLeft, cyan, FontStyle.Bold);
        Place(kicker, tl, tl, tl, new Vector2(24, -14), new Vector2(380, 20));
        card.title = Label("Title", panel.transform, "", 34, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(card.title, tl, tl, tl, new Vector2(24, -36), new Vector2(380, 44));

        // the picture
        var demo = Img("Demo", panel.transform, new Color32(2, 10, 16, 255));
        Place(demo, tl, tl, tl, new Vector2(24, -90), new Vector2(382, 150));
        demo.gameObject.AddComponent<RectMask2D>();
        Image Shape(string name, Transform parent, Vector2 at, Vector2 size, Color colour, float turn = 0f)
        {
            var shape = Img(name, parent, colour);
            Place(shape, mid, mid, mid, at, size);
            shape.rectTransform.localRotation = Quaternion.Euler(0, 0, turn);
            return shape;
        }
        GameObject Set(string name)
        {
            var set = UiObject(name, demo.transform);
            Stretch(set.transform);
            return set;
        }
        card.jumpSet = Set("JumpSet");
        Shape("LedgeLeft", card.jumpSet.transform, new Vector2(-120, -48), new Vector2(190, 18), ground);
        Shape("LedgeRight", card.jumpSet.transform, new Vector2(120, -48), new Vector2(190, 18), ground);
        card.slideSet = Set("SlideSet");
        Shape("Slope", card.slideSet.transform, new Vector2(0, -22), new Vector2(230, 8), ground, -20.7f);
        card.wallSet = Set("WallSet");
        Shape("Wall", card.wallSet.transform, new Vector2(-4, 2), new Vector2(118, 74), new Color32(60, 220, 255, 90));
        Shape("WallEdge", card.wallSet.transform, new Vector2(-4, -35), new Vector2(118, 3), cyan);
        Shape("LedgeLeft", card.wallSet.transform, new Vector2(-150, -48), new Vector2(150, 18), ground);
        Shape("LedgeRight", card.wallSet.transform, new Vector2(150, -70), new Vector2(150, 18), ground);
        card.grappleSet = Set("GrappleSet");
        Shape("Anchor", card.grappleSet.transform, new Vector2(0, 46), new Vector2(16, 16), cyan, 45f);
        Shape("LedgeLeft", card.grappleSet.transform, new Vector2(-150, -40), new Vector2(110, 18), ground);
        Shape("LedgeRight", card.grappleSet.transform, new Vector2(150, -40), new Vector2(110, 18), ground);
        // the rope hangs from its top end, which TutorialCard pins to the anchor
        var rope = Img("Rope", demo.transform, cyan);
        card.rope = Place(rope, mid, mid, new Vector2(0.5f, 1f), new Vector2(0, 46), new Vector2(2, 62));
        // the figure: a body and a head, in the amber of the mask
        var figure = UiObject("Figure", demo.transform);
        card.figure = Place(figure.transform, mid, mid, mid, Vector2.zero, new Vector2(14, 34));
        Shape("Body", figure.transform, new Vector2(0, -5), new Vector2(11, 24), Amber);
        Shape("Head", figure.transform, new Vector2(0, 13), new Vector2(11, 11), Cream);

        // the keys: one row per move, in a holder that dips when the key should be down
        var holder = UiObject("Keys", panel.transform);
        Place(holder.transform, tl, tl, new Vector2(0f, 0.5f), new Vector2(24, -272), new Vector2(380, 40));
        var dip = UiObject("Dip", holder.transform);           // TutorialCard moves and scales this one, about the holder
        card.key = Place(dip.transform, left, left, left, Vector2.zero, new Vector2(380, 40));
        var rows = new[]
        {
            KeyRow(dip.transform, left, Vector2.zero, 18, Cream, 38f, ("SPACE", "JUMP")),
            KeyRow(dip.transform, left, Vector2.zero, 18, Cream, 38f, ("SHIFT", "HOLD TO SLIDE")),
            KeyRow(dip.transform, left, Vector2.zero, 18, Cream, 38f, ("SPACE", "JUMP ON / KICK OFF")),
            KeyRow(dip.transform, left, Vector2.zero, 18, Cream, 38f, ("LMB", null), ("F", "HOLD TO GRAPPLE")),
        };
        card.keyRows = rows;
        card.instruction = Label("Instruction", panel.transform, "", 18, TextAnchor.UpperLeft, new Color32(200, 232, 240, 255));
        Place(card.instruction, tl, tl, tl, new Vector2(24, -304), new Vector2(384, 56));
        card.instruction.lineSpacing = 1.1f;
        return card;
    }

    /// <summary>A small pixel heart (drawn here, saved as a sprite), tinted by the HUD: red when full, grey when lost.</summary>
    static Sprite HeartSprite()
    {
        const string path = "Assets/Art/UI/Heart.png";
        string[] rows = { ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." };
        var tex = new Texture2D(7, 6, TextureFormat.RGBA32, false);
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 7; x++)
                tex.SetPixel(x, 5 - y, rows[y][x] == 'X' ? Color.white : new Color(1, 1, 1, 0));
        tex.Apply();
        System.IO.Directory.CreateDirectory("Assets/Art/UI");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>The workstation desktop's mouse pointer: a small pixel arrow, cream with a dark edge (drawn here, saved as a sprite).</summary>
    static Sprite PointerSprite()
    {
        const string path = "Assets/Art/UI/Pointer.png";
        string[] rows =
        {
            "X...........", "XX..........", "XOX.........", "XOOX........", "XOOOX.......", "XOOOOX......",
            "XOOOOOX.....", "XOOOOOOX....", "XOOOOOOOX...", "XOOOOOOOOX..", "XOOOOOXXXXX.", "XOOXOOX.....",
            "XOX.XOOX....", "XX..XOOX....", "X....XOOX...", ".....XOOX...", "......XX....",
        };
        int width = rows[0].Length, height = rows.Length;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                char c = rows[y][x];
                tex.SetPixel(x, height - 1 - y, c == 'X' ? (Color)new Color32(10, 16, 22, 255) : c == 'O' ? (Color)Cream : new Color(1, 1, 1, 0));
            }
        tex.Apply();
        System.IO.Directory.CreateDirectory("Assets/Art/UI");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>The "?" / "!" mark that floats over a security robot's head.</summary>
    static AlertIcon AlertMark(Transform robot, float height)
    {
        var go = new GameObject("AlertIcon", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(robot, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        go.transform.localScale = Vector3.one * 0.0085f;
        go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(120, 140);
        var mark = Label("Mark", go.transform, "?", 110, TextAnchor.MiddleCenter, Amber, FontStyle.Bold);
        mark.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(mark);
        var icon = go.AddComponent<AlertIcon>();
        icon.label = mark;
        icon.size = 0.0085f;
        return icon;
    }

    /// <summary>
    /// The pause menu (Esc): PAUSED and its four choices on the left, the Esc hint bottom left,
    /// nothing else. SETTINGS swaps the choices for the look-sensitivity control.
    /// The level builder fills in the player and the level's flow afterwards.
    /// </summary>
    static PauseMenu BuildPauseMenu(Transform canvas, HudController hud)
    {
        Vector2 tl = new Vector2(0, 1), tr = new Vector2(1, 1), bl = new Vector2(0, 0), left = new Vector2(0, 0.5f), mid = new Vector2(0.5f, 0.5f);
        var soft = new Color32(170, 190, 200, 255);
        var go = UiObject("PauseMenu", canvas);
        Stretch(go.transform);
        var menu = go.AddComponent<PauseMenu>();
        menu.group = go.AddComponent<CanvasGroup>();
        menu.group.alpha = 0f;
        menu.group.blocksRaycasts = false;
        menu.hud = hud;

        // one even veil over the whole frozen picture, then a soft shade that is deepest behind the text
        // at the two sides and fades away toward the middle: no panel edges anywhere
        var dim = Img("Dim", go.transform, new Color32(2, 8, 16, 165));
        dim.raycastTarget = true;
        Stretch(dim);
        var shade = UiObject("SideShade", go.transform).AddComponent<RawImage>();
        shade.texture = PauseShadeTexture();
        shade.color = new Color32(2, 8, 16, 255);
        shade.raycastTarget = false;
        Stretch(shade);

        var title = Label("Title", go.transform, "PAUSED", 132, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(title, tl, tl, tl, new Vector2(160, -200), new Vector2(760, 150));
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        Place(Img("Rule", go.transform, Amber), tl, tl, tl, new Vector2(164, -352), new Vector2(526, 5));

        // the four choices
        var main = UiObject("Main", go.transform);
        Stretch(main.transform);
        menu.mainPage = main;
        string[] names = { "RESUME", "RESTART CHECKPOINT", "SETTINGS", "MAIN MENU" };
        menu.items = new Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var item = Label("Item_" + names[i], main.transform, names[i], 40, TextAnchor.MiddleLeft, soft, FontStyle.Bold);
            Place(item, tl, tl, tl, new Vector2(200, -408 - i * 80), new Vector2(560, 64));
            item.horizontalOverflow = HorizontalWrapMode.Overflow;
            item.raycastTarget = true;                     // the mouse can point at it and click it
            var entry = item.gameObject.AddComponent<PauseMenuItem>();
            entry.menu = menu;
            entry.index = i;
            menu.items[i] = item;
        }
        // the marker beside the chosen line: a small open crosshair
        var marker = UiObject("Marker", menu.items[0].transform);
        menu.marker = Place(marker.transform, left, left, mid, new Vector2(-52, 0), new Vector2(44, 44));
        foreach (var (x, y, w, h) in new[] { (-15f, 0f, 12f, 3f), (15f, 0f, 12f, 3f), (0f, -15f, 3f, 12f), (0f, 15f, 3f, 12f) })
            Place(Img("Tick", marker.transform, Amber), mid, mid, mid, new Vector2(x, y), new Vector2(w, h));

        // settings: look sensitivity
        var settings = UiObject("Settings", go.transform);
        Stretch(settings.transform);
        menu.settingsPage = settings;
        var heading = Label("Heading", settings.transform, "SETTINGS", 40, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(heading, tl, tl, tl, new Vector2(200, -408), new Vector2(560, 64));
        var what = Label("Name", settings.transform, "LOOK SENSITIVITY", 24, TextAnchor.MiddleLeft, soft, FontStyle.Bold);
        Place(what, tl, tl, tl, new Vector2(200, -500), new Vector2(400, 34));
        menu.sensitivityValue = Label("Value", settings.transform, "5", 30, TextAnchor.MiddleRight, Amber, FontStyle.Bold);
        Place(menu.sensitivityValue, tl, tl, tl, new Vector2(560, -500), new Vector2(60, 34));
        menu.sensitivityFill = Bar(settings.transform, tl, new Vector2(200, -548), new Vector2(420, 14), Amber).rectTransform;
        foreach (var (sign, x) in new[] { ("-", 200f), ("+", 566f) })
        {
            var button = TextButton(sign == "-" ? "Less" : "More", settings.transform, sign, 34, out var label);
            Place(button, tl, tl, tl, new Vector2(x, -584), new Vector2(54, 54));
            button.GetComponent<Image>().color = Panel;
            Border(button.transform, Amber, 2f);
            button.gameObject.AddComponent<MenuItemHighlight>().label = label;
            UnityEventTools.AddPersistentListener(button.onClick, sign == "-" ? new UnityAction(menu.LessSensitive) : new UnityAction(menu.MoreSensitive));
        }
        KeyRow(settings.transform, tl, new Vector2(200, -672), 18, soft, 32f, ("A", null), ("D", "ADJUST"), ("ESC", "BACK"));
        // both pages stay in the scene; PauseMenu slides and fades between them
        foreach (var page in new[] { main, settings })
        {
            var pageGroup = page.AddComponent<CanvasGroup>();
            pageGroup.alpha = page == main ? 1f : 0f;
            pageGroup.blocksRaycasts = page == main;
        }

        KeyRow(go.transform, bl, new Vector2(72, 64), 22, Cream, 36f, ("ESC", "RESUME"));
        return menu;
    }

    /// <summary>A strip that is see-through in the middle and smoothly darker toward both ends, stretched across the pause screen.</summary>
    static Texture2D PauseShadeTexture()
    {
        const string path = "Assets/Art/UI/PauseShade.png";
        const int width = 256;
        var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        for (int x = 0; x < width; x++)
        {
            float u = x / (width - 1f);
            float left = Mathf.SmoothStep(0.62f, 0f, u / 0.55f);                 // behind PAUSED and the choices
            tex.SetPixel(x, 0, new Color(1f, 1f, 1f, left));
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
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>
    /// The little studio the waypoint arrow is filmed in: the arrow model on a pivot, far below the
    /// level, and a camera looking down on it from behind. ObjectiveArrow turns the pivot and shows
    /// the camera's picture (see-through background) in the HUD.
    /// </summary>
    static void BuildWaypointStudio(ObjectiveArrow waypoint)
    {
        var studio = new GameObject("WaypointStudio").transform;
        studio.position = new Vector3(0f, -400f, 0f);
        var pivot = new GameObject("ArrowPivot").transform;
        pivot.SetParent(studio, false);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(CityDir + "/Hud_WaypointArrow.fbx");
        var arrow = (GameObject)PrefabUtility.InstantiatePrefab(model);
        arrow.name = "WaypointArrow";
        arrow.transform.SetParent(pivot, false);
        SetLayer(pivot.gameObject, Layers.Hud3D);
        foreach (var r in arrow.GetComponentsInChildren<MeshRenderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        var camGo = new GameObject("WaypointCamera");
        camGo.transform.SetParent(studio, false);
        camGo.transform.localPosition = new Vector3(0f, 1.3f, -1.75f);
        camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, 0.05f) - camGo.transform.localPosition);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 34f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = LayerMask.GetMask(Layers.Hud3D);
        cam.allowHDR = false;                    // keeps the alpha channel, so only the arrow shows on the HUD
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;

        waypoint.arrow = pivot;
        waypoint.arrowRenderer = arrow.GetComponentInChildren<Renderer>();
        waypoint.arrowCamera = cam;
    }

    /// <summary>The travel card: objective text and a progress bar. Off until a world transition starts.</summary>
    static LoadingScreen BuildLoadingScreen(Transform canvas)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        var card = Img("Loading", canvas, new Color32(4, 14, 20, 255));
        card.raycastTarget = true;
        Stretch(card);
        var screen = card.gameObject.AddComponent<LoadingScreen>();
        screen.group = card.gameObject.AddComponent<CanvasGroup>();
        screen.group.alpha = 0f;

        var tag = Label("Tag", card.transform, "THE LAST HUMAN", 18, TextAnchor.MiddleLeft, Amber, FontStyle.Bold);
        Place(tag, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -52), new Vector2(400, 26));
        var title = Label("Title", card.transform, "CROSSING INTO THE ROBOTS' WORLD", 20, TextAnchor.MiddleCenter, new Color32(120, 255, 235, 255), FontStyle.Bold);
        Place(title, mid, mid, mid, new Vector2(0, 196), new Vector2(1240, 30));
        Place(Img("RuleTop", card.transform, new Color32(255, 168, 40, 200)), mid, mid, mid, new Vector2(0, 150), new Vector2(1240, 3));
        Place(Img("RuleBottom", card.transform, new Color32(255, 168, 40, 200)), mid, mid, mid, new Vector2(0, -150), new Vector2(1240, 3));
        screen.body = Label("Body", card.transform, "", 40, TextAnchor.MiddleCenter, Cream);
        Place(screen.body, mid, mid, mid, Vector2.zero, new Vector2(1240, 270));
        screen.body.lineSpacing = 1.25f;
        screen.barFill = Bar(card.transform, mid, new Vector2(0, -214), new Vector2(720, 8), Amber);
        screen.status = Label("Status", card.transform, "LOADING", 16, TextAnchor.MiddleCenter, new Color32(244, 236, 214, 200), FontStyle.Bold);
        Place(screen.status, mid, mid, mid, new Vector2(0, -246), new Vector2(900, 24));
        card.gameObject.SetActive(false);
        return screen;
    }

    /// <summary>
    /// The stance plate, bottom right: a pictogram and one word. Standing is quiet; crouching and
    /// lying low fill the plate with colour (done at runtime by HudController.SetStance).
    /// </summary>
    static void BuildStancePlate(Transform root, HudController hud)
    {
        Vector2 br = new Vector2(1, 0), left = new Vector2(0, 0.5f);
        var plate = Img("Stance", root, new Color(0.03f, 0.09f, 0.1f, 0.8f));
        Place(plate, br, br, br, new Vector2(-36, 40), new Vector2(350, 62)).localScale = Vector3.one * 1.2f;     // pictogram and word scale together
        // the keys that stand the player back up, on the plate itself (it is only shown while crouched)
        Place(KeyCap(plate.transform, "CTRL", 30f), left, left, left, new Vector2(236, 0), new Vector2(66, 30));
        Place(KeyCap(plate.transform, "C", 30f), left, left, left, new Vector2(310, 0), new Vector2(30, 30));
        hud.stancePanel = plate;
        var graphics = new List<Graphic>();
        var edge = Img("Edge", plate.transform, Cream);
        Place(edge, new Vector2(0, 0), new Vector2(0, 1), left, Vector2.zero, new Vector2(5, 0));
        graphics.Add(edge);
        hud.stanceText = Label("Text", plate.transform, "STANDING", 21, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(hud.stanceText, left, left, left, new Vector2(72, 0), new Vector2(162, 30));
        graphics.Add(hud.stanceText);

        // side-view pictograms made of bars: x, y (from the left-middle of the plate), width, height, turn
        hud.standIcon = StanceIcon("StandIcon", plate.transform, graphics,
            (38, 19, 11, 11, 0), (38, 2, 11, 20, 0), (34.5f, -19, 4, 20, 0), (41.5f, -19, 4, 20, 0));
        hud.crouchIcon = StanceIcon("CrouchIcon", plate.transform, graphics,
            (42, 6, 11, 11, 0), (35, -8, 9, 17, -22), (38, -16.5f, 14, 4, 0), (43, -22, 4, 13, -18));
        hud.tuckIcon = StanceIcon("TuckIcon", plate.transform, graphics,
            (40, 8, 48, 4, 0), (19, -8, 3, 28, 0), (61, -8, 3, 28, 0), (27, -17, 9, 9, 0), (45, -18, 24, 7, 0));
        hud.crouchIcon.SetActive(false);
        hud.tuckIcon.SetActive(false);
        hud.stanceGraphics = graphics.ToArray();
    }

    static GameObject StanceIcon(string name, Transform plate, List<Graphic> graphics,
                                 params (float x, float y, float w, float h, float turn)[] parts)
    {
        Vector2 left = new Vector2(0, 0.5f), mid = new Vector2(0.5f, 0.5f);
        var icon = UiObject(name, plate);
        Place(icon.transform, left, left, left, Vector2.zero, Vector2.zero);
        foreach (var p in parts)
        {
            var part = Img("Part", icon.transform, Cream);
            Place(part, mid, mid, mid, new Vector2(p.x, p.y), new Vector2(p.w, p.h)).localRotation = Quaternion.Euler(0, 0, p.turn);
            graphics.Add(part);
        }
        return icon;
    }

    /// <summary>
    /// The mask's visor, hidden until the mask goes on: a blue tint and scanlines over the whole
    /// picture, corner brackets, a reticle and one telemetry panel, bottom left (battery, disguise
    /// integrity and the robots' attention). VisorHud animates it.
    /// </summary>
    static VisorHud BuildVisor(Transform root)
    {
        Vector2 tl = new Vector2(0, 1), tr = new Vector2(1, 1), mid = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1),
                left = new Vector2(0, 0.5f);
        var blue = new Color32(70, 180, 255, 255);
        var soft = new Color32(70, 180, 255, 170);
        var plate = new Color32(6, 20, 44, 205);

        var go = UiObject("Visor", root);
        Stretch(go.transform);
        var visor = go.AddComponent<VisorHud>();
        visor.group = go.AddComponent<CanvasGroup>();
        visor.group.blocksRaycasts = false;
        visor.group.alpha = 0f;

        Stretch(Img("Tint", go.transform, new Color32(40, 120, 255, 4)));
        visor.scanlines = UiObject("Scanlines", go.transform).AddComponent<RawImage>();
        visor.scanlines.texture = ScanlineTexture();
        visor.scanlines.color = new Color32(150, 205, 255, 11);
        visor.scanlines.uvRect = new Rect(0, 0, 1, 270);        // one line every four pixels at 1080p
        visor.scanlines.raycastTarget = false;
        Stretch(visor.scanlines);

        // brackets in the four corners of the screen
        foreach (var (cx, cy) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
        {
            var corner = new Vector2(cx, cy);
            var inset = new Vector2(cx == 0f ? 14 : -14, cy == 0f ? 14 : -14);
            Place(Img("Bracket", go.transform, soft), corner, corner, corner, inset, new Vector2(84, 3));
            Place(Img("Bracket", go.transform, soft), corner, corner, corner, inset, new Vector2(3, 84));
        }
        // reticle: four small corners round the crosshair
        foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            var pivot = new Vector2(sx < 0f ? 0 : 1, sy < 0f ? 0 : 1);
            var at = new Vector2(sx * 26, sy * 26);
            Place(Img("Reticle", go.transform, soft), mid, mid, pivot, at, new Vector2(12, 2));
            Place(Img("Reticle", go.transform, soft), mid, mid, pivot, at, new Vector2(2, 12));
        }

        // shown only while an X-ray has broken the disguise; it sits clear below the waypoint's name and distance
        visor.header = Label("Header", go.transform, "SIGNATURE EXPOSED", 16, TextAnchor.MiddleCenter, new Color32(255, 84, 60, 255), FontStyle.Bold);
        Place(visor.header, top, top, top, new Vector2(0, -192), new Vector2(760, 22));

        // telemetry, bottom left: where the mask clock and the robots line stand for human eyes. Its BATTERY row is
        // the mask's battery and its last row is how the robots are treating the player.
        Vector2 bl = new Vector2(0, 0);
        var panel = Img("Telemetry", go.transform, plate);
        Place(panel, bl, bl, bl, new Vector2(36, 36), new Vector2(312, 292));
        Border(panel.transform, soft, 2f);
        var title = Label("Title", panel.transform, "SYSTEM TELEMETRY", 14, TextAnchor.MiddleLeft, blue, FontStyle.Bold);
        Place(title, tl, tl, tl, new Vector2(16, -12), new Vector2(280, 20));
        Place(Img("Rule", panel.transform, new Color32(70, 180, 255, 110)), tl, tl, tl, new Vector2(16, -38), new Vector2(280, 1));
        (Image fill, Text value) Row(string name, int index)
        {
            float y = -52f - index * 50f;
            var label = Label(name, panel.transform, name, 13, TextAnchor.MiddleLeft, new Color32(170, 214, 255, 255));
            Place(label, tl, tl, tl, new Vector2(16, y), new Vector2(210, 18));
            var value = Label(name + " Value", panel.transform, "0%", 15, TextAnchor.MiddleRight, blue, FontStyle.Bold);
            Place(value, tr, tr, tr, new Vector2(-16, y), new Vector2(80, 18));
            return (Bar(panel.transform, tl, new Vector2(16, y - 24f), new Vector2(280, 8), blue), value);
        }
        (visor.cpuFill, visor.cpuValue) = Row("CPU USAGE", 0);
        (visor.ramFill, visor.ramValue) = Row("RAM LOAD", 1);
        (visor.batteryFill, visor.batteryValue) = Row("BATTERY", 2);
        (visor.integrityFill, visor.integrityValue) = Row("DISGUISE INTEGRITY", 3);
        // the last row: the robots' attention, in words
        var robots = UiObject("RobotsRow", panel.transform);
        Stretch(robots.transform);
        Place(Img("Rule", robots.transform, new Color32(70, 180, 255, 110)), tl, tl, tl, new Vector2(16, -250), new Vector2(280, 1));
        var robotsLabel = Label("ROBOTS", robots.transform, "ROBOTS", 13, TextAnchor.MiddleLeft, new Color32(170, 214, 255, 255));
        Place(robotsLabel, tl, tl, tl, new Vector2(16, -260), new Vector2(120, 18));
        visor.robotsValue = Label("ROBOTS Value", robots.transform, "IGNORING YOU", 15, TextAnchor.MiddleRight, blue, FontStyle.Bold);
        Place(visor.robotsValue, tr, tr, tr, new Vector2(-16, -260), new Vector2(200, 18));
        visor.robotsValue.horizontalOverflow = HorizontalWrapMode.Overflow;
        visor.robotsRow = robots;
        return visor;
    }

    /// <summary>A tiny repeating texture: one faint line every four pixels.</summary>
    static Texture2D ScanlineTexture()
    {
        const string path = "Assets/Art/UI/VisorScanlines.png";
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 80), new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0) });
        tex.Apply();
        System.IO.Directory.CreateDirectory("Assets/Art/UI");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>A progress bar: dark track with a fill that grows from the left. Returns the fill.</summary>
    static Image Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var track = Img("BarTrack", parent, new Color32(8, 24, 26, 230));
        Place(track, anchor, anchor, anchor, position, size);
        var fill = Img("BarFill", track.transform, color);
        var rt = fill.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(0, 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return fill;
    }

    /// <summary>The plain story card shown between stages.</summary>
    static StoryScreen BuildStoryScreen(Transform canvas)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        var card = Img("Story", canvas, new Color32(6, 20, 22, 255));
        card.raycastTarget = true;
        Stretch(card);
        var screen = card.gameObject.AddComponent<StoryScreen>();
        screen.group = card.gameObject.AddComponent<CanvasGroup>();

        var tag = Label("Tag", card.transform, "THE LAST HUMAN", 18, TextAnchor.MiddleLeft, Amber, FontStyle.Bold);
        Place(tag, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -52), new Vector2(400, 26));
        Place(Img("RuleTop", card.transform, new Color32(255, 168, 40, 200)), mid, mid, mid, new Vector2(0, 150), new Vector2(1240, 3));
        Place(Img("RuleBottom", card.transform, new Color32(255, 168, 40, 200)), mid, mid, mid, new Vector2(0, -150), new Vector2(1240, 3));
        screen.body = Label("Body", card.transform, "", 40, TextAnchor.MiddleCenter, Cream);
        Place(screen.body, mid, mid, mid, Vector2.zero, new Vector2(1240, 270));
        screen.body.lineSpacing = 1.25f;
        screen.hint = Label("Hint", card.transform, "Press E to continue", 20, TextAnchor.MiddleCenter, new Color32(244, 236, 214, 200));
        Place(screen.hint, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 96), new Vector2(600, 28));
        return screen;
    }
}
