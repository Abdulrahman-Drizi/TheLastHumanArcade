using UnityEngine;

namespace TheLastHuman
{
    /// <summary>Layer names used by the prototype. The scene builder creates them in Project Settings.</summary>
    public static class Layers
    {
        public const string Minimap = "Minimap";         // drawn only by the minimap camera
        public const string FirstPerson = "FirstPerson"; // the player's arms, drawn by the overlay camera
        public const string Player = "Player";
        public const string Robot = "Robot";
        public const string Vehicle = "Vehicle";         // the car: solid to the player and the city, passes through robots
        public const string Hud3D = "Hud3D";             // the 3D waypoint arrow, drawn only by its own camera
        public const string Curb = "Curb";               // invisible kerbs round the pavements: they stop the car and nothing else
        public const string WallRun = "WallRun";         // the lit panels of the escape course that can be run along

        /// <summary>Which layers collide: the car hits robots; kerbs stop only the car.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SetUpCollisions()
        {
            int vehicle = LayerMask.NameToLayer(Vehicle), robot = LayerMask.NameToLayer(Robot), curb = LayerMask.NameToLayer(Curb);
            if (vehicle >= 0 && robot >= 0) Physics.IgnoreLayerCollision(vehicle, robot, false);
            if (curb < 0) return;
            for (int layer = 0; layer < 32; layer++)
                Physics.IgnoreLayerCollision(curb, layer, layer != vehicle);
        }

        public static int Id(string name) => LayerMask.NameToLayer(name);

        /// <summary>What blocks a robot's line of sight: level geometry only (the Default layer).</summary>
        public static int SightBlockers => 1 << 0;
    }
}
