using System;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The colossus that guards the kill switch. It stalks the player round its arena and has three
    /// attacks, each with its own danger zone (shown on the floor by BossFight):
    ///   - Slam: a fist wound up over its head and punched into the floor in front of it. Hurts in
    ///     a circle where the fist lands, and right at its feet;
    ///   - Sweep: its sword arm comes round in a wide flat arc. Hurts everything in front of it
    ///     out to the blade's length;
    ///   - Lunge: it crouches and leaps across the hall at wherever the player is standing,
    ///     landing fists first. Hurts in a circle round the landing. This is how it reaches a
    ///     player working on a battery in a far corner.
    /// Every battery torn out of the room makes it angrier: its lights go from amber to red and
    /// pulse faster, it walks and turns faster, winds up quicker and rests less between blows.
    /// Its blows are deliberate rather than frantic: each has a long, readable wind-up and a
    /// strike slow enough to follow, and even at full anger there is time to see it and answer.
    /// It has no animation clips: every pose is worked out here and eased into, joint by joint.
    /// </summary>
    public class BossController : MonoBehaviour
    {
        public enum State { Dormant, Emerging, Stalk, WindUp, Leap, Strike, Recover, Stagger, Dead, Off }

        [Header("Arcade")]
        public bool slamsOnly;                  // ground slams only: the fist up close, the leaping slam from range (no sword)
        public bool redOnlyInWindUp;            // its lights are amber until a blow is coming, and red only then
        public enum Attack { Slam, Sweep, Lunge }

        [Header("Joints")]
        public Transform hips;
        public Transform chest, head, upperArmL, forearmL, upperArmR, forearmR, thighL, shinL, footL, thighR, shinR, footR;
        public float thighLength = 1.29f, shinLength = 1.2f;

        [Header("Lights")]
        public Renderer[] glowRenderers;        // every part that carries the glow material (the sword's edge too)
        public string glowMaterialName = "Boss_Glow";
        public Light coreLight;
        public Color calm = new Color32(255, 168, 40, 255);
        public Color furious = new Color32(255, 40, 30, 255);

        [Header("Fight")]
        public Transform player;
        public Bounds arena;                    // it never walks out of this
        public float walkSpeed = 2.3f;
        public float turnRate = 85f;            // degrees per second
        public float windUpSeconds = 1.7f;      // long enough to read which blow is coming, and from where
        public float restSeconds = 2.1f;
        [Header("Slam")]
        public float reach = 4.6f;              // how far in front of it the fist lands
        public float slamRadius = 4.4f;         // how far from that point the blow hurts
        public float bodyRadius = 2.8f;         // and how far from its own feet
        [Header("Sword sweep")]
        public float swordReach = 8f;
        public float sweepArc = 150f;           // degrees, centred on where it is facing
        [Header("Lunge")]
        public float lungeFrom = 9f;            // it leaps when the player is at least this far away
        public float lungeRadius = 4.8f;
        public float leapSeconds = 0.95f;
        public float leapHeight = 3.2f;

        public State Current { get; private set; } = State.Dormant;
        /// <summary>The attack being wound up, struck or recovered from.</summary>
        public Attack Move { get; private set; }
        /// <summary>0 at the start, one more for every battery removed.</summary>
        public int Anger { get; private set; }
        /// <summary>An attack is on its way (winding up, in the air, or landing).</summary>
        public bool Threatening => Current == State.WindUp || Current == State.Leap || Current == State.Strike;
        /// <summary>Seconds until the blow lands.</summary>
        public float WindUpLeft
        {
            get
            {
                switch (Current)
                {
                    case State.WindUp: return windLeft + (Move == Attack.Lunge ? leapSeconds : StrikeTime);
                    case State.Leap: return (1f - leap) * leapSeconds;
                    case State.Strike: return timer;
                    default: return 0f;
                }
            }
        }
        public float WindUpProgress => Threatening ? 1f - Mathf.Clamp01(WindUpLeft / Mathf.Max(0.01f, attackTotal)) : 0f;
        /// <summary>Where the blow lands, on the floor (for the sweep: where it stands).</summary>
        public Vector3 StrikePoint
        {
            get
            {
                Vector3 p;
                if (Move == Attack.Lunge) p = committed ? lungeTarget : player.position;
                else if (Move == Attack.Sweep) p = transform.position;
                else p = transform.position + transform.forward * reach;
                p.y = floorY;
                return p;
            }
        }
        /// <summary>The blow has landed.</summary>
        public event Action Impact;
        /// <summary>An attack has begun to wind up.</summary>
        public event Action<Attack> Began;

        float WalkSpeed => walkSpeed + 0.6f * Anger;
        float TurnRate => turnRate * (1f + 0.3f * Anger);
        // (anger shortens the wind-up and the rest, but never below what can still be read and answered)
        float WindUpTime => Mathf.Max(1.05f, windUpSeconds - 0.2f * Anger);
        float RestTime => Mathf.Max(0.95f, restSeconds - 0.38f * Anger);
        float StrikeTime => Move == Attack.Sweep ? 0.3f : 0.2f;

        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        Material glow;
        Vector3 hipsRest, lungeTarget, leapFrom, leapTo, fallFrom, fallTo;
        float timer, rest, walkPhase, glowFade = 1f, windLeft, attackTotal, leap, floorY, fall = 1f, windUpRed;
        bool committed;
        // the pose being eased toward, and the pose shown
        struct Pose { public float lean, twist, nod, armL, foreL, armR, foreR, thighL, shinL, thighR, shinR; }
        Pose shown;

        void Awake()
        {
            hipsRest = hips.localPosition;
            floorY = transform.position.y;
            // one private copy of the glow material, shared by every lit part, so its colour can change
            foreach (var r in glowRenderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].name != glowMaterialName) continue;
                    if (glow == null) glow = new Material(mats[i]);
                    mats[i] = glow;
                }
                r.sharedMaterials = mats;
            }
            shown = Kneeling();
            ApplyPose(shown);
        }

        void OnDestroy()
        {
            if (glow != null) Destroy(glow);
        }

        // ---------------------------------------------------------------- commands
        /// <summary>Rise from its knees and start hunting.</summary>
        public void Wake()
        {
            Current = State.Emerging;
            timer = 2.4f;
        }

        /// <summary>A battery was torn out: reel for a moment, then come back angrier.</summary>
        public void Enrage()
        {
            Anger++;
            if (Current == State.Dead || Current == State.Dormant) return;
            Ground();
            Current = State.Stagger;
            timer = 1.2f;
        }

        /// <summary>All its batteries are back in: as calm as at the start.</summary>
        public void Calm() => Anger = 0;

        public void Die() => Die(transform.position);

        /// <summary>Out of tokens: whatever it was doing stops, and it sits down on the floor where it stands, lights fading.</summary>
        public void PowerDown()
        {
            Ground();
            Current = State.Off;
        }

        /// <summary>
        /// It is finished. As it goes down it reels back to 'restAt', so that wherever it was
        /// standing its body does not end up lying across the way to the switch or the ladder.
        /// </summary>
        public void Die(Vector3 restAt)
        {
            Ground();
            fallFrom = transform.position;
            restAt.y = floorY;
            fallTo = restAt;
            fall = (fallTo - fallFrom).sqrMagnitude > 0.04f ? 0f : 1f;
            Current = State.Dead;
        }

        /// <summary>Back to a spot, ready to hunt again (the player was beaten and the fight starts over).</summary>
        public void ResetTo(Vector3 position, float yaw)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (Current == State.Dead) return;
            Current = State.Stalk;
            rest = RestTime + 0.6f;
        }

        /// <summary>Feet back on the floor (an attack was cut short in mid-air).</summary>
        void Ground()
        {
            var p = transform.position;
            p.y = floorY;
            transform.position = p;
        }

        /// <summary>Would a blow landing now hurt someone standing at this point?</summary>
        public bool DangerAt(Vector3 point)
        {
            var fromStrike = point - StrikePoint;
            fromStrike.y = 0f;
            var fromBody = point - transform.position;
            fromBody.y = 0f;
            switch (Move)
            {
                case Attack.Sweep:
                    return fromBody.magnitude < swordReach && Vector3.Angle(transform.forward, fromBody) < sweepArc / 2f;
                case Attack.Lunge:
                    return fromStrike.magnitude < lungeRadius;
                default:
                    return fromStrike.magnitude < slamRadius || fromBody.magnitude < bodyRadius;
            }
        }

        // ---------------------------------------------------------------- behaviour
        void Update()
        {
            switch (Current)
            {
                case State.Emerging:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) { Current = State.Stalk; rest = 1.2f; }
                    break;
                case State.Stalk:
                    Stalk();
                    break;
                case State.WindUp:
                    WindUp();
                    break;
                case State.Leap:
                    // through the air in an arc, to land just short of where the player was
                    leap += Time.deltaTime / leapSeconds;
                    float e = Mathf.Clamp01(leap);
                    var at = Vector3.Lerp(leapFrom, leapTo, e);
                    at.y = floorY + Mathf.Sin(e * Mathf.PI) * leapHeight;
                    transform.position = at;
                    if (leap >= 1f) Land(1.0f);
                    break;
                case State.Strike:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) Land(0.9f);
                    break;
                case State.Recover:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) { Current = State.Stalk; rest = RestTime; }
                    break;
                case State.Stagger:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) { Current = State.Stalk; rest = 0.7f; }
                    break;
                case State.Dead:
                    if (fall < 1f)                  // staggering back to where it will lie
                    {
                        fall = Mathf.Min(1f, fall + Time.deltaTime / 1.1f);
                        transform.position = Vector3.Lerp(fallFrom, fallTo, 1f - (1f - fall) * (1f - fall));
                    }
                    break;
            }
        }

        void Land(float recover)
        {
            Ground();
            Impact?.Invoke();
            Current = State.Recover;
            timer = recover;
        }

        void Stalk()
        {
            rest -= Time.deltaTime;
            var to = player.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            float off = TurnToward(player.position, TurnRate);
            bool moving = distance > reach * 0.8f && off < 60f;
            if (moving)
            {
                var next = transform.position + transform.forward * (WalkSpeed * Time.deltaTime);
                next.x = Mathf.Clamp(next.x, arena.min.x, arena.max.x);
                next.z = Mathf.Clamp(next.z, arena.min.z, arena.max.z);
                transform.position = next;
                walkPhase += Time.deltaTime * (2.2f + 0.5f * Anger) * (WalkSpeed / 1.7f) * 0.8f;
            }
            if (rest > 0f) return;
            if (slamsOnly)
            {
                // two ground slams and nothing else: the leaping one from range, the fist up close
                if (distance > lungeFrom && off < 40f) Begin(Attack.Lunge);
                else if (distance < reach + slamRadius * 0.7f && off < 35f) Begin(Attack.Slam);
                return;
            }
            // far away: leap at them. Close: the fist, or (more often the angrier it is) the sword.
            if (distance > lungeFrom && off < 40f) Begin(Attack.Lunge);
            else if (distance < swordReach - 0.6f && off < 30f)
            {
                // at arm's length it is always the sword; right up close, the fist or the sword
                bool close = distance < reach + slamRadius * 0.5f;
                Begin(!close || UnityEngine.Random.value < 0.35f + 0.1f * Anger ? Attack.Sweep : Attack.Slam);
            }
        }

        void Begin(Attack attack)
        {
            Move = attack;
            Current = State.WindUp;
            committed = false;
            windLeft = WindUpTime * (attack == Attack.Lunge ? 0.75f : attack == Attack.Sweep ? 0.9f : 1f);
            attackTotal = windLeft + (attack == Attack.Lunge ? leapSeconds : StrikeTime);
            Began?.Invoke(attack);
        }

        void WindUp()
        {
            float whole = attackTotal - (Move == Attack.Lunge ? leapSeconds : StrikeTime);
            // it keeps turning with the player for the first part, then commits
            if (windLeft > whole * 0.45f) TurnToward(player.position, TurnRate * 0.8f);
            else if (!committed)
            {
                committed = true;
                lungeTarget = player.position;
            }
            windLeft -= Time.deltaTime;
            if (windLeft > 0f) return;
            if (Move == Attack.Lunge)
            {
                if (!committed) { committed = true; lungeTarget = player.position; }
                leapFrom = transform.position;
                var dir = lungeTarget - leapFrom;
                dir.y = 0f;
                float distance = dir.magnitude;
                dir = distance > 0.01f ? dir / distance : transform.forward;
                leapTo = leapFrom + dir * Mathf.Max(0f, distance - 2.2f);       // fists reach the last two metres
                leapTo.x = Mathf.Clamp(leapTo.x, arena.min.x, arena.max.x);
                leapTo.z = Mathf.Clamp(leapTo.z, arena.min.z, arena.max.z);
                transform.rotation = Quaternion.LookRotation(dir);
                leap = 0f;
                Current = State.Leap;
            }
            else
            {
                Current = State.Strike;
                timer = StrikeTime;
            }
        }

        /// <summary>Turns toward a point at the given rate; returns the angle still to go.</summary>
        float TurnToward(Vector3 point, float rate)
        {
            var to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return 0f;
            var want = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rate * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, want);
        }

        // ---------------------------------------------------------------- poses
        void LateUpdate()
        {
            Pose want;
            float snap = 7f;
            float t = Time.time;
            switch (Current)
            {
                case State.Dormant:
                    want = Kneeling();
                    break;
                case State.Emerging:
                    want = Standing(t);
                    want.armR = -24f;
                    snap = 2.2f;
                    break;
                case State.WindUp:
                {
                    float w = 1f - Mathf.Clamp01(windLeft / Mathf.Max(0.01f, attackTotal - (Move == Attack.Lunge ? leapSeconds : StrikeTime)));
                    w = w * w * (3f - 2f * w);
                    want = Standing(t);
                    if (Move == Attack.Slam)
                    {
                        want.armR = Mathf.Lerp(-20f, -178f, w);       // the fist goes up over the head
                        want.foreR = -22f;
                        want.lean = -14f * w;
                        want.twist = -16f * w;
                        want.nod = -10f * w;
                    }
                    else if (Move == Attack.Sweep)
                    {
                        want.armL = Mathf.Lerp(-30f, -88f, w);        // the sword comes up level and is drawn back
                        want.foreL = Mathf.Lerp(-40f, -4f, w);
                        want.twist = -85f * w;
                        want.lean = 8f * w;
                        want.armR = 24f * w;
                    }
                    else
                    {
                        want.thighL = want.thighR = -62f * w;         // down on its haunches, arms thrown back
                        want.shinL = want.shinR = 112f * w;
                        want.lean = 28f * w;
                        want.armR = 46f * w;
                        want.armL = Mathf.Lerp(-30f, 30f, w);
                        want.nod = -14f * w;
                    }
                    snap = 8f;
                    break;
                }
                case State.Leap:
                    want = Standing(t);
                    want.thighL = want.thighR = -34f;
                    want.shinL = want.shinR = 70f;
                    want.armR = -172f;
                    want.armL = -150f;
                    want.foreL = want.foreR = -10f;
                    want.lean = -10f;
                    snap = 11f;
                    break;
                case State.Strike:
                case State.Recover:
                    want = Standing(t);
                    if (Move == Attack.Sweep)
                    {
                        float through = Current == State.Strike ? 1f - Mathf.Clamp01(timer / StrikeTime) : 1f;
                        want.armL = -88f;                              // the blade comes round flat, right across the front
                        want.foreL = -4f;
                        want.twist = Mathf.Lerp(-85f, 85f, through);
                        want.lean = 16f;
                        want.armR = 30f;
                        snap = Current == State.Strike ? 26f : 4f;
                    }
                    else
                    {
                        want.armR = -66f;                              // down into the floor
                        want.foreR = -4f;
                        want.armL = Move == Attack.Lunge ? -66f : -30f;
                        want.foreL = Move == Attack.Lunge ? -4f : -40f;
                        want.lean = 44f;
                        want.twist = Move == Attack.Lunge ? 0f : 10f;
                        want.nod = 10f;
                        want.thighL = want.thighR = -40f;
                        want.shinL = want.shinR = 74f;
                        snap = Current == State.Strike ? 20f : 5f;
                    }
                    break;
                case State.Stagger:
                    want = Standing(t);
                    want.lean = -22f;
                    want.nod = -24f;
                    want.armL = -70f + 12f * Mathf.Sin(t * 26f);
                    want.armR = -70f + 12f * Mathf.Sin(t * 23f + 1f);
                    want.foreL = want.foreR = -60f;
                    snap = 9f;
                    break;
                case State.Dead:
                    want = Kneeling();
                    want.lean = 58f;
                    want.nod = 42f;
                    want.armR = 8f;
                    snap = 2.4f;
                    break;
                case State.Off:
                    // sat on the floor, legs out in front, hands in its lap, head down
                    want = new Pose
                    {
                        lean = 16f, nod = 34f, armL = -50f, foreL = -22f, armR = -42f, foreR = -30f,
                        thighL = -86f, shinL = 5f, thighR = -84f, shinR = 3f,
                    };
                    snap = 1.7f;
                    break;
                default:
                    want = Walking(t);
                    break;
            }
            float k = 1f - Mathf.Exp(-snap * Time.deltaTime);
            shown.lean = Mathf.Lerp(shown.lean, want.lean, k);
            shown.twist = Mathf.Lerp(shown.twist, want.twist, k);
            shown.nod = Mathf.Lerp(shown.nod, want.nod, k);
            shown.armL = Mathf.Lerp(shown.armL, want.armL, k);
            shown.foreL = Mathf.Lerp(shown.foreL, want.foreL, k);
            shown.armR = Mathf.Lerp(shown.armR, want.armR, k);
            shown.foreR = Mathf.Lerp(shown.foreR, want.foreR, k);
            shown.thighL = Mathf.Lerp(shown.thighL, want.thighL, k);
            shown.shinL = Mathf.Lerp(shown.shinL, want.shinL, k);
            shown.thighR = Mathf.Lerp(shown.thighR, want.thighR, k);
            shown.shinR = Mathf.Lerp(shown.shinR, want.shinR, k);
            ApplyPose(shown);
            Glow(t);
        }

        // (the left arm carries the sword: held out ahead, point down, whenever it is not being swung)
        Pose Standing(float t) => new Pose
        {
            lean = 3f + 1.5f * Mathf.Sin(t * 1.3f), nod = 4f,
            armL = -30f, foreL = -40f, armR = -8f, foreR = -16f,
        };

        Pose Walking(float t)
        {
            var p = Standing(t);
            float s = Mathf.Sin(walkPhase), c = Mathf.Cos(walkPhase);
            p.thighL = 24f * s;
            p.thighR = -24f * s;
            p.shinL = 34f * Mathf.Max(0f, c);
            p.shinR = 34f * Mathf.Max(0f, -c);
            p.armL = -30f - 5f * s;
            p.armR = -8f + 14f * s;
            p.twist = 5f * s;
            return p;
        }

        // on its knees, leaning on the sword planted in front of it
        static Pose Kneeling() => new Pose
        {
            lean = 30f, nod = 30f, armL = -74f, foreL = -18f, armR = 4f, foreR = -12f,
            thighL = -78f, shinL = 128f, thighR = -78f, shinR = 128f,
        };

        void ApplyPose(Pose p)
        {
            // the hips come down by however much the straighter leg has folded, so a foot stays on the floor
            float drop = Mathf.Min(LegDrop(p.thighL, p.shinL), LegDrop(p.thighR, p.shinR));
            hips.localPosition = hipsRest + Vector3.down * drop;
            hips.localRotation = Quaternion.Euler(0f, p.twist * 0.3f, 0f);
            chest.localRotation = Quaternion.Euler(p.lean, p.twist * 0.7f, 0f);
            head.localRotation = Quaternion.Euler(p.nod, -p.twist * 0.5f, 0f);       // the head stays on its target while the body turns
            upperArmL.localRotation = Quaternion.Euler(p.armL, 0f, -6f);
            forearmL.localRotation = Quaternion.Euler(p.foreL, 0f, 0f);
            upperArmR.localRotation = Quaternion.Euler(p.armR, 0f, 6f);
            forearmR.localRotation = Quaternion.Euler(p.foreR, 0f, 0f);
            thighL.localRotation = Quaternion.Euler(p.thighL, 0f, 0f);
            shinL.localRotation = Quaternion.Euler(p.shinL, 0f, 0f);
            footL.localRotation = Quaternion.Euler(-(p.thighL + p.shinL), 0f, 0f);
            thighR.localRotation = Quaternion.Euler(p.thighR, 0f, 0f);
            shinR.localRotation = Quaternion.Euler(p.shinR, 0f, 0f);
            footR.localRotation = Quaternion.Euler(-(p.thighR + p.shinR), 0f, 0f);
        }

        float LegDrop(float thigh, float shin) =>
            thighLength * (1f - Mathf.Cos(thigh * Mathf.Deg2Rad)) + shinLength * (1f - Mathf.Cos((thigh + shin) * Mathf.Deg2Rad));

        void Glow(float t)
        {
            // amber and steady when calm; red, brighter and beating faster with every battery lost; dark when dead
            glowFade = Mathf.MoveTowards(glowFade, Current == State.Dead || Current == State.Off ? 0.03f : Current == State.Dormant ? 0.3f : 1f, Time.deltaTime * 0.6f);
            float anger = Mathf.Clamp01(Anger / 3f);
            float beat = 0.5f + 0.5f * Mathf.Sin(t * (2.5f + 5.5f * anger));
            if (redOnlyInWindUp) windUpRed = Mathf.MoveTowards(windUpRed, Threatening ? 1f : 0f, Time.deltaTime * 6f);
            var colour = Color.Lerp(calm, furious, redOnlyInWindUp ? windUpRed : anger);
            float flare = Threatening ? 1.35f : 1f;                    // it burns brighter as a blow comes
            float strength = (2.2f + 3.4f * anger) * Mathf.Lerp(1f, 0.55f + 0.9f * beat, anger) * glowFade * flare;
            if (glow != null) glow.SetColor(Emission, colour.linear * strength);
            if (coreLight != null)
            {
                coreLight.color = colour;
                // (arcade: the armour is paint, not a lamp. The core barely lights it until a blow is coming, and then it is red.)
                coreLight.intensity = redOnlyInWindUp ? Mathf.Lerp(1.1f, 7f, windUpRed) * glowFade
                                                      : (5f + 10f * anger * beat) * glowFade * flare;
                coreLight.range = 9f + 5f * anger;
            }
        }
    }
}
