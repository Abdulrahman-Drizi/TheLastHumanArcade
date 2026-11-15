using System;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// A hover car the player can take while wearing the mask. Getting in hides the player from
    /// patrols and holds the mask's clock; the player's own camera is borrowed as a chase camera.
    /// W/S drive, A/D steer, hold Shift for a boost. The player can only get out at the destination.
    /// It is solid against robots, lamps and buildings, and rides over kerbs and pavements freely.
    /// A robot it runs into is stunned for a couple of seconds.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Vehicle : Interactable
    {
        [Header("The driver")]
        public FirstPersonController driver;
        public PlayerIdentity identity;
        public PlayerBody driverBody;
        public MaskController maskRules;
        public HudController hud;
        public Transform viewCamera;            // the player's main camera
        public Transform seat;

        [Header("Where it is going")]
        public Transform destination;           // centre of the place the player may get out
        public float destinationRadius = 15f;
        public Transform faceOnExit;            // what the player is turned toward on getting out

        [Header("Handling")]
        public float topSpeed = 17f;
        public float reverseSpeed = 6f;
        public float acceleration = 8f;
        public float braking = 20f;
        public float coasting = 3.5f;
        public float turnRate = 78f;            // degrees per second at speed
        public float boostFactor = 1.65f;       // top speed while Shift is held
        public float boostAcceleration = 20f;
        public float stunSeconds = 2f;          // how long a robot stays dazed after being hit
        public Light hoverGlow;                 // flares while boosting
        public Transform visual;                // the model: bobs and leans, the collider does not

        [Header("Chase camera")]
        public Vector3 cameraOffset = new Vector3(0f, 3.2f, -7.4f);
        public float cameraLookAhead = 4.5f;

        public bool Driving { get; private set; }
        /// <summary>Set once the trip is over: the car cannot be taken again.</summary>
        public bool Locked { get; set; }
        public float Speed { get; private set; }
        public bool Boosting { get; private set; }
        public bool AtDestination
        {
            get
            {
                if (destination == null) return false;
                var gap = destination.position - transform.position;
                gap.y = 0f;
                return gap.magnitude < destinationRadius;
            }
        }
        public event Action Entered, Exited;

        Rigidbody body;
        CharacterController driverCapsule;
        Vector3 cameraVelocity, visualRest, home;
        Quaternion homeRotation;
        float lean, pitch, glowRest, boostView, lensRest;
        Camera viewLens;

        /// <summary>Running into a robot dazes it.</summary>
        void OnCollisionEnter(Collision hit)
        {
            if (!Driving || hit.relativeVelocity.magnitude < 1.5f) return;
            var patrol = hit.collider.GetComponentInParent<RobotPatrol>();
            if (patrol != null) { patrol.Stun(stunSeconds); return; }
            var walker = hit.collider.GetComponentInParent<CrowdWalker>();
            if (walker != null) walker.Stun(stunSeconds);
        }

        public override string Prompt => Locked ? null : Driving ? (AtDestination ? "Leave the car" : null) : "Get in the car";

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;            // parked
            driverCapsule = driver.GetComponent<CharacterController>();
            if (visual != null) visualRest = visual.localPosition;
            viewLens = viewCamera.GetComponent<Camera>();
            if (viewLens != null) lensRest = viewLens.fieldOfView;
            home = transform.position;
            homeRotation = transform.rotation;
        }

        /// <summary>Put the car back where it was parked, stopped (restart from a checkpoint while driving).</summary>
        public void Recall()
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.position = home;
            body.rotation = homeRotation;
            transform.SetPositionAndRotation(home, homeRotation);
            Speed = 0f;
        }

        public override void Interact(PlayerInteractor player)
        {
            if (Locked) return;
            if (Driving)
            {
                if (AtDestination) GetOut();
                return;
            }
            if (!identity.IsRobot)
            {
                hud.ShowWarning("A human at the wheel would be spotted. Put the mask on first.");
                return;
            }
            GetIn();
        }

        void GetIn()
        {
            Driving = true;
            driver.StandUp();
            driver.enabled = false;             // the car moves the player now
            driverCapsule.enabled = false;
            driver.transform.SetParent(transform, true);
            driver.transform.SetLocalPositionAndRotation(seat.localPosition, Quaternion.identity);
            driverBody.Concealed = true;
            identity.SetHidden(this, true);     // patrols see a robot's car, not the driver
            maskRules.Paused = true;
            body.isKinematic = false;
            Entered?.Invoke();
        }

        void GetOut()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            Driving = false;
            Speed = 0f;

            // step out on the left if there is room, otherwise on the right
            var side = -transform.right;
            if (Blocked(transform.position + side * 2.1f)) side = transform.right;
            var feet = transform.position + side * 2.1f;
            feet.y = 0.05f;
            float yaw = transform.eulerAngles.y;
            if (faceOnExit != null)
            {
                var look = faceOnExit.position - feet;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(look).eulerAngles.y;
            }

            driver.transform.SetParent(null, true);
            viewCamera.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (viewLens != null) viewLens.fieldOfView = lensRest;
            Boosting = false;
            boostView = 0f;
            driverCapsule.enabled = true;
            driver.enabled = true;
            driver.Teleport(feet, yaw);
            driverBody.Concealed = false;
            identity.SetHidden(this, false);
            maskRules.Paused = false;
            Exited?.Invoke();
        }

        static bool Blocked(Vector3 feet)
        {
            const float r = 0.33f;
            return Physics.CheckCapsule(feet + Vector3.up * (r + 0.15f), feet + Vector3.up * (1.8f - r), r,
                                        Layers.SightBlockers, QueryTriggerInteraction.Ignore);
        }

        void FixedUpdate()
        {
            if (!Driving) return;
            var input = driver.ControlEnabled ? GameInput.Move : Vector2.zero;
            float speed = Vector3.Dot(body.linearVelocity, transform.forward);      // what is left after any bump
            Boosting = driver.ControlEnabled && GameInput.BoostHeld && input.y > 0f;
            float top = Boosting ? topSpeed * boostFactor : topSpeed;
            float target = input.y > 0f ? top * input.y : input.y < 0f ? reverseSpeed * input.y : 0f;
            bool reversing = Mathf.Abs(speed) > 0.5f && Mathf.Abs(target) > 0.01f && Mathf.Sign(target) != Mathf.Sign(speed);
            float rate = Mathf.Abs(input.y) < 0.01f ? coasting : reversing ? braking
                       : Boosting ? boostAcceleration : speed > target ? coasting * 2f : acceleration;     // boost speed bleeds off gently
            speed = Mathf.MoveTowards(speed, target, rate * Time.fixedDeltaTime);

            // steering needs some speed, and reverses when backing up
            float steer = input.x * turnRate * Mathf.Clamp(speed / 5f, -1f, 1f);
            body.angularVelocity = new Vector3(0f, steer * Mathf.Deg2Rad, 0f);
            body.linearVelocity = transform.forward * speed;
            Speed = speed;
            lean = Mathf.Lerp(lean, -input.x * Mathf.Clamp01(Mathf.Abs(speed) / topSpeed) * 9f, 6f * Time.fixedDeltaTime);
            pitch = Mathf.Lerp(pitch, -(target - speed) / topSpeed * 4f, 4f * Time.fixedDeltaTime);
        }

        void Update()
        {
            if (hoverGlow != null)
            {
                if (glowRest <= 0f) glowRest = hoverGlow.intensity;
                hoverGlow.intensity = Mathf.Lerp(hoverGlow.intensity, glowRest * (Driving && Boosting ? 3.2f : 1f), 8f * Time.deltaTime);
            }
            if (visual == null) return;
            // it hovers: a slow bob, and it leans into turns and squats under power
            visual.localPosition = visualRest + Vector3.up * (0.05f * Mathf.Sin(Time.time * 2.2f));
            visual.localRotation = Quaternion.Euler(Driving ? pitch : 0f, 0f, Driving ? lean : 0f);
        }

        void LateUpdate()
        {
            if (!Driving) return;
            // boosting pulls the camera back a little and opens the view
            boostView = Mathf.Lerp(boostView, Boosting ? 1f : 0f, 4f * Time.deltaTime);
            var want = transform.TransformPoint(cameraOffset + new Vector3(0f, 0.25f, -1.6f) * boostView);
            if (viewLens != null) viewLens.fieldOfView = lensRest + 12f * boostView;
            var position = Vector3.SmoothDamp(viewCamera.position, want, ref cameraVelocity, 0.16f);
            var aim = transform.position + Vector3.up * 1.3f + transform.forward * cameraLookAhead - position;
            viewCamera.SetPositionAndRotation(position,
                Quaternion.Slerp(viewCamera.rotation, Quaternion.LookRotation(aim), 1f - Mathf.Exp(-9f * Time.deltaTime)));
        }
    }
}
