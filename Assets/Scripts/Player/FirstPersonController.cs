using UnityEngine;

namespace TheLastHuman
{
    /// <summary>The player's posture. Tucked is the low position inside a hiding place.</summary>
    public enum Stance { Standing, Crouching, Tucked }

    /// <summary>First-person walking, crouching and mouse look. The camera sits on <see cref="cameraPivot"/>.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Transform cameraPivot;
        public float walkSpeed = 3.4f;
        public float sprintSpeed = 5.0f;
        public float crouchSpeed = 1.8f;
        public float mouseSensitivity = 0.09f;   // degrees per pixel
        public float stickSensitivity = 140f;    // degrees per second
        public float gravity = -20f;

        [Header("Crouch (hold Left Ctrl, or tap C)")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.15f;
        public float standEye = 1.65f;
        public float crouchEye = 1.0f;

        [Header("Tucked into a hiding place")]
        public float tuckHeight = 0.7f;
        public float tuckEye = 0.46f;

        [Header("Sprint: the faintest sway of the view: momentum that is felt, never noticed")]
        public float sprintBob = 0.011f;         // metres up and down
        public float sprintSway = 0.006f;        // metres side to side
        public float sprintRoll = 0.15f;         // degrees
        public float sprintStepsPerSecond = 2.6f;

        [Header("Parkour run: a hard, pounding stride (used while the parkour motor moves the body)")]
        public float runBob = 0.06f;             // metres up and down
        public float runSway = 0.035f;           // metres side to side
        public float runRoll = 1.6f;             // degrees, rocking with each foot
        public float runDip = 0.9f;              // degrees the view nods at each footfall
        public float runStepsPerSecond = 3.4f;

        /// <summary>False while a cutscene, fade or end screen has taken over.</summary>
        public bool ControlEnabled { get; set; } = true;
        /// <summary>Feet stay planted (looking still works), e.g. inside a closet.</summary>
        public bool MoveLocked { get; set; }
        /// <summary>Feet and body direction are held (only looking up and down works), e.g. during a fixed hand animation.</summary>
        public bool BodyLocked { get; set; }
        /// <summary>Raises or lowers the eyes, e.g. to look through a slot.</summary>
        public float EyeAdjust { get; set; }
        /// <summary>
        /// How far down the player may look right now, in degrees. A hand animation lowers this so
        /// the hands stay in the bottom of the picture; the view eases up to it rather than snapping.
        /// </summary>
        public float MaxPitch { get; set; } = 80f;

        public Stance Stance { get; private set; }
        public bool IsCrouching => Stance == Stance.Crouching;
        public bool IsTucked => Stance == Stance.Tucked;
        /// <summary>True once the eyes are back at full standing height (not during the rise from a crouch).</summary>
        public bool EyesAtStandingHeight => eye > standEye - 0.05f;
        public float PlanarSpeed { get; private set; }
        public Vector2 LookDelta { get; private set; }
        /// <summary>How far the player is looking down, in degrees (negative = up).</summary>
        public float Pitch => pitch;
        public bool CursorCaptured => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Another component (the parkour motor) moves the body; this one only looks.</summary>
        public bool ExternalMovement { get; set; }
        /// <summary>Extra roll of the view in degrees (wall running leans it), eased in and out.</summary>
        public float ExtraRoll { get; set; }
        /// <summary>How hard the parkour motor is running (0 to 1): drives the pounding of the view.</summary>
        public float RunIntensity { get; set; }

        const float EvadeSeconds = 0.5f;
        float evadeLeft, evadeSide, smoothRoll, smoothAdjust;

        CharacterController body;
        Vector3 pivotRest, push;
        float pitch, verticalVelocity, eye, eyeVelocity, eyeTime = 0.085f;
        float bobWeight, bobPhase, shake, shakeLeft, shakeSeconds;
        bool crouchLatch, heldBefore, sprinting;

        /// <summary>Turn the view straight at a point (a robot's face, say).</summary>
        public void LookAt(Vector3 point)
        {
            var to = point - cameraPivot.position;
            var flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.LookRotation(flat);
            pitch = Mathf.Clamp(-Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -80f, 80f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Throw the player in a direction (metres per second); it dies away within a moment.</summary>
        public void Knockback(Vector3 velocity) => push = velocity;

        /// <summary>Rattle the view for a moment (degrees at the start, fading out).</summary>
        public void Shake(float degrees, float seconds)
        {
            shake = degrees;
            shakeLeft = shakeSeconds = Mathf.Max(seconds, 0.01f);
        }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            pivotRest = cameraPivot.localPosition;
            eye = standEye;
        }

        void OnEnable() => CaptureCursor(true);

        void Update()
        {
            if (ControlEnabled && !CursorCaptured && GameInput.ClickPressed) CaptureCursor(true);

            var planar = Vector3.zero;
            LookDelta = Vector2.zero;
            bool held = GameInput.CrouchHeld;
            if (ControlEnabled)
            {
                if (CursorCaptured)
                {
                    LookDelta = GameInput.Look(mouseSensitivity, stickSensitivity);
                    if (!BodyLocked) transform.Rotate(0f, LookDelta.x, 0f);
                    pitch = Mathf.Clamp(pitch - LookDelta.y, -80f, 80f);
                }

                bool locked = MoveLocked || BodyLocked;
                if (Stance != Stance.Tucked && !ExternalMovement)
                {
                    // C latches a crouch; Left Ctrl crouches while held. The latch always follows what the
                    // player can see: C flips the current stance, pressing Ctrl takes over from the latch,
                    // and the latch is dropped whenever the game holds the feet (closet, hand animation),
                    // so nothing can leave the player crouching with no key that stands them up.
                    if (locked) crouchLatch = false;
                    else if (GameInput.CrouchTogglePressed) crouchLatch = !IsCrouching;
                    else if (held && !heldBefore) crouchLatch = false;
                    SetCrouch(!locked && (crouchLatch || held));
                }
                sprinting = false;
                if (!locked && !ExternalMovement)
                {
                    var move = GameInput.Move;
                    sprinting = !IsCrouching && GameInput.SprintHeld && move.sqrMagnitude > 0.1f;
                    float speed = IsCrouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
                    planar = (transform.right * move.x + transform.forward * move.y) * speed;
                }
            }
            else sprinting = false;
            heldBefore = held;

            // the view eases up to the current limit (also during cutscenes, when looking is off)
            if (pitch > MaxPitch) pitch = Mathf.Lerp(pitch, MaxPitch, 1f - Mathf.Exp(-9f * Time.deltaTime));

            // sprinting: a slow, shallow sway in step with the feet; it fades in and out rather than starting with a jolt
            // (the parkour run is the same idea turned right up: a pounding stride that rocks and nods the view)
            bool running = ExternalMovement;
            float bobWant = running ? RunIntensity : sprinting && PlanarSpeed > walkSpeed ? 1f : 0f;
            bobWeight = Mathf.MoveTowards(bobWeight, bobWant, Time.deltaTime * (running ? 6f : 3f));
            bobPhase += Time.deltaTime * (running ? runStepsPerSecond : sprintStepsPerSecond) * Mathf.PI;
            float side = Mathf.Sin(bobPhase) * bobWeight, lift = Mathf.Sin(bobPhase * 2f) * bobWeight;
            float bobUp = running ? runBob : sprintBob, bobSide = running ? runSway : sprintSway, bobRoll = running ? runRoll : sprintRoll;
            float dip = running ? Mathf.Max(0f, -lift) * runDip : 0f;

            // a knock (a failed hack) rattles the view briefly
            var rattle = Vector3.zero;
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.deltaTime;
                float k = shake * Mathf.Clamp01(shakeLeft / shakeSeconds);
                rattle = new Vector3((Mathf.PerlinNoise(Time.time * 31f, 0f) - 0.5f) * k, (Mathf.PerlinNoise(0f, Time.time * 29f) - 0.5f) * k, 0f);
            }
            // a dodge rolls the view over and back in one smooth swing
            float evadeRoll = 0f;
            if (evadeLeft > 0f)
            {
                evadeLeft -= Time.deltaTime;
                evadeRoll = Mathf.Sin(Mathf.Clamp01(1f - evadeLeft / EvadeSeconds) * Mathf.PI) * 24f * evadeSide;
            }
            smoothRoll = Mathf.Lerp(smoothRoll, ExtraRoll, 1f - Mathf.Exp(-10f * Time.deltaTime));
            cameraPivot.localRotation = Quaternion.Euler(pitch + rattle.x + dip, rattle.y, side * bobRoll + evadeRoll + smoothRoll);

            // eyes follow the stance smoothly
            float target = Stance == Stance.Tucked ? tuckEye : Stance == Stance.Crouching ? crouchEye : standEye;
            eye = Mathf.SmoothDamp(eye, target, ref eyeVelocity, eyeTime);
            smoothAdjust = Mathf.Lerp(smoothAdjust, EyeAdjust, 1f - Mathf.Exp(-12f * Time.deltaTime));
            cameraPivot.localPosition = new Vector3(pivotRest.x + side * bobSide, eye + (ExternalMovement ? smoothAdjust : EyeAdjust) + lift * bobUp, pivotRest.z);

            if (ExternalMovement) return;        // the parkour motor moves the body

            verticalVelocity = body.isGrounded ? -2f : verticalVelocity + gravity * Time.deltaTime;
            push = Vector3.MoveTowards(push, Vector3.zero, 22f * Time.deltaTime);
            body.Move((planar + push + Vector3.up * verticalVelocity) * Time.deltaTime);
            var v = body.velocity;
            PlanarSpeed = new Vector3(v.x, 0f, v.z).magnitude;
        }

        /// <summary>
        /// A dodge: thrown sideways in the given direction with the view rolling over and back.
        /// The same move whatever form the player is in.
        /// </summary>
        public void Evade(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) direction = transform.right;
            direction.Normalize();
            push = direction * 15f;
            evadeLeft = EvadeSeconds;
            evadeSide = Vector3.Dot(direction, transform.right) >= 0f ? -1f : 1f;
        }

