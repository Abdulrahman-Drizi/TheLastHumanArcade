// The escape: a race against the deletion wave, from the Monolith's roof back to the portal the
// player came in by. The scene is the Robots City itself (rebuilt here without its game systems),
// with a parkour course strung over it. From the roof garden a maintenance tunnel leads down to
// the start platform; then, over the facility, the forecourt and the fortress wall and on down the
// avenue: two jump gaps, a slide ramp under low beams, a wall run, a zig-zag of two wall runs, a
// grapple swing, two grapples in a row; a sweep round through the air over the junction; and along
// Arrival Street a last slide in two parts and a sprint down the street itself to the portal, which
// is shut until the mask powers it on at its console.
// The course is laid out along one straight line (x across, z metres along the way, as the first
// version of it was) and cut into sections. Each section is built under its own root, and the
// roots are then turned about the pivots between them, so the line folds into a zig-zag: no two
// stretches run the same way, and the quarter turn onto Arrival Street is taken in four bends.
// Behind the player the DeletionWave eats the city from the north; below, the crowd runs in panic.
using System.Collections.Generic;
using TheLastHuman;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static partial class PrototypeBuilder
{
    const float RoofY = 44.6f;              // the roof garden, level with the top of the Monolith's first tier (the 10th floor's roof)
    const float RoofEdgeZ = 46f;            // where the parkour starts: the garden is behind (north of) this, the tunnel comes down to it
    const float GardenY = RoofY + 5f;       // the roof garden itself, one tunnel's descent above the start platform
    static Transform escapeChecks, escapeAnchors;
    static EscapeFlow escapeFlow;
    static int escapeOrder, escapeAnchorCount;
    // the course's sections, in order: each one's root, the place along the straight line where it starts (its
    // pivot), and the turn taken there (degrees, to the right)
    static readonly List<Transform> escapeSections = new List<Transform>();
    static readonly List<float> escapePivots = new List<float>();
    static readonly List<float> escapeBends = new List<float>();

    static void BuildEscape()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var city = new GameObject("Environment").transform;
        env = city;
        mapRoot = new GameObject("MinimapDrawing").transform;      // the shared city builders draw the map; it is thrown away below
        mapScale = 3.6f;
        escapeOrder = 0;

        // ---- the city, as the player left it
        BuildCityLighting();
        BuildCityStreets();
        BuildCityBlocks();
        BuildCitySkyline();
        BuildMonolith(out _, out _);
        BuildDeadPortal();
        BuildLightPatterns();
        // the facility hall between the gatehouse and the tower (its inside is not needed here)
        Box("HallShell", new Vector3(0, 6.25f, (HallZ0 + HallZ1) / 2f), new Vector3(HallX * 2f + 2f, 12.5f, HallZ1 - HallZ0 + 2f), mHull, false);
        for (float z = HallZ0 + 5f; z < HallZ1; z += 8.5f)
            Box("HallRoofLight", new Vector3(0, 12.56f, z), new Vector3(HallX * 2f - 6f, 0.1f, 0.4f), cLane, false);
        // nothing down there is ever touched: no colliders, so the course is the only solid thing (apart from the ground)
        foreach (var c in city.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var d in city.GetComponentsInChildren<SlidingDoor>(true)) Object.DestroyImmediate(d);
        Object.DestroyImmediate(mapRoot.gameObject);
        mapRoot = new GameObject("MinimapDrawing").transform;
        SplitGround(city);

        // ---- the course: its first section now (the roof garden, the tunnel, the start platform); the rest once the flow exists
        escapeSections.Clear();
        escapePivots.Clear();
        escapeBends.Clear();
        escapeAnchorCount = 0;
        var first = BeginSection(-40f, 0f);
        var park = new GameObject("RoofGarden").transform;
        env = park;
        BuildEscapePark(out var hatch, out var hatchLid);
        park.SetParent(first, false);
        park.localPosition = new Vector3(0, 0, -24f);               // 20 m deep, against the tower; the maintenance tunnel takes the 14 m from its edge down to the start platform
        env = first;
        // where the player stands once out of the hatch: a step in front of it, facing the tunnel's mouth
        var start = Child(first, "RoofHatchSpawn");
        start.localPosition = new Vector3(0f, GardenY + 0.05f, -28.5f);

        var player = BuildPlayer(out var pivot, out var body, out var flash, out var playerCameras);
        playerCameras[0].farClipPlane = 900f;
        var view = playerCameras[0].transform;
        var hud = BuildHud(out _, out var story, view);
        story.gameObject.SetActive(false);
        hudMinimap.SetActive(false);                    // no map and no watchers up here
        hudStatus.SetActive(false);
        NewEventSystem();

        var systems = new GameObject("GameSystems");
        var identity = player.GetComponent<PlayerIdentity>();
        var controller = player.GetComponent<FirstPersonController>();
        var fx = systems.AddComponent<TransitionFx>();
        fx.volume = BuildPostProcessing("Escape", new Color(0.97f, 1f, 1.02f));
        fx.hud = hud;
        fx.flash = flash;
        body.fx = fx;
        hud.visor.grade = visorVolume;
        hud.visor.gradeStrength = 0.3f;                 // the mask stays on all the way down: keep the city and the course easy to read
        var interactor = player.GetComponent<PlayerInteractor>();
        interactor.hud = hud;
        interactor.body = body;

        // the robot's way of moving
        var motor = player.AddComponent<ParkourMotor>();
        motor.look = controller;
        motor.viewCamera = playerCameras[0];
        motor.wallMask = LayerMask.GetMask(Layers.WallRun);
        var hand = new GameObject("GrappleHand").transform;
        hand.SetParent(view, false);
        hand.localPosition = new Vector3(0.28f, -0.3f, 0.45f);
        motor.hand = hand;
        var ropeGo = new GameObject("GrappleRope");
        var rope = ropeGo.AddComponent<LineRenderer>();
        rope.sharedMaterial = cRope;
        rope.positionCount = 2;
        rope.useWorldSpace = true;
        rope.startWidth = 0.05f;
        rope.endWidth = 0.09f;
        rope.numCapVertices = 2;
        rope.shadowCastingMode = ShadowCastingMode.Off;
        rope.receiveShadows = false;
        motor.rope = rope;
        hudSpeed.motor = motor;                         // speed lines at a run and in a slide

        escapeFlow = systems.AddComponent<EscapeFlow>();
        escapeFlow.hud = hud;
        escapeFlow.player = controller;
        escapeFlow.motor = motor;
        escapeFlow.body = body;
        escapeFlow.identity = identity;
        escapeFlow.card = hudCard;
        escapeFlow.start = start;
        escapeFlow.hatch = hatch;
        escapeFlow.hatchLid = hatchLid;
        hudPause.player = controller;
        hudPause.flow = escapeFlow;
        // the end card doubles as the game-over card here: RETRY and MAIN MENU
        foreach (var b in hud.completePanel.GetComponentsInChildren<Button>(true))
        {
            bool menu = b.name == "MenuButton";
            UnityEventTools.AddPersistentListener(b.onClick, menu ? new UnityAction(escapeFlow.LoadMenu) : new UnityAction(escapeFlow.Retry));
            if (!menu) b.GetComponentInChildren<Text>(true).text = "RETRY";
        }

        // the rest of the course, along its straight line; then every section is turned into place about its pivot
        var portalTrigger = BuildEscapeCourse();
        FoldCourse();
        // (the portal's trigger stays exactly where the gate is, whatever the folding made of the last street)
        portalTrigger.SetPositionAndRotation(new Vector3(CityWest + 3f, 0f, ArrivalZ), escapeSections[escapeSections.Count - 1].rotation);
        escapeFlow.portal = portalTrigger;
        var allChecks = new List<Checkpoint>(Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None));
        allChecks.Sort((a, b) => a.order.CompareTo(b.order));
        escapeFlow.checkpoints = allChecks.ToArray();
        env = city;
        player.transform.SetPositionAndRotation(start.position, start.rotation);

        // ---- the portal: the dead one the city builder made, and what it turns into
        var dead = new List<GameObject>();
        foreach (Transform child in city)
            if (child.name == "DeadPortalSurface" || child.name == "DeadPortalBar" || child.name == "PortalOfflineSign" || child.name == "DeadPortalLight")
                dead.Add(child.gameObject);
        escapeFlow.deadPortal = dead.ToArray();
        escapeFlow.livePortal = new[] { BuildLivePortal() };
        escapeFlow.portalNoticeAt = 7;                  // the landing deck over the junction, where Arrival Street and the (shut) portal come into view
        // the portal's console, beside the gate on the north pavement: the mask links to it and powers the portal on
        var consoleAt = new Vector3(CityWest + 4.6f, 0f, ArrivalZ + 4.4f);
        var console = CityProp("Mono_Terminal", consoleAt, 115f, 1.25f, false, city);
        console.name = "PortalConsole";
        var consoleLink = console.AddComponent<PortalConsole>();
        consoleLink.range = 3.4f;
        consoleLink.flow = escapeFlow;
        PointLight("ConsoleGlow", consoleAt + new Vector3(0.6f, 1.8f, -0.6f), Amber, 3.5f, 5f, console.transform);
        Box("ConsolePad", consoleAt + new Vector3(0.9f, 0.03f, -0.9f), new Vector3(2.2f, 0.04f, 2.2f), cElevator, false);
        WorldText("ConsoleSign", "PORTAL POWER", consoleAt + new Vector3(0f, 2.6f, 0f), Quaternion.Euler(0, -65f, 0), 0.0036f, 96, Amber);

        // ---- the deletion wave, the words in the sky, the crowd
        var wave = systems.AddComponent<DeletionWave>();
        // (every section and its holders are listed, so the pieces inside them still go one at a time)
        var roots = new List<Transform> { city, park };
        foreach (var section in escapeSections)
        {
            roots.Add(section);
            roots.Add(section.Find("Checkpoints"));
            roots.Add(section.Find("GrappleAnchors"));
        }
        foreach (var name in new[] { "Sky", "StreetChasers", "ForecourtRunway", "TowerClimbers", "HallCeilingRipple", "HallAisleRunway" })
        {
            var go = GameObject.Find(name);
            if (go != null) roots.Add(go.transform);
        }
        wave.roots = roots.ToArray();
        var banner = BuildSkyBanner(new Vector3(0f, 150f, 60f), out var bannerTexts);
        var singles = new List<GameObject> { banner };
        foreach (var sweep in Object.FindObjectsByType<Sweep>(FindObjectsSortMode.None)) singles.Add(sweep.gameObject);
        wave.singles = singles.ToArray();
        wave.wallNorth = BuildWaveWall("DeletionWave_South", new Vector3(0f, 0f, wave.northEdge), 0f, 1000f);
        wave.wallWest = BuildWaveWall("DeletionWave_West", new Vector3(0f, 0f, (wave.turnZ + CitySouth - 40f) / 2f), 90f, wave.turnZ - (CitySouth - 40f));
        wave.wallWest.gameObject.SetActive(false);

        var dissolution = systems.AddComponent<DissolutionSequence>();
        dissolution.flightCamera = BuildFlightCamera();
        dissolution.flightCamera.farClipPlane = 900f;
        dissolution.playerCameras = playerCameras;
        dissolution.hud = hud;
        dissolution.fx = fx;
        dissolution.banner = banner;
        dissolution.bannerTexts = bannerTexts;
        dissolution.wave = wave;
        dissolution.gatherPoint = new Vector3(-6.5f, 0f, -160f);        // the avenue's west pavement: clear of the course overhead
        dissolution.crowd = BuildPanicCrowd(wave, dissolution.gatherPoint);
        // the arrival shot: from the dead portal on the ground, over the city, up to the masked figure on the Monolith's roof.
        // (A stand-in for the player stands there while the camera is outside their head, an arrow over it.)
        var figureRoot = new GameObject("RoofFigure").transform;
        var figure = Dummy(figureRoot, "PlayerStandIn", "Orange", start.position, start.eulerAngles.y);
        var figureArrow = Prop("Arrow", start.position + Vector3.up * 2.65f, 0f, Vector3.one * 1.5f, null, figureRoot);
        var arrowSpin = figureArrow.AddComponent<Hover>();
        arrowSpin.spin = 90f;
        arrowSpin.amplitude = 0.12f;
        arrowSpin.period = 1.4f;
        PointLight("FigureLight", start.position + new Vector3(0f, 2.6f, -1.2f), Amber, 5f, 6f, figureRoot);
        figureRoot.gameObject.SetActive(false);
        dissolution.roofFigure = figureRoot.gameObject;
        var gateway = new Vector3(CityWest + 0.5f, 2.4f, ArrivalZ);
        var eyes = start.position + Vector3.up * 1.7f;
        dissolution.arrivalPath = new[]
        {
            gateway + new Vector3(10f, 0f, 0f), gateway + new Vector3(30f, 6f, 2f), new Vector3(-44f, 26f, ArrivalZ + 2f), new Vector3(-6f, 46f, -150f),
            new Vector3(4f, 60f, -40f), new Vector3(2.5f, 56.5f, 56f), eyes + new Vector3(1.4f, 1.0f, -7.5f),
        };
        dissolution.arrivalFocus = new[]
        {
            gateway, gateway + new Vector3(8f, 1f, 0f), new Vector3(-10f, 22f, -150f), new Vector3(0f, 70f, 70f),
            new Vector3(0f, 54f, 74f), eyes, eyes,
        };
        // the course hangs over the avenue, between the street and the words: it is left out of the street-level shot
        // (all but the roof garden, which the player is standing on)
        var overhead = new List<GameObject>();
        foreach (Transform child in first)
            if (child != park && child != start) overhead.Add(child.gameObject);
        for (int k = 1; k < escapeSections.Count; k++) overhead.Add(escapeSections[k].gameObject);
        dissolution.hideForStreetShot = overhead.ToArray();
        banner.SetActive(false);
        escapeFlow.dissolution = dissolution;
        escapeFlow.wave = wave;
        escapeFlow.runners = dissolution.crowd;

        Object.DestroyImmediate(mapRoot.gameObject);
        mapRoot = null;
        mapScale = 1f;
        ApplySurfaceDetail();
        EditorSceneManager.SaveScene(scene, SceneDir + "/RooftopEscape.unity");
        int coursePieces = 0;
        foreach (var section in escapeSections) coursePieces += section.childCount;
        log.Add("RooftopEscape scene saved: " + city.childCount + " city pieces, " + coursePieces + " course pieces in " + escapeSections.Count
                + " sections, " + escapeOrder + " checkpoints, " + dissolution.crowd.Length + " runners");
    }

    // ================================================================ the course's sections and their folding
    /// <summary>
    /// Start a new section of the course at 'pivotZ' metres along its straight line, where the way
    /// turns by 'bend' degrees (to the right; negative is left). Everything built from now on goes
    /// under this section's root. The name carries the pivot (in centimetres) for the play-test driver.
    /// </summary>
    static Transform BeginSection(float pivotZ, float bend)
    {
        var root = new GameObject("Course_S" + escapeSections.Count + "@" + Mathf.RoundToInt(pivotZ * 100f)).transform;
        escapeSections.Add(root);
        escapePivots.Add(pivotZ);
        escapeBends.Add(bend);
        Child(root, "Checkpoints");
        Child(root, "GrappleAnchors");
        UseSection(escapeSections.Count - 1);
        return root;
    }

    /// <summary>Build under an existing section again.</summary>
    static void UseSection(int index)
    {
        env = escapeSections[index];
        escapeChecks = env.Find("Checkpoints");
        escapeAnchors = env.Find("GrappleAnchors");
    }

    /// <summary>
    /// Where a section's root goes: the first keeps the old placement (its +z is the city's south);
    /// each later one is the one before it, turned by its bend about the vertical line through its pivot.
    /// </summary>
    static void SectionFrame(int index, out Vector3 position, out Quaternion rotation)
    {
        float heading = 180f;
        rotation = Quaternion.Euler(0f, heading, 0f);
        position = new Vector3(0f, 0f, RoofEdgeZ);
        for (int k = 1; k <= index; k++)
        {
            var pivot = position + rotation * new Vector3(0f, 0f, escapePivots[k]);
            heading += escapeBends[k];
            rotation = Quaternion.Euler(0f, heading, 0f);
            position = pivot - rotation * new Vector3(0f, 0f, escapePivots[k]);
        }
    }

    /// <summary>Where the point 'z' metres along the straight line ends up in the city, as the sections begun so far fold it (z must lie in the last of them).</summary>
    static Vector3 CoursePoint(float z)
    {
        SectionFrame(escapeSections.Count - 1, out var position, out var rotation);
        return position + rotation * new Vector3(0f, 0f, z);
    }

    /// <summary>How far to the right of due south the course is heading in the last section begun (degrees).</summary>
    static float CourseHeading()
    {
        float heading = 0f;
        foreach (float bend in escapeBends) heading += bend;
        return heading;
    }

    /// <summary>Turn every section into place.</summary>
    static void FoldCourse()
    {
        float heading = 0f;
        for (int k = 0; k < escapeSections.Count; k++)
        {
            SectionFrame(k, out var position, out var rotation);
            escapeSections[k].SetPositionAndRotation(position, rotation);
            heading += escapeBends[k];
            var pivot = position + rotation * new Vector3(0f, 0f, escapePivots[k]);
            log.Add("  course section " + k + ": from " + escapePivots[k].ToString("0.00") + " m, bend " + escapeBends[k].ToString("0.0")
                    + ", heading " + heading.ToString("0.0") + ", pivot at x " + pivot.x.ToString("0.0") + " z " + pivot.z.ToString("0.0"));
        }
    }

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    /// <summary>
    /// The city's ground is one slab. Here it is cut into strips across the wave's path (east-west
    /// strips in the north, north-south strips along the last street), so the wave can take it a piece at a time.
    /// </summary>
    static void SplitGround(Transform city)
    {
        var old = city.Find("Ground");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        const float north = 170f, turn = -182f, south = -290f, strip = 16f;
        for (float z = north; z > turn + 0.1f; z -= strip)
            Box("GroundStrip", new Vector3(0f, -0.5f, z - strip / 2f), new Vector3(400f, 1f, strip), cRoad);
        for (float x = 200f; x > -200f + 0.1f; x -= strip)
            Box("GroundStrip", new Vector3(x - strip / 2f, -0.5f, (turn + south) / 2f), new Vector3(strip, 1f, turn - south), cRoad);
    }

    // ================================================================ the roof garden (built round its own middle, way out toward +z)
    /// <summary>
    /// The garden on the Monolith's roof: 34 m wide, 20 m deep, its back against the tower's upper
    /// tiers. The player arrives through the hatch in it (the ladder up from the kill switch's hall
    /// ends here); the only way off it is the maintenance tunnel in the middle of its far side.
    /// 'hatch' is where the climber's feet are as they reach the roof (facing the garden), 'lid'
    /// the hatch's cover, standing open until they are out.
    /// </summary>
    static void BuildEscapePark(out Transform hatch, out Transform lid)
    {
        const float half = 17f, deep = 10f, y = GardenY;
        // the hatch: a square hole right through the roof, 1.8 m across, with the ladder on its near (garden) side
        const float hx = 0.9f, hz0 = -8.1f, hz1 = -6.3f;
        HoledSlab("RoofSlab", y - 0.25f, 0.5f, half, deep, hx, hz0, hz1, mPlate, true);
        HoledSlab("RoofSlabUnder", y - 0.9f, 0.8f, half - 1f, deep - 1f, hx, hz0, hz1, mHull, false);
        // it stands out from the tower over the facility's roof, on four pylons
        foreach (float sx in new[] { -1f, 1f })
            foreach (float z in new[] { 0f, 8f })
                Box("RoofPylon", new Vector3(sx * 13f, (12.5f + y - 1.3f) / 2f, z), new Vector3(1.4f, y - 1.3f - 12.5f, 1.4f), mPlate, false);
        HoledSlab("Lawn", y + 0.03f, 0.06f, half - 1.5f, deep - 1.5f, hx + 0.3f, hz0 - 0.3f, hz1 + 0.3f, cGrass, false);
        Box("Path", new Vector3(0, y + 0.07f, 2.05f), new Vector3(3.4f, 0.04f, 15.7f), cWalk, false);
        Box("PathCross", new Vector3(0, y + 0.07f, -1f), new Vector3(24f, 0.04f, 2.4f), cWalk, false);
        // parapet on three sides (the tower is the fourth); the far side is broken only by the tunnel's mouth
        foreach (float s in new[] { -1f, 1f })
        {
            Box("Parapet", new Vector3(s * (half - 0.3f), y + 0.6f, 0), new Vector3(0.6f, 1.2f, deep * 2f), cPlanter);
            Box("ParapetLight", new Vector3(s * (half - 0.3f), y + 1.23f, 0), new Vector3(0.2f, 0.07f, deep * 2f - 1f), cLane, false);
            Box("ParapetFar", new Vector3(s * (half + 2.3f) / 2f, y + 0.6f, deep - 0.3f), new Vector3(half - 2.3f, 1.2f, 0.6f), cPlanter);
            Box("ParapetFarLight", new Vector3(s * (half + 2.3f) / 2f, y + 1.23f, deep - 0.3f), new Vector3(half - 3.3f, 0.07f, 0.2f), cLane, false);
            // the tunnel's mouth: two heavy posts and a lit lintel
            Box("MouthPost", new Vector3(s * 2.45f, y + 1.8f, deep - 0.3f), new Vector3(0.6f, 3.6f, 0.8f), mPlate);
            Box("MouthPostLight", new Vector3(s * 2.45f, y + 1.8f, deep - 0.72f), new Vector3(0.14f, 3.2f, 0.06f), cElevator, false);
        }
        Box("ParapetBack", new Vector3(0, y + 1.5f, -deep + 0.3f), new Vector3(half * 2f, 3f, 0.6f), mHull);
        // (no lettering over the mouth: the lit posts and lintel say "this way" by themselves)
        Box("MouthHead", new Vector3(0, y + 3.95f, deep - 0.3f), new Vector3(5.5f, 0.9f, 0.8f), mPlate, false);
        Box("MouthHeadLight", new Vector3(0, y + 3.56f, deep - 0.72f), new Vector3(4.3f, 0.1f, 0.06f), cElevator, false);

        // ---- the hatch the ladder comes up through: a rim round the hole, the shaft under it, and a lid standing open
        foreach (float s in new[] { -1f, 1f })
        {
            Box("HatchRim", new Vector3(s * (hx + 0.15f), y + 0.12f, (hz0 + hz1) / 2f), new Vector3(0.3f, 0.24f, hz1 - hz0), mPlate, false);
            Box("HatchRim", new Vector3(0, y + 0.12f, s < 0 ? hz0 - 0.15f : hz1 + 0.15f), new Vector3(hx * 2f + 0.6f, 0.24f, 0.3f), mPlate, false);
            Box("ShaftWall", new Vector3(s * (hx + 0.075f), y - 1.8f, (hz0 + hz1) / 2f), new Vector3(0.15f, 3.6f, hz1 - hz0 + 0.3f), mHull, false);
            Box("ShaftWall", new Vector3(0, y - 1.8f, s < 0 ? hz0 - 0.075f : hz1 + 0.075f), new Vector3(hx * 2f, 3.6f, 0.15f), mHull, false);
            Box("ShaftLight", new Vector3(s * 0.5f, y - 1.9f, hz0 + 0.02f), new Vector3(0.08f, 3.0f, 0.04f), cElevator, false);
        }
        Box("ShaftFloor", new Vector3(0, y - 3.65f, (hz0 + hz1) / 2f), new Vector3(hx * 2f + 0.3f, 0.1f, hz1 - hz0 + 0.3f), mHull, false);
        CityProp("Mono_Ladder", new Vector3(0, y - 5.9f, hz1 - 0.14f), 180f, 1f);
        PointLight("ShaftLamp", new Vector3(0, y - 1.6f, (hz0 + hz1) / 2f), Amber, 3f, 4f);
        PointLight("HatchLight", new Vector3(0, y + 1.4f, -7.2f), Amber, 3f, 6f);
        // the lid, hinged on the tower side: open until the player has climbed out, then it drops shut and can be walked on
        lid = new GameObject("HatchLid").transform;
        lid.SetParent(env, false);
        lid.position = new Vector3(0, y + 0.26f, hz0);
        Box("HatchLidPlate", new Vector3(0, y + 0.26f, (hz0 + hz1) / 2f), new Vector3(hx * 2f + 0.1f, 0.07f, hz1 - hz0 + 0.1f), mPlate, true, null, 0f, lid);
        Box("HatchLidGlow", new Vector3(0, y + 0.305f, (hz0 + hz1) / 2f), new Vector3(1.5f, 0.03f, 1.5f), cElevator, false, null, 0f, lid);
        lid.localRotation = Quaternion.Euler(-100f, 0f, 0f);
        // where the climber's feet are on reaching roof level: on the ladder, facing the garden and the tunnel beyond it
        hatch = new GameObject("HatchClimb").transform;
        hatch.SetParent(env, false);
        hatch.position = new Vector3(0, y + 0.05f, hz1 - 0.55f);
        // trees, benches and lamps
        foreach (var (x, z) in new[] { (-11f, -6f), (-13f, 0.5f), (-10f, 6.5f), (11f, -6.5f), (13f, 1f), (10.5f, 6.5f), (-6f, -8.2f), (6.5f, -8.2f) })
        {
            var tree = CityProp("City_Tree", new Vector3(x, y, z), x * 37f, 1.25f + 0.2f * Mathf.Sin(x * z));
            var trunk = tree.AddComponent<CapsuleCollider>();
            trunk.center = new Vector3(0, 1.2f, 0);
            trunk.height = 2.6f;
            trunk.radius = 0.45f;
        }
        foreach (var (x, z, yaw) in new[] { (-5.5f, 1f, 90f), (5.5f, 1f, -90f), (-5.5f, 6f, 90f), (5.5f, 6f, -90f) })
        {
            var bench = CityProp("City_Bench", new Vector3(x, y + 0.06f, z), yaw, 1.1f);
            var seat = bench.AddComponent<BoxCollider>();
            seat.center = new Vector3(0, 0.5f, -0.05f);
            seat.size = new Vector3(1.8f, 1.0f, 0.6f);
        }
        foreach (var (x, z) in new[] { (-3f, -3f), (3f, -3f), (-7.5f, 8f), (7.5f, 8f) })
        {
            var lamp = CityProp("City_Lamp", new Vector3(x, y, z));
            var post = lamp.AddComponent<CapsuleCollider>();
            post.center = new Vector3(0, 2.8f, 0);
            post.height = 5.6f;
            post.radius = 0.22f;
        }
    }

    /// <summary>
    /// A flat slab centred on the origin ('halfX' by 'halfZ', its middle at height 'y') with a
    /// rectangular hole through it from -holeX to holeX and from z0 to z1: four boxes round the hole.
    /// </summary>
    static void HoledSlab(string name, float y, float thick, float halfX, float halfZ, float holeX, float z0, float z1, Material m, bool solid)
    {
        Box(name, new Vector3(0, y, (-halfZ + z0) / 2f), new Vector3(halfX * 2f, thick, z0 + halfZ), m, solid);
        Box(name, new Vector3(0, y, (z1 + halfZ) / 2f), new Vector3(halfX * 2f, thick, halfZ - z1), m, solid);
        foreach (float s in new[] { -1f, 1f })
            Box(name, new Vector3(s * (holeX + halfX) / 2f, y, (z0 + z1) / 2f), new Vector3(halfX - holeX, thick, z1 - z0), m, solid);
    }
    // ================================================================ the course, along its straight line (z is metres along the way)
    /// <summary>
    /// Builds the course from the tunnel to the portal and returns the portal's trigger. Every
    /// piece is placed on one straight line; BeginSection marks where the line will turn, and
    /// FoldCourse then turns it. The turns go left and right by turns down the forecourt and the
    /// avenue, each one at a jump gap or on a round pad where two flat decks meet; a wall run
    /// always shares a section with the decks either side of it. The last four are all to the
    /// right: a sweep through the air over the junction, ending due west down Arrival Street.
    /// Heights, gaps, walls and anchors are those of the straight course, which was tuned and proven.
    /// </summary>
    static Transform BuildEscapeCourse()
    {
        float y = RoofY;
        UseSection(0);

        // ---- the maintenance tunnel: from its mouth on the roof garden, down a ramp inside a lit duct, out onto the start platform
        Vector3 t0 = new Vector3(0, GardenY, -14.2f), t1 = new Vector3(0, y, -1.2f);
        Slab("TunnelFloor", t0, t1, Vector3.zero, new Vector3(4.2f, 0.6f, 0.6f), mPlate, true);
        Slab("TunnelRoof", t0, t1, new Vector3(0f, 3.45f, 0f), new Vector3(4.9f, 0.3f, 0.6f), mHull, false);
        Slab("TunnelRoofLight", t0, t1, new Vector3(0f, 3.27f, 0f), new Vector3(0.4f, 0.05f, -1f), cLane, false);
        foreach (float s in new[] { -1f, 1f })
        {
            Slab("TunnelWall", t0, t1, new Vector3(s * 2.25f, 1.7f, 0f), new Vector3(0.3f, 3.5f, 0.6f), mHull, true);
            Slab("TunnelWallBand", t0, t1, new Vector3(s * 2.08f, 1.1f, 0f), new Vector3(0.05f, 0.12f, -0.6f), cElevator, false);
            Slab("TunnelPipe", t0, t1, new Vector3(s * 1.92f, 2.7f, 0f), new Vector3(0.22f, 0.22f, -0.4f), mMetal, false);
            Slab("TunnelPipeLow", t0, t1, new Vector3(s * 1.96f, 2.35f, 0f), new Vector3(0.12f, 0.12f, -0.4f), mMetal, false);
            Slab("TunnelKerb", t0, t1, new Vector3(s * 1.95f, 0.04f, 0f), new Vector3(0.1f, 0.06f, 0f), cElevator, false);
        }
        // ribs along it, every other pair of posts lit: an old emergency way out
        for (int i = 0; i <= 6; i++)
        {
            var rib0 = Vector3.Lerp(t0, t1, i / 6f - 0.012f);
            var rib1 = Vector3.Lerp(t0, t1, i / 6f + 0.012f);
            Slab("TunnelRib", rib0, rib1, new Vector3(0f, 3.25f, 0f), new Vector3(4.5f, 0.2f, 0f), mPlate, false);
            foreach (float s in new[] { -1f, 1f })
                Slab("TunnelRibSide", rib0, rib1, new Vector3(s * 2.05f, 1.7f, 0f), new Vector3(0.12f, 3.4f, 0f), i % 2 == 0 ? mPlate : cData, false);
        }
        PointLight("TunnelLamp", Vector3.Lerp(t0, t1, 0.3f) + Vector3.up * 2.6f, new Color32(255, 190, 120, 255), 4f, 8f);
        PointLight("TunnelLampLow", Vector3.Lerp(t0, t1, 0.75f) + Vector3.up * 2.6f, new Color32(120, 235, 255, 255), 4f, 8f);

        // ---- the start platform at the tunnel's foot
        Deck("StartPlatform", 0f, y, -1.6f, 7f, 9f);
        // what the player and the mask say at the top of the course (the player's own lines, exactly as written)
        Check(new Vector3(0, y, 1.5f), 9f, TutorialKind.Jump,
              "HUMAN 017|This parkour looks like it was designed?",
              "MASK|Yes, that was a designed kill switch before robots took over.",
              "MASK|They were never able to delete it.");

        // ---- left at the first gap: the two jump decks run off at an angle
        BeginSection(7f, -20f);
        Deck("JumpDeckA", 0f, y - 1.5f, 11f, 21f, 9f);              // a little wider than the rest: the first angled landing should forgive a straight jump
        Deck("JumpDeckB", 0f, y - 3f, 25.5f, 30f, 8f, false);
        Check(new Vector3(0, y - 3f, 27f), 8f, TutorialKind.Slide,
              "HUMAN 017|There is no railing. Who builds a ramp with no railing?", "MASK|Robots. We do not fall. Usually.");

        // ---- right, on a round pad: the slide, a ramp over the gatehouse with two low beams to go under
        BeginSection(30f, 33f);
        TurnPad("SlidePad", y - 3f, 30f, 4.5f);                     // (as big as it can be without reaching into the gap before it)
        Deck("SlideDeck", 0f, y - 3f, 30f, 35f, 8f);
        Vector3 r0 = new Vector3(0, y - 3f, 35f), r1 = new Vector3(0, y - 14f, 67f);
        Ramp("SlideRamp", r0, r1, 7f);
        RampBeam(r0, r1, 0.35f, 7f);
        RampBeam(r0, r1, 0.7f, 7f);
        y -= 14f;                                                   // 30.6, over the forecourt

        // ---- left at the ramp's foot (gently: a slide comes off it fast). Wall run: the deck stops; a lit wall
        //      on the right carries on over the fortress wall
        BeginSection(67f, -15f);
        TurnPad("RampFootPad", y, 67f, 4.5f);                       // wide enough to catch a slide that swings out a little on the turn
        Deck("WallDeckA", 0f, y, 67f, 83f, 6f);
        Check(new Vector3(0, y, 69f), 6f, TutorialKind.WallRun,
              "MASK|The wall is a floor now.", "HUMAN 017|The wall has ALWAYS been a wall!");
        RunWall("RunWallA", 3f, y - 2.5f, y + 5f, 84f, 96f);
        Deck("WallDeckB", 0f, y - 1.5f, 97f, 103.5f, 6f, false);
        y -= 1.5f;                                                  // 29.1
        Check(new Vector3(0, y, 99f), 6f, TutorialKind.None,
              "MASK|Left wall. Kick off. Right wall.", "HUMAN 017|You say that like it is a sentence.");

        // ---- right: the zig-zag of two walls, a wall on the left, kick off across to a wall on the right
        BeginSection(103.5f, 10f);
        TurnPad("WallPad", y, 103.5f, 3.3f);
        Deck("WallDeckC", 0f, y, 103.5f, 110f, 6f);
        RunWall("RunWallB", -3f, y - 4.5f, y + 3.5f, 111f, 123f);
        RunWall("RunWallC", 3f, y - 7f, y + 1.5f, 119f, 133f);
        Deck("GrappleDeckA", 0f, y - 5f, 134f, 142f, 10f, false);
        y -= 5f;                                                    // 24.1, over the avenue
        Check(new Vector3(0, y, 136f), 10f, TutorialKind.Grapple,
              "MASK|Grappling line. Every robot carries one. Do not ask where.", "HUMAN 017|...I was not going to ask.");

        // ---- left: the grapple, one anchor over a gap far too wide to jump
        BeginSection(142f, -18f);
        TurnPad("GrapplePadA", y, 142f, 5.3f);
        Deck("GrappleDeckA2", 0f, y, 142f, 150f, 10f);
        Anchor(new Vector3(0, y + 10f, 162f));
        Deck("GrappleDeckB", 0f, y - 6f, 174f, 182f, 12f, false);
        y -= 6f;                                                    // 18.1
        Check(new Vector3(0, y, 176f), 12f, TutorialKind.None,
              "HUMAN 017|Two in a row? Who designed this?", "MASK|You did. You pressed the kill switch.");

        // ---- right (the sweep toward Arrival Street begins): two in a row, swing, let go, catch the next one in
        //      the air, and come down on the landing deck over the junction
        BeginSection(182f, 15f);
        TurnPad("GrapplePadB", y, 182f, 6.3f);
        Deck("GrappleDeckB2", 0f, y, 182f, 191f, 12f);
        Anchor(new Vector3(0f, y + 10f, 203f));
        Anchor(new Vector3(0f, y + 6.5f, 221f));
        y -= 8f;                                                    // 10.1
        Deck("LandingDeck", 0f, y, 230f, 240f, 12f, false);
        Check(new Vector3(0, y, 232.5f), 12f, TutorialKind.None,
              "MASK|The portal is shut, but I can power it on.", "HUMAN 017|Then be quick. I can see the white from here.");

        // ---- right again, on the landing deck's round pad: a short run to the last gap
        BeginSection(240f, 35f);
        TurnPad("SweepPad", y, 240f, 6.3f);
        Deck("SweepDeck", 0f, y, 240f, 247f, 8f);

        // ---- and right in the air over that gap. The deck beyond it runs on to wherever this heading meets the
        //      middle of Arrival Street: that is where the last turn is taken.
        BeginSection(247f, 22f);
        float sweep = CourseHeading();
        float toStreet = Mathf.Max(8f, (CoursePoint(247f).z - ArrivalZ) / Mathf.Cos(sweep * Mathf.Deg2Rad));
        float last = 247f + toStreet;
        Deck("TurnDeck", 0f, y - 1.5f, 251f, last, 10f, false);     // wide: it is landed on at an angle
        Check(new Vector3(0, y - 1.5f, 252.5f), 10f, TutorialKind.None,
              "MASK|Last slide. The portal is at the end of the street.", "HUMAN 017|If we live, I am keeping you.");

        // ---- the last of the quarter turn, on a pad: due west now, down Arrival Street. A last slide in two parts.
        BeginSection(last, 90f - sweep);
        TurnPad("StreetPad", y - 1.5f, last, 5f);
        float top = last + 7f;                                      // the head of the last ramp
        Deck("TurnDeck2", 0f, y - 1.5f, last, top, 8f);
        Vector3 f0 = new Vector3(0, y - 1.5f, top), f1 = new Vector3(0, 2.6f, top + 26f);
        Ramp("FinalRampA", f0, f1, 7f);
        RampBeam(f0, f1, 0.5f, 7f);
        Deck("Kicker", 0f, 2.6f, top + 26f, top + 31f, 7f);
        // no gap after the ledge: a slide that simply runs off it drops onto this last ramp and carries on down to the street
        Ramp("FinalRampB", new Vector3(0, 1.3f, top + 31f), new Vector3(0, 0.04f, top + 43f), 8f);
        // the street itself from here: a straight sprint to the portal
        Check(new Vector3(0, 0.02f, top + 49f), 10f, TutorialKind.None,
              "HUMAN 017|Shift 017 is over.", "MASK|Go home, human.");

        // the portal's trigger: just in front of the gate at the street's end (BuildEscape puts it there once the course is folded)
        var portalGo = new GameObject("EscapePortal");
        portalGo.transform.SetParent(env, false);
        var trigger = portalGo.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 2f, 0f);
        trigger.size = new Vector3(6.4f, 4f, 5f);
        portalGo.AddComponent<EscapePortal>().flow = escapeFlow;
        return portalGo.transform;
    }

    // ================================================================ the portal, alive again (shown when the player reaches the corner)
    static GameObject BuildLivePortal()
    {
        float x = CityWest + 0.5f, z = ArrivalZ;
        var live = new GameObject("LivePortal").transform;
        live.position = new Vector3(x, 0, z);
        var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        surface.name = "PortalSurface";
        surface.transform.SetParent(live, false);
        surface.transform.SetPositionAndRotation(new Vector3(x - 0.86f, 2.3f, z), Quaternion.Euler(0, -90f, 0));
        surface.transform.localScale = new Vector3(4.6f, 4.6f, 1f);
        surface.GetComponent<MeshRenderer>().sharedMaterial = cPortalLive;
        var bars = new List<Transform>();
        for (int i = 0; i < 7; i++)
            bars.Add(Box("PortalBar", new Vector3(x - 0.6f, 2.3f, z - 1.8f + i * 0.6f), new Vector3(0.06f, 2.2f, 0.1f), mPortalBar, false, null, 0f, live).transform);
        var portalFx = live.gameObject.AddComponent<PortalFx>();
        portalFx.bars = bars.ToArray();
        portalFx.glow = PointLight("PortalLight", new Vector3(x + 2.5f, 2.6f, z), new Color32(120, 255, 235, 255), 10f, 18f, live);
        var beam = Box("PortalBeacon", new Vector3(x - 2.2f, 34f, z), new Vector3(0.6f, 40f, 0.6f), cPortalLive, false, null, 0f, live);
        beam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        WorldText("PortalOnlineSign", "PORTAL ONLINE", new Vector3(x + 1.25f, 5.5f, z), Quaternion.Euler(0, -90f, 0), 0.0042f, 96, new Color32(120, 255, 235, 255), live);
        return live.gameObject;
    }

    // ================================================================ the words in the sky
    static GameObject BuildSkyBanner(Vector3 at, out Text[] texts)
    {
        var banner = new GameObject("SkyBanner").transform;
        banner.position = at;
        // the plate is unlit (so the haze does not grey it) and drawn just before the lettering, whatever the camera's angle
        var plateMaterial = Flat("FX_BannerPlate", new Color32(8, 10, 18, 255));
        plateMaterial.renderQueue = 2900;
        EditorUtility.SetDirty(plateMaterial);
        var plate = Box("BannerPlate", at + new Vector3(0, 0, 1.2f), new Vector3(190f, 62f, 0.6f), plateMaterial, false, null, 0f, banner);
        plate.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var redGlow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/Mono_GlowRed.mat");
        foreach (float s in new[] { -1f, 1f })
        {
            Box("BannerEdge", at + new Vector3(0, s * 30.4f, 0.6f), new Vector3(190f, 1.2f, 0.8f), redGlow, false, null, 0f, banner);
            Box("BannerEdge", at + new Vector3(s * 94.4f, 0, 0.6f), new Vector3(1.2f, 62f, 0.8f), redGlow, false, null, 0f, banner);
        }
        var red = new Color32(255, 60, 50, 255);
        texts = new[]
        {
            WorldText("BannerTop", "KILL SWITCH", at + new Vector3(0, 12.5f, 0), Quaternion.identity, 0.2f, 96, red, banner),
            WorldText("BannerBottom", "ACTIVATED", at + new Vector3(0, -13.5f, 0), Quaternion.identity, 0.2f, 96, red, banner),
        };
        return banner.gameObject;
    }

    // ================================================================ the deletion wave's visible front
    /// <summary>A tall sheet of white light across the map ('width' metres wide, turned by 'yaw'), with a burning edge along the ground.</summary>
    static Transform BuildWaveWall(string name, Vector3 at, float yaw, float width)
    {
        var root = new GameObject(name).transform;
        root.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(sheet.GetComponent<Collider>());
        sheet.name = "Sheet";
        sheet.transform.SetParent(root, false);
        sheet.transform.localPosition = new Vector3(0f, 250f, 0f);
        sheet.transform.localScale = new Vector3(width, 520f, 1f);
        var renderer = sheet.GetComponent<MeshRenderer>();
        // (drawn before the sky banner's plate and every sign, so it never washes over lettering that stands in front of it)
        var wallMaterial = Flat("FX_DeletionWall", new Color32(228, 248, 255, 150));
        wallMaterial.renderQueue = 2880;
        EditorUtility.SetDirty(wallMaterial);
        renderer.sharedMaterial = wallMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        // a second, fainter sheet a little ahead: the air whitens before the wall arrives
        var haze = Object.Instantiate(sheet, root);
        haze.name = "Haze";
        haze.transform.localPosition = new Vector3(0f, 250f, -9f);
        var hazeMaterial = Flat("FX_DeletionHaze", new Color32(228, 248, 255, 46));
        hazeMaterial.renderQueue = 2885;
        EditorUtility.SetDirty(hazeMaterial);
        haze.GetComponent<MeshRenderer>().sharedMaterial = hazeMaterial;
        void Glow(string part, Vector3 local, Vector3 size, Material m)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.name = part;
            bar.transform.SetParent(root, false);
            bar.transform.localPosition = local;
            bar.transform.localScale = size;
            var r = bar.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        Glow("GroundEdge", new Vector3(0f, 0.5f, -0.4f), new Vector3(width, 1.0f, 0.8f), cPortalLive);
        for (float x = -width / 2f + 20f; x < width / 2f; x += 44f)
            Glow("Column", new Vector3(x, 250f, -0.6f), new Vector3(0.9f, 520f, 0.9f), cLane);
        return root;
    }

    // ================================================================ the crowd in the streets
    /// <summary>
    /// Robots on the avenue, Mid Street and Arrival Street, below the course. They cower when the
    /// words appear, then scatter in blind panic: each runs wherever its own fear takes it, up and
    /// down the streets, across them, round corners and back (see PanicRunner). Nobody heads for
    /// the portal, which is shut. Returned nearest the gathering point first (those are the ones
    /// the opening shot stands in front of the camera).
    /// </summary>
    static PanicRunner[] BuildPanicCrowd(DeletionWave wave, Vector3 gatherPoint)
    {
        var root = new GameObject("PanicCrowd").transform;
        var runners = new List<PanicRunner>();
        var panicGlow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/Mono_GlowRed.mat");
        // the streets they may run along, each a line with two ends: the three east-west streets, West and East
        // Street, and the avenue south of Mid Street (north of it the barricade shuts it)
        float westEnd = CityWest + 8f, eastEnd = CityEast - 8f;
        var streets = new[]
        {
            new PanicRunner.Street { eastWest = true, line = ArrivalZ, from = westEnd, to = eastEnd },
            new PanicRunner.Street { eastWest = true, line = MidZ, from = westEnd, to = eastEnd },
            new PanicRunner.Street { eastWest = true, line = RingZ, from = westEnd, to = eastEnd },
            new PanicRunner.Street { eastWest = false, line = WestX, from = ArrivalZ, to = RingZ },
            new PanicRunner.Street { eastWest = false, line = AvenueX, from = ArrivalZ, to = MidZ },
            new PanicRunner.Street { eastWest = false, line = EastX, from = ArrivalZ, to = RingZ },
        };
        int n = 0;
        PanicRunner Runner(Vector3 at, float yaw)
        {
            GameObject go;
            if (n % 6 == 5)
            {
                go = Dummy(root, "Runner_" + n, n % 12 == 5 ? "Orange" : "Blue", at, yaw);       // a few security units among them: they run too
                WireGesture(go);
            }
            else go = Civilian(root, "Runner_" + n, at, yaw);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var body = go.GetComponent<Rigidbody>();
            if (body != null) Object.DestroyImmediate(body);
            var runner = go.AddComponent<PanicRunner>();
            runner.streets = streets;
            runner.halfWidth = 8f;                  // the road and most of both pavements; the block platforms start at 10 m
            runner.seed = 1009 + n * 7919;
            runner.speed = 5.0f + 0.3f * (n % 5);
            runner.animator = go.GetComponent<Animator>();
            runner.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            runner.gesture = go.GetComponent<RobotGesture>();
            runner.wave = wave;
            // an alarm light over each head, so the stampede can be picked out from the course high above it
            var beacon = Box("PanicBeacon", at + Vector3.up * 2.35f, new Vector3(0.34f, 0.34f, 0.34f), panicGlow, false, null, 45f, go.transform);
            beacon.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            runners.Add(runner);
            n++;
            return runner;
        }
        // Mid Street, either side of the avenue
        foreach (float side in new[] { -1f, 1f })
            foreach (float dz in new[] { -2.6f, 2.6f })
                for (int i = 0; i < 5; i++)
                {
                    float x = side * (15f + i * 8.5f + (dz > 0f ? 4f : 0f)), z = MidZ + dz;
                    Runner(new Vector3(x, 0f, z), n * 47f);
                }
        // the avenue, south of the barricade: under the course all the way down
        float[] lanes = { -4.4f, -1.5f, 1.5f, 4.4f };
        for (int lane = 0; lane < lanes.Length; lane++)
            for (int i = 0; i < 9; i++)
            {
                float x = lanes[lane], z = MidZ + 8f - i * 7.4f - lane * 1.7f;
                Runner(new Vector3(x, 0f, z), n * 47f);
            }
        // Arrival Street itself
        foreach (float dz in new[] { -4.8f, 4.8f })
            for (int i = 0; i < 8; i++)
            {
                float x = -14f - i * 11.5f, z = ArrivalZ + dz;
                Runner(new Vector3(x, 0f, z), n * 47f);
            }
        runners.Sort((a, b) => (a.transform.position - gatherPoint).sqrMagnitude.CompareTo((b.transform.position - gatherPoint).sqrMagnitude));
        return runners.ToArray();
    }

    // ================================================================ course pieces
    /// <summary>
    /// A flat stretch of the course: top surface at 'top', from z0 to z1, lit along its edges and
    /// (unless it only runs on into a turning pad: 'lip' false) across its far end.
    /// </summary>
    static void Deck(string name, float x, float top, float z0, float z1, float width, bool lip = true)
    {
        float mid = (z0 + z1) / 2f, length = z1 - z0;
        Box(name, new Vector3(x, top - 0.4f, mid), new Vector3(width, 0.8f, length), mPlate);
        Box(name + "Under", new Vector3(x, top - 0.95f, mid), new Vector3(width - 1.2f, 0.3f, length - 1.2f), mHull, false);
        foreach (float s in new[] { -1f, 1f })
            Box(name + "Edge", new Vector3(x + s * (width / 2f - 0.12f), top + 0.012f, mid), new Vector3(0.14f, 0.03f, length - 0.4f), cLane, false);
        if (lip) Box(name + "Lip", new Vector3(x, top + 0.012f, z1 - 0.18f), new Vector3(width - 0.5f, 0.03f, 0.26f), cElevator, false);       // amber: the take-off line
    }

    /// <summary>
    /// A round pad where the course turns: flat, flush with the two decks that meet on it (so it
    /// fills the wedge the turn would leave between them), with a lit rim. Its middle is the pivot.
    /// </summary>
    static void TurnPad(string name, float top, float z, float radius)
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = name;
        pad.transform.SetParent(env, false);
        pad.transform.position = new Vector3(0f, top - 0.4f, z);
        pad.transform.localScale = new Vector3(radius * 2f, 0.4f, radius * 2f);
        pad.GetComponent<MeshRenderer>().sharedMaterial = mPlate;
        // (the primitive comes wrapped in a capsule; the pad has to be flat right out to its edge)
        Object.DestroyImmediate(pad.GetComponent<Collider>());
        var solid = pad.AddComponent<MeshCollider>();
        solid.sharedMesh = pad.GetComponent<MeshFilter>().sharedMesh;
        solid.convex = true;
        GameObjectUtility.SetStaticEditorFlags(pad, StaticEditorFlags.BatchingStatic);

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(rim.GetComponent<Collider>());
        rim.name = name + "Rim";
        rim.transform.SetParent(env, false);
        rim.transform.position = new Vector3(0f, top - 0.06f, z);
        rim.transform.localScale = new Vector3(radius * 2f + 0.24f, 0.03f, radius * 2f + 0.24f);
        var rimRenderer = rim.GetComponent<MeshRenderer>();
        rimRenderer.sharedMaterial = cLane;
        rimRenderer.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(rim, StaticEditorFlags.BatchingStatic);
    }

    /// <summary>A sloping stretch whose top surface runs from 'from' to 'to', with low rails so a slide stays on it.</summary>
    static void Ramp(string name, Vector3 from, Vector3 to, float width)
    {
        Slab(name, from, to, Vector3.zero, new Vector3(width, 0.8f, 0f), mPlate, true);
        foreach (float s in new[] { -1f, 1f })
        {
            Slab(name + "Rail", from, to, new Vector3(s * (width / 2f - 0.15f), 0.35f, 0f), new Vector3(0.3f, 0.7f, 0f), cPlanter, true);
            Slab(name + "RailLight", from, to, new Vector3(s * (width / 2f - 0.15f), 0.73f, 0f), new Vector3(0.14f, 0.05f, -0.4f), cLane, false);
        }
        // chevrons down the middle: this is a slide
        float length = Vector3.Distance(from, to);
        for (float d = 4f; d < length - 3f; d += 6f)
        {
            var a = Vector3.Lerp(from, to, d / length);
            var b = Vector3.Lerp(from, to, (d + 1.6f) / length);
            Slab(name + "Mark", a, b, new Vector3(0f, 0.012f, 0f), new Vector3(0.5f, 0.03f, 0f), cElevator, false);
        }
    }

    /// <summary>
    /// A box laid along the line from 'from' to 'to'. 'offset' moves it sideways (x) and off the
    /// surface (y) in the line's own frame; 'size' is its width and thickness, and size.z is added to its length.
    /// With no offset its top face lies on the line.
    /// </summary>
    static GameObject Slab(string name, Vector3 from, Vector3 to, Vector3 offset, Vector3 size, Material m, bool solid)
    {
        var rotation = Quaternion.LookRotation(to - from, Vector3.up);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(env, false);
        float along = offset.y > 0f ? offset.y : -size.y / 2f;       // on the surface, or raised to a given height above it
        go.transform.SetPositionAndRotation((from + to) / 2f + rotation * new Vector3(offset.x, along, 0f), rotation);
        go.transform.localScale = new Vector3(size.x, size.y, Vector3.Distance(from, to) + size.z);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = m;
        if (!solid)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    /// <summary>A beam across a ramp, low enough that it can only be passed sliding. 't' is how far down the ramp it is (0 to 1).</summary>
    static void RampBeam(Vector3 from, Vector3 to, float t, float width)
    {
        var rotation = Quaternion.LookRotation(to - from, Vector3.up);
        var at = Vector3.Lerp(from, to, t);
        // The gap has to leave room for the sliding capsule (1.0 m) plus the step the character controller
        // lifts itself by while it moves; a standing player (1.8 m) still cannot get under.
        const float clear = 1.5f, thick = 0.9f;
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "LowBeam";
        beam.transform.SetParent(env, false);
        beam.transform.SetPositionAndRotation(at + rotation * new Vector3(0f, clear + thick / 2f, 0f), rotation);
        beam.transform.localScale = new Vector3(width + 0.6f, thick, 0.7f);
        beam.GetComponent<MeshRenderer>().sharedMaterial = mHull;
        GameObjectUtility.SetStaticEditorFlags(beam, StaticEditorFlags.BatchingStatic);
        var red = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/Mono_GlowRed.mat");
        foreach (var (dy, dz) in new[] { (-thick / 2f + 0.06f, -0.36f), (thick / 2f - 0.2f, -0.36f) })
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            strip.name = "LowBeamLight";
            strip.transform.SetParent(env, false);
            strip.transform.SetPositionAndRotation(at + rotation * new Vector3(0f, clear + thick / 2f + dy, dz), rotation);
            strip.transform.localScale = new Vector3(width + 0.4f, 0.1f, 0.04f);
            var renderer = strip.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = red;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        foreach (float s in new[] { -1f, 1f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "LowBeamPost";
            post.transform.SetParent(env, false);
            post.transform.SetPositionAndRotation(at + rotation * new Vector3(s * (width / 2f + 0.15f), (clear + thick) / 2f - 0.4f, 0f), rotation);
            post.transform.localScale = new Vector3(0.3f, clear + thick + 0.8f, 0.5f);
            post.GetComponent<MeshRenderer>().sharedMaterial = mPlate;
        }
    }

    /// <summary>
    /// A wall that can be run along. Its running face is at x = 'face' (the side toward x = 0);
    /// it is on the WallRun layer, which is what the parkour motor looks for.
    /// </summary>
    static void RunWall(string name, float face, float bottom, float top, float z0, float z1)
    {
        float side = Mathf.Sign(face), mid = (z0 + z1) / 2f, length = z1 - z0, height = top - bottom;
        var wall = Box(name, new Vector3(face + side * 0.4f, (bottom + top) / 2f, mid), new Vector3(0.8f, height, length), cWallRun);
        wall.layer = LayerMask.NameToLayer(Layers.WallRun);
        // lit lines along it and an arrow of chevrons: run this way
        foreach (float h in new[] { 0.12f, 0.5f, 0.88f })
            Box(name + "Line", new Vector3(face - side * 0.02f, bottom + height * h, mid), new Vector3(0.05f, 0.12f, length - 0.6f), cLane, false);
        for (float z = z0 + 2f; z < z1 - 1.5f; z += 3.2f)
            foreach (float tilt in new[] { -1f, 1f })
            {
                var mark = Box(name + "Chevron", new Vector3(face - side * 0.03f, bottom + height * 0.69f + tilt * 0.42f, z), new Vector3(0.05f, 0.16f, 1.3f), cElevator, false);
                mark.transform.rotation = Quaternion.Euler(tilt * 40f, 0f, 0f);
            }
        Box(name + "Cap", new Vector3(face + side * 0.4f, top + 0.06f, mid), new Vector3(0.9f, 0.12f, length), cLane, false);
    }

    /// <summary>A hovering grapple point: the kit beacon, a lit halo that swells when it is the one in reach, and a light.</summary>
    static void Anchor(Vector3 at)
    {
        var go = CityProp("City_Anchor", at, 0f, 1.5f, false, escapeAnchors);
        go.name = "GrappleAnchor_" + ++escapeAnchorCount;           // numbered down the whole course, whichever section each is in
        // the halo: a see-through disc in a holder that GrappleAnchor swells and spins
        var ring = new GameObject("Ring").transform;
        ring.SetParent(go.transform, false);
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.name = "Halo";
        disc.transform.SetParent(ring, false);
        disc.transform.localScale = new Vector3(2.4f, 0.01f, 2.4f);
        var renderer = disc.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = cBeam;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        foreach (float turn in new[] { 0f, 90f })      // and a lit cross through it, so the spin can be seen
        {
            var spoke = Box("Spoke", at, new Vector3(3.5f, 0.05f, 0.08f), cRope, false, null, turn, ring);
            spoke.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        var anchor = go.AddComponent<GrappleAnchor>();
        anchor.ring = ring;
        anchor.beacon = PointLight("Beacon", at, new Color32(120, 255, 245, 255), 2.5f, 9f, go.transform);
        // a faint line down from it, so its place over the gap can be judged from a distance
        var plumb = Box("Plumb", at + Vector3.down * 9f, new Vector3(0.06f, 16f, 0.06f), cLane, false, null, 0f, go.transform);
        plumb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    /// <summary>A checkpoint across the course at 'at' (a point on the floor), 'width' wide.</summary>
    static void Check(Vector3 at, float width, TutorialKind tutorial, params string[] lines)
    {
        escapeOrder++;
        var go = new GameObject("Checkpoint_" + escapeOrder);
        go.transform.SetParent(escapeChecks, false);
        go.transform.position = at;
        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 2.5f, 0);
        trigger.size = new Vector3(width + 1f, 5f, 2.5f);
        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(go.transform, false);
        spawn.localPosition = new Vector3(0, 0.05f, 0);
        var check = go.AddComponent<Checkpoint>();
        check.flow = escapeFlow;
        check.order = escapeOrder;
        check.spawn = spawn;
        check.tutorial = tutorial;
        check.lines = lines;
        // a lit line across the floor marks it
        Box("CheckpointLine", at + new Vector3(0, 0.014f, 0), new Vector3(width - 0.6f, 0.03f, 0.2f), cData, false);
    }
}
