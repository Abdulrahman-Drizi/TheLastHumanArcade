// The Robots City scene: where the portal leads.
// Layout (top view, +Z is north / up on the minimap):
//   south-west: the dead portal at the end of Arrival Street, where the player appears
//   middle: a grid of streets between walled platforms crowded with round retro-future buildings
//   north: the Monolith Data Center behind its fortress wall, reached through one forecourt
// The player walks (masked) to a hover car on Arrival Street, drives to the forecourt, gets out and
// opens the password prompt at the main door, where the prototype ends.
// Buildings come from the city kit (Assets/Art/Models/City, built by ArtSource/City/build_city.py).
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
    const string CityDir = "Assets/Art/Models/City";

    // street centre lines; a street is a 12 m road with a 4 m pavement on each side
    const float ArrivalZ = -200f, MidZ = -130f, RingZ = -60f;
    const float WestX = -70f, AvenueX = 0f, EastX = 70f;
    const float CityWest = -124f, CityEast = 124f, CitySouth = -250f;
    const float WallZ = -46f;                         // the Monolith's outer wall, north of Ring Street
    // the facility hall behind the main door (inner faces of its walls), and the tower behind that
    const float HallX = 20f, HallZ0 = 18f, HallZ1 = 72f, HallH = 11f;
    const float TowerZ = 98f;
    static int CivilianCount;
    static Material cCivilian, cHallFloor, cPartition, cDesk, cBeam, cSearchBeam, cElevator, mHull, mPlate;
    static Material cTelegraph, cShield, cRope, cWallRun, cGrass, cPortalLive;
    static Transform hallLeafLeft, hallLeafRight, hallEntry;      // the hall's elevator doors, for the entry scene
    // where the elevator car and the two floors it opens on are built: under the ground slab, out of sight of the city
    static readonly Vector3 TowerSet = new Vector3(150f, -60f, 120f);

    static readonly Vector3 CitySpawn = new Vector3(-114f, 0.05f, ArrivalZ);
    static readonly Vector3 CarSpot = new Vector3(-82f, 0f, ArrivalZ - 4.3f);
    static readonly Vector3 EntranceSpot = new Vector3(0f, 0f, -25f);      // middle of the forecourt
    static readonly Vector3 TerminalSpot = new Vector3(2.3f, 0f, 6.5f);

    static Material cRoad, cWalk, cSlab, cPlanter, cLane, cForecourt, cData, cDeadPortal, mapRoad, mapBlock, mapMono;
    static readonly Color32 DoorCyan = new Color32(150, 240, 255, 255);

    // ================================================================ materials (shared with the city kit by name)
    static void MakeCityMaterials()
    {
        string dir = MatDir + "/City";
        Lit("City_Teal", new Color32(58, 142, 146, 255), 0.45f, 0.2f, dir);
        Lit("City_TealDark", new Color32(34, 92, 104, 255), 0.45f, 0.2f, dir);
        Lit("City_Aqua", new Color32(138, 206, 198, 255), 0.5f, 0.15f, dir);
        Lit("City_Cream", new Color32(226, 214, 186, 255), 0.4f, 0f, dir);
        Lit("City_Copper", new Color32(182, 108, 68, 255), 0.6f, 0.7f, dir);
        Lit("City_Rust", new Color32(134, 70, 52, 255), 0.3f, 0.4f, dir);
        Lit("City_Steel", new Color32(118, 136, 150, 255), 0.6f, 0.6f, dir);
        Lit("City_Dark", new Color32(26, 32, 42, 255), 0.4f, 0.2f, dir);
        Lit("City_Cloud", new Color32(240, 246, 250, 255), 0.05f, 0f, dir, new Color32(210, 225, 235, 255), 0.25f);
        Lit("City_GlowCyan", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(90, 240, 255, 255), 2.6f);
        Lit("City_GlowAmber", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(255, 170, 50, 255), 3f);
        // (named magenta by the city kit; in the game's palette it is a warm coral, in the amber and red family)
        Lit("City_GlowMagenta", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(255, 110, 70, 255), 2.6f);
        Lit("City_GlowWindow", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(214, 244, 255, 255), 1.5f);
        mHull = Lit("Mono_Hull", new Color32(20, 24, 32, 255), 0.55f, 0.5f, dir);
        mPlate = Lit("Mono_Plate", new Color32(38, 46, 58, 255), 0.6f, 0.6f, dir);
        Lit("Mono_Trim", new Color32(74, 88, 106, 255), 0.6f, 0.7f, dir);
        Lit("Mono_GlowRed", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(255, 48, 40, 255), 3.4f);
        cData = Lit("Mono_GlowData", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(60, 220, 255, 255), 3f);
        Lit("Mono_GlowDoor", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, DoorCyan, 2f);
        Lit("Car_Body", new Color32(232, 150, 60, 255), 0.7f, 0.3f, dir);
        Lit("Car_Trim", new Color32(240, 226, 196, 255), 0.5f, 0.1f, dir);
        Lit("Car_Dark", new Color32(30, 34, 42, 255), 0.5f, 0.4f, dir);
        Lit("Car_Glass", new Color32(28, 60, 90, 255), 0.95f, 0.3f, dir);
        Lit("Car_GlowLight", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(255, 240, 200, 255), 3f);
        Lit("Car_GlowTail", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(255, 50, 40, 255), 3f);
        Lit("Car_GlowHover", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(90, 240, 255, 255), 3f);
        Lit("Hud_Arrow", Amber, 0.45f, 0f, dir, Amber, 0.85f);
        // used only by pieces built here
        cRoad = Lit("City_Road", new Color32(34, 44, 56, 255), 0.55f, 0.1f, dir);
        cWalk = Lit("City_Pavement", new Color32(86, 118, 128, 255), 0.4f, 0.1f, dir);
        cSlab = Lit("City_Platform", new Color32(62, 96, 108, 255), 0.35f, 0.1f, dir);
        cPlanter = Lit("City_PlatformWall", new Color32(44, 74, 88, 255), 0.4f, 0.2f, dir);
        cLane = Lit("City_GlowLane", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(90, 240, 255, 255), 1.6f);
        cForecourt = Lit("Mono_Forecourt", new Color32(24, 30, 40, 255), 0.6f, 0.5f, dir);
        cDeadPortal = Lit("City_DeadPortal", new Color32(8, 10, 14, 255), 0.8f, 0.2f, dir, new Color32(255, 40, 30, 255), 0.02f);
        // the facility interior
        cHallFloor = Lit("Mono_HallFloor", new Color32(52, 62, 78, 255), 0.7f, 0.3f, dir);
        cPartition = Lit("Mono_Partition", new Color32(30, 52, 66, 255), 0.85f, 0.4f, dir);
        cDesk = Lit("Mono_Desk", new Color32(58, 70, 86, 255), 0.5f, 0.5f, dir);
        cElevator = Lit("Mono_GlowElevator", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, Amber, 2.4f);
        cBeam = Flat("FX_DetectorBeam", new Color32(90, 240, 255, 50));
        cSearchBeam = Flat("FX_SearchBeam", new Color32(120, 235, 255, 46));
        Flat("FX_Shockwave", Color.white);
        // the colossus, the batteries that feed it, and the escape course
        Lit("Boss_Bronze", new Color32(176, 112, 58, 255), 0.6f, 0.75f, dir);
        Lit("Boss_Iron", new Color32(74, 78, 88, 255), 0.55f, 0.7f, dir);
        Lit("Boss_Dark", new Color32(24, 24, 30, 255), 0.4f, 0.4f, dir);
        Lit("Boss_Glow", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, Amber, 2.4f);
        Lit("City_Leaf", new Color32(60, 170, 130, 255), 0.15f, 0f, dir);
        Lit("Mono_GlowBattery", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, Mint, 3f);         // live power: the palette's mint
        cTelegraph = Flat("FX_SlamZone", new Color32(255, 50, 36, 110));
        cShield = Flat("FX_Shield", new Color32(255, 70, 50, 70));
        cRope = Flat("FX_GrappleRope", new Color32(150, 255, 245, 255));
        cWallRun = Lit("Escape_WallRun", new Color32(18, 40, 52, 255), 0.6f, 0.3f, dir, new Color32(90, 240, 255, 255), 0.9f);
        cGrass = Lit("Escape_Grass", new Color32(70, 150, 110, 255), 0.1f, 0f, dir);
        cPortalLive = Lit("City_LivePortal", new Color32(10, 10, 10, 255), 0.3f, 0f, dir, new Color32(120, 255, 235, 255), 3.2f);
        // every civilian and worker robot shares this one material, so the whole crowd draws in a few batches
        cCivilian = Lit("Robot_Civilian", new Color32(176, 198, 204, 255), 0.5f, 0.35f, dir);
        cCivilian.enableInstancing = true;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art/Models/Robots" }))
        {
            var robotMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (robotMaterial == null) continue;
            robotMaterial.enableInstancing = true;
            EditorUtility.SetDirty(robotMaterial);
        }
        mapRoad = Flat("Map_Road", new Color32(14, 34, 44, 255));
        mapBlock = Flat("Map_Block", new Color32(44, 96, 108, 255));
        mapMono = Flat("Map_Monolith", new Color32(110, 190, 230, 255));
    }

    // ================================================================ the scene
    static void BuildCity()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        env = new GameObject("Environment").transform;
        mapRoot = new GameObject("MinimapDrawing").transform;
        mapScale = 3.6f;
        oreCounter = 0;
        RobotCount = 0;
        CivilianCount = 0;

        BuildCityLighting();
        BuildCityStreets();
        BuildCityBlocks();
        BuildCitySkyline();
        var entrance = BuildMonolith(out var terminalGo, out var door);
        BuildDeadPortal();

        var player = BuildPlayer(out var pivot, out var body, out var flash, out var playerCameras);
        player.transform.SetPositionAndRotation(CitySpawn, Quaternion.Euler(0, 90, 0));       // facing east, the dead portal behind
        playerCameras[0].farClipPlane = 900f;                                                 // the Monolith is seen from the far end of the city
        var view = playerCameras[0].transform;
        var hud = BuildHud(out var mapImage, out var story, view);
        story.gameObject.SetActive(false);                                                    // no story cards in this level
        // the travel card from the last world is already up when this scene starts
        hudLoading.gameObject.SetActive(true);
        hudLoading.group.alpha = 1f;
        hudLoading.body.text = LoadingScreen.Objective;
        hudLoading.barFill.rectTransform.anchorMax = Vector2.one;
        hudLoading.status.text = "LOADING ROBOTS CITY  100%";
        NewEventSystem();

        // systems
        var systems = new GameObject("GameSystems");
        var identity = player.GetComponent<PlayerIdentity>();
        var controller = player.GetComponent<FirstPersonController>();
        var detection = systems.AddComponent<DetectionSystem>();
        detection.player = player.transform;
        detection.playerHead = pivot;
        detection.identity = identity;
        // the end of the street in front of the dead portal: robots leave a human alone here, so the player can get their bearings
        detection.workZone = new Bounds(new Vector3(-116f, 1f, ArrivalZ), new Vector3(16f, 4f, 20f));
        var fx = systems.AddComponent<TransitionFx>();
        fx.volume = BuildPostProcessing("City", new Color(0.96f, 1f, 1.03f));
        fx.hud = hud;
        fx.flash = flash;
        body.fx = fx;
        hud.visor.grade = visorVolume;

        var interactor = player.GetComponent<PlayerInteractor>();
        interactor.hud = hud;
        interactor.body = body;
        var maskRules = player.AddComponent<MaskController>();
        maskRules.identity = identity;
        maskRules.body = body;
        maskRules.interactor = interactor;
        maskRules.hud = hud;
        maskRules.fx = fx;

        // crates to hide under on the way to the car: one every few steps, on alternate pavements, so that
        // wherever the mask runs out between the dead portal and the car there is cover within a short dash
        hideSpots.Clear();
        var spots = new GameObject("HideSpots").transform;
        HideBox(spots, new Vector3(-106.5f, 0, ArrivalZ - 9.1f), controller, identity, detection, hud);
        HideBox(spots, new Vector3(-99f, 0, ArrivalZ + 9.1f), controller, identity, detection, hud);
        HideBox(spots, new Vector3(-90f, 0, ArrivalZ - 9.1f), controller, identity, detection, hud);
        HideBox(spots, new Vector3(-84f, 0, ArrivalZ + 9.1f), controller, identity, detection, hud);
        HideBox(spots, new Vector3(-76.5f, 0, ArrivalZ - 9.1f), controller, identity, detection, hud);

        var car = BuildCar(CarSpot, 90f, controller, identity, body, maskRules, hud, view, entrance, terminalGo.transform);

        var hack = BuildHackTerminal(hud.transform, controller, identity, maskRules, hud);
        var terminal = terminalGo.AddComponent<PasswordTerminal>();
        terminal.range = 2.4f;
        terminal.terminal = hack;

        var interior = BuildFacility(controller, identity, detection, hud, maskRules, player.transform,
                                     out var elevatorTarget, out var elevator, out var insideSpawn);

        // the establishing flight: from the Monolith, down the avenue, past the car, into the player's eyes
        var intro = systems.AddComponent<IntroCinematic>();
        intro.flightCamera = BuildFlightCamera();
        intro.flightCamera.farClipPlane = 900f;
        intro.playerCameras = playerCameras;
        intro.playerEyes = pivot;
        intro.body = body;
        var head = CitySpawn + Vector3.up * 1.6f;
        var carLook = CarSpot + Vector3.up * 1f;
        intro.path = new[]
        {
            new Vector3(0f, 58f, -122f), new Vector3(0f, 30f, -80f), new Vector3(24f, 24f, -128f), new Vector3(-28f, 14f, -184f),
            new Vector3(-68f, 5.5f, -195.5f), new Vector3(-102f, 4.4f, -193f), new Vector3(-119.5f, 3.1f, -197.6f),
        };
        intro.focus = new[]
        {
            new Vector3(0f, 96f, 98f), new Vector3(0f, 10f, 8f), new Vector3(0f, 3f, -150f), carLook,
            carLook, head, head,
        };
        intro.seconds = 14f;
        intro.startFieldOfView = 56f;

        var flow = systems.AddComponent<CityFlow>();
        flow.hud = hud;
        flow.loading = hudLoading;
        flow.intro = intro;
        flow.capture = BuildCaptureDirector(systems, controller, playerCameras[0], hud);
        flow.player = controller;
        flow.identity = identity;
        flow.maskRules = maskRules;
        flow.detection = detection;
        flow.car = car;
        flow.terminal = terminal;
        flow.hack = hack;
        flow.door = door;
        flow.shockwaves = BuildShockwaves();
        flow.interior = interior;
        flow.entrance = entrance;
        flow.elevator = elevatorTarget;
        flow.insideSpawn = insideSpawn;
        elevator.flow = flow;
        flow.tower = BuildTower(systems, flow, hud, controller, body, maskRules, fx, playerCameras[0].transform);
        // the boss fight opens with a camera tour of its hall: it borrows the flight camera of the arrival
        flow.boss.flightCamera = intro.flightCamera;
        flow.boss.playerCameras = playerCameras;
        hudPause.player = controller;
        hudPause.flow = flow;
        BuildLightPatterns();
        var spawn = new GameObject("ArrivalSpawn").transform;
        spawn.SetPositionAndRotation(CitySpawn, Quaternion.Euler(0, 90, 0));
        flow.arrivalSpawn = spawn;

        // the end card of this level
        foreach (var t in hud.completePanel.GetComponentsInChildren<Text>(true))
        {
            if (t.name == "Title") t.text = "GATE OPEN";
            else if (t.name == "Body") t.text = "The way to the kill switch is clear.";
            else if (t.name == "Note") t.text = "End of the demo.";
        }
        foreach (var b in hud.completePanel.GetComponentsInChildren<Button>(true))
            UnityEventTools.AddPersistentListener(b.onClick, b.name == "MenuButton" ? new UnityAction(flow.LoadMenu) : new UnityAction(flow.Replay));

        BuildCityRobots();
        BuildCityCrowd();
        BuildCityMinimap(mapImage, player.transform, car.transform, entrance, elevatorTarget);

        ApplySurfaceDetail();
        EditorSceneManager.SaveScene(scene, SceneDir + "/RobotsCity.unity");
        log.Add("RobotsCity scene saved: " + env.childCount + " environment pieces, " + RobotCount + " security robots, " + CivilianCount + " civilians and workers");
        mapScale = 1f;
    }

    // ================================================================ helpers
    /// <summary>A piece from the city kit standing on 'position'. Moving pieces must not be static.</summary>
    static GameObject CityProp(string model, Vector3 position, float yaw = 0f, float scale = 1f, bool isStatic = true, Transform parent = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CityDir + "/" + model + ".fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = model;
        go.transform.SetParent(parent != null ? parent : env, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = Vector3.one * scale;
        if (isStatic && parent == null) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    /// <summary>An invisible wall or block: stops the player and the car, and blocks a robot's view.</summary>
    static void Solid(string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(env, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider>().size = size;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // (There are no kerb colliders any more: the pavements are flush with the road, so the hover car
    //  rides on and off them without a jolt. Only buildings, walls, lamp posts and robots stop it.)

    /// <summary>
    /// Unlit lettering standing in the world (signs). The text is readable looking along 'rotation's forward.
    /// </summary>
    static Text WorldText(string name, string text, Vector3 centre, Quaternion rotation, float metresPerPixel, int size, Color color,
                          Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent != null ? parent : env, false);
        go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size * text.Length, size * 1.5f);
        rt.SetPositionAndRotation(centre, rotation);
        rt.localScale = Vector3.one * metresPerPixel;
        var label = Label("Text", go.transform, text, size, TextAnchor.MiddleCenter, color, FontStyle.Bold);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(label);
        return label;
    }

    // ================================================================ light and sky
    static void BuildCityLighting()
    {
        // a clear, bright morning over the robots' world: high cool sun, deep teal-blue sky, long haze
        var sun = new GameObject("CitySun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color32(255, 244, 226, 255);
        sun.intensity = 1.5f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.Euler(46f, 142f, 0f);

        var sky = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/City_Sky.mat");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, MatDir + "/City/City_Sky.mat");
        }
        sky.shader = Shader.Find("Skybox/Procedural");
        sky.SetFloat("_SunSize", 0.05f);
        sky.SetFloat("_SunSizeConvergence", 6f);
        sky.SetFloat("_AtmosphereThickness", 0.9f);
        sky.SetColor("_SkyTint", new Color32(70, 170, 200, 255));
        sky.SetColor("_GroundColor", new Color32(120, 170, 180, 255));
        sky.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color32(150, 196, 222, 255);
        RenderSettings.ambientEquatorColor = new Color32(136, 172, 186, 255);
        RenderSettings.ambientGroundColor = new Color32(58, 80, 92, 255);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color32(150, 204, 216, 255);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 620f;
    }

    // ================================================================ streets
    static void BuildCityStreets()
    {
        // one slab is the road surface and the only ground collider; pavements and platforms sit on it
        Box("Ground", new Vector3(0f, -0.5f, -60f), new Vector3(400f, 1f, 460f), cRoad);
        MapRect("Ground", 0f, -60f, 400f, 460f, mapRoad, MapY);

        // lit lane dashes down the middle of every street
        foreach (float z in new[] { ArrivalZ, MidZ, RingZ })
            for (float x = CityWest + 6f; x < CityEast; x += 8f)
                if (Mathf.Abs(x - WestX) > 9f && Mathf.Abs(x - AvenueX) > 9f && Mathf.Abs(x - EastX) > 9f)
                    Box("LaneDash", new Vector3(x, 0.012f, z), new Vector3(3.2f, 0.024f, 0.22f), cLane, false);
        foreach (float x in new[] { WestX, AvenueX, EastX })
            for (float z = ArrivalZ + 12f; z < RingZ - 8f; z += 8f)
                if (Mathf.Abs(z - MidZ) > 9f)
                    Box("LaneDash", new Vector3(x, 0.012f, z), new Vector3(0.22f, 0.024f, 3.2f), cLane, false);
        // stop lines at the junctions
        foreach (float z in new[] { ArrivalZ, MidZ, RingZ })
            foreach (float x in new[] { WestX, AvenueX, EastX })
                foreach (float s in new[] { -1f, 1f })
                {
                    Box("StopLine", new Vector3(x + s * 9.2f, 0.012f, z), new Vector3(0.3f, 0.024f, 11f), cLane, false);
                    if (z > ArrivalZ || s > 0) Box("StopLine", new Vector3(x, 0.012f, z + s * 9.2f), new Vector3(11f, 0.024f, 0.3f), cLane, false);
                }

        // the ends of the streets are closed by giant half-sunk gears
        foreach (float z in new[] { MidZ, RingZ })
            foreach (float s in new[] { -1f, 1f })
            {
                CityProp("City_Gear", new Vector3(s * (CityEast + 2f), 0, z), 90f, 1.25f);
                Solid("StreetEnd", new Vector3(s * (CityEast + 2f), 10f, z), new Vector3(3f, 20f, 26f));
            }
        CityProp("City_Gear", new Vector3(CityEast + 2f, 0, ArrivalZ), 90f, 1.25f);
        Solid("StreetEnd", new Vector3(CityEast + 2f, 10f, ArrivalZ), new Vector3(3f, 20f, 26f));

        // the avenue is closed north of Mid Street: the drive to the Monolith has to go round by a side street
        float barZ = MidZ + 14f;
        for (int i = -2; i <= 2; i++)
            CityProp("Mono_Barrier", new Vector3(AvenueX + i * 4.1f, 0, barZ), 0f);
        Solid("Barricade", new Vector3(AvenueX, 3f, barZ), new Vector3(20.6f, 6f, 1f));
        var sign = CityProp("City_Billboard", new Vector3(AvenueX, 0, barZ + 2.4f), 180f);
        var signSolid = sign.AddComponent<BoxCollider>();
        signSolid.center = new Vector3(0, 3.8f, 0);
        signSolid.size = new Vector3(6.6f, 7.6f, 0.5f);
        WorldText("RoadClosedSign", "ROAD CLOSED", new Vector3(AvenueX, 6f, barZ + 2.2f), Quaternion.identity, 0.0075f, 96, new Color32(20, 24, 32, 255));
    }

    // ================================================================ the built-up platforms between the streets
    static void BuildCityBlocks()
    {
        var rnd = new System.Random(7);
        float[][] columns = { new[] { CityWest, WestX - 10f }, new[] { WestX + 10f, AvenueX - 10f }, new[] { AvenueX + 10f, EastX - 10f }, new[] { EastX + 10f, CityEast } };
        float[][] rows = { new[] { ArrivalZ + 10f, MidZ - 10f }, new[] { MidZ + 10f, RingZ - 10f } };
        foreach (var row in rows)
            foreach (var column in columns)
                CityBlock(column[0], column[1], row[0], row[1], rnd);
        CityBlock(CityWest - 36f, CityEast + 36f, CitySouth, ArrivalZ - 10f, rnd);       // the long row south of Arrival Street
    }

    static readonly (string model, float low, float high, float reach)[] MainBuildings =
    {
        ("City_Dome", 0.95f, 1.2f, 12.7f), ("City_TowerDiscs", 1.0f, 1.3f, 10.6f), ("City_TowerBulb", 1.3f, 1.9f, 6.1f),
        ("City_SphereTower", 1.1f, 1.5f, 9.3f), ("City_Capsule", 1.3f, 1.9f, 5.7f), ("City_Dome", 0.8f, 1.0f, 12.7f),
    };
    static readonly (string model, float low, float high)[] SmallBuildings =
    {
        ("City_Spire", 0.7f, 1.2f), ("City_Capsule", 0.55f, 0.8f), ("City_SphereTower", 0.45f, 0.65f), ("City_TowerBulb", 0.55f, 0.8f),
        ("City_Block", 0.6f, 0.8f), ("City_Spire", 0.5f, 0.8f),
    };

    /// <summary>
    /// A walled platform between streets, with its pavement round it, crowded with buildings.
    /// The whole platform is one solid block: nothing on it can be reached, which keeps collisions cheap.
    /// </summary>
    static void CityBlock(float x0, float x1, float z0, float z1, System.Random rnd)
    {
        float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, w = x1 - x0, d = z1 - z0;
        Box("Pavement", new Vector3(cx, 0.05f, cz), new Vector3(w + 8f, 0.1f, d + 8f), cWalk, false);
        Box("Platform", new Vector3(cx, 0.3f, cz), new Vector3(w, 0.6f, d), cSlab, false);
        // a low wall with a lit cap runs round the edge
        foreach (float s in new[] { -1f, 1f })
        {
            Box("PlatformWall", new Vector3(cx, 0.75f, cz + s * (d / 2f - 0.3f)), new Vector3(w, 1.5f, 0.6f), cPlanter, false);
            Box("PlatformWall", new Vector3(cx + s * (w / 2f - 0.3f), 0.75f, cz), new Vector3(0.6f, 1.5f, d), cPlanter, false);
            Box("PlatformLight", new Vector3(cx, 1.53f, cz + s * (d / 2f - 0.3f)), new Vector3(w - 1f, 0.07f, 0.2f), cLane, false);
            Box("PlatformLight", new Vector3(cx + s * (w / 2f - 0.3f), 1.53f, cz), new Vector3(0.2f, 0.07f, d - 1f), cLane, false);
        }
        Solid("BlockSolid", new Vector3(cx, 20f, cz), new Vector3(w, 40f, d));
        MapRect("Block", cx, cz, w, d, mapBlock, MapY + 0.1f);

        // lamps on the pavement at the corners
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var lamp = CityProp("City_Lamp", new Vector3(cx + sx * (w / 2f + 2.2f), 0.1f, cz + sz * (d / 2f + 2.2f)));
                var post = lamp.AddComponent<CapsuleCollider>();        // lamp posts are solid
                post.center = new Vector3(0, 2.8f, 0);
                post.height = 5.6f;
                post.radius = 0.22f;
            }

        // buildings: one big one in the middle of each plot, small ones in its corners
        int nx = Mathf.Max(1, Mathf.RoundToInt(w / 48f)), nz = Mathf.Max(1, Mathf.RoundToInt(d / 48f));
        float pw = w / nx, pd = d / nz;
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float px = x0 + (ix + 0.5f) * pw, pz = z0 + (iz + 0.5f) * pd;
                var main = MainBuildings[rnd.Next(MainBuildings.Length)];
                float fit = (Mathf.Min(pw, pd) / 2f - 5.5f) / main.reach;                  // leave the corners for the small ones
                float scale = Mathf.Min(Mathf.Lerp(main.low, main.high, (float)rnd.NextDouble()), Mathf.Max(fit, 0.7f));
                CityProp(main.model, new Vector3(px, 0.6f, pz), rnd.Next(4) * 90f + rnd.Next(-20, 20), scale);
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        if (rnd.NextDouble() < 0.2) continue;
                        var small = SmallBuildings[rnd.Next(SmallBuildings.Length)];
                        var at = new Vector3(px + sx * (pw / 2f - 6.5f), 0.6f, pz + sz * (pd / 2f - 6.5f));
                        CityProp(small.model, at, rnd.Next(4) * 90f, Mathf.Lerp(small.low, small.high, (float)rnd.NextDouble()));
                    }
            }
    }

    // ================================================================ the wider city, the sky and its traffic
    static void BuildCitySkyline()
    {
        var rnd = new System.Random(19);
        // towers beyond the streets: the city goes on to the haze in every direction
        string[] far = { "City_TowerBulb", "City_TowerDiscs", "City_SphereTower", "City_Capsule", "City_Dome", "City_Spire" };
        var centre = new Vector3(0f, 0f, -110f);
        for (int i = 0; i < 70; i++)
        {
            float angle = (float)rnd.NextDouble() * Mathf.PI * 2f, distance = 210f + (float)rnd.NextDouble() * 280f;
            var p = centre + new Vector3(Mathf.Sin(angle) * distance, 0f, Mathf.Cos(angle) * distance);
            if (p.z > WallZ - 20f && Mathf.Abs(p.x) < 150f) continue;                    // keep the Monolith's surroundings its own
            CityProp(far[rnd.Next(far.Length)], p, rnd.Next(360), 1.6f + (float)rnd.NextDouble() * 2.4f);
        }
        // airships drifting round the city, flying cars on high lanes, and clouds
        var sky = new GameObject("Sky").transform;
        var ships = new (float radius, float height, float speed, float start)[] { (150f, 74f, 2.2f, 20f), (205f, 104f, -1.6f, 150f), (255f, 132f, 1.2f, 260f) };
        foreach (var s in ships)
        {
            var ship = CityProp("City_Airship", Vector3.zero, 0f, 2.4f, false, sky);
            var orbit = ship.AddComponent<Orbiter>();
            orbit.centre = new Vector3(0f, s.height, -100f);
            orbit.radius = s.radius;
            orbit.degreesPerSecond = s.speed;
            orbit.startAngle = s.start;
        }
        for (int i = 0; i < 10; i++)
        {
            var flyer = CityProp("City_Car", Vector3.zero, 0f, 1.5f, false, sky);
            flyer.name = "SkyTraffic";
            var orbit = flyer.AddComponent<Orbiter>();
            orbit.centre = new Vector3((float)rnd.NextDouble() * 60f - 30f, 34f + i * 4f, -120f + (float)rnd.NextDouble() * 60f);
            orbit.radius = 60f + i * 11f;
            orbit.degreesPerSecond = (i % 2 == 0 ? 1f : -1f) * (16f - i);
            orbit.startAngle = i * 67f;
        }
        for (int i = 0; i < 14; i++)
        {
            float angle = i * 0.47f + (float)rnd.NextDouble(), distance = 160f + (float)rnd.NextDouble() * 420f;
            var p = centre + new Vector3(Mathf.Sin(angle) * distance, 170f + (float)rnd.NextDouble() * 110f, Mathf.Cos(angle) * distance);
            CityProp("City_Cloud", p, rnd.Next(360), 4f + (float)rnd.NextDouble() * 6f, true, sky);
        }
    }

    // ================================================================ the Monolith Data Center
    /// <summary>Builds the fortress and returns the middle of its forecourt (where the car may stop).</summary>
    static Transform BuildMonolith(out GameObject terminal, out SlidingDoor slidingDoor)
    {
        var root = new GameObject("MonolithDataCenter").transform;
        root.SetParent(env, false);

        // the tower: 225 m of armoured slab, plus masts, standing behind the gatehouse and the facility hall
        CityProp("Mono_Tower", new Vector3(0, 0, TowerZ), 180f, 1.5f);
        Solid("TowerSolid", new Vector3(0, 40f, TowerZ), new Vector3(68f, 80f, 50f));
        MapRect("Monolith", 0f, TowerZ, 66f, 48f, mapMono, MapY + 0.2f);
        // lesser data towers and gun bastions crowd round its base, inside the wall
        foreach (float s in new[] { -1f, 1f })
        {
            CityProp("Mono_Tower", new Vector3(s * 78f, 0, 78f), 180f, 0.5f);
            CityProp("Mono_Tower", new Vector3(s * 112f, 0, 40f), 90f, 0.34f);
            CityProp("Mono_Bastion", new Vector3(s * 46f, 0, 14f), 0f, 2.4f);
            CityProp("Mono_Bastion", new Vector3(s * 70f, 0, -8f), 0f, 1.7f);
            CityProp("Mono_Bastion", new Vector3(s * 98f, 0, -14f), 0f, 1.4f);
            MapRect("Bastion", s * 46f, 14f, 14f, 14f, mapMono, MapY + 0.2f);
        }

        // gatehouse and the glowing main door, facing south down the forecourt
        var gate = CityProp("Mono_Gatehouse", new Vector3(0, 0, 13f), 180f);
        foreach (float s in new[] { -1f, 1f })
        {
            var mass = gate.AddComponent<BoxCollider>();
            mass.center = new Vector3(s * 8.4f, 8f, 0f);
            mass.size = new Vector3(10.4f, 16f, 10f);
            var pylon = gate.AddComponent<BoxCollider>();
            pylon.center = new Vector3(s * 4.7f, 6.5f, 5.7f);
            pylon.size = new Vector3(2.4f, 13f, 2.4f);
        }
        MapRect("Gatehouse", 0f, 13f, 27f, 10f, mapMono, MapY + 0.2f);
        var door = CityProp("Mono_Door", new Vector3(0, 0, 8.7f), 180f, 1f, false);        // it moves: it rises when the password is broken
        door.name = "MainDoor";
        var doorSolid = door.AddComponent<BoxCollider>();
        doorSolid.center = new Vector3(0, 3.5f, 0);
        doorSolid.size = new Vector3(6.4f, 7f, 0.8f);
        slidingDoor = door.AddComponent<SlidingDoor>();
        PointLight("DoorGlow", new Vector3(0, 3.4f, 4.6f), DoorCyan, 5f, 13f);
        PointLight("DoorGlowHigh", new Vector3(0, 8.5f, 4f), DoorCyan, 4f, 12f);
        // its name, in lit letters standing above the gatehouse roof
        WorldText("MonolithSign", "MONOLITH DATA CENTER", new Vector3(0, 19.3f, 8f), Quaternion.identity, 0.0125f, 96, DoorCyan);

        // the keypad terminal stands just to the right of the door
        terminal = CityProp("Mono_Terminal", TerminalSpot, 180f, 1f, false);
        terminal.name = "DoorTerminal";
        var terminalSolid = terminal.AddComponent<BoxCollider>();
        terminalSolid.center = new Vector3(0, 0.85f, 0);
        terminalSolid.size = new Vector3(0.9f, 1.7f, 0.7f);
        PointLight("TerminalGlow", TerminalSpot + new Vector3(0, 1.5f, -0.7f), DoorCyan, 2.4f, 4f);

        // forecourt: dark plates with two data lines running straight to the door, walled in on both sides
        Box("Forecourt", new Vector3(0, 0.03f, -21f), new Vector3(32f, 0.06f, 58f), cForecourt, false);
        foreach (float x in new[] { -3.4f, 3.4f })
            Box("ForecourtLine", new Vector3(x, 0.07f, -21f), new Vector3(0.16f, 0.03f, 58f), cData, false);
        foreach (float s in new[] { -1f, 1f })
        {
            for (int i = 0; i < 5; i++)
                FortressWall(new Vector3(s * 17.4f, 0, -40f + i * 12f), 90f);
            CityProp("Mono_Bastion", new Vector3(s * 20.5f, 0, WallZ - 2f), 0f, 1.35f);
            Solid("BastionSolid", new Vector3(s * 20.5f, 8f, WallZ - 2f), new Vector3(8.2f, 16f, 8.2f));
            MapRect("Bastion", s * 20.5f, WallZ - 2f, 8f, 8f, mapMono, MapY + 0.2f);
            // the outer wall along Ring Street, out to the edges of the city
            for (float x = 30.5f; x < CityEast + 40f; x += 12f)
                FortressWall(new Vector3(s * x, 0, WallZ), 0f);
            Solid("WallGap", new Vector3(s * 15f, 5f, 6f), new Vector3(3.4f, 10f, 6f));       // no way round the gatehouse
            Box("Pavement", new Vector3(s * 93f, 0.05f, RingZ + 8.5f), new Vector3(150f, 0.1f, 5f), cWalk, false);   // along the foot of the wall
            PointLight("ForecourtLight", new Vector3(s * 13f, 6f, -20f), new Color32(255, 60, 50, 255), 5f, 16f);
        }
        MapRect("Forecourt", 0f, -21f, 32f, 58f, Flat("Map_Forecourt", new Color32(30, 60, 80, 255)), MapY + 0.05f);

        var entrance = new GameObject("MonolithEntrance").transform;
        entrance.SetParent(root, false);
        entrance.position = EntranceSpot;
        return entrance;
    }

    static void FortressWall(Vector3 at, float yaw)
    {
        var wall = CityProp("Mono_Wall", at, yaw);
        var solid = wall.AddComponent<BoxCollider>();
        solid.center = new Vector3(0, 4.5f, 0);
        solid.size = new Vector3(12f, 9f, 2.6f);
        bool alongX = Mathf.Abs(yaw) < 1f;
        MapRect("FortressWall", at.x, at.z, alongX ? 12f : 2.6f, alongX ? 2.6f : 12f, mapMono, MapY + 0.2f);
    }

    // ================================================================ the dead portal the player came through
    static void BuildDeadPortal()
    {
        float x = CityWest + 0.5f, z = ArrivalZ;
        var gate = Prop("GateFrame", new Vector3(x, 0, z), 90f);
        foreach (float s in new[] { -1f, 1f })
        {
            var pier = gate.AddComponent<BoxCollider>();
            pier.center = new Vector3(s * 2.75f, 3.25f, 0);
            pier.size = new Vector3(1.3f, 6.5f, 2.0f);
        }
        // the wall it stands in closes the end of the street
        Box("PortalWall", new Vector3(x - 2.6f, 9f, z), new Vector3(3f, 18f, 30f), cPlanter);
        Box("PortalWallBand", new Vector3(x - 1.05f, 7.4f, z), new Vector3(0.2f, 0.5f, 30f), cLane, false);
        MapRect("PortalWall", x - 2.6f, z, 3f, 30f, mapWall, MapY + 0.15f);
        // dark surface and dead bars; a solid pane keeps the player out
        var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        surface.name = "DeadPortalSurface";
        surface.transform.SetParent(env, false);
        surface.transform.SetPositionAndRotation(new Vector3(x - 0.9f, 2.3f, z), Quaternion.Euler(0, -90f, 0));
        surface.transform.localScale = new Vector3(4.6f, 4.6f, 1f);
        surface.GetComponent<MeshRenderer>().sharedMaterial = cDeadPortal;
        for (int i = 0; i < 7; i++)
            Box("DeadPortalBar", new Vector3(x - 0.7f, 2.3f, z - 1.8f + i * 0.6f), new Vector3(0.06f, 2.2f, 0.1f), mDark, false);
        Solid("DeadPortalPane", new Vector3(x - 0.2f, 3f, z), new Vector3(1f, 6f, 5f));
        WorldText("PortalOfflineSign", "PORTAL OFFLINE", new Vector3(x + 1.25f, 5.5f, z), Quaternion.Euler(0, -90f, 0), 0.0042f, 96, new Color32(255, 70, 50, 255));
        PointLight("DeadPortalLight", new Vector3(x + 2.2f, 4.6f, z), new Color32(255, 60, 40, 255), 0.9f, 7f);     // only a dull warning glow is left
        // lit edge of the safe ground in front of it
        Box("ArrivalLine", new Vector3(CitySpawn.x + 6f, 0.012f, z), new Vector3(0.2f, 0.024f, 20f), mWorkLine, false);
    }

    // ================================================================ the car
    static Vehicle BuildCar(Vector3 at, float yaw, FirstPersonController controller, PlayerIdentity identity, PlayerBody body,
                            MaskController maskRules, HudController hud, Transform viewCamera, Transform destination, Transform faceOnExit)
    {
        var go = new GameObject("HoverCar");
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
        var visual = CityProp("City_Car", Vector3.zero, 0f, 1.15f, false, go.transform);
        visual.transform.localPosition = new Vector3(0, 0.05f, 0);
        visual.transform.localRotation = Quaternion.identity;
        // a rounded, frictionless hull: it glances off walls, lamp posts and robots instead of catching on them
        var slick = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Settings/Prototype/CarHull.physicMaterial");
        if (slick == null)
        {
            slick = new PhysicsMaterial("CarHull");
            System.IO.Directory.CreateDirectory("Assets/Settings/Prototype");
            AssetDatabase.CreateAsset(slick, "Assets/Settings/Prototype/CarHull.physicMaterial");
        }
        slick.dynamicFriction = slick.staticFriction = 0f;
        slick.bounciness = 0f;
        slick.frictionCombine = PhysicsMaterialCombine.Minimum;
        slick.bounceCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(slick);
        var shell = go.AddComponent<CapsuleCollider>();
        shell.direction = 2;                    // lying along the car
        shell.center = new Vector3(0, 1.3f, 0);         // clear of the road surface it hovers over
        shell.radius = 1.15f;
        shell.height = 4.8f;
        shell.sharedMaterial = slick;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 900f;
        rb.useGravity = false;                  // it hovers: height and tilt are fixed, it only slides and turns
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        var seat = new GameObject("Seat").transform;
        seat.SetParent(go.transform, false);
        seat.localPosition = new Vector3(0, 0.2f, -0.1f);
        var hoverGlow = PointLight("HoverGlow", Vector3.zero, new Color32(90, 240, 255, 255), 2.4f, 5f, go.transform);
        hoverGlow.transform.localPosition = new Vector3(0, 0.3f, 0);
        SetLayer(go, Layers.Vehicle);

        var car = go.AddComponent<Vehicle>();
        car.range = 3.4f;
        car.driver = controller;
        car.identity = identity;
        car.driverBody = body;
        car.maskRules = maskRules;
        car.hud = hud;
        car.viewCamera = viewCamera;
        car.seat = seat;
        car.visual = visual.transform;
        car.destination = destination;
        car.destinationRadius = 17f;
        car.faceOnExit = faceOnExit;
        car.hoverGlow = hoverGlow;
        return car;
    }

    // ================================================================ street patrols (none north of Ring Street: the forecourt is clear)
    static void BuildCityRobots()
    {
        // In this city every security unit is the orange model: they are the only robots that watch for humans.
        var root = new GameObject("Robots").transform;
        float n = 7.2f, s = -7.2f;       // the pavement lanes either side of a street's centre line
        // Arrival Street, between the dead portal and the car
        Robot(root, "Patrol_ArrivalNorth", "Orange", 1.3f, 90f, At(-100f, ArrivalZ + n, 2f, 180f), At(-24f, ArrivalZ + n, 2.5f, 180f));
        Robot(root, "Patrol_ArrivalSouth", "Orange", 1.3f, 270f, At(-44f, ArrivalZ + s, 2f, 0f), At(-104f, ArrivalZ + s, 2f, 0f));
        // (every route keeps to the pavements: the roads belong to the car)
        Robot(root, "Guard_Car", "Orange", 1.4f, 200f, At(-72f, ArrivalZ + n, 4.5f, 200f), At(-72f, ArrivalZ + n, 3.5f, 150f));
        Robot(root, "Scout_Avenue", "Orange", 1.6f, 0f, At(AvenueX + 7.2f, ArrivalZ + 14f, 1.5f), At(AvenueX + 7.2f, MidZ - 14f, 1.5f));
        // the rest of the city
        Robot(root, "Patrol_AvenueWest", "Orange", 1.3f, 0f, At(AvenueX - 7.2f, ArrivalZ + 14f, 2f), At(AvenueX - 7.2f, MidZ - 14f, 2f));
        Robot(root, "Patrol_MidWest", "Orange", 1.3f, 90f, At(-58f, MidZ + s, 2f, 0f), At(-12f, MidZ + s, 2f, 0f));
        Robot(root, "Patrol_MidEast", "Orange", 1.3f, 90f, At(12f, MidZ + n, 2f, 180f), At(58f, MidZ + n, 2f, 180f));
        Robot(root, "Guard_BarricadeWest", "Orange", 1.4f, 180f, At(-8f, MidZ + 11f, 5f, 180f), At(-8f, MidZ + 11f, 4f, 140f));
        Robot(root, "Guard_BarricadeEast", "Orange", 1.4f, 180f, At(8f, MidZ + 11f, 4f, 220f), At(8f, MidZ + 11f, 5f, 180f));
        // police: the blue units. Two stand guard either side of the Monolith's main door, facing down the forecourt.
        // The keypad is beside and behind their line of sight; anyone walking up to the door unmasked is seen.
        Robot(root, "Police_DoorWest", "Blue", 1.3f, 180f, At(-4.7f, 5.0f, 5f, 180f), At(-4.7f, 5.0f, 4f, 205f));
        Robot(root, "Police_DoorEast", "Blue", 1.3f, 180f, At(4.7f, 5.0f, 4f, 180f), At(4.7f, 5.0f, 5f, 155f));
        Robot(root, "Patrol_EastStreet", "Orange", 1.3f, 0f, At(EastX + 7.2f, ArrivalZ + 14f, 2f), At(EastX + 7.2f, RingZ - 14f, 2f));
        Robot(root, "Scout_WestStreet", "Orange", 1.6f, 0f, At(WestX - 7.2f, MidZ - 12f, 1f), At(WestX - 7.2f, RingZ - 14f, 1f));
        Robot(root, "Patrol_Ring", "Orange", 1.3f, 90f, At(-58f, RingZ + s, 2f, 0f), At(58f, RingZ + s, 2f, 0f));
    }

    // ================================================================ the crowd: robots with somewhere to be
    /// <summary>
    /// A civilian or worker robot: the small model in the one shared pale material, a single
    /// capsule on a kinematic body (solid to the player), no sight, and an animator that sleeps
    /// while off screen. Cheap enough to fill the streets and the facility.
    /// </summary>
    static GameObject Civilian(Transform root, string name, Vector3 position, float yaw)
    {
        string variant = CivilianCount % 2 == 0 ? "Blue" : "Purple";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Robots/Robot_" + variant + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        SetLayer(go, Layers.Robot);
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            var shared = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < shared.Length; i++) shared[i] = cCivilian;
            r.sharedMaterials = shared;
        }
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0, 0.9f, 0);
        capsule.height = 1.8f;
        capsule.radius = 0.4f;
        Solidify(go);
        WireGesture(go);        // every robot can throw its arms up when the kill switch goes
        go.GetComponent<Animator>().cullingMode = AnimatorCullingMode.CullCompletely;
        CivilianCount++;
        return go;
    }

    /// <summary>A civilian walking a loop of points for ever.</summary>
    static GameObject Walker(Transform root, string name, float speed, int startAt, params Vector3[] route)
    {
        var from = route[startAt % route.Length];
        var next = route[(startAt + 1) % route.Length];
        var go = Civilian(root, name, from, Quaternion.LookRotation(next - from).eulerAngles.y);
        var walker = go.AddComponent<CrowdWalker>();
        walker.route = route;
        walker.speed = speed;
        walker.startAt = startAt + 1;
        walker.animator = go.GetComponent<Animator>();
        walker.icon = AlertMark(go.transform, 2.15f);       // the stars it sees when the car knocks it silly
        return go;
    }

    static void BuildCityCrowd()
    {
        var root = new GameObject("Crowd").transform;
        const float lane = 2.4f;           // out from a platform's edge: the pavement lane the crowd keeps to
        int n = 0;
        // four walkers circling each of the four inner platforms, two each way
        foreach (float[] xs in new[] { new[] { WestX + 10f, AvenueX - 10f }, new[] { AvenueX + 10f, EastX - 10f } })
            foreach (float[] zs in new[] { new[] { ArrivalZ + 10f, MidZ - 10f }, new[] { MidZ + 10f, RingZ - 10f } })
            {
                Vector3 a = new Vector3(xs[0] - lane, 0, zs[0] - lane), b = new Vector3(xs[1] + lane, 0, zs[0] - lane),
                        c = new Vector3(xs[1] + lane, 0, zs[1] + lane), d = new Vector3(xs[0] - lane, 0, zs[1] + lane);
                for (int i = 0; i < 4; i++)
                {
                    bool clockwise = i % 2 == 0;
                    Walker(root, "Civilian_" + n++, 1.05f + 0.12f * (i % 3), i, clockwise ? new[] { a, d, c, b } : new[] { a, b, c, d });
                }
            }
        // back and forth along the outer pavements
        float south = ArrivalZ - 7.6f, north = RingZ + 7.6f, arrivalNorth = ArrivalZ + 7.6f;
        // Arrival Street itself, where the player first sees the city: both pavements, and two crossing the road
        Walker(root, "Civilian_" + n++, 1.1f, 0, new Vector3(-106f, 0, arrivalNorth), new Vector3(-80f, 0, arrivalNorth));
        Walker(root, "Civilian_" + n++, 1.2f, 1, new Vector3(-104f, 0, arrivalNorth), new Vector3(-84f, 0, arrivalNorth));
        Walker(root, "Civilian_" + n++, 1.0f, 0, new Vector3(-82f, 0, arrivalNorth), new Vector3(-62f, 0, arrivalNorth));
        Walker(root, "Civilian_" + n++, 1.15f, 0, new Vector3(-106f, 0, south), new Vector3(-74f, 0, south));
        Walker(root, "Civilian_" + n++, 1.05f, 1, new Vector3(-100f, 0, south), new Vector3(-66f, 0, south));
        Walker(root, "Civilian_" + n++, 1.2f, 0, new Vector3(-70f, 0, south), new Vector3(-40f, 0, south));
        Walker(root, "Civilian_" + n++, 1.1f, 0, new Vector3(-58f, 0, arrivalNorth), new Vector3(-30f, 0, arrivalNorth));
        Walker(root, "Civilian_" + n++, 1.1f, 1, new Vector3(-36f, 0, south), new Vector3(-8f, 0, south));
        Walker(root, "Civilian_" + n++, 1.1f, 0, new Vector3(-70f, 0, south), new Vector3(20f, 0, south));
        Walker(root, "Civilian_" + n++, 1.2f, 1, new Vector3(-30f, 0, south), new Vector3(60f, 0, south));
        Walker(root, "Civilian_" + n++, 1.0f, 0, new Vector3(30f, 0, south), new Vector3(112f, 0, south));
        Walker(root, "Civilian_" + n++, 1.1f, 0, new Vector3(EastX + 7.6f, 0, ArrivalZ + 12f), new Vector3(EastX + 7.6f, 0, RingZ - 12f));
        Walker(root, "Civilian_" + n++, 1.2f, 1, new Vector3(EastX + 7.6f, 0, MidZ + 12f), new Vector3(EastX + 7.6f, 0, MidZ - 40f));
        Walker(root, "Civilian_" + n++, 1.1f, 0, new Vector3(WestX - 7.6f, 0, ArrivalZ + 12f), new Vector3(WestX - 7.6f, 0, MidZ - 12f));
        Walker(root, "Civilian_" + n++, 1.2f, 1, new Vector3(WestX - 7.6f, 0, RingZ - 12f), new Vector3(WestX - 7.6f, 0, MidZ + 12f));
        // along the foot of the fortress wall, turning back well short of the forecourt (which stays empty)
        Walker(root, "Civilian_" + n++, 1.1f, 0, new Vector3(-104f, 0, north), new Vector3(-30f, 0, north));
        Walker(root, "Civilian_" + n++, 1.0f, 1, new Vector3(-28f, 0, north), new Vector3(-80f, 0, north));
        Walker(root, "Civilian_" + n++, 1.1f, 0, new Vector3(104f, 0, north), new Vector3(30f, 0, north));
    }

    // ================================================================ minimap: scrolls with the player
    static void BuildCityMinimap(RawImage display, Transform player, Transform car, Transform entrance, Transform elevator)
    {
        var camGo = new GameObject("MinimapCamera");
        camGo.transform.SetPositionAndRotation(new Vector3(player.position.x, MapY + 20f, player.position.z), Quaternion.Euler(90, 0, 0));
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 52f;
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
        controller.follow = player;

        MarkerObject("Marker_Player", player, MarkerShape.Arrow, 5f, MapY + 0.9f, mapPlayer, true, false);
        MarkerObject("Marker_Car", car, MarkerShape.Diamond, 4.5f, MapY + 0.7f, mapMask, true, false);
        MarkerObject("Marker_Monolith", entrance, MarkerShape.Ring, 8f, MapY + 0.5f, mapPortal, false, false);
        MarkerObject("Marker_Elevator", elevator, MarkerShape.Ring, 5f, MapY + 0.5f, mapMask, false, false);
    }

    // ================================================================ inside the Monolith: the working floor
    /// <summary>
    /// The hall behind the main door. Three sections, south to north, divided by two partitions:
    ///   1. desks either side of a central aisle, a manager walking the cross aisle, one X-ray gate in partition A;
    ///   2. more desks, a manager on the cross corridor, carriers doing the rounds, two X-ray gates (out of step) in partition B;
    ///   3. the supervisor in front of the elevator, which is lit, signed and tall enough to see from the door.
    /// One closet to hide in per section. Returns the root of everything that only wakes when the door opens.
    /// </summary>
    static GameObject BuildFacility(FirstPersonController controller, PlayerIdentity identity, DetectionSystem detection, HudController hud,
                                    MaskController maskRules, Transform player, out Transform elevatorTarget, out Elevator elevator,
                                    out Transform insideSpawn)
    {
        float mid = (HallZ0 + HallZ1) / 2f, depth = HallZ1 - HallZ0, top = HallH + 1f;
        const float ClosetRackZ = HallZ0 + 2.2f + 2f * 3.1f;       // the third server rack along the east wall gives up its place to a closet
        var cool = new Color32(200, 230, 255, 255);

        // ---- shell: floor in tiles (each takes its own nearest lights), four walls with the doorway, roof
        for (int ix = 0; ix < 4; ix++)
            for (int iz = 0; iz < 5; iz++)
                Box("HallFloor", new Vector3(-HallX + (ix + 0.5f) * HallX / 2f, 0.04f, HallZ0 + (iz + 0.5f) * depth / 5f),
                    new Vector3(HallX / 2f, 0.08f, depth / 5f), cHallFloor, false);
        foreach (float s in new[] { -1f, 1f })
        {
            Box("HallWall", new Vector3(s * (HallX + 0.5f), top / 2f, mid), new Vector3(1f, top, depth + 2f), mHull);
            Box("HallWallSouth", new Vector3(s * (3.3f + (HallX - 2.3f) / 2f), top / 2f, HallZ0 - 0.5f), new Vector3(HallX - 2.3f, top, 1f), mHull);
            Box("HallWallBand", new Vector3(s * (HallX - 0.02f), 3.4f, mid), new Vector3(0.06f, 0.14f, depth), cData, false);
            Box("HallFrontLight", new Vector3(s * 17f, 6f, HallZ0 - 1.03f), new Vector3(5f, 0.25f, 0.1f), cLane, false);
            MapRect("HallWall", s * (HallX + 0.5f), mid, 1f, depth + 2f, mapMono, MapY + 0.2f);
            MapRect("HallWallSouth", s * (3.3f + (HallX - 2.3f) / 2f), HallZ0 - 0.5f, HallX - 2.3f, 1f, mapMono, MapY + 0.2f);
        }
        Box("HallWallNorth", new Vector3(0, top / 2f, HallZ1 + 0.5f), new Vector3(HallX * 2f + 2f, top, 1f), mHull);
        Box("HallDoorHead", new Vector3(0, 7f + (top - 7f) / 2f, HallZ0 - 0.5f), new Vector3(6.6f, top - 7f, 1f), mHull, false);
        Box("HallRoof", new Vector3(0, top + 0.25f, mid), new Vector3(HallX * 2f + 2f, 0.5f, depth + 2f), mPlate, false);
        MapRect("HallFloor", 0f, mid, HallX * 2f, depth, Flat("Map_HallFloor", new Color32(24, 52, 70, 255)), MapY + 0.06f);
        // lit strips across the ceiling and lamps under them
        for (float z = HallZ0 + 5f; z < HallZ1; z += 8.5f)
        {
            Box("CeilingStrip", new Vector3(0, HallH - 0.1f, z), new Vector3(HallX * 2f - 4f, 0.12f, 0.5f), cLane, false);
            foreach (float x in new[] { -10f, 10f })
                PointLight("HallLamp", new Vector3(x, HallH - 2.5f, z), cool, 12f, 17f);
        }
        // server racks line both side walls, and four lit pillars carry the roof
        for (float z = HallZ0 + 2.2f; z < HallZ1 - 1f; z += 3.1f)
            foreach (float s in new[] { -1f, 1f })
            {
                if (Mathf.Abs(z - 44f) < 3.2f) continue;                                  // the wall boards are here
                if (s > 0f && Mathf.Abs(z - ClosetRackZ) < 0.2f) continue;                // a hiding closet stands in this rack's place
                Box("ServerRack", new Vector3(s * (HallX - 0.5f), 1.4f, z), new Vector3(0.9f, 2.8f, 1.5f), mPlate);
                Box("ServerRackLights", new Vector3(s * (HallX - 0.97f), 1.5f, z), new Vector3(0.04f, 1.9f, 0.9f), cData, false);
                Box("ServerRackStatus", new Vector3(s * (HallX - 0.97f), 2.62f, z), new Vector3(0.04f, 0.1f, 1.1f), cElevator, false);
            }
        foreach (float px in new[] { -10f, 10f })
            foreach (float pz in new[] { 43f, 61f })
            {
                Box("HallPillar", new Vector3(px, HallH / 2f, pz), new Vector3(1.2f, HallH, 1.2f), mPlate);
                foreach (float s in new[] { -1f, 1f })
                    Box("HallPillarLight", new Vector3(px + s * 0.62f, HallH / 2f, pz), new Vector3(0.06f, HallH - 1f, 0.3f), cData, false);
                MapRect("Pillar", px, pz, 1.2f, 1.2f, mapMono, MapY + 0.2f);
            }
        // a lit line down the centre aisle from the door
        Box("AisleLine", new Vector3(0, 0.09f, (HallZ0 + 31f) / 2f), new Vector3(0.16f, 0.02f, 31f - HallZ0), cData, false);

        // ---- the elevator: tall lit portal in the north wall, signed, with a pad in front of it
        float ez = HallZ1 - 0.5f;
        foreach (float s in new[] { -1f, 1f })
        {
            Box("ElevatorPillar", new Vector3(s * 3.3f, 4.2f, ez), new Vector3(1f, 8.4f, 1.2f), mPlate);
            Box("ElevatorPillarLight", new Vector3(s * 2.76f, 4.2f, ez - 0.62f), new Vector3(0.12f, 7.6f, 0.08f), cElevator, false);
            // the two door leaves slide apart when the player arrives, so each is its own moving object
            var leaf = new GameObject(s < 0 ? "ElevatorLeafLeft" : "ElevatorLeafRight").transform;
            Box("ElevatorLeaf", new Vector3(s * 1.4f, 3.8f, ez + 0.1f), new Vector3(2.7f, 7.6f, 0.3f), mPlate, false, null, 0f, leaf);
            Box("ElevatorLeafLight", new Vector3(s * 1.4f, 3.8f, ez - 0.07f), new Vector3(0.1f, 6.6f, 0.06f), cElevator, false, null, 0f, leaf);
            if (s < 0) hallLeafLeft = leaf; else hallLeafRight = leaf;
        }
        Box("ElevatorShaftBack", new Vector3(0, 3.8f, ez + 0.36f), new Vector3(5.4f, 7.6f, 0.1f), cElevator, false);      // the lit car behind the doors
        hallEntry = new GameObject("ElevatorEntry").transform;
        hallEntry.position = new Vector3(0, 0.05f, ez - 0.95f);
        Box("ElevatorHead", new Vector3(0, 8.9f, ez), new Vector3(7.6f, 1f, 1.2f), mPlate, false);
        Box("ElevatorHeadLight", new Vector3(0, 8.36f, ez - 0.62f), new Vector3(6.4f, 0.12f, 0.08f), cElevator, false);
        Box("ElevatorPad", new Vector3(0, 0.095f, ez - 3f), new Vector3(5.6f, 0.02f, 4f), cElevator, false);
        WorldText("ElevatorSign", "ELEVATOR", new Vector3(0, 10.1f, ez - 0.7f), Quaternion.identity, 0.012f, 96, Amber);
        PointLight("ElevatorGlow", new Vector3(0, 4.5f, ez - 3.5f), Amber, 9f, 15f);
        var lift = new GameObject("Elevator");
        lift.transform.position = new Vector3(0, 0, ez - 1.9f);
        var trigger = lift.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 1.5f, 0);
        trigger.size = new Vector3(5.4f, 3f, 2.6f);
        elevator = lift.AddComponent<Elevator>();
        elevator.hud = hud;
        elevatorTarget = lift.transform;

        // ---- partitions with their X-ray gates
        const float za = 31f, zb = 56f;
        Partition(-HallX, -2.2f, za);
        Partition(2.2f, HallX, za);
        Partition(-HallX, -14.2f, zb);
        Partition(-9.8f, 9.8f, zb);
        Partition(14.2f, HallX, zb);
        Detector(new Vector3(0, 0, za), 4.4f, player, identity, detection, hud);
        Detector(new Vector3(-12f, 0, zb), 4.4f, player, identity, detection, hud);
        Detector(new Vector3(12f, 0, zb), 4.4f, player, identity, detection, hud);

        // ---- everything alive: off until the door opens
        var living = new GameObject("FacilityInterior");
        var robots = new GameObject("Workers").transform;
        robots.SetParent(living.transform, false);

        // desks with typists (screen toward the typist, who stands on the north side)
        var desks = new (float x, float z, bool staffed)[]
        {
            (-16f, 22.5f, true), (-12f, 22.5f, true), (-8f, 22.5f, false), (8f, 22.5f, true), (12f, 22.5f, false), (16f, 22.5f, true),
            (-16f, 26.5f, false), (-12f, 26.5f, true), (-8f, 26.5f, true), (8f, 26.5f, true), (12f, 26.5f, true), (16f, 26.5f, false),
            (-4.6f, 22.5f, true), (4.6f, 22.5f, true), (4.6f, 26.5f, true),       // close to the aisle: the first thing seen from the door
            (-15f, 36f, true), (-11f, 36f, true), (-7f, 36f, true), (7f, 36f, false), (11f, 36f, true), (15f, 36f, true),
            (-15f, 40f, true), (-11f, 40f, false), (-7f, 40f, true), (7f, 40f, true), (11f, 40f, true), (15f, 40f, false),
            (-13f, 50.5f, true), (-8f, 50.5f, true), (8f, 50.5f, true), (13f, 50.5f, false),
            (-9f, 64f, true), (9f, 64f, true),
        };
        int typist = 0;
        foreach (var d in desks)
        {
            Prop("Console", new Vector3(d.x, 0, d.z), 0f, null, new Vector3(1.0f, 1.3f, 0.5f));
            MapRect("Desk", d.x, d.z, 1.2f, 0.7f, mapCover, MapY + 0.1f);
            if (!d.staffed) continue;
            var worker = Civilian(robots, "Typist_" + typist++, new Vector3(d.x, 0, d.z + 0.78f), 180f);
            worker.GetComponent<RobotGesture>().gesture = Gesture.Typing;
        }
        // two at the wall boards, writing
        foreach (float s in new[] { -1f, 1f })
        {
            Box("WallBoard", new Vector3(s * (HallX - 0.08f), 2.1f, 44f), new Vector3(0.1f, 2.2f, 4.2f), mPlate, false);
            Box("WallBoardGlow", new Vector3(s * (HallX - 0.15f), 2.1f, 44f), new Vector3(0.04f, 1.9f, 3.8f), cLane, false);
            var writer = Civilian(robots, s < 0 ? "Writer_West" : "Writer_East", new Vector3(s * (HallX - 0.85f), 0, 44f - s * 0.6f), s * 90f);
            writer.GetComponent<RobotGesture>().gesture = Gesture.Writing;
        }
        // carriers doing the rounds with boxes (solid, like every robot: wait for them or go round)
        Carrier(robots, "Carrier_South", 1.15f, 0, new Vector3(-17.5f, 0, 20f), new Vector3(17.5f, 0, 20f));     // to and fro just inside the door
        Carrier(robots, "Carrier_MidA", 1.2f, 0, new Vector3(-17.5f, 0, 33.4f), new Vector3(17.5f, 0, 33.4f), new Vector3(17.5f, 0, 52.4f), new Vector3(-17.5f, 0, 52.4f));
        Carrier(robots, "Carrier_MidB", 1.1f, 2, new Vector3(-17.5f, 0, 33.4f), new Vector3(17.5f, 0, 33.4f), new Vector3(17.5f, 0, 52.4f), new Vector3(-17.5f, 0, 52.4f));
        Carrier(robots, "Carrier_North", 1.1f, 0, new Vector3(-16f, 0, 59.5f), new Vector3(-16f, 0, 68f), new Vector3(-7f, 0, 68f), new Vector3(-7f, 0, 59.5f));

        // managers: the orange units, and the only ones in here that watch
        Robot(robots, "Manager_FloorOne", "Orange", 1.2f, 90f, At(-14f, 24.6f, 3f, 0f), At(14f, 24.6f, 3f, 180f));
        Robot(robots, "Manager_FloorTwo", "Orange", 1.2f, 270f, At(15f, 45.5f, 3f, 0f), At(-15f, 45.5f, 3f, 180f));
        Robot(robots, "Supervisor_Elevator", "Orange", 1.2f, 180f, At(0f, 65.5f, 5f, 180f), At(0f, 65.5f, 3f, 130f), At(0f, 65.5f, 3f, 230f));

        // ---- the lobby in front of the elevator, and the corridors leading to it: busy with staff at their jobs
        var amberGlow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/City_GlowAmber.mat");
        GameObject Staff(string name, float x, float z, float yaw, Gesture job)
        {
            var unit = Civilian(robots, name, new Vector3(x, 0, z), yaw);
            unit.GetComponent<RobotGesture>().gesture = job;
            return unit;
        }
        // the dispatch desk: a long counter with a clerk behind it
        Box("DispatchCounter", new Vector3(6.2f, 0.52f, 59.3f), new Vector3(4.2f, 1.04f, 0.8f), cDesk);
        Box("DispatchCounterTop", new Vector3(6.2f, 1.07f, 59.3f), new Vector3(4.4f, 0.06f, 1.0f), mPlate, false);
        Box("DispatchCounterGlow", new Vector3(6.2f, 0.5f, 58.88f), new Vector3(4.0f, 0.08f, 0.04f), cElevator, false);
        Box("DispatchScreen", new Vector3(7.4f, 1.42f, 59.5f), new Vector3(0.9f, 0.6f, 0.06f), mHull, false);
        Box("DispatchScreenGlow", new Vector3(7.4f, 1.42f, 59.54f), new Vector3(0.8f, 0.5f, 0.02f), cLane, false);
        Staff("Clerk_Dispatch", 5.6f, 60.15f, 180f, Gesture.Typing);
        Staff("Clerk_Slate", 7.7f, 60.2f, 200f, Gesture.Inspecting);
        MapRect("Dispatch", 6.2f, 59.3f, 4.2f, 0.8f, mapCover, MapY + 0.1f);
        // a queue for the elevator, beside the aisle: one reads its orders, one carries a parcel, one just waits
        Staff("Queue_Reader", 3.7f, 66.9f, 0f, Gesture.Inspecting);
        var parcelBearer = Staff("Queue_Parcel", 3.75f, 65.5f, 0f, Gesture.Carrying);
        var parcel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(parcel.GetComponent<Collider>());
        parcel.name = "Parcel";
        parcel.transform.SetParent(parcelBearer.transform.Find("Hips/Chest"), false);
        parcel.transform.localPosition = new Vector3(0, 0.02f, 0.46f);
        parcel.transform.localScale = new Vector3(0.44f, 0.3f, 0.36f);
        parcel.GetComponent<MeshRenderer>().sharedMaterial = cDesk;
        parcel.layer = LayerMask.NameToLayer(Layers.Robot);
        Staff("Queue_Waiting", 3.65f, 64.1f, 350f, Gesture.None);
        // maintenance: a unit welding inside an open server rack (its torch flickers), and one on charge at another
        Staff("Welder", -18.25f, 60.5f, 270f, Gesture.Welding);
        Box("OpenRackPanel", new Vector3(-18.6f, 0.9f, 59.55f), new Vector3(0.06f, 1.5f, 0.8f), mHull, false, null, 24f);
        Box("WeldSpark", new Vector3(-18.95f, 1.25f, 60.5f), new Vector3(0.08f, 0.08f, 0.08f), amberGlow, false);
        var torch = PointLight("WeldLight", new Vector3(-18.7f, 1.3f, 60.5f), new Color32(255, 226, 170, 255), 5f, 4.5f);
        var flicker = torch.gameObject.AddComponent<Flicker>();
        flicker.low = 0.5f;
        flicker.high = 7f;
        Prop("Barrel", new Vector3(-17.2f, 0, 59.2f), 12f, null, new Vector3(0.6f, 0.92f, 0.6f));
        Staff("OnCharge", 18.3f, 63.6f, 270f, Gesture.Charging);
        Box("ChargeCable", new Vector3(18.75f, 1.2f, 63.6f), new Vector3(0.7f, 0.06f, 0.06f), amberGlow, false);
        Box("ChargePad", new Vector3(18.3f, 0.092f, 63.6f), new Vector3(1.0f, 0.02f, 1.0f), cElevator, false);
        // a cleaner working its way round the east side, broom in hand
        var cleaner = Walker(robots, "Cleaner", 0.7f, 0, new Vector3(13.6f, 0, 58.8f), new Vector3(17.2f, 0, 58.8f), new Vector3(17.2f, 0, 68.6f), new Vector3(13.6f, 0, 68.6f));
        cleaner.GetComponent<RobotGesture>().gesture = Gesture.Sweeping;
        var broom = new GameObject("Broom").transform;
        broom.SetParent(cleaner.transform.Find("Hips/Chest"), false);
        broom.localPosition = new Vector3(0.12f, -0.45f, 0.52f);
        broom.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        foreach (var (name, at, size) in new[] { ("Stick", new Vector3(0, 0, 0), new Vector3(0.05f, 1.6f, 0.05f)), ("Head", new Vector3(0, -0.8f, 0), new Vector3(0.55f, 0.1f, 0.18f)) })
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.name = name;
            piece.transform.SetParent(broom, false);
            piece.transform.localPosition = at;
            piece.transform.localScale = size;
            piece.GetComponent<MeshRenderer>().sharedMaterial = name == "Head" ? cDesk : mHull;
            piece.layer = LayerMask.NameToLayer(Layers.Robot);
        }
        // two in conversation in the north-east corner, and two more further back in the hall
        Staff("Talker_A", 8.4f, 67.8f, 70f, Gesture.Talking);
        Staff("Talker_B", 9.6f, 68.25f, 250f, Gesture.Inspecting);
        Staff("Talker_C", 5.2f, 47.2f, 300f, Gesture.Talking);
        Staff("Talker_D", 4.1f, 47.9f, 120f, Gesture.None);
        // an inspector doing the rounds of the middle section with a slate
        var inspector = Walker(robots, "Inspector", 0.9f, 0, new Vector3(-3.4f, 0, 33.2f), new Vector3(-3.4f, 0, 53.6f), new Vector3(-5.6f, 0, 53.6f), new Vector3(-5.6f, 0, 33.2f));
        inspector.GetComponent<RobotGesture>().gesture = Gesture.Inspecting;
        // a waiting corner: a bench, and a stack of freight by the north wall
        var bench = CityProp("City_Bench", new Vector3(-8.2f, 0.09f, 57.3f), 0f, 1.1f);
        var benchSolid = bench.AddComponent<BoxCollider>();
        benchSolid.center = new Vector3(0, 0.5f, -0.05f);
        benchSolid.size = new Vector3(1.8f, 1.0f, 0.6f);
        Staff("Bench_Waiting", -9.9f, 57.6f, 20f, Gesture.Inspecting);
        Prop("Pallet", new Vector3(-15.2f, 0.09f, 69.6f), 0f, null, new Vector3(1.2f, 0.14f, 1.0f));
        Prop("Crate", new Vector3(-15.2f, 0.23f, 69.6f), 8f, Vector3.one * 0.8f, Vector3.one);
        Prop("Crate", new Vector3(-13.8f, 0.09f, 69.9f), 30f, Vector3.one * 1.0f, Vector3.one);
        Prop("Crate", new Vector3(-13.9f, 1.09f, 69.95f), 52f, Vector3.one * 0.7f);
        Prop("Barrel", new Vector3(16.4f, 0.09f, 70.3f), 0f, null, new Vector3(0.6f, 0.92f, 0.6f));
        Prop("Barrel", new Vector3(17.2f, 0.09f, 70.0f), 60f, null, new Vector3(0.6f, 0.92f, 0.6f));
        // pipes along the north wall either side of the elevator, and two spotlights: on the elevator pad and on the dispatch desk
        foreach (float sx in new[] { -1f, 1f })
            for (int i = 0; i < 2; i++)
                Prop("PipeRun", new Vector3(sx * (8.5f + i * 6f), 7.2f, HallZ1), 180f);
        var padSpot = PointLight("ElevatorSpot", new Vector3(0, HallH - 0.6f, ez - 6.5f), Amber, 28f, 15f);
        padSpot.type = LightType.Spot;
        padSpot.spotAngle = 52f;
        padSpot.innerSpotAngle = 30f;
        padSpot.transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, ez - 3f) - padSpot.transform.position);
        var deskSpot = PointLight("DispatchSpot", new Vector3(6.2f, 5.6f, 60.6f), new Color32(190, 235, 255, 255), 14f, 7.5f);
        deskSpot.type = LightType.Spot;
        deskSpot.spotAngle = 60f;
        deskSpot.transform.rotation = Quaternion.LookRotation(new Vector3(6.2f, 0.9f, 59.5f) - deskSpot.transform.position);

        // closets to hide in: one backing onto a partition in each section, one in the row of server racks on the
        // east wall of the first section (a few steps from the door), and one in the lobby, against the north wall
        // beside the elevator
        var spots = new GameObject("FacilityHideSpots").transform;
        spots.SetParent(living.transform, false);
        Closet(spots, new Vector3(-6.5f, 0, za - 0.82f), 180f, controller, identity, detection, hud);
        Closet(spots, new Vector3(0f, 0, zb - 0.82f), 180f, controller, identity, detection, hud);
        Closet(spots, new Vector3(-4.5f, 0, zb + 0.82f), 0f, controller, identity, detection, hud);
        Closet(spots, new Vector3(HallX - 0.62f, 0, ClosetRackZ), -90f, controller, identity, detection, hud);
        Closet(spots, new Vector3(-5.7f, 0, HallZ1 - 0.62f), 180f, controller, identity, detection, hud);

        insideSpawn = new GameObject("InsideSpawn").transform;
        insideSpawn.SetPositionAndRotation(new Vector3(0f, 0.05f, 14.5f), Quaternion.identity);
        return living;
    }

    /// <summary>A stretch of partition across the hall at z, from x0 to x1: solid, and tall enough to block a manager's view.</summary>
    static void Partition(float x0, float x1, float z)
    {
        float cx = (x0 + x1) / 2f, w = x1 - x0;
        Box("Partition", new Vector3(cx, 1.7f, z), new Vector3(w, 3.4f, 0.4f), cPartition);
        Box("PartitionCap", new Vector3(cx, 3.44f, z), new Vector3(w, 0.08f, 0.46f), cLane, false);
        MapRect("Partition", cx, z, w, 0.8f, mapMono, MapY + 0.2f);
    }

    /// <summary>A Human Detector gate filling an opening 'width' metres wide in a partition.</summary>
    static void Detector(Vector3 at, float width, Transform player, PlayerIdentity identity, DetectionSystem detection, HudController hud)
    {
        var go = new GameObject("HumanDetector");
        go.transform.SetParent(env, false);
        go.transform.position = at;
        foreach (float s in new[] { -1f, 1f })
            Box("XrayPost", at + new Vector3(s * (width / 2f - 0.15f), 1.85f, 0), new Vector3(0.3f, 3.7f, 0.7f), mPlate, true, null, 0f, go.transform);
        Box("XrayHead", at + new Vector3(0, 3.85f, 0), new Vector3(width, 0.5f, 0.8f), mPlate, false, null, 0f, go.transform);
        var beam = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.name = "XrayBeam";
        beam.transform.SetParent(go.transform, false);
        beam.transform.localPosition = new Vector3(0, 1.8f, 0);
        beam.transform.localScale = new Vector3(width - 0.6f, 3.6f, 1f);
        var beamRenderer = beam.GetComponent<MeshRenderer>();
        beamRenderer.sharedMaterial = cBeam;
        beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
        var lamp = PointLight("XrayLamp", at + new Vector3(0, 3.3f, 0), new Color32(90, 240, 255, 255), 2.2f, 6f, go.transform);
        WorldText("DetectorSign", "HUMAN DETECTOR", at + new Vector3(0, 3.85f, -0.42f), Quaternion.identity, 0.0027f, 96, new Color32(255, 84, 60, 255));
        WorldText("DetectorSignBack", "HUMAN DETECTOR", at + new Vector3(0, 3.85f, 0.42f), Quaternion.Euler(0, 180f, 0), 0.0027f, 96, new Color32(255, 84, 60, 255));

        var detector = go.AddComponent<HumanDetector>();
        detector.beam = beamRenderer;
        detector.lamp = lamp;
        detector.zoneSize = new Vector3(width, 3f, 1.4f);
        detector.player = player;
        detector.identity = identity;
        detector.detection = detection;
        detector.hud = hud;
    }

    /// <summary>A worker carrying a box round a loop.</summary>
    static void Carrier(Transform root, string name, float speed, int startAt, params Vector3[] route)
    {
        var go = Walker(root, name, speed, startAt, route);
        go.GetComponent<RobotGesture>().gesture = Gesture.Carrying;
        var chest = go.transform.Find("Hips/Chest");
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.name = "CarriedBox";
        box.transform.SetParent(chest, false);
        box.transform.localPosition = new Vector3(0, 0.02f, 0.46f);
        box.transform.localScale = new Vector3(0.5f, 0.36f, 0.4f);
        box.GetComponent<MeshRenderer>().sharedMaterial = cDesk;
        box.layer = LayerMask.NameToLayer(Layers.Robot);
    }

    // ================================================================ the shockwave of a failed hack
    static Shockwave[] BuildShockwaves()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Minimap/FX_Shockwave.mat");
        var waves = new Shockwave[2];
        for (int i = 0; i < waves.Length; i++)
        {
            var go = new GameObject("Shockwave_" + i);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 2;
            waves[i] = go.AddComponent<Shockwave>();
            waves[i].radius = i == 0 ? 11f : 7f;
            waves[i].colour = i == 0 ? DoorCyan : new Color32(255, 84, 60, 255);
        }
        return waves;
    }

    // ================================================================ the door terminal: password slots, BRUTE FORCE, timing bar
    static HackTerminal BuildHackTerminal(Transform canvas, FirstPersonController controller, PlayerIdentity identity, MaskController maskRules,
                                          HudController hud)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);
        var red = new Color32(255, 84, 60, 255);
        var go = UiObject("HackTerminal", canvas);
        go.transform.SetSiblingIndex(hud.fade.transform.GetSiblingIndex());      // above the game HUD, under the fade
        Stretch(go.transform);
        var hack = go.AddComponent<HackTerminal>();
        hack.group = go.AddComponent<CanvasGroup>();
        hack.group.alpha = 0f;
        hack.group.blocksRaycasts = false;
        hack.player = controller;
        hack.identity = identity;
        hack.maskRules = maskRules;
        hack.hud = hud;

        Stretch(Img("Dim", go.transform, new Color32(0, 6, 12, 185)));
        var panel = Img("Panel", go.transform, new Color32(6, 18, 34, 244));
        Place(panel, mid, mid, mid, new Vector2(0, 6), new Vector2(1000, 580));
        Border(panel.transform, DoorCyan, 2f);
        var title = Label("Title", panel.transform, "MONOLITH DATA CENTER", 34, TextAnchor.MiddleCenter, DoorCyan, FontStyle.Bold);
        Place(title, mid, mid, mid, new Vector2(0, 238), new Vector2(760, 44));
        var sub = Label("Sub", panel.transform, "MAIN ENTRANCE  //  4-CHARACTER PASSWORD", 16, TextAnchor.MiddleCenter, red, FontStyle.Bold);
        Place(sub, mid, mid, mid, new Vector2(0, 202), new Vector2(760, 24));
        Place(Img("Rule", panel.transform, new Color32(150, 240, 255, 110)), mid, mid, mid, new Vector2(0, 180), new Vector2(700, 1));

        // the four characters
        hack.slots = new Text[4];
        hack.slotFrames = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var slot = Img("Slot" + i, panel.transform, new Color32(2, 8, 16, 255));
            Place(slot, mid, mid, mid, new Vector2(-189f + i * 126f, 96), new Vector2(108, 124));
            hack.slots[i] = Label("Char", slot.transform, "?", 76, TextAnchor.MiddleCenter, DoorCyan, FontStyle.Bold);
            Stretch(hack.slots[i]);
            hack.slotFrames[i] = Img("Under", slot.transform, DoorCyan);
            Place(hack.slotFrames[i], new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 6));
        }
        hack.status = Label("Status", panel.transform, "PASSWORD REQUIRED", 17, TextAnchor.MiddleCenter, DoorCyan, FontStyle.Bold);
        Place(hack.status, mid, mid, mid, new Vector2(0, 4), new Vector2(940, 26));

        // timing bar: lit zone in the middle, a marker sweeping across
        var bar = UiObject("TimingBar", panel.transform);
        Place(bar.transform, mid, mid, mid, new Vector2(0, -50), new Vector2(700, 40));
        hack.barGroup = bar.AddComponent<CanvasGroup>();
        var track = Img("Track", bar.transform, new Color32(2, 8, 16, 255));
        Stretch(track);
        Border(track.transform, new Color32(150, 240, 255, 150), 2f);
        var sweet = Img("LitZone", track.transform, new Color32(120, 255, 235, 120));
        hack.sweet = sweet.rectTransform;
        hack.sweet.anchorMin = new Vector2(0.36f, 0f);
        hack.sweet.anchorMax = new Vector2(0.64f, 1f);
        hack.sweet.offsetMin = hack.sweet.offsetMax = Vector2.zero;
        var marker = Img("Marker", track.transform, Color.white);
        hack.marker = marker.rectTransform;
        hack.marker.anchorMin = new Vector2(0f, 0f);
        hack.marker.anchorMax = new Vector2(0f, 1f);
        hack.marker.pivot = mid;
        hack.marker.anchoredPosition = Vector2.zero;
        hack.marker.sizeDelta = new Vector2(8, 14);

        // the BRUTE FORCE button (HackTerminal makes it jitter and flicker)
        var brute = Img("BruteForce", panel.transform, DoorCyan);
        brute.raycastTarget = true;
        hack.bruteRect = Place(brute, mid, mid, mid, new Vector2(0, -136), new Vector2(420, 66));
        hack.bruteImage = brute;
        var plate = Img("Plate", brute.transform, new Color32(6, 18, 34, 255));
        Stretch(plate).offsetMin = new Vector2(3, 3);
        ((RectTransform)plate.transform).offsetMax = new Vector2(-3, -3);
        hack.bruteLabel = Label("Label", brute.transform, "BRUTE FORCE", 27, TextAnchor.MiddleCenter, DoorCyan, FontStyle.Bold);
        Stretch(hack.bruteLabel);
        var button = brute.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = brute;
        UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(hack.OnBruteForce));

        hack.error = Label("Error", panel.transform, "", 15, TextAnchor.MiddleCenter, red, FontStyle.Bold);
        Place(hack.error, mid, mid, mid, new Vector2(0, -204), new Vector2(940, 44));
        hack.error.lineSpacing = 1.15f;
        KeyRow(panel.transform, new Vector2(0.5f, 0f), new Vector2(0, 14), 14, new Color32(205, 232, 255, 220), 28f,
               ("Q", "MASK"), ("B", "BRUTE FORCE"), ("SPACE", "STOP"), ("ESC", "CLOSE"));

        // code streams down both sides
        hack.streamLeft = Label("StreamLeft", panel.transform, "", 13, TextAnchor.UpperLeft, DoorCyan);
        Place(hack.streamLeft, mid, mid, mid, new Vector2(-412, 84), new Vector2(130, 170));
        hack.streamRight = Label("StreamRight", panel.transform, "", 13, TextAnchor.UpperRight, DoorCyan);
        Place(hack.streamRight, mid, mid, mid, new Vector2(412, 84), new Vector2(130, 170));
        hack.bruteLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        hack.streamLeft.horizontalOverflow = hack.streamRight.horizontalOverflow = HorizontalWrapMode.Overflow;
        return hack;
    }
}