        void SetCrouch(bool want)
        {
            if (want == IsCrouching) return;
            if (!want && !CanStand()) return;      // something overhead: stay down, and stand up by itself once clear
            eyeTime = 0.085f;
            Apply(want ? Stance.Crouching : Stance.Standing, want ? crouchHeight : standHeight);
        }

        void Apply(Stance stance, float height)
        {
            Stance = stance;
            body.height = height;
            body.center = new Vector3(0f, height / 2f + 0.02f, 0f);
        }

        /// <summary>Is there headroom to stand up here?</summary>
        bool CanStand()
        {
            float r = body.radius * 0.9f;
            var p = transform.position;
            return !Physics.CheckCapsule(p + Vector3.up * (r + 0.1f), p + Vector3.up * (standHeight - r), r,
                                         Layers.SightBlockers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Get down low enough to fit into a hiding place. Only the hiding place calls this; it also brings the player back up.</summary>
        public void Tuck()
        {
            crouchLatch = false;
            eyeTime = 0.17f;
            Apply(Stance.Tucked, tuckHeight);
        }

        /// <summary>Stand up (used when the game moves the player, or brings them out of a hiding place).</summary>
        public void StandUp()
        {
            crouchLatch = false;
            eyeTime = IsTucked ? 0.17f : 0.085f;
            Apply(Stance.Standing, standHeight);
        }

        /// <summary>Put the player somewhere, standing, with a level view.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            StandUp();
            eye = standEye;
            eyeVelocity = 0f;
            SetPose(position, yaw);
            pitch = 0f;
            cameraPivot.localRotation = Quaternion.identity;
        }

        /// <summary>Put the player somewhere without touching where they are looking up or down.</summary>
        public void SetPose(Vector3 position, float yaw)
        {
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            body.enabled = true;
        }

        public static void CaptureCursor(bool capture)
        {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }
    }
}
