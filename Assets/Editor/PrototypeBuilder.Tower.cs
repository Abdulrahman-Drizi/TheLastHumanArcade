// Inside the Monolith's tower: the elevator car, the two floors its doors open on (the police
// corridor on floor 6 and the secure room on floor 10), the workstation's desktop, and the
// synchronised light patterns of the robots' world.
// The car and both floors are built together under the city's ground slab, out of sight; the car
// never moves. See TowerSequence for how the ride is staged.
using System.Collections.Generic;
using TheLastHuman;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static partial class PrototypeBuilder
{
    static TowerSequence BuildTower(GameObject systems, CityFlow flow, HudController hud, FirstPersonController controller, PlayerBody body,
                                    MaskController maskRules, TransitionFx fx, Transform view)
    {
        var O = TowerSet;
        var cool = new Color32(200, 230, 255, 255);
        var red = new Color32(255, 60, 50, 255);
        var redGlow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/Mono_GlowRed.mat");
        var trim = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/Mono_Trim.mat");
        var tower = systems.AddComponent<TowerSequence>();
        tower.flow = flow;
        tower.hud = hud;
        tower.player = controller;
        tower.body = body;
        tower.maskRules = maskRules;
        tower.fx = fx;
        tower.hallLeafLeft = hallLeafLeft;
        tower.hallLeafRight = hallLeafRight;
        tower.hallEntry = hallEntry;

        // small local helpers: everything here is placed relative to the set's origin
        GameObject Part(string name, Vector3 at, Vector3 size, Material m, bool solid, Transform parent) =>
            Box(name, O + at, size, m, solid, null, 0f, parent);
        Transform Point(string name, Vector3 at, float yaw, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(O + at, Quaternion.Euler(0, yaw, 0));
            return t;
        }

        // ================================================================ the elevator car (doors on its +z side)
        var car = new GameObject("ElevatorCar").transform;
        car.position = O;
        Part("CarFloor", new Vector3(0, -0.1f, 0), new Vector3(3.4f, 0.2f, 3.4f), cHallFloor, true, car);
        Part("CarCeiling", new Vector3(0, 3.1f, 0), new Vector3(3.4f, 0.2f, 3.4f), mPlate, false, car);
        Part("CarCeilingLight", new Vector3(0, 2.98f, 0), new Vector3(1.8f, 0.04f, 1.8f), cLane, false, car);
        foreach (float s in new[] { -1f, 1f })
        {
            Part("CarWall", new Vector3(s * 1.7f, 1.5f, 0), new Vector3(0.2f, 3f, 3.4f), mPlate, true, car);
            Part("CarWallBand", new Vector3(s * 1.59f, 1.1f, 0), new Vector3(0.03f, 0.08f, 3.0f), cData, false, car);
            Part("CarBack", new Vector3(s * 1.02f, 1.5f, -1.7f), new Vector3(1.36f, 3f, 0.2f), mPlate, true, car);
            Part("CarDoorFrame", new Vector3(s * 1.45f, 1.5f, 1.7f), new Vector3(0.5f, 3f, 0.2f), mPlate, true, car);
            Part("LandingWall", new Vector3(s * 3.4f, 2.1f, 1.95f), new Vector3(4.4f, 4.2f, 0.2f), mHull, true, car);       // outside the doors, on every floor
        }
        // a window slit in the back wall looks onto the shaft, where lights rush past while the car is moving
        Part("CarBackLow", new Vector3(0, 0.45f, -1.7f), new Vector3(0.7f, 0.9f, 0.2f), mPlate, true, car);
        Part("CarBackHigh", new Vector3(0, 2.7f, -1.7f), new Vector3(0.7f, 0.6f, 0.2f), mPlate, true, car);
        var pane = new GameObject("CarWindowPane");
        pane.transform.SetParent(car, false);
        pane.transform.position = O + new Vector3(0, 1.65f, -1.7f);
        pane.AddComponent<BoxCollider>().size = new Vector3(0.7f, 1.5f, 0.2f);
        Part("ShaftBack", new Vector3(0, 1.6f, -3.6f), new Vector3(5f, 10f, 0.2f), mHull, false, car);
        var bars = Point("ShaftLights", new Vector3(0, 1.6f, -2.7f), 0f, car);
        for (int i = 0; i < 7; i++)
            Part("ShaftLight", new Vector3(0, 1.6f - 3f + i, -2.7f), new Vector3(3.4f, 0.14f, 0.1f), cLane, false, bars);
        tower.shaftBars = bars;
        tower.shaftSpan = 7f;
        Part("CarDoorHead", new Vector3(0, 2.75f, 1.7f), new Vector3(2.4f, 0.5f, 0.2f), mPlate, false, car);
        Part("LandingHead", new Vector3(0, 3.35f, 1.95f), new Vector3(2.4f, 1.7f, 0.2f), mHull, false, car);
        foreach (float s in new[] { -1f, 1f })
        {
            var leaf = Point(s < 0 ? "CarLeafLeft" : "CarLeafRight", Vector3.zero, 0f, car);
            Part("Leaf", new Vector3(s * 0.6f, 1.25f, 1.72f), new Vector3(1.2f, 2.5f, 0.12f), cPartition, true, leaf);
            Part("LeafLight", new Vector3(s * 0.08f, 1.25f, 1.65f), new Vector3(0.04f, 2.2f, 0.02f), cElevator, false, leaf);
            if (s < 0) tower.carLeafLeft = leaf; else tower.carLeafRight = leaf;
        }
        tower.leafTravel = 1.2f;
        tower.floorDisplay = WorldText("FloorDisplay", "1", O + new Vector3(0, 2.76f, 1.585f), Quaternion.identity, 0.0045f, 96, Amber, car);
        PointLight("CarLight", O + new Vector3(0, 2.5f, 0), cool, 5f, 6f, car);
        tower.carSpawn = Point("CarSpawn", new Vector3(0.6f, 0.05f, -0.6f), 0f, car);
        // the car's fittings: a handrail and a kick plate along each side wall, lit corners, a vent in the ceiling,
        // and the control panel beside the door with the button for floor 10 lit
        foreach (float s in new[] { -1f, 1f })
        {
            Part("CarHandrail", new Vector3(s * 1.5f, 0.94f, -0.1f), new Vector3(0.05f, 0.05f, 2.7f), trim, false, car);
            for (int i = -1; i <= 1; i++)
                Part("CarHandrailBracket", new Vector3(s * 1.55f, 0.94f, -0.1f + i * 1.2f), new Vector3(0.1f, 0.04f, 0.04f), trim, false, car);
            Part("CarKickPlate", new Vector3(s * 1.59f, 0.14f, 0f), new Vector3(0.02f, 0.28f, 3.2f), trim, false, car);
            foreach (float e in new[] { -1f, 1f })
                Part("CarCornerLight", new Vector3(s * 1.57f, 1.5f, e * 1.57f), new Vector3(0.04f, 2.7f, 0.04f), cLane, false, car);
        }
        Part("CarVent", new Vector3(0f, 2.985f, -1.25f), new Vector3(1.1f, 0.03f, 0.42f), mHull, false, car);
        for (int i = 0; i < 4; i++)
            Part("CarVentSlat", new Vector3(0f, 2.966f, -1.4f + i * 0.1f), new Vector3(1.0f, 0.012f, 0.03f), trim, false, car);
        Part("CarPanel", new Vector3(1.43f, 1.3f, 1.585f), new Vector3(0.3f, 0.62f, 0.03f), mHull, false, car);
        Part("CarPanelScreen", new Vector3(1.43f, 1.54f, 1.567f), new Vector3(0.22f, 0.07f, 0.01f), cLane, false, car);
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 2; col++)
            {
                bool chosen = row == 0 && col == 1;
                Part(chosen ? "CarButtonFloor10" : "CarButton", new Vector3(1.38f + col * 0.1f, 1.43f - row * 0.075f, 1.567f),
                     new Vector3(0.055f, 0.045f, 0.012f), chosen ? cElevator : cData, false, car);
            }

        // the worker who rides up with the player
        var worker = Civilian(car, "ElevatorWorker", O + new Vector3(-0.75f, 0, 0.15f), 0f);
        worker.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
        tower.worker = worker.transform;
        tower.workerGesture = worker.GetComponent<RobotGesture>();
        tower.workerAnimator = worker.GetComponent<Animator>();
        tower.workerIcon = AlertMark(worker.transform, 2.3f);
        tower.workerExit = Point("WorkerExit", new Vector3(-2.5f, 0, 3.7f), 0f, car);
        // its way out on floor 6: through the middle of the doorway (which is 2.4 m wide), clear of the landing
        // wall, and only then aside into the corridor
        tower.workerPath = new[] { O + new Vector3(-0.35f, 0, 1.0f), O + new Vector3(-0.35f, 0, 3.1f), O + new Vector3(-2.5f, 0, 3.7f) };

        // ================================================================ floor 6: a corridor full of police
        var hallway = new GameObject("Floor6_PoliceCorridor");
        var h = hallway.transform;
        h.position = O;
        Part("CorridorFloor", new Vector3(0, -0.1f, 10.05f), new Vector3(7.4f, 0.2f, 16.2f), cHallFloor, true, h);
        Part("CorridorCeiling", new Vector3(0, 3.6f, 10.05f), new Vector3(7.4f, 0.2f, 16.2f), mPlate, false, h);
        Part("CorridorEnd", new Vector3(0, 1.8f, 18.2f), new Vector3(7.6f, 3.8f, 0.2f), mHull, true, h);
        foreach (float s in new[] { -1f, 1f })
        {
            Part("CorridorWall", new Vector3(s * 3.7f, 1.8f, 10.05f), new Vector3(0.2f, 3.8f, 16.2f), mHull, true, h);
            Part("CorridorWallLight", new Vector3(s * 3.58f, 2.4f, 10.05f), new Vector3(0.04f, 0.12f, 15f), redGlow, false, h);
        }
        for (int i = 0; i < 4; i++)
            Part("CorridorCeilingLight", new Vector3(0, 3.48f, 4.5f + i * 3.6f), new Vector3(4.6f, 0.05f, 0.3f), i % 2 == 0 ? redGlow : cData, false, h);
        PointLight("CorridorRed", O + new Vector3(-1.5f, 2.9f, 6f), red, 6f, 10f, h);
        PointLight("CorridorBlue", O + new Vector3(1.5f, 2.9f, 12f), new Color32(70, 130, 255, 255), 6f, 10f, h);
        WorldText("CorridorSign", "FLOOR 6", O + new Vector3(0, 3.0f, 18.05f), Quaternion.identity, 0.0052f, 96, red, h);
        var spots = new (float x, float z, float yaw)[]
        {
            (-1.3f, 4.6f, 90f), (1.4f, 5.4f, 250f), (-2.4f, 7.2f, 20f), (0.4f, 8.2f, 320f),
            (2.4f, 9.8f, 270f), (-1.2f, 11.4f, 60f), (1.2f, 13.2f, 0f), (-2.6f, 14.6f, 110f),
        };
        var police = new List<Transform>();
        var icons = new List<AlertIcon>();
        foreach (var spot in spots)
        {
            var unit = Dummy(h, "CorridorPolice_" + police.Count, "Blue", O + new Vector3(spot.x, 0, spot.z), spot.yaw);
            police.Add(unit.transform);
            icons.Add(AlertMark(unit.transform, 2.3f));
        }
        tower.police = police.ToArray();
        tower.policeIcons = icons.ToArray();
        tower.hallway = hallway;
        hallway.SetActive(false);

        // ================================================================ floor 10: the secure room and its electric gate
        var room = new GameObject("Floor10_SecureRoom");
        var r = room.transform;
        r.position = O;
        const float gz = 10.4f;                                        // the gate's line across the room
        // the room ends at the gate's wall; the colossus's hall begins on the other side of it
        Part("RoomFloor", new Vector3(0, -0.1f, 6.275f), new Vector3(9.6f, 0.2f, 8.65f), cHallFloor, true, r);
        Part("RoomCeiling", new Vector3(0, 4.2f, 6.275f), new Vector3(9.6f, 0.2f, 8.65f), mPlate, false, r);
        foreach (float s in new[] { -1f, 1f })
        {
            Part("RoomWall", new Vector3(s * 4.8f, 2.1f, 6.275f), new Vector3(0.2f, 4.4f, 8.65f), mHull, true, r);
            Part("RoomWallBand", new Vector3(s * 4.68f, 3.3f, 6f), new Vector3(0.04f, 0.1f, 8f), cData, false, r);
            Part("GateWall", new Vector3(s * 3.2f, 2.1f, gz), new Vector3(3.2f, 4.4f, 0.4f), mPlate, true, r);
            Part("GateWallLight", new Vector3(s * 1.7f, 2.0f, gz - 0.22f), new Vector3(0.08f, 3.6f, 0.06f), cData, false, r);
        }
        Part("GateHead", new Vector3(0, 3.95f, gz), new Vector3(3.2f, 0.7f, 0.4f), mPlate, false, r);
        for (int i = 0; i < 3; i++)
            Part("RoomCeilingLight", new Vector3(0, 4.08f, 3.6f + i * 2.6f), new Vector3(5f, 0.05f, 0.3f), cLane, false, r);
        PointLight("RoomLamp", O + new Vector3(0, 3.4f, 4.6f), cool, 7f, 11f, r);
        PointLight("RoomLampFar", O + new Vector3(0, 3.4f, 8.2f), cool, 6f, 10f, r);

        // the gate: a rack of live bars that lifts as one piece
        var gate = Point("ElectricGate", new Vector3(0, 0, gz), 0f, r);
        var gateSolid = gate.gameObject.AddComponent<BoxCollider>();
        gateSolid.center = new Vector3(0, 1.8f, 0);
        gateSolid.size = new Vector3(3.2f, 3.6f, 0.3f);
        for (int i = -4; i <= 4; i++)
            Part("GateBar", new Vector3(i * 0.36f, 1.8f, gz), new Vector3(0.07f, 3.6f, 0.07f), cData, false, gate);
        Part("GateRailLow", new Vector3(0, 0.07f, gz), new Vector3(3.2f, 0.14f, 0.16f), mPlate, false, gate);
        Part("GateRailHigh", new Vector3(0, 3.53f, gz), new Vector3(3.2f, 0.14f, 0.16f), mPlate, false, gate);
        tower.gate = gate;
        tower.gateRise = 3.7f;
        tower.gateLight = PointLight("GateGlow", O + new Vector3(0, 2f, gz - 0.9f), DoorCyan, 5f, 7f, r);

        // its keypad, on a post to the right
        Part("KeypadPost", new Vector3(2.35f, 0.75f, gz - 0.65f), new Vector3(0.5f, 1.5f, 0.3f), mPlate, true, r);
        Part("KeypadFace", new Vector3(2.35f, 1.38f, gz - 0.815f), new Vector3(0.46f, 0.56f, 0.04f), mHull, false, r);
        tower.keypadDisplay = WorldText("KeypadDisplay", "----", O + new Vector3(2.35f, 1.585f, gz - 0.84f), Quaternion.identity, 0.00085f, 96,
                                        Amber, r);
        var keys = new List<Renderer>();
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 3; col++)
            {
                var at = new Vector3(2.35f + (col - 1) * 0.12f, 1.47f - row * 0.08f, gz - 0.84f);
                Part("Key", at, new Vector3(0.09f, 0.06f, 0.02f), mPlate, false, r);
                var lit = Part("KeyLit", at + new Vector3(0, 0, -0.006f), new Vector3(0.09f, 0.06f, 0.02f), cElevator, false, r).GetComponent<Renderer>();
                lit.enabled = false;
                keys.Add(lit);
            }
        tower.keypadKeys = keys.ToArray();
        tower.keypadLook = Point("KeypadLook", new Vector3(2.35f, 1.4f, gz - 0.82f), 0f, r);
        // E at the post brings the keypad up on screen; the player types the code they read at the workstation
        tower.keypad = BuildKeypad(hud.transform, hud, controller, tower.keypadDisplay);
        var keypadGo = Point("GateKeypad", new Vector3(2.35f, 0, gz - 1.0f), 0f, r).gameObject;
        tower.gateKeypad = keypadGo.AddComponent<GateKeypad>();
        tower.gateKeypad.range = 2.3f;
        tower.gateKeypad.keypad = tower.keypad;
        PointLight("KeypadGlow", O + new Vector3(2.35f, 1.7f, gz - 1.3f), Amber, 1.6f, 2.6f, r);

        // ================================================================ beyond the gate: the colossus's hall and the kill switch
        const float ax = 13f, az0 = gz + 0.2f, az1 = gz + 28.2f, ah = 10f;      // half width, front, back, height
        float amid = (az0 + az1) / 2f, adepth = az1 - az0;
        var arena = Point("BossArena", Vector3.zero, 0f, r);
        for (int ix = 0; ix < 2; ix++)
            for (int iz = 0; iz < 2; iz++)
                Part("ArenaFloor", new Vector3((ix - 0.5f) * ax, -0.1f, az0 + (iz + 0.5f) * adepth / 2f), new Vector3(ax, 0.2f, adepth / 2f), cForecourt, true, arena);
        // the ceiling, with a square hole over the exit ladder: the hatch to the roof is a real opening
        const float hatchX = 7.5f, hatchHalf = 0.9f;
        float hatchZ = az1 - 1.05f;
        Part("ArenaCeiling", new Vector3((hatchX - hatchHalf - ax) / 2f, ah + 0.1f, amid), new Vector3(hatchX - hatchHalf + ax, 0.2f, adepth), mPlate, false, arena);
        Part("ArenaCeiling", new Vector3((hatchX + hatchHalf + ax) / 2f, ah + 0.1f, amid), new Vector3(ax - hatchX - hatchHalf, 0.2f, adepth), mPlate, false, arena);
        Part("ArenaCeiling", new Vector3(hatchX, ah + 0.1f, (az0 + hatchZ - hatchHalf) / 2f), new Vector3(hatchHalf * 2f, 0.2f, hatchZ - hatchHalf - az0), mPlate, false, arena);
        Part("ArenaCeiling", new Vector3(hatchX, ah + 0.1f, (az1 + hatchZ + hatchHalf) / 2f), new Vector3(hatchHalf * 2f, 0.2f, az1 - hatchZ - hatchHalf), mPlate, false, arena);
        Part("ArenaBack", new Vector3(0, ah / 2f, az1 + 0.1f), new Vector3(ax * 2f + 0.4f, ah, 0.2f), mHull, true, arena);
        Part("ArenaFrontHigh", new Vector3(0, 4.3f + (ah - 4.3f) / 2f, gz), new Vector3(9.8f, ah - 4.3f, 0.4f), mHull, false, arena);    // over the gate
        foreach (float s in new[] { -1f, 1f })
        {
            Part("ArenaWall", new Vector3(s * (ax + 0.1f), ah / 2f, amid), new Vector3(0.2f, ah, adepth), mHull, true, arena);
            Part("ArenaFront", new Vector3(s * (4.9f + (ax - 4.9f) / 2f), ah / 2f, gz), new Vector3(ax - 4.9f, ah, 0.4f), mHull, true, arena);
            Part("ArenaWallBand", new Vector3(s * (ax - 0.02f), 3.2f, amid), new Vector3(0.05f, 0.14f, adepth - 2f), redGlow, false, arena);
            Part("ArenaWallBandHigh", new Vector3(s * (ax - 0.02f), 7.2f, amid), new Vector3(0.05f, 0.1f, adepth - 2f), cData, false, arena);
            // ribs up the side walls
            for (int i = 0; i < 4; i++)
                Part("ArenaRib", new Vector3(s * (ax - 0.25f), ah / 2f, az0 + 3.5f + i * 7f), new Vector3(0.5f, ah, 0.8f), mPlate, false, arena);
        }
        for (int i = 0; i < 3; i++)
            Part("ArenaCeilingLight", new Vector3(0, ah - 0.03f, az0 + 5f + i * 9f), new Vector3(12f, 0.05f, 0.4f), cLane, false, arena);
        var arenaLampFront = PointLight("ArenaLampFront", O + new Vector3(0, 7.5f, az0 + 7f), cool, 9f, 18f, arena);
        var arenaLampBack = PointLight("ArenaLampBack", O + new Vector3(0, 7.5f, az1 - 7f), cool, 9f, 18f, arena);
        MapRect("BossArena", O.x, O.z + amid, ax * 2f, adepth, Flat("Map_HallFloor", new Color32(24, 52, 70, 255)), MapY + 0.06f);

        var identity = controller.GetComponent<PlayerIdentity>();
        var fight = systems.AddComponent<BossFight>();
        fight.flow = flow;
        fight.hud = hud;
        fight.player = controller;
        fight.dodge = hudDodge;
        fight.fx = fx;
        fight.arenaEntry = Point("ArenaEntry", new Vector3(0, 0.05f, az0 + 1.6f), 0f, arena);
        fight.bossHome = Point("BossHome", new Vector3(0, 0, amid + 2f), 180f, arena);

        // the colossus: kneeling in the middle of its hall, facing the gate
        var bossGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CityDir + "/Robot_Boss.fbx"));
        bossGo.name = "Colossus";
        bossGo.transform.SetParent(arena, false);
        bossGo.transform.SetPositionAndRotation(fight.bossHome.position, fight.bossHome.rotation);
        SetLayer(bossGo, Layers.Robot);
        var bossBody = bossGo.AddComponent<CapsuleCollider>();
        bossBody.center = new Vector3(0, 2.9f, 0);
        bossBody.height = 5.8f;
        bossBody.radius = 1.25f;
        Solidify(bossGo);
        var boss = bossGo.AddComponent<BossController>();
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
        // its sword, in the left hand: the blade runs on from the forearm (which hangs along the hand's own -y)
        var handL = Bone("Hips/Chest/UpperArm.L/Forearm.L/Hand.L");
        if (handL == null) handL = boss.forearmL;
        var sword = new GameObject("Sword").transform;
        sword.SetParent(handL, false);
        sword.localScale = Vector3.one / Mathf.Max(0.001f, handL.lossyScale.y);       // built in metres, whatever the rig's own scale
        void Blade(string name, Vector3 at, Vector3 size, string material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.name = name;
            piece.transform.SetParent(sword, false);
            piece.transform.localPosition = at;
            piece.transform.localScale = size;
            piece.layer = LayerMask.NameToLayer(Layers.Robot);
            piece.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/" + material + ".mat");
        }
        Blade("Grip", new Vector3(0, -0.15f, 0), new Vector3(0.24f, 1.0f, 0.24f), "Boss_Dark");
        Blade("Guard", new Vector3(0, -0.72f, 0), new Vector3(1.5f, 0.2f, 0.42f), "Boss_Bronze");
        Blade("GuardGlow", new Vector3(0, -0.72f, 0), new Vector3(0.5f, 0.24f, 0.46f), "Boss_Glow");
        Blade("Blade", new Vector3(0, -2.75f, 0), new Vector3(0.56f, 3.9f, 0.12f), "Boss_Iron");
        Blade("BladeSpine", new Vector3(0, -2.75f, 0), new Vector3(0.12f, 3.8f, 0.17f), "Boss_Glow");
        Blade("BladeTip", new Vector3(0, -4.85f, 0), new Vector3(0.36f, 0.4f, 0.1f), "Boss_Iron");

        boss.glowRenderers = bossGo.GetComponentsInChildren<MeshRenderer>();
        boss.coreLight = PointLight("CoreLight", Vector3.zero, Amber, 5f, 9f, boss.chest);
        boss.coreLight.transform.localPosition = new Vector3(0, 0.3f, 1.4f);
        boss.player = controller.transform;
        boss.arena = new Bounds(O + new Vector3(0, ah / 2f, amid), new Vector3(17f, ah, adepth - 9f));
        fight.boss = boss;
        fight.voice = bossGo.AddComponent<BossAudio>();
        fight.maskRules = maskRules;
        fight.arenaLamps = new[] { arenaLampFront, arenaLampBack };

        // four batteries in the corners, each with a live cable running across the floor to where it stands
        var cellGlow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/Mono_GlowBattery.mat");
        var batteries = new List<PowerBattery>();
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var at = new Vector3(sx * (ax - 2f), 0, amid + sz * (adepth / 2f - 2.4f));
                var socket = Point("PowerBattery_" + batteries.Count, at, 0f, arena).gameObject;
                // (the socket plate goes with the battery, so a corner that has been emptied is left quite bare)
                var socketPlate = Part("BatterySocket", at + new Vector3(0, 0.1f, 0), new Vector3(1.6f, 0.2f, 1.6f), mPlate, true, socket.transform);
                var cell = CityProp("Mono_Battery", O + at + new Vector3(0, 0.2f, 0), 0f, 1.15f, false, socket.transform);
                var cellSolid = cell.AddComponent<BoxCollider>();
                cellSolid.center = new Vector3(0, 1.0f, 0);
                cellSolid.size = new Vector3(1.0f, 2.0f, 1.0f);
                var battery = socket.AddComponent<PowerBattery>();
                battery.range = 3.0f;
                battery.identity = identity;
                battery.hud = hud;
                battery.fight = fight;
                battery.cell = cell.transform;
                battery.glow = PointLight("BatteryGlow", O + at + new Vector3(-sx * 0.9f, 1.6f, -sz * 0.9f), Mint, 4f, 6f, socket.transform);
                // the cable: along the side wall's foot to the boss's line, then in to the middle
                var cable = Point("Cable", Vector3.zero, 0f, socket.transform);
                float zc = fight.bossHome.position.z - O.z + sz * 1.2f;
                Part("CableRun", new Vector3(at.x, 0.02f, (at.z + zc) / 2f), new Vector3(0.16f, 0.04f, Mathf.Abs(at.z - zc)), cellGlow, false, cable);
                Part("CableIn", new Vector3(at.x / 2f, 0.02f, zc), new Vector3(Mathf.Abs(at.x), 0.04f, 0.16f), cellGlow, false, cable);
                battery.alsoOff = new[] { cable.gameObject, socketPlate };
                // an arrow hovers over it, pointing down at it, for as long as it is in place and the fight is on:
                // big, lit and turning, so all four corners can be picked out from anywhere in the hall
                var pointer = Prop("Arrow", O + at + new Vector3(0, 3.1f, 0), 0f, Vector3.one * 2.4f, null, socket.transform);
                pointer.name = "BatteryArrow";
                foreach (var arrowRenderer in pointer.GetComponentsInChildren<MeshRenderer>())
                {
                    arrowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    arrowRenderer.receiveShadows = false;
                }
                var bob = pointer.AddComponent<Hover>();
                bob.spin = 80f;
                bob.amplitude = 0.22f;
                bob.period = 1.2f;
                PointLight("BatteryArrowLight", O + at + new Vector3(0, 3.7f, 0), Amber, 2.2f, 5f, pointer.transform);
                pointer.SetActive(false);
                battery.marker = pointer;
                batteries.Add(battery);
            }
        fight.batteries = batteries.ToArray();

        // the kill switch against the back wall, behind a shield that only the colossus's fall takes down:
        // a lever standing in a slotted housing, pulled down toward the room (see KillSwitch)
        var switchAt = new Vector3(0, 0, az1 - 1.1f);
        var switchGo = Point("KillSwitch", switchAt, 0f, arena).gameObject;
        var switchSolid = switchGo.AddComponent<BoxCollider>();
        switchSolid.center = new Vector3(0, 0.6f, 0);
        switchSolid.size = new Vector3(1.5f, 1.2f, 1.0f);
        var lever = switchGo.transform;
        Part("LeverHousing", switchAt + new Vector3(0, 0.55f, 0), new Vector3(1.5f, 1.1f, 1.0f), mPlate, false, lever);
        Part("LeverHousingFoot", switchAt + new Vector3(0, 0.06f, 0), new Vector3(1.8f, 0.12f, 1.3f), mHull, false, lever);
        Part("LeverTop", switchAt + new Vector3(0, 1.13f, 0), new Vector3(1.6f, 0.06f, 1.1f), trim, false, lever);
        Part("LeverSlot", switchAt + new Vector3(0, 1.165f, 0), new Vector3(0.14f, 0.012f, 0.86f), mHull, false, lever);
        Part("LeverBackplate", switchAt + new Vector3(0, 1.6f, 0.58f), new Vector3(1.9f, 2.0f, 0.1f), mHull, false, lever);
        foreach (float s in new[] { -1f, 1f })
        {
            Part("LeverCheek", switchAt + new Vector3(s * 0.16f, 1.31f, 0), new Vector3(0.08f, 0.3f, 0.9f), trim, false, lever);      // the guides either side of the slot
            Part("LeverHousingLight", switchAt + new Vector3(s * 0.69f, 0.55f, -0.505f), new Vector3(0.06f, 0.8f, 0.02f), cData, false, lever);
            Part("LeverBackplateLight", switchAt + new Vector3(s * 0.86f, 1.6f, 0.525f), new Vector3(0.06f, 1.7f, 0.02f), cData, false, lever);
        }
        // its lamp: red until the lever is down, then live
        var leverLamp = Part("LeverLamp", switchAt + new Vector3(0, 0.74f, -0.505f), new Vector3(0.7f, 0.2f, 0.03f), redGlow, false, lever);
        // the arm, on its own pivot at the top of the housing: a steel bar with a red grip across its end
        var leverArm = Point("LeverArm", switchAt + new Vector3(0, 1.2f, 0), 0f, lever);
        Part("LeverHub", switchAt + new Vector3(0, 1.2f, 0), new Vector3(0.2f, 0.16f, 0.16f), mHull, false, leverArm);
        Part("LeverBar", switchAt + new Vector3(0, 1.62f, 0), new Vector3(0.08f, 0.84f, 0.08f), trim, false, leverArm);
        Part("LeverGrip", switchAt + new Vector3(0, 2.1f, 0), new Vector3(0.44f, 0.14f, 0.14f), redGlow, false, leverArm);
        var killSwitch = switchGo.AddComponent<KillSwitch>();
        killSwitch.range = 2.8f;
        killSwitch.flow = flow;
        killSwitch.arm = leverArm;
        leverArm.localRotation = Quaternion.Euler(killSwitch.upAngle, 0f, 0f);       // up: leaning back toward the wall
        killSwitch.lamp = leverLamp.GetComponent<Renderer>();
        killSwitch.liveMaterial = cPortalLive;
        killSwitch.liveColour = Mint;
        fight.killSwitch = killSwitch;
        Part("SwitchDais", switchAt + new Vector3(0, 0.05f, -0.4f), new Vector3(6f, 0.1f, 4f), mPlate, false, arena);
        Part("SwitchDaisEdge", switchAt + new Vector3(0, 0.11f, -2.36f), new Vector3(6f, 0.03f, 0.1f), redGlow, false, arena);
        WorldText("KillSwitchSign", "KILL SWITCH", O + new Vector3(0, 5.6f, az1 - 0.05f), Quaternion.identity, 0.011f, 96, red, arena);
        killSwitch.glow = PointLight("KillSwitchGlow", O + switchAt + new Vector3(0, 2.6f, -1.6f), red, 6f, 8f, arena);
        var shield = Part("SwitchShield", switchAt + new Vector3(0, 1.7f, -0.3f), new Vector3(3.4f, 3.4f, 2.6f), cShield, true, arena);
        shield.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        foreach (float s in new[] { -1f, 1f })
            Part("ShieldPost", switchAt + new Vector3(s * 1.75f, 1.7f, -1.6f), new Vector3(0.12f, 3.4f, 0.12f), redGlow, false, shield.transform);
        fight.shield = shield;

        // where the fist will land: a red disc on the floor; and the rings its blow throws out
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.name = "SlamTelegraph";
        disc.transform.SetParent(arena, false);
        var discRenderer = disc.GetComponent<MeshRenderer>();
        discRenderer.sharedMaterial = cTelegraph;
        discRenderer.shadowCastingMode = ShadowCastingMode.Off;
        discRenderer.receiveShadows = false;
        fight.telegraph = disc.transform;
        // a second disc round its own feet (the slam hurts there too), and a fan for the sword's sweep
        var feet = Object.Instantiate(disc, arena);
        feet.name = "SlamTelegraphFeet";
        fight.telegraphBody = feet.transform;
        var fan = new GameObject("SweepTelegraph");
        fan.transform.SetParent(arena, false);
        fan.AddComponent<MeshFilter>().sharedMesh = SweepFanMesh(boss.sweepArc);
        var fanRenderer = fan.AddComponent<MeshRenderer>();
        fanRenderer.sharedMaterial = cTelegraph;
        fanRenderer.shadowCastingMode = ShadowCastingMode.Off;
        fanRenderer.receiveShadows = false;
        fight.telegraphArc = fan.transform;
        // (the rings are no wider than the blow itself: what is drawn is what hurts)
        fight.waves = BuildShockwaves();
        fight.waves[0].radius = boss.slamRadius + 0.4f;
        fight.waves[0].colour = Amber;
        fight.waves[1].radius = boss.bodyRadius;
        fight.waves[1].colour = red;
        foreach (var wave in fight.waves) wave.name = "Boss" + wave.name;
        flow.boss = fight;

        // the way out: a ladder up the back wall, through the hole in the ceiling and on up a short shaft to the lit
        // hatch in the roof. Nobody uses it with E: pulling the kill switch sends the player up it (see CityFlow).
        var ladderAt = new Vector3(hatchX, 0, az1 - 0.25f);
        float shaftTop = ah + 3.4f, shaftHigh = shaftTop - ah - 0.2f, shaftMid = ah + 0.2f + shaftHigh / 2f;
        var ladderGo = CityProp("Mono_Ladder", O + ladderAt, 180f, ah / 6f, false, arena);
        ladderGo.name = "ExitLadder";
        ladderGo.transform.localScale = new Vector3(ah / 6f, (shaftTop - 0.1f) / 6f, ah / 6f);      // it runs on up the shaft
        var ladder = ladderGo.AddComponent<ExitLadder>();
        ladder.rung = 0.3f * (shaftTop - 0.1f) / 6f;
        ladder.secondsPerRung = 0.29f;
        ladder.foot = Point("LadderFoot", ladderAt + new Vector3(0, 0.05f, -0.6f), 0f, arena);
        ladder.top = Point("LadderTop", ladderAt + new Vector3(0, ah - 0.9f, -0.6f), 0f, arena);
        flow.ladder = ladder;
        foreach (float s in new[] { -1f, 1f })
        {
            Part("HatchShaft", new Vector3(hatchX + s * (hatchHalf + 0.05f), shaftMid, hatchZ), new Vector3(0.1f, shaftHigh, hatchHalf * 2f + 0.2f), mHull, false, arena);
            Part("HatchShaft", new Vector3(hatchX, shaftMid, hatchZ + s * (hatchHalf + 0.05f)), new Vector3(hatchHalf * 2f, shaftHigh, 0.1f), mHull, false, arena);
            Part("HatchShaftLight", new Vector3(hatchX + s * (hatchHalf - 0.01f), shaftMid, hatchZ - 0.5f), new Vector3(0.02f, shaftHigh - 0.4f, 0.08f), cElevator, false, arena);
            Part("HatchRim", new Vector3(hatchX + s * hatchHalf, ah - 0.02f, hatchZ), new Vector3(0.08f, 0.05f, hatchHalf * 2f + 0.08f), cElevator, false, arena);
            Part("HatchRim", new Vector3(hatchX, ah - 0.02f, hatchZ + s * hatchHalf), new Vector3(hatchHalf * 2f + 0.08f, 0.05f, 0.08f), cElevator, false, arena);
        }
        Part("Hatch", new Vector3(hatchX, shaftTop, hatchZ), new Vector3(hatchHalf * 2f, 0.06f, hatchHalf * 2f), cElevator, false, arena);      // lit, at the top of the shaft
        PointLight("HatchLight", O + new Vector3(hatchX, ah + 2.2f, hatchZ), Amber, 4f, 7f, arena);
        WorldText("LadderSign", "ROOF ACCESS", O + ladderAt + new Vector3(0, 3.2f, 0.2f) + new Vector3(1.9f, 0, 0), Quaternion.identity, 0.0034f, 96, Amber, arena);
        PointLight("LadderGlow", O + ladderAt + new Vector3(0, 3f, -1.2f), Amber, 3f, 6f, arena);

        // the workstation on the west wall, with the quest marker over it
        Part("Desk", new Vector3(-4.2f, 0.39f, 6.2f), new Vector3(1.0f, 0.78f, 2.2f), cDesk, true, r);
        Part("Monitor", new Vector3(-4.5f, 1.26f, 6.2f), new Vector3(0.08f, 0.7f, 1.16f), mHull, false, r);
        Part("MonitorStand", new Vector3(-4.5f, 0.86f, 6.2f), new Vector3(0.1f, 0.18f, 0.24f), mHull, false, r);
        Part("ScreenOff", new Vector3(-4.452f, 1.26f, 6.2f), new Vector3(0.012f, 0.6f, 1.06f), mPlate, false, r);
        var screen = Part("ScreenOn", new Vector3(-4.44f, 1.26f, 6.2f), new Vector3(0.012f, 0.6f, 1.06f), cLane, false, r).GetComponent<Renderer>();
        screen.enabled = false;
        tower.workstationScreen = screen;
        Part("Keyboard", new Vector3(-4.0f, 0.8f, 6.2f), new Vector3(0.24f, 0.03f, 0.72f), mHull, false, r);
        Part("Stool", new Vector3(-3.15f, 0.24f, 6.2f), new Vector3(0.44f, 0.48f, 0.44f), mPlate, false, r);
        tower.seat = Point("Seat", new Vector3(-3.05f, 0.05f, 6.2f), 270f, r);
        tower.screenCentre = Point("ScreenCentre", new Vector3(-4.44f, 1.26f, 6.2f), 0f, r);
        var marker = Prop("Arrow", O + new Vector3(-4.2f, 2.3f, 6.2f), 0f, Vector3.one * 1.3f, null, r);
        marker.name = "QuestMarker";
        var hover = marker.AddComponent<Hover>();
        hover.spin = 90f;
        hover.amplitude = 0.1f;
        hover.period = 1.4f;
        tower.questMarker = marker;
        WorldText("WorkstationSign", "WORKSTATION", O + new Vector3(-4.68f, 2.2f, 6.2f), Quaternion.Euler(0, -90f, 0), 0.0026f, 96, Amber, r);
        // the desk is its own thing to use (E). Until the face scan across the room is passed it only refuses:
        // a red screen that stays up, saying what it wants
        var deskGo = Point("Workstation", new Vector3(-3.5f, 0, 6.2f), 0f, r).gameObject;
        tower.workstation = deskGo.AddComponent<DeskComputer>();
        tower.workstation.range = 2.2f;
        tower.workstation.tower = tower;
        var denied = Point("ScreenDenied", Vector3.zero, 0f, r);
        Part("DeniedGlow", new Vector3(-4.43f, 1.26f, 6.2f), new Vector3(0.012f, 0.6f, 1.06f), redGlow, false, denied);
        WorldText("DeniedTitle", "ACCESS DENIED", O + new Vector3(-4.42f, 1.36f, 6.2f), Quaternion.Euler(0, -90f, 0), 0.0011f, 96, new Color32(20, 4, 4, 255), denied);
        WorldText("DeniedLine", "SCAN FACE FIRST", O + new Vector3(-4.42f, 1.16f, 6.2f), Quaternion.Euler(0, -90f, 0), 0.0008f, 96, new Color32(20, 4, 4, 255), denied);
        denied.gameObject.SetActive(false);
        tower.deniedScreen = denied.gameObject;
        // until the scan is passed the monitor says so itself: locked, and what it wants
        var locked = Point("ScreenLocked", Vector3.zero, 0f, r);
        WorldText("LockedTitle", "LOCKED", O + new Vector3(-4.44f, 1.37f, 6.2f), Quaternion.Euler(0, -90f, 0), 0.0012f, 96, Amber, locked);
        WorldText("LockedLine", "FACE SCAN REQUIRED", O + new Vector3(-4.44f, 1.17f, 6.2f), Quaternion.Euler(0, -90f, 0), 0.00062f, 96, new Color32(244, 236, 214, 220), locked);
        Part("LockedBar", new Vector3(-4.443f, 1.27f, 6.2f), new Vector3(0.004f, 0.012f, 0.7f), cElevator, false, locked);
        tower.lockedScreen = locked.gameObject;
        SpotLight("DeskSpot", O + new Vector3(-3.2f, 4.0f, 6.2f), O + new Vector3(-4.1f, 0.8f, 6.2f), new Color32(255, 200, 120, 255), 14f, 6f, 54f, r);

        // the Face Scan: a separate machine on the opposite (east) wall
        Part("ScanPillar", new Vector3(4.4f, 1.0f, 4.1f), new Vector3(0.5f, 2.0f, 0.62f), mPlate, true, r);
        Part("ScanHood", new Vector3(4.2f, 1.62f, 4.1f), new Vector3(0.14f, 0.5f, 0.5f), mHull, false, r);
        Part("ScanLens", new Vector3(4.12f, 1.62f, 4.1f), new Vector3(0.03f, 0.34f, 0.34f), cData, false, r);
        Part("ScanFootplate", new Vector3(3.5f, 0.012f, 4.1f), new Vector3(1.0f, 0.02f, 1.0f), cData, false, r);      // stand here
        foreach (float s in new[] { -1f, 1f })
            Part("ScanArm", new Vector3(4.3f, 1.62f, 4.1f + s * 0.42f), new Vector3(0.5f, 0.08f, 0.08f), mHull, false, r);
        WorldText("FaceScanSign", "FACE SCAN", O + new Vector3(4.12f, 2.16f, 4.1f), Quaternion.Euler(0, 90f, 0), 0.0022f, 96, DoorCyan, r);
        tower.scanEye = Point("ScanEye", new Vector3(4.12f, 1.62f, 4.1f), 0f, r);
        tower.scanLight = PointLight("ScanLight", O + new Vector3(3.6f, 1.62f, 4.1f), DoorCyan, 4f, 3.6f, r);
        tower.scanLight.enabled = false;
        var scanGo = Point("FaceScan", new Vector3(4.05f, 0, 4.1f), 0f, r).gameObject;
        tower.faceScan = scanGo.AddComponent<FaceScanTerminal>();
        tower.faceScan.range = 2.3f;
        tower.faceScan.tower = tower;

        // ================================================================ dressing: the secure room
        // server racks along both side walls, each with its column of lights
        foreach (var (x, z) in new[] { (-4.3f, 8.3f), (-4.3f, 9.5f), (4.3f, 6.2f), (4.3f, 7.4f), (4.3f, 8.6f) })
        {
            float inward = -Mathf.Sign(x);
            Part("ServerRack", new Vector3(x, 1.35f, z), new Vector3(0.8f, 2.7f, 1.1f), mPlate, true, r);
            Part("ServerRackLights", new Vector3(x + inward * 0.41f, 1.45f, z), new Vector3(0.03f, 1.9f, 0.7f), cData, false, r);
            Part("ServerRackStatus", new Vector3(x + inward * 0.41f, 2.55f, z), new Vector3(0.03f, 0.1f, 0.9f), cElevator, false, r);
        }
        // cable trays and pipes under the ceiling
        foreach (float s in new[] { -1f, 1f })
        {
            Part("CableTray", new Vector3(s * 2.6f, 3.92f, 6.2f), new Vector3(0.5f, 0.1f, 8.2f), mHull, false, r);
            Part("CableTrayLine", new Vector3(s * 2.6f, 3.86f, 6.2f), new Vector3(0.06f, 0.03f, 8f), cData, false, r);
            for (int i = 0; i < 4; i++)
                Part("CableDrop", new Vector3(s * 2.6f, 3.6f, 3.2f + i * 2f), new Vector3(0.05f, 0.6f, 0.05f), mPlate, false, r);
        }
        Prop("PipeRun", O + new Vector3(-4.72f, 3.3f, 5.2f), 90f, null, null, r);
        Prop("PipeRun", O + new Vector3(4.72f, 3.3f, 5.6f), -90f, null, null, r);
        // a beacon over the gate
        Part("GateBeacon", new Vector3(0, 4.05f, gz - 0.5f), new Vector3(0.5f, 0.16f, 0.3f), redGlow, false, r);

        // ---- the floor's crew, at work in the corner the player comes in by (clear of the way to the workstation,
        //      the Face Scan and the keypad): two typing at a short row of consoles on the west wall, one writing
        //      on a plain lit board beside the elevator door, and one carrying a box round the west server racks
        var crew = Point("Crew", Vector3.zero, 0f, r);
        foreach (float cz in new[] { 3.0f, 4.2f })
        {
            Prop("Console", O + new Vector3(-4.3f, 0, cz), 90f, null, new Vector3(1.0f, 1.3f, 0.5f), r);
            var typist = Civilian(crew, "Crew_Typist", O + new Vector3(-3.52f, 0, cz), 270f);
            typist.GetComponent<RobotGesture>().gesture = Gesture.Typing;
        }
        Part("CrewBoard", new Vector3(-2.3f, 1.9f, 2.08f), new Vector3(2.0f, 1.5f, 0.06f), mPlate, false, r);
        Part("CrewBoardGlow", new Vector3(-2.3f, 1.9f, 2.115f), new Vector3(1.8f, 1.3f, 0.02f), cLane, false, r);
        var writer = Civilian(crew, "Crew_Writer", O + new Vector3(-2.2f, 0, 2.86f), 180f);
        writer.GetComponent<RobotGesture>().gesture = Gesture.Writing;
        Carrier(crew, "Crew_Carrier", 0.9f, 0, O + new Vector3(-3.2f, 0, 7.9f), O + new Vector3(-3.2f, 0, 9.6f),
                O + new Vector3(-2.2f, 0, 9.6f), O + new Vector3(-2.2f, 0, 7.9f));

        // ================================================================ dressing: the colossus's hall
        // (its floor is left bare: nothing is painted on it, and nothing lies about on it)
        Vector3 post = new Vector3(0, 0, amid + 2f);
        // catwalks along both side walls, with rails and brackets
        foreach (float s in new[] { -1f, 1f })
        {
            float cx = s * (ax - 0.95f);
            Part("Catwalk", new Vector3(cx, 5.6f, amid), new Vector3(1.5f, 0.14f, adepth - 3f), mPlate, false, arena);
            Part("CatwalkEdge", new Vector3(cx - s * 0.74f, 5.69f, amid), new Vector3(0.06f, 0.05f, adepth - 3f), cData, false, arena);
            Part("CatwalkRail", new Vector3(cx - s * 0.74f, 6.6f, amid), new Vector3(0.06f, 0.06f, adepth - 3f), mHull, false, arena);
            for (int i = 0; i < 9; i++)
            {
                float z = az0 + 2.5f + i * (adepth - 5f) / 8f;
                Part("CatwalkPost", new Vector3(cx - s * 0.74f, 6.15f, z), new Vector3(0.06f, 0.9f, 0.06f), mHull, false, arena);
                Part("CatwalkBracket", new Vector3(cx + s * 0.2f, 5.2f, z), new Vector3(1.0f, 0.7f, 0.1f), mHull, false, arena);
            }
        }
        // cables hanging from the roof over its post
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f, length = 2.6f + (i % 3) * 0.9f;
            var top = post + new Vector3(Mathf.Sin(a) * 1.9f, ah - length / 2f, Mathf.Cos(a) * 1.9f);
            Part("HangingCable", top, new Vector3(0.09f, length, 0.09f), mHull, false, arena);
            Part("HangingCableTip", top + new Vector3(0, -length / 2f - 0.08f, 0), new Vector3(0.16f, 0.16f, 0.16f), cData, false, arena);
        }
        Part("RoofHub", post + new Vector3(0, ah - 0.25f, 0), new Vector3(5f, 0.5f, 5f), mPlate, false, arena);
        // coolant tanks either side of the kill switch
        foreach (float s in new[] { -1f, 1f })
        {
            var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = "CoolantTank";
            tank.transform.SetParent(arena, false);
            tank.transform.position = O + new Vector3(s * 4.6f, 1.7f, az1 - 1.3f);
            tank.transform.localScale = new Vector3(1.7f, 1.7f, 1.7f);
            tank.GetComponent<MeshRenderer>().sharedMaterial = mPlate;
            Part("CoolantBand", new Vector3(s * 4.6f, 2.4f, az1 - 2.18f), new Vector3(1.2f, 0.16f, 0.06f), cData, false, arena);
            Part("CoolantBandLow", new Vector3(s * 4.6f, 0.9f, az1 - 2.18f), new Vector3(1.2f, 0.16f, 0.06f), cData, false, arena);
        }
        // spotlights: one down on its post, one on the kill switch
        SpotLight("PostSpot", O + post + new Vector3(0, ah - 0.6f, 0), O + post, cool, 26f, 14f, 62f, arena);
        SpotLight("SwitchSpot", O + new Vector3(0, ah - 0.6f, az1 - 5f), O + switchAt + Vector3.up, red, 22f, 12f, 46f, arena);
        tower.standPoint = Point("StandPoint", new Vector3(0.3f, 0.05f, 6.6f), 32f, r);
        MapRect("SecureRoom", O.x, O.z + 8.2f, 9.6f, 12.5f, Flat("Map_HallFloor", new Color32(24, 52, 70, 255)), MapY + 0.06f);
        MapRect("SecureGate", O.x, O.z + gz, 9.6f, 0.6f, mapMono, MapY + 0.2f);
        tower.floorTen = room;
        room.SetActive(false);

        // the mask, held up to the scanner: a child of the camera, hidden until then
        var mask = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + "/Mask.fbx"));
        mask.name = "ScanMask";
        mask.transform.SetParent(view, false);
        mask.transform.localPosition = new Vector3(0.16f, -0.55f, 0.5f);
        mask.transform.localRotation = Quaternion.identity;
        mask.transform.localScale = Vector3.one * 1.05f;
        foreach (var renderer in mask.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
        mask.SetActive(false);
        tower.maskProp = mask.transform;

        tower.desktop = BuildDesktop(hud.transform, hud);
        return tower;
    }

    /// <summary>A flat fan on the floor, 'arc' degrees wide, one metre in radius, opening toward +z (the sword's sweep, drawn to scale by BossFight).</summary>
    static Mesh SweepFanMesh(float arc)
    {
        const string path = "Assets/Art/FX/SweepFan.asset";
        const int steps = 28;
        var vertices = new Vector3[steps + 2];
        var triangles = new int[steps * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(-arc / 2f, arc / 2f, i / (float)steps) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
        for (int i = 0; i < steps; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }
        System.IO.Directory.CreateDirectory("Assets/Art/FX");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = "SweepFan" };
            AssetDatabase.CreateAsset(mesh, path);
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    /// <summary>A spotlight at 'from' aimed at 'at' (no shadows: these are for mood, and there are several).</summary>
    static Light SpotLight(string name, Vector3 from, Vector3 at, Color colour, float intensity, float range, float angle, Transform parent)
    {
        var light = PointLight(name, from, colour, intensity, range, parent);
        light.type = LightType.Spot;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.55f;
        light.transform.rotation = Quaternion.LookRotation(at - from);
        return light;
    }

    /// <summary>A robot that only stands there for a cutscene: no sight, no route, no collider.</summary>
    static GameObject Dummy(Transform root, string name, string variant, Vector3 position, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Robots/Robot_" + variant + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        SetLayer(go, Layers.Robot);
        return go;
    }

    // ================================================================ the workstation's desktop
    static DesktopUI BuildDesktop(Transform canvas, HudController hud)
    {
        Vector2 tl = new Vector2(0, 1), mid = new Vector2(0.5f, 0.5f), bottom = new Vector2(0.5f, 0f);
        var bar = new Color32(14, 44, 70, 255);
        var window = new Color32(8, 20, 34, 252);
        var go = UiObject("Desktop", canvas);
        go.transform.SetSiblingIndex(hud.fade.transform.GetSiblingIndex());       // above the game HUD, under the fade and the pause menu
        Stretch(go.transform);
        var desktop = go.AddComponent<DesktopUI>();
        desktop.group = go.AddComponent<CanvasGroup>();
        desktop.group.alpha = 0f;
        desktop.group.blocksRaycasts = false;

        // wallpaper and top bar
        var wallpaper = Img("Wallpaper", go.transform, new Color32(6, 18, 32, 255));
        wallpaper.raycastTarget = true;
        Stretch(wallpaper);
        var watermark = Label("Watermark", go.transform, "MONOLITH", 220, TextAnchor.MiddleCenter, new Color32(40, 90, 130, 60), FontStyle.Bold);
        Place(watermark, mid, mid, mid, new Vector2(0, -40), new Vector2(1800, 300));
        watermark.horizontalOverflow = HorizontalWrapMode.Overflow;
        Place(Img("TopBar", go.transform, bar), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 46));
        var os = Label("Os", go.transform, "MONOLITH OS    //    WORKSTATION 10-A    //    SIGNED IN: UNIT 017", 16, TextAnchor.MiddleLeft, DoorCyan, FontStyle.Bold);
        Place(os, tl, tl, tl, new Vector2(28, -12), new Vector2(1200, 22));

        // the folder on the desktop
        var folder = Img("Folder", go.transform, new Color(0, 0, 0, 0));
        folder.raycastTarget = true;
        Place(folder, tl, tl, tl, new Vector2(84, -110), new Vector2(230, 190));
        // (a folder in three layers: the back with its tab, a sheet of paper inside, and the front flap, which drops
        // open from its bottom edge when the folder is clicked)
        var folderBack = new Color32(196, 124, 28, 255);
        Place(Img("Tab", folder.transform, folderBack), tl, tl, tl, new Vector2(52, -14), new Vector2(54, 18));
        Place(Img("Back", folder.transform, folderBack), tl, tl, tl, new Vector2(52, -28), new Vector2(126, 86));
        Place(Img("Paper", folder.transform, Cream), tl, tl, tl, new Vector2(62, -36), new Vector2(106, 60));
        var flap = Img("Flap", folder.transform, Amber);
        desktop.folderFlap = Place(flap, tl, tl, new Vector2(0, 0), new Vector2(52, -114), new Vector2(126, 80));
        var folderName = Label("Name", folder.transform, "PASSWORDS", 17, TextAnchor.UpperCenter, Cream, FontStyle.Bold);
        Place(folderName, tl, tl, tl, new Vector2(0, -126), new Vector2(230, 50));
        var folderButton = folder.gameObject.AddComponent<Button>();
        folderButton.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(folderButton.onClick, new UnityAction(desktop.OpenFolder));
        folder.gameObject.AddComponent<MenuItemHighlight>().label = folderName;        // its name lights up under the pointer
        desktop.folderIcon = folder.gameObject;

        // the rest of the desktop: the robots' own files. They are there to be looked at; a courier unit may not open them.
        // (one quiet badge colour for all of them, so the amber folder is the only thing that stands out)
        var badgeColour = new Color32(120, 200, 220, 255);
        var files = new (string name, string badge, Color32 colour, Vector2 at)[]
        {
            ("Human Control\nScript.js", "JS", badgeColour, new Vector2(84, -330)),
            ("Robot Games.exe", "EXE", badgeColour, new Vector2(84, -550)),
            ("Training Data.xls", "XLS", badgeColour, new Vector2(334, -110)),
            ("Programming\nInterface", "API", badgeColour, new Vector2(334, -330)),
        };
        foreach (var f in files)
        {
            var icon = Img("File_" + f.badge, go.transform, new Color(0, 0, 0, 0));
            icon.raycastTarget = true;
            Place(icon, tl, tl, tl, f.at, new Vector2(230, 190));
            Place(Img("Page", icon.transform, Cream), tl, tl, tl, new Vector2(72, -10), new Vector2(86, 108));
            Place(Img("Fold", icon.transform, new Color32(190, 180, 150, 255)), tl, tl, tl, new Vector2(134, -10), new Vector2(24, 24));
            Place(Img("Badge", icon.transform, f.colour), tl, tl, tl, new Vector2(62, -70), new Vector2(72, 34));
            var badge = Label("BadgeText", icon.transform, f.badge, 18, TextAnchor.MiddleCenter, new Color32(10, 20, 30, 255), FontStyle.Bold);
            Place(badge, tl, tl, tl, new Vector2(62, -70), new Vector2(72, 34));
            for (int i = 0; i < 3; i++)
                Place(Img("Line", icon.transform, new Color32(150, 140, 120, 255)), tl, tl, tl, new Vector2(84, -26 - i * 12), new Vector2(40, 4));
            var iconName = Label("Name", icon.transform, f.name, 17, TextAnchor.UpperCenter, Cream, FontStyle.Bold);
            Place(iconName, tl, tl, tl, new Vector2(0, -126), new Vector2(230, 50));
            var iconButton = icon.gameObject.AddComponent<Button>();
            iconButton.transition = Selectable.Transition.None;
            UnityEventTools.AddPersistentListener(iconButton.onClick, new UnityAction(desktop.Refuse));
            icon.gameObject.AddComponent<MenuItemHighlight>().label = iconName;
        }
        desktop.denied = Label("Denied", go.transform, "ACCESS DENIED  -  COURIER UNITS MAY NOT OPEN THIS FILE", 22, TextAnchor.MiddleCenter,
                               new Color32(255, 84, 60, 255), FontStyle.Bold);
        Place(desktop.denied, bottom, bottom, bottom, new Vector2(0, 120), new Vector2(1200, 34));

        // the folder's window: one file
        var folderWindow = Img("FolderWindow", go.transform, window);
        Place(folderWindow, mid, mid, mid, new Vector2(-120, 60), new Vector2(860, 430));
        Border(folderWindow.transform, new Color32(150, 240, 255, 150), 2f);
        Place(Img("TitleBar", folderWindow.transform, bar), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 44));
        var folderTitle = Label("Title", folderWindow.transform, "PASSWORDS", 18, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(folderTitle, tl, tl, tl, new Vector2(20, -11), new Vector2(700, 24));
        var columns = Label("Columns", folderWindow.transform, "NAME                                                  TYPE                    SIZE", 13, TextAnchor.MiddleLeft,
                            new Color32(150, 205, 255, 200));
        Place(columns, tl, tl, tl, new Vector2(34, -62), new Vector2(780, 20));
        var file = Img("File", folderWindow.transform, new Color32(20, 60, 90, 200));
        file.raycastTarget = true;
        Place(file, tl, tl, tl, new Vector2(20, -92), new Vector2(820, 60));
        Place(Img("Page", file.transform, Cream), tl, tl, tl, new Vector2(16, -9), new Vector2(32, 42));
        Place(Img("PageFold", file.transform, new Color32(190, 180, 150, 255)), tl, tl, tl, new Vector2(36, -9), new Vector2(12, 12));
        var fileName = Label("Name", file.transform, "Password.txt", 24, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(fileName, tl, tl, tl, new Vector2(66, -15), new Vector2(360, 30));
        var fileKind = Label("Kind", file.transform, "TEXT FILE                    1 KB", 15, TextAnchor.MiddleLeft, new Color32(150, 205, 255, 220));
        Place(fileKind, tl, tl, tl, new Vector2(470, -20), new Vector2(340, 22));
        var fileButton = file.gameObject.AddComponent<Button>();
        fileButton.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(fileButton.onClick, new UnityAction(desktop.OpenFile));
        file.gameObject.AddComponent<MenuItemHighlight>().label = fileName;
        desktop.fileRow = file.rectTransform;
        desktop.folderWindow = folderWindow.gameObject;

        // the file's window: the password
        var fileWindow = Img("FileWindow", go.transform, window);
        Place(fileWindow, mid, mid, mid, new Vector2(210, -70), new Vector2(680, 350));
        Border(fileWindow.transform, Amber, 2f);
        Place(Img("TitleBar", fileWindow.transform, bar), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 44));
        var fileTitle = Label("Title", fileWindow.transform, "Password.txt   -   TEXT VIEWER", 18, TextAnchor.MiddleLeft, Cream, FontStyle.Bold);
        Place(fileTitle, tl, tl, tl, new Vector2(20, -11), new Vector2(620, 24));
        var line1 = Label("Line1", fileWindow.transform, "FLOOR 10  -  ELECTRIC GATE  -  ACCESS CODE", 17, TextAnchor.MiddleLeft, new Color32(150, 205, 255, 230));
        Place(line1, tl, tl, tl, new Vector2(34, -78), new Vector2(620, 24));
        desktop.passwordText = Label("Password", fileWindow.transform, "----", 104, TextAnchor.MiddleCenter, Amber, FontStyle.Bold);
        Place(desktop.passwordText, mid, mid, mid, new Vector2(0, -14), new Vector2(640, 130));
        desktop.passwordText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var line2 = Label("Line2", fileWindow.transform, "Type it into the keypad beside the gate. Do not leave this file open.", 14, TextAnchor.MiddleLeft,
                          new Color32(150, 205, 255, 180));
        Place(line2, tl, tl, tl, new Vector2(34, -262), new Vector2(620, 22));
        var close = TextButton("CloseButton", fileWindow.transform, "CLOSE", 18, out var closeLabel);
        Place(close, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 16), new Vector2(150, 40));
        close.GetComponent<Image>().color = bar;
        Border(close.transform, Amber, 2f);
        close.gameObject.AddComponent<MenuItemHighlight>().label = closeLabel;
        UnityEventTools.AddPersistentListener(close.onClick, new UnityAction(desktop.Close));
        desktop.fileWindow = fileWindow.gameObject;

        // what to do: it is all done with the mouse
        var hintRow = KeyRow(go.transform, bottom, new Vector2(0, 60), 20, Cream, 36f, ("LMB", "CLICK THE PASSWORDS FOLDER"));
        desktop.hint = hintRow.transform.GetChild(1).GetComponent<Text>();
        folderWindow.gameObject.SetActive(false);
        fileWindow.gameObject.SetActive(false);

        // the desktop's own mouse pointer, over everything else on it (DesktopUI keeps it under the mouse)
        var pointer = Img("Pointer", go.transform, Color.white);
        pointer.sprite = PointerSprite();
        desktop.pointer = Place(pointer, mid, mid, new Vector2(0, 1), Vector2.zero, new Vector2(30f, 42.5f));
        return desktop;
    }

    // ================================================================ the gate's keypad, on screen
    static KeypadUI BuildKeypad(Transform canvas, HudController hud, FirstPersonController controller, Text worldDisplay)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f), bottom = new Vector2(0.5f, 0f);
        Color32 amber = Amber;
        var go = UiObject("Keypad", canvas);
        go.transform.SetSiblingIndex(hud.fade.transform.GetSiblingIndex());       // above the game HUD, under the fade and the pause menu
        Stretch(go.transform);
        var keypad = go.AddComponent<KeypadUI>();
        keypad.group = go.AddComponent<CanvasGroup>();
        keypad.group.alpha = 0f;
        keypad.group.blocksRaycasts = false;
        keypad.player = controller;
        keypad.worldDisplay = worldDisplay;

        var dim = Img("Dim", go.transform, new Color32(0, 6, 12, 170));
        dim.raycastTarget = true;
        Stretch(dim);
        var panel = Img("Panel", go.transform, new Color32(10, 16, 24, 248));
        Place(panel, mid, mid, mid, new Vector2(0, 10), new Vector2(520, 760));
        Border(panel.transform, amber, 2f);
        var title = Label("Title", panel.transform, "ELECTRIC GATE", 30, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
        Place(title, top, top, top, new Vector2(0, -22), new Vector2(480, 40));
        var sub = Label("Sub", panel.transform, "FLOOR 10  //  ACCESS CODE", 15, TextAnchor.MiddleCenter, new Color32(255, 84, 60, 255), FontStyle.Bold);
        Place(sub, top, top, top, new Vector2(0, -62), new Vector2(480, 22));

        // the four digits
        keypad.slots = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            var slot = Img("Slot" + i, panel.transform, new Color32(2, 6, 10, 255));
            Place(slot, top, top, top, new Vector2(-153f + i * 102f, -104), new Vector2(88, 104));
            keypad.slots[i] = Label("Digit", slot.transform, "_", 66, TextAnchor.MiddleCenter, amber, FontStyle.Bold);
            Stretch(keypad.slots[i]);
            Place(Img("Under", slot.transform, amber), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 5));
        }
        keypad.status = Label("Status", panel.transform, "ENTER THE 4-DIGIT ACCESS CODE", 17, TextAnchor.MiddleCenter, amber, FontStyle.Bold);
        Place(keypad.status, top, top, top, new Vector2(0, -222), new Vector2(480, 26));

        // the keys: 1 to 9, then back, 0, close
        var presses = new UnityAction[]
        {
            keypad.Press1, keypad.Press2, keypad.Press3, keypad.Press4, keypad.Press5, keypad.Press6,
            keypad.Press7, keypad.Press8, keypad.Press9, keypad.Backspace, keypad.Press0, keypad.Close,
        };
        string[] faces = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "<", "0", "X" };
        for (int i = 0; i < 12; i++)
        {
            var key = TextButton("Key_" + faces[i], panel.transform, faces[i], 38, out var label);
            Place(key, top, top, top, new Vector2((i % 3 - 1) * 122f, -268 - (i / 3) * 98f), new Vector2(108, 84));
            key.GetComponent<Image>().color = new Color32(34, 44, 58, 255);
            Border(key.transform, new Color32(255, 168, 40, 120), 2f);
            if (i == 9 || i == 11) label.color = new Color32(255, 130, 100, 255);
            key.gameObject.AddComponent<MenuItemHighlight>().label = label;
            UnityEventTools.AddPersistentListener(key.onClick, presses[i]);
        }
        KeyRow(panel.transform, bottom, new Vector2(0, 18), 15, Cream, 28f, ("0-9", "TYPE"), ("BKSP", "BACK"), ("ESC", "STEP AWAY"));
        return keypad;
    }

    // ================================================================ synchronised lights
    /// <summary>
    /// The flashing, travelling light patterns of the robots' world: chasers down every street
    /// kerb, amber runs up the forecourt to the door, red columns climbing the Monolith, a ripple
    /// across the facility's ceiling toward the elevator, and searchlights swinging in step.
    /// </summary>
    static void BuildLightPatterns()
    {
        var amberGlow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/City_GlowAmber.mat");
        var redGlow = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/City/Mono_GlowRed.mat");
        var cyan = new Color32(90, 240, 255, 255);
        var lights = new List<(Vector3 at, Vector3 size, int phase)>();

        // street kerbs: a pulse runs along every road, on both sides, all in step
        foreach (float z in new[] { ArrivalZ, MidZ, RingZ })
        {
            int i = 0;
            for (float x = CityWest + 8f; x < CityEast - 2f; x += 6f, i++)
            {
                if (Mathf.Abs(x - WestX) < 9f || Mathf.Abs(x - AvenueX) < 9f || Mathf.Abs(x - EastX) < 9f) continue;
                foreach (float side in new[] { -5.75f, 5.75f })
                    lights.Add((new Vector3(x, 0.02f, z + side), new Vector3(1.0f, 0.04f, 0.16f), i));
            }
        }
        foreach (float x in new[] { WestX, AvenueX, EastX })
        {
            int i = 0;
            for (float z = ArrivalZ + 12f; z < RingZ - 9f; z += 6f, i++)
            {
                if (Mathf.Abs(z - MidZ) < 9f || (x == AvenueX && z > MidZ)) continue;       // the closed stretch of the avenue stays dark
                foreach (float side in new[] { -5.75f, 5.75f })
                    lights.Add((new Vector3(x + side, 0.02f, z), new Vector3(0.16f, 0.04f, 1.0f), i));
            }
        }
        LightPattern("StreetChasers", cLane, cyan, 8, 0.8f, 3.5f, lights);

        // forecourt: amber bars run up both sides toward the main door
        lights.Clear();
        for (int i = 0; i < 17; i++)
            foreach (float side in new[] { -5.4f, 5.4f })
                lights.Add((new Vector3(side, 0.08f, -46f + i * 3f), new Vector3(1.5f, 0.04f, 0.3f), i));
        LightPattern("ForecourtRunway", amberGlow, Amber, 6, 1.1f, 3f, lights);

        // the Monolith: red columns of light climb its front, tier by tier
        lights.Clear();
        for (int i = 0; i < 10; i++)
            foreach (float side in new[] { -31f, 31f })
                lights.Add((new Vector3(side, 4f + i * 4f, TowerZ - 24.5f), new Vector3(1.4f, 1.4f, 0.4f), i));
        for (int i = 0; i < 15; i++)
            foreach (float side in new[] { -25f, 25f })
                lights.Add((new Vector3(side, 46f + i * 5f, TowerZ - 18.5f), new Vector3(1.4f, 1.6f, 0.4f), 10 + i));
        for (int i = 0; i < 11; i++)
            foreach (float side in new[] { -19f, 19f })
                lights.Add((new Vector3(side, 128f + i * 6f, TowerZ - 14f), new Vector3(1.4f, 1.8f, 0.4f), 25 + i));
        LightPattern("TowerClimbers", redGlow, new Color32(255, 48, 40, 255), 8, 0.7f, 4f, lights, 0.1f, 4.5f);

        // the facility hall: a ripple across the ceiling, closing in on the elevator
        lights.Clear();
        for (float x = -16f; x <= 16f; x += 4f)
            for (float z = HallZ0 + 3f; z < HallZ1 - 1f; z += 4f)
            {
                float away = Vector2.Distance(new Vector2(x, z), new Vector2(0f, HallZ1));
                lights.Add((new Vector3(x, HallH - 0.05f, z), new Vector3(0.6f, 0.06f, 0.6f), Mathf.FloorToInt(away / 3.5f)));
            }
        LightPattern("HallCeilingRipple", cLane, cyan, 8, -0.6f, 3f, lights);

        // (no marks on the hall floor: the single lit line down the aisle is enough to lead to the elevator)

        // searchlights: two pairs of beams over the fortress, each pair crossing and parting in step
        Searchlight(new Vector3(-20.5f, 17f, WallZ - 2f), 18f, 1f, 0f);
        Searchlight(new Vector3(20.5f, 17f, WallZ - 2f), -18f, -1f, 0f);
        Searchlight(new Vector3(-46f, 34f, 14f), 30f, -1f, 2.2f);
        Searchlight(new Vector3(46f, 34f, 14f), -30f, 1f, 2.2f);
    }

    static void LightPattern(string name, Material template, Color colour, int phases, float cyclesPerSecond, float sharpness,
                             List<(Vector3 at, Vector3 size, int phase)> lights, float low = 0.15f, float high = 3.2f)
    {
        var root = new GameObject(name);
        var pattern = root.AddComponent<PatternLights>();
        var renderers = new List<Renderer>();
        var phaseOf = new List<int>();
        foreach (var (at, size, phase) in lights)
        {
            var box = Box("Light", at, size, template, false, null, 0f, root.transform);
            var renderer = box.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderers.Add(renderer);
            phaseOf.Add(((phase % phases) + phases) % phases);
        }
        pattern.lights = renderers.ToArray();
        pattern.phaseOf = phaseOf.ToArray();
        pattern.phases = phases;
        pattern.template = template;
        pattern.colour = colour;
        pattern.cyclesPerSecond = cyclesPerSecond;
        pattern.sharpness = sharpness;
        pattern.low = low;
        pattern.high = high;
        log.Add(name + ": " + renderers.Count + " lights in " + phases + " phases");
    }

    /// <summary>A long see-through beam leaning up from 'at', swinging about the vertical.</summary>
    static void Searchlight(Vector3 at, float yaw, float mirror, float delay)
    {
        var pivot = new GameObject("Searchlight").transform;
        pivot.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.name = "Beam";
        beam.transform.SetParent(pivot, false);
        var lean = Quaternion.Euler(-56f, 0, 0);
        beam.transform.localRotation = lean;
        beam.transform.localPosition = lean * Vector3.forward * 130f;
        beam.transform.localScale = new Vector3(1.1f, 1.1f, 260f);
        var renderer = beam.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = cSearchBeam;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var sweep = pivot.gameObject.AddComponent<Sweep>();
        sweep.angle = 34f;
        sweep.period = 9f;
        sweep.mirror = mirror;
        sweep.delay = delay;
    }
}
