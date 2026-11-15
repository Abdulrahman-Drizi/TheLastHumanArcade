using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The kill switch: a lever standing in a slotted housing. It is shielded until the boss is
    /// beaten. E pulls it: the arm swings down toward the player and lands with a clunk, its lamp
    /// goes from red to live, and CityFlow carries on from there (the climb to the roof).
    /// </summary>
    public class KillSwitch : Interactable
    {
        public CityFlow flow;
        public Transform arm;                   // the lever, on its own pivot at the top of the housing
        public float upAngle = 32f;             // at rest it leans back toward the wall (degrees about the pivot's x axis)
        public float downAngle = -58f;          // pulled, it lies toward the player
        public float pullSeconds = 0.6f;
        public Renderer lamp;                   // the indicator on the front of the housing
        public Material liveMaterial;           // what the lamp shows once the lever is down
        public Light glow;                      // the light over the switch
        public Color liveColour = new Color32(120, 255, 235, 255);

        public bool Locked { get; set; } = true;
        public bool Pulled { get; private set; }
        /// <summary>The top of the lever's grip (where a hand would be).</summary>
        public Vector3 GripPoint => arm != null ? arm.TransformPoint(new Vector3(0f, 0.9f, 0f)) : transform.position + Vector3.up * 1.6f;

        public override string Prompt => Locked || Pulled ? null : "Pull the kill switch";

        void Awake()
        {
            if (arm != null) arm.localRotation = Quaternion.Euler(upAngle, 0f, 0f);
        }

        public override void Interact(PlayerInteractor player)
        {
            if (Locked || Pulled) return;
            Pulled = true;
            flow.ActivateKillSwitch(this);
        }

        /// <summary>
        /// The pull itself: the arm starts slowly, gathers speed and stops dead at the bottom of its
        /// slot (a small bounce), and the lamp changes as it lands.
        /// </summary>
        public IEnumerator Swing()
        {
            if (arm != null)
            {
                for (float t = 0; t < 1f; t += Time.deltaTime / pullSeconds)
                {
                    arm.localRotation = Quaternion.Euler(Mathf.Lerp(upAngle, downAngle, t * t), 0f, 0f);
                    yield return null;
                }
                arm.localRotation = Quaternion.Euler(downAngle, 0f, 0f);
            }
            if (lamp != null && liveMaterial != null) lamp.sharedMaterial = liveMaterial;
            if (glow != null) glow.color = liveColour;
            if (arm != null)
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.14f)
                {
                    arm.localRotation = Quaternion.Euler(downAngle + 5f * Mathf.Sin(t * Mathf.PI) * (1f - t), 0f, 0f);
                    yield return null;
                }
            if (arm != null) arm.localRotation = Quaternion.Euler(downAngle, 0f, 0f);
        }
    }
}
