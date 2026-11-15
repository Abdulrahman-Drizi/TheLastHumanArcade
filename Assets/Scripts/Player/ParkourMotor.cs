using System;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The robot parkour skills, used in the escape. Movement is built on momentum:
    ///   - run and jump (SPACE), with a little air control and a short grace period after leaving a ledge;
    ///   - slide (hold SHIFT at speed): low, almost frictionless, and it picks up speed on slopes;
    ///   - wall run: in the air beside a marked wall panel the player sticks to it and runs along,
    ///     sinking slowly; SPACE kicks off it, up and away;
    ///   - grapple (hold the left mouse button or F while looking at an anchor): a rope of fixed
    ///     length that the player swings on; letting go keeps the speed, with a little extra.
    /// FirstPersonController still does the looking; this component moves the body while it is on.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ParkourMotor : MonoBehaviour
    {
        public enum Mode { Ground, Air, Slide, WallRun, Grapple }

        public FirstPersonController look;
        public Camera viewCamera;
        public Transform hand;                  // where the rope leaves the player (a point by the camera)
        public LineRenderer rope;

        // Tuned to feel weighty and readable rather than fast and twitchy: a steady run, wall runs that
        // hold their line, a kick-off that carries across without flinging, and a slow, heavy swing.
        [Header("Running and jumping")]
        public float runSpeed = 9.2f;            // a real sprint: the course is run, not walked
        public float groundAcceleration = 60f;
        public float stopping = 45f;
        public float jumpSpeed = 8.0f;
        public float gravity = 24f;
        public float airAcceleration = 16f;
        public float graceSeconds = 0.2f;       // how late (after a ledge) or early (before landing) a jump still counts
        [Range(0f, 1f)] public float strideFeel = 0.5f;     // how much of the view's pounding stride is let through at a full run: momentum, not a distraction

        [Header("Slide")]
        public float slideHeight = 1.0f;
        public float slideEye = -0.75f;
        public float slideDrag = 3.5f;          // metres per second lost each second on the flat
        public float slideBoost = 2f;           // a kick of speed on starting a slide
        public float slideTopSpeed = 16f;

        [Header("Wall run")]
        public LayerMask wallMask;
        public float wallReach = 1.5f;          // generous: jumping roughly at a lit wall is enough to catch it
        public float wallRunSpeed = 8.5f;
        public float wallGravity = 1.0f;        // a wall run sinks only slowly: about a metre over a long wall
        public float wallSeconds = 3.5f;
        public float wallKickOut = 7.6f;         // the push off a wall: strong enough to cross to the next one with room to spare
        public float wallKickUp = 8.8f;
        public float wallKickGrace = 0.25f;      // a kick still counts this long after running off the end of a wall

        [Header("Grapple")]
        public float grappleRange = 30f;
        public float grappleCone = 30f;         // degrees from the centre of the view
        public float reelSpeed = 0.8f;
        public float minRope = 6f;
        public float swingAcceleration = 5f;    // how much the player can pump the swing
        public float swingGravity = 15f;        // gentler than free fall: the swing is slow and easy to read
        public float swingDrag = 0.3f;          // share of the speed lost each second on the rope
        public float maxSwingSpeed = 15f;
        public float releaseBoost = 1.0f;       // letting go keeps exactly the speed of the swing

        public Mode Current { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Speed => new Vector3(Velocity.x, 0f, Velocity.z).magnitude;
        /// <summary>The anchor the grapple would take right now (lit up in the world).</summary>
        public GrappleAnchor Target { get; private set; }
        public GrappleAnchor Hooked { get; private set; }
        /// <summary>How fast it all feels right now, 0 to 1 (the HUD draws speed lines from this).</summary>
        public float Rush { get; private set; }
        /// <summary>Fired the moment a move is used: "jump", "slide", "wallrun", "grapple".</summary>
        public event Action<string> Did;

        CharacterController body;
        Vector3 velocity, wallNormal;
        float standHeight, standStep, lastGrounded, jumpBuffer, nextSlideBoost, wallTime, wallBlockedUntil, ropeLength, baseFieldOfView;
        bool sliding, jumped, wasGrounded = true;
        float fallSpeed, wallLeftAt = -10f;
        Vector3 leftWallNormal, leftWallAlong;
        Collider lastWall;
        readonly Vector3[] probes = new Vector3[4];

        void Awake()
        {
            body = GetComponent<CharacterController>();
            standHeight = body.height;
            standStep = body.stepOffset;
            baseFieldOfView = viewCamera.fieldOfView;
            rope.enabled = false;
        }

        void OnEnable() => look.ExternalMovement = true;

        void OnDisable()
        {
            look.ExternalMovement = false;
            look.ExtraRoll = 0f;
            look.EyeAdjust = 0f;
        }

        /// <summary>Stop dead and drop everything (used when the player is put back at a checkpoint).</summary>
        public void Halt()
        {
            velocity = Vector3.zero;
            Unhook();
            EndSlide();
            Current = Mode.Air;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool control = look.ControlEnabled && !look.MoveLocked;
            var input = control ? GameInput.Move : Vector2.zero;
            var wish = transform.right * input.x + transform.forward * input.y;
            wish.y = 0f;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            if (control && GameInput.JumpPressed) jumpBuffer = graceSeconds;
            jumpBuffer -= dt;
            bool grounded = body.isGrounded;
            if (grounded) { lastGrounded = Time.time; jumped = false; }

            // the grapple: take the anchor being looked at while the button is held; let go when it is released
            Target = Hooked == null ? FindAnchor() : null;
            bool grappleHeld = control && GameInput.GrappleHeld;
            if (Hooked == null && grappleHeld && Target != null) Hook(Target);
            else if (Hooked != null && !grappleHeld) Unhook(true);
            foreach (var anchor in GrappleAnchor.All) anchor.Lit = anchor == Target || anchor == Hooked;

            if (Hooked != null) Swing(wish, dt);
            else if (Current == Mode.WallRun) WallRun(wish, dt);
            else if (grounded) OnGround(wish, control && GameInput.SlideHeld, dt);
            else InAir(wish, dt);

            var flags = body.Move(velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            // leaving the ground without jumping keeps the vertical speed the slope gave (ramps launch, drops fall)
            if (grounded && !body.isGrounded && !jumped && Hooked == null) velocity.y = Mathf.Clamp(body.velocity.y, -30f, 14f);
            Velocity = velocity;

            // ---- how it feels: a light stride in the view (bob, sway and roll all scaled down together), a view that
            //      opens out with speed, a thud on landing, a lean on a wall. The speed itself is told by the wider
            //      view and the speed lines, not by shaking the camera.
            float planarSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            float run = Mathf.InverseLerp(3f, runSpeed, planarSpeed);
            look.RunIntensity = body.isGrounded && !sliding && Hooked == null ? run * strideFeel : 0f;
            float fast = Mathf.InverseLerp(runSpeed, slideTopSpeed, velocity.magnitude);
            Rush = Mathf.Clamp01(0.7f * run + 0.5f * fast) *(Current == Mode.Ground || Current == Mode.Slide || Current == Mode.WallRun ? 1f : 0.7f);
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, baseFieldOfView + 8f * run + 10f * fast, 1f - Mathf.Exp(-5f * dt));
            if (body.isGrounded && !wasGrounded && fallSpeed < -9f) look.Shake(Mathf.Lerp(0.8f, 2.2f, Mathf.InverseLerp(-9f, -22f, fallSpeed)), 0.18f);
            wasGrounded = body.isGrounded;
            if (!body.isGrounded) fallSpeed = velocity.y;
            // on a wall the view leans away from it (the wall on the right tips the horizon to the left)
            look.ExtraRoll = Current == Mode.WallRun ? -11f * Mathf.Sign(Vector3.Dot(wallNormal, transform.right)) : 0f;
            DrawRope();
        }

        // ---------------------------------------------------------------- ground and air
        void OnGround(Vector3 wish, bool slideHeld, float dt)
        {
            var planar = new Vector3(velocity.x, 0f, velocity.z);
            float speed = planar.magnitude;
            if (slideHeld && (sliding || speed > 4f)) Slide(ref planar, wish, dt);
            else
            {
                EndSlide();
                Current = Mode.Ground;
                if (wish.sqrMagnitude > 0.01f)
                    planar = Vector3.MoveTowards(planar, wish * runSpeed, (speed > runSpeed + 1f ? 12f : groundAcceleration) * dt);   // extra speed bleeds off gently
                else planar = Vector3.MoveTowards(planar, Vector3.zero, stopping * dt);
            }
            // pressed down the slope enough to stay on it at speed
            velocity = planar + Vector3.down * (2f + planar.magnitude * 0.6f);
            if (jumpBuffer > 0f) Jump(jumpSpeed);
        }

        void Slide(ref Vector3 planar, Vector3 wish, float dt)
        {
            if (!sliding)
            {
                sliding = true;
                body.stepOffset = 0.1f;         // the controller lifts itself by its step while moving: keep that small under low beams
                body.height = slideHeight;
                body.center = new Vector3(0f, slideHeight / 2f + 0.02f, 0f);
                look.EyeAdjust = slideEye;
                if (Time.time > nextSlideBoost && planar.sqrMagnitude > 0.01f)
                {
                    planar += planar.normalized * slideBoost;
                    nextSlideBoost = Time.time + 1.5f;
                }
                Did?.Invoke("slide");
            }
            Current = Mode.Slide;
            // gravity pulls along the slope; the flat slowly takes speed away
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var floor, 1.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var along = Vector3.ProjectOnPlane(Vector3.down * gravity, floor.normal);
                planar += new Vector3(along.x, 0f, along.z) * dt;
            }
            planar = Vector3.MoveTowards(planar, Vector3.zero, slideDrag * dt);
            if (wish.sqrMagnitude > 0.01f && planar.sqrMagnitude > 1f)            // steering a slide is slow
                planar = Vector3.RotateTowards(planar, wish * planar.magnitude, 1.1f * dt, 0f);
            planar = Vector3.ClampMagnitude(planar, slideTopSpeed);
            if (planar.magnitude < 2.5f) EndSlide();
        }

        void EndSlide()
        {
            if (!sliding) return;
            sliding = false;
            body.height = standHeight;
            body.stepOffset = standStep;
            body.center = new Vector3(0f, standHeight / 2f + 0.02f, 0f);
            look.EyeAdjust = 0f;
        }

        void Jump(float up)
        {
            EndSlide();
            velocity.y = up;
            jumpBuffer = 0f;
            jumped = true;
            Current = Mode.Air;
            Did?.Invoke("jump");
        }

        void InAir(Vector3 wish, float dt)
        {
            EndSlide();
            Current = Mode.Air;
            velocity.y -= gravity * dt;
            // air control: push toward the wished direction, but never past running speed along it
            var planar = new Vector3(velocity.x, 0f, velocity.z);
            float along = Vector3.Dot(planar, wish);
            planar += wish * Mathf.Clamp(runSpeed - along, 0f, airAcceleration * dt);
            velocity = new Vector3(planar.x, velocity.y, planar.z);
            // a moment's grace after running off an edge
            if (jumpBuffer > 0f && !jumped && Time.time - lastGrounded < graceSeconds) { Jump(jumpSpeed); return; }
            // and after running off the end of a wall: the kick-off is still there for a moment
            if (jumpBuffer > 0f && Time.time - wallLeftAt < wallKickGrace)
            {
                float speed = Mathf.Max(Vector3.Dot(planar, leftWallAlong), wallRunSpeed);
                velocity = leftWallAlong * speed + leftWallNormal * wallKickOut;
                wallBlockedUntil = Time.time + 0.6f;
                wallLeftAt = -10f;
                Jump(wallKickUp);
                return;
            }
            TryWallRun(planar);
        }

        // ---------------------------------------------------------------- wall running
        void TryWallRun(Vector3 planar)
        {
            if (Time.time - lastGrounded < 0.12f || planar.magnitude < 3.5f) return;
            var origin = transform.position + Vector3.up * 1.0f;
            // to either side of the way the player is travelling (not the way they are looking), and a little ahead
            var ahead = planar.normalized;
            var across = Vector3.Cross(Vector3.up, ahead);
            probes[0] = across;
            probes[1] = -across;
            probes[2] = (across + ahead * 0.6f).normalized;
            probes[3] = (-across + ahead * 0.6f).normalized;
            foreach (var side in probes)
            {
                if (!Physics.Raycast(origin, side, out var hit, wallReach, wallMask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.normal.y) > 0.2f) continue;
                if (Vector3.Dot(ahead, hit.normal) < -0.8f) continue;                          // running straight into it is not a wall run
                if (hit.collider == lastWall && Time.time < wallBlockedUntil) continue;       // just kicked off this one
                wallNormal = hit.normal;
                lastWall = hit.collider;
                wallTime = 0f;
                velocity.y = Mathf.Clamp(velocity.y, 0f, 4.5f);                               // it catches the fall completely
                Current = Mode.WallRun;
                Did?.Invoke("wallrun");
                return;
            }
        }

        void WallRun(Vector3 wish, float dt)
        {
            wallTime += dt;
            var origin = transform.position + Vector3.up * 1.0f;
            bool stillThere = Physics.Raycast(origin, -wallNormal, out var hit, wallReach + 0.4f, wallMask, QueryTriggerInteraction.Ignore);
            if (stillThere) wallNormal = hit.normal;
            var along = Vector3.Cross(Vector3.up, wallNormal);
            var planar = new Vector3(velocity.x, 0f, velocity.z);
            if (Vector3.Dot(along, planar) < 0f) along = -along;
            float speed = Mathf.Max(Vector3.Dot(planar, along), wallRunSpeed);       // the speed along the wall only (not the push into it)
            velocity = along * speed - wallNormal * 1.2f + Vector3.up * Mathf.Max(velocity.y - wallGravity * dt, -7f);

            if (jumpBuffer > 0f)
            {
                // kick off: keep the run, add a shove away from the wall and up
                velocity = along * speed + wallNormal * wallKickOut;
                wallBlockedUntil = Time.time + 0.6f;
                Jump(wallKickUp);
                return;
            }
            bool pullingAway = Vector3.Dot(wish, wallNormal) > 0.6f;
            if (!stillThere || body.isGrounded || wallTime > wallSeconds || pullingAway)
            {
                wallBlockedUntil = Time.time + 0.4f;
                Current = Mode.Air;
                // (a kick pressed a moment too late, just after the wall has run out, still counts: see InAir)
                wallLeftAt = body.isGrounded ? -10f : Time.time;
                leftWallNormal = wallNormal;
                leftWallAlong = along;
            }
        }

        // ---------------------------------------------------------------- grapple
        GrappleAnchor FindAnchor()
        {
            GrappleAnchor best = null;
            float bestAngle = grappleCone;
            var eye = viewCamera.transform.position;
            foreach (var anchor in GrappleAnchor.All)
            {
                var to = anchor.transform.position - eye;
                float distance = to.magnitude;
                if (distance > grappleRange || distance < 2f) continue;
                float angle = Vector3.Angle(viewCamera.transform.forward, to);
                if (angle >= bestAngle) continue;
                if (Physics.Linecast(eye, anchor.transform.position - to.normalized * 1.2f, Layers.SightBlockers, QueryTriggerInteraction.Ignore)) continue;
                best = anchor;
                bestAngle = angle;
            }
            return best;
        }

        void Hook(GrappleAnchor anchor)
        {
            EndSlide();
            Hooked = anchor;
            ropeLength = Mathf.Max(minRope, Vector3.Distance(Chest, anchor.transform.position));
            Current = Mode.Grapple;
            Did?.Invoke("grapple");
        }

        void Unhook(bool flung = false)
        {
            if (Hooked == null) return;
            Hooked = null;
            if (flung) velocity *= releaseBoost;             // letting go at speed carries a little further
            Current = Mode.Air;
            jumped = true;                                   // no free jump out of a swing
        }

        Vector3 Chest => transform.position + Vector3.up * 1.2f;

        void Swing(Vector3 wish, float dt)
        {
            Current = Mode.Grapple;
            if (body.isGrounded && velocity.y <= 0f)
            {
                // hooked but still standing: walk as usual until the feet leave the ground (or jump into the swing)
                var planar = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), wish * runSpeed, groundAcceleration * dt);
                velocity = planar + Vector3.down * 2f;
                if (jumpBuffer > 0f) { velocity.y = jumpSpeed; jumpBuffer = 0f; jumped = true; }
            }
            else
            {
                velocity.y -= swingGravity * dt;
                velocity += wish * (swingAcceleration * dt);     // pump the swing
                velocity *= Mathf.Exp(-swingDrag * dt);          // the rope is heavy: a swing settles instead of building up
            }
            ropeLength = Mathf.Max(minRope, ropeLength - reelSpeed * dt);         // it draws in a little, so each swing ends higher
            // the rope cannot stretch: speed away from the anchor is taken out, and any slack overshoot pulled back in
            var next = Chest + velocity * dt;
            var toAnchor = Hooked.transform.position - next;
            float distance = toAnchor.magnitude;
            if (distance > ropeLength)
            {
                var pull = toAnchor / distance;
                float outward = Vector3.Dot(velocity, -pull);
                if (outward > 0f) velocity += pull * outward;
                velocity += pull * ((distance - ropeLength) / dt * 0.5f);
            }
            velocity = Vector3.ClampMagnitude(velocity, maxSwingSpeed);
        }

        void DrawRope()
        {
            bool show = Hooked != null;
            if (rope.enabled != show) rope.enabled = show;
            if (!show) return;
            rope.SetPosition(0, hand.position);
            rope.SetPosition(1, Hooked.transform.position);
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // running into a wall takes away the speed going into it, and keeps the rest
            if (Current == Mode.WallRun || hit.normal.y > 0.3f) return;
            float into = Vector3.Dot(velocity, hit.normal);
            if (into < 0f) velocity -= hit.normal * into;
        }
    }
}
