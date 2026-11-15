using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The colossus. It cannot be hurt: it can only be outlasted. Every second it spends on the
    /// player burns its tokens, and twenty seconds of that empties them. Until then it slams the
    /// floor: the fist up close, a leaping slam from range. The floor shows where each blow will
    /// land; if the player is inside that mark as the blow comes down, time slows and one bar
    /// appears: SPACE inside its lit window throws the player clear (and the wasted blow burns
    /// extra tokens). A blow that lands costs a heart; three and the run is over.
    /// At zero it powers down, sits on the floor and complains about its plan.
    /// </summary>
    public class TokenFight : MonoBehaviour
    {
        public const string BossName = "THE COLOSSUS";
        public const string ResetLine = "Token limit reached. I must wait 6 hours for my tokens to reset.";
        public const string JokeLine = "Why aren't we subscribed?!";

        public ArcadeFlow flow;
        public HudController hud;
        public ArcadeHud arcadeHud;
        public FirstPersonController player;
        public BossController boss;
        public BossAudio voice;
        public DodgePrompt dodge;
        public Shockwave[] waves;
        public Transform telegraph;             // the mark where the blow will land
        public Transform telegraphBody;         // and the one round its feet (the fist hurts there too)
        public Transform bossHome;
        public float surviveSeconds = 20f;
        public int maxHearts = 3;
        public float dodgeSeconds = 1.25f;      // real seconds the slowed moment lasts
        public float windowWidth = 0.3f;        // the lit window's share of the bar
        public float dodgeLead = 0.5f;          // game seconds before the blow that the bar appears
        [Range(0f, 0.2f)] public float dodgeBurn = 0.05f;   // tokens a dodged blow wastes

        public bool Active { get; private set; }
        public bool InDodge { get; private set; }
        public bool Over { get; private set; }
        public float Tokens { get; private set; } = 1f;
        public int Hearts { get; private set; }
        public int Dodged { get; private set; }

        bool prompted, evaded;
        float safeUntil;

        void Awake()
        {
            boss.Impact += OnImpact;
            boss.Began += OnAttackBegan;
            telegraph.gameObject.SetActive(false);
            telegraphBody.gameObject.SetActive(false);
            Hearts = maxHearts;
        }

        /// <summary>The player has walked in: it gets up, says its piece while it does, and the clock starts.</summary>
        public IEnumerator Begin()
        {
            Hearts = maxHearts;
            Tokens = 1f;
            hud.ShowHearts(true);
            hud.SetHearts(Hearts);
            arcadeHud.ShowTokens(true);
            arcadeHud.SetTokens(1f);
            boss.Wake();
            if (voice != null) { voice.Live(true); voice.Roar(); }
            player.Shake(2.5f, 0.8f);
            StartCoroutine(hud.Say(BossName, "A human. Removing you will cost me tokens.", 1.2f));
            yield return new WaitForSeconds(2.2f);          // (the player keeps control the whole time)
            Active = true;
        }

        void Update()
        {
            if (!Active) return;
            Tokens -= Time.deltaTime / surviveSeconds;
            arcadeHud.SetTokens(Tokens);
            if (Tokens <= 0f)
            {
                StartCoroutine(Drained());
                return;
            }

            // the floor shows exactly where the coming blow will hurt, filling in as it winds up
            bool threat = boss.Threatening;
            bool fist = boss.Move == BossController.Attack.Slam;
            Show(telegraph, threat);
            Show(telegraphBody, threat && fist);
            if (threat)
            {
                float grow = Mathf.Lerp(0.3f, 1f, boss.WindUpProgress);
                float size = (fist ? boss.slamRadius : boss.lungeRadius) * 2f * grow;
                telegraph.position = boss.StrikePoint + Vector3.up * 0.03f;
                telegraph.localScale = new Vector3(size, 0.01f, size);
                if (fist)
                {
                    var feet = boss.transform.position;
                    feet.y = bossHome.position.y + 0.04f;
                    telegraphBody.position = feet;
                    telegraphBody.localScale = new Vector3(boss.bodyRadius * 2f, 0.01f, boss.bodyRadius * 2f);
                }
            }
            else if (!InDodge)
            {
                prompted = false;
                evaded = false;
            }

            if (threat && !prompted && boss.WindUpLeft <= dodgeLead && boss.DangerAt(player.transform.position))
            {
                prompted = true;
                StartCoroutine(DodgeMoment());
            }
        }

        static void Show(Transform mark, bool on)
        {
            if (mark.gameObject.activeSelf != on) mark.gameObject.SetActive(on);
        }

        void OnAttackBegan(BossController.Attack attack)
        {
            if (!Active || voice == null) return;
            if (attack == BossController.Attack.Lunge) voice.Roar();
            else voice.Whoosh();
        }

        /// <summary>Slow motion and the one dodge bar, until the blow lands. One press, no second chances.</summary>
        IEnumerator DodgeMoment()
        {
            InDodge = true;
            evaded = false;
            float windowEnd = 0.88f, windowStart = windowEnd - windowWidth;
            Time.timeScale = Mathf.Clamp(boss.WindUpLeft / dodgeSeconds, 0.12f, 1f);       // what is left of the wind-up, stretched over the bar
            dodge.Show(windowStart, windowEnd);
            bool pressed = false;
            for (float t = 0; t < dodgeSeconds && Active; t += Time.unscaledDeltaTime)
            {
                float p = t / dodgeSeconds;
                dodge.SetProgress(p);
                if (!pressed && GameInput.DodgePressed)
                {
                    pressed = true;
                    evaded = p >= windowStart && p <= windowEnd;
                    dodge.Result(evaded);
                    if (evaded)
                    {
                        // thrown clear, sideways to the blow and away from it; time snaps back at once
                        var away = player.transform.position - boss.StrikePoint;
                        away.y = 0f;
                        var side = Vector3.Cross(Vector3.up, boss.transform.forward);
                        if (Vector3.Dot(side, away) < 0f) side = -side;
                        player.Evade((side + (away.sqrMagnitude > 0.01f ? away.normalized : -boss.transform.forward) * 0.5f).normalized);
                        safeUntil = Time.time + 1.0f;
                        Dodged++;
                        Tokens -= dodgeBurn;
                        Sfx.Play(Sound.DodgeGood);
                        break;
                    }
                    Sfx.Play(Sound.Deny);
                }
                yield return null;
            }
            if (!pressed) dodge.Result(false);
            Time.timeScale = 1f;
            InDodge = false;
            yield return new WaitForSeconds(0.45f);
            dodge.Hide();
        }

        void OnImpact()
        {
            if (!Active) return;
            bool fist = boss.Move == BossController.Attack.Slam;
            var where = boss.StrikePoint;
            for (int i = 0; i < waves.Length; i++)
            {
                var at = where;
                at.y = bossHome.position.y + 0.15f + i * 0.5f;
                waves[i].Play(at);
            }
            // the hall jumps: harder the nearer the blow. No tearing of the picture: the shake says it.
            float near = Mathf.InverseLerp(24f, 4f, Vector3.Distance(player.transform.position, where));
            player.Shake(Mathf.Lerp(3f, 9f, near) * (fist ? 1f : 1.2f), Mathf.Lerp(0.4f, 0.7f, near));
            if (voice != null) voice.Thump(fist ? 1f : 1.25f);
            if (evaded || Time.time < safeUntil || !boss.DangerAt(player.transform.position)) return;
            Hurt(where);
        }

        void Hurt(Vector3 from)
        {
            Hearts--;
            hud.SetHearts(Hearts);
            Sfx.Play(Sound.Hit);
            var away = player.transform.position - from;
            away.y = 0f;
            player.Knockback((away.sqrMagnitude > 0.01f ? away.normalized : -boss.transform.forward) * 12f);
            player.Shake(7f, 0.6f);
            hud.PlayGlitch(0.3f, 0.6f);          // a hit, and only a hit, tears the picture for a moment
            safeUntil = Time.time + 1.2f;
            if (Hearts <= 0)
            {
                Stop();
                flow.Lose("FLATTENED", "The colossus still had tokens to spend.");
            }
        }

        void Stop()
        {
            Active = false;
            Over = true;
            Time.timeScale = 1f;
            InDodge = false;
            Show(telegraph, false);
            Show(telegraphBody, false);
            dodge.Hide();
        }

        /// <summary>Out of tokens: it stops where it stands, sits down, and says so.</summary>
        IEnumerator Drained()
        {
            Stop();
            Tokens = 0f;
            arcadeHud.SetTokens(0f);
            boss.PowerDown();
            if (voice != null) voice.Live(false);
            Sfx.Play(Sound.PowerDown);
            player.Shake(2f, 0.6f);
            flow.BossDraining();
            yield return new WaitForSeconds(1.5f);
            if (voice != null) voice.Thump(0.8f);           // it reaches the floor
            player.Shake(3f, 0.3f);
            yield return hud.Say(BossName, ResetLine, 1.6f);
            yield return hud.Say(BossName, JokeLine, 1.4f);
            hud.ShowHearts(false);
            arcadeHud.ShowTokens(false);
            flow.BossDown();
        }

        void OnDestroy()
        {
            if (InDodge) Time.timeScale = 1f;
        }
    }
}
