using System;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Asks every robot whether it can see the player. A robot that sees a human (or a failing mask)
    /// away from the conveyor line walks after them, and catches them if it gets close.
    /// A masked player, a hidden player, and a human working at the conveyor are left alone.
    /// </summary>
    public class DetectionSystem : MonoBehaviour
    {
        public Transform player;            // feet
        public Transform playerHead;
        public PlayerIdentity identity;
        public Bounds workZone;             // the conveyor line, where a human is expected to be
        public float catchDistance = 1.3f;

        /// <summary>True while at least one robot has line of sight on a player it recognises as human.</summary>
        public bool AnyWatching { get; private set; }
        public bool AnyChasing { get; private set; }
        public bool Paused { get; set; }
        public event Action Caught;
        /// <summary>The robot that made the last catch.</summary>
        public RobotVision Catcher { get; private set; }

        public bool PlayerInWorkZone
        {
            get
            {
                var p = player.position;
                return workZone.Contains(new Vector3(p.x, workZone.center.y, p.z));
            }
        }

        void Update()
        {
            bool looksHuman = identity.LooksHuman && !Paused;
            bool fairGame = looksHuman && !PlayerInWorkZone;
            var head = playerHead.position;
            var feet = player.position;
            bool watching = false, chasing = false, caught = false;

            foreach (var robot in RobotVision.All)
            {
                bool sees = looksHuman && robot.CanSee(head);
                robot.SeesPlayer = sees;
                watching |= sees;
                var patrol = robot.Patrol;
                if (patrol == null) continue;
                if (sees && fairGame) patrol.Pursue(feet);
                if (!patrol.Chasing) continue;
                chasing = true;
                var gap = feet - robot.transform.position;
                gap.y = 0f;
                if (fairGame && !caught && patrol.Running && gap.magnitude < catchDistance)
                {
                    caught = true;
                    Catcher = robot;
                }
            }
            AnyWatching = watching;
            AnyChasing = chasing;
            if (caught) Caught?.Invoke();
        }

        /// <summary>An alarm: every security robot within 'radius' of the point runs straight to it, no questions asked.</summary>
        public void Alarm(Vector3 point, float radius)
        {
            foreach (var robot in RobotVision.All)
            {
                if (robot.Patrol == null) continue;
                var gap = robot.transform.position - point;
                gap.y = 0f;
                if (gap.magnitude <= radius) robot.Patrol.Alarm(point);
            }
        }

        /// <summary>Every robot goes back to its route.</summary>
        public void CallOff()
        {
            foreach (var robot in RobotVision.All)
                if (robot.Patrol != null) robot.Patrol.GiveUp();
        }
    }
}
