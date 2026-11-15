using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The ladder up the back wall of the colossus's hall to the hatch in its roof. Nobody uses it
    /// with E any more: the moment the kill switch is pulled, CityFlow carries the player to its
    /// foot and up it. This only says where that climb starts and ends.
    /// </summary>
    public class ExitLadder : MonoBehaviour
    {
        public Transform foot;                  // where the climber stands at the bottom, facing the rungs
        public Transform top;                   // where their feet are once their head is up inside the hatch
        public float rung = 0.5f;               // metres between rungs
        public float secondsPerRung = 0.22f;
    }
}
