#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastHuman
{
    /// <summary>
    /// The automated play-test (editor only, and only when ArcadeDev.PlayTest has asked for it from
    /// the command line: it never runs in a build or in an ordinary Play session).
    /// It plays the game the way a player would be allowed to, through the same inputs: checks the
    /// disguise's clock, gets itself caught and retries, collects the batteries, uses a hiding
    /// place, powers the gate, fails one dodge and makes the rest, pulls the plug and replays.
    /// Along the way it writes what it measured to Logs/bot.txt and saves pictures from the
    /// player's camera to Docs/Play.
    /// </summary>
    public class ArcadeBot : MonoBehaviour
    {
        static string Root => Directory.GetParent(Application.dataPath).FullName;
        static string Flag => Path.Combine(Root, "Logs", "bot.flag");
        static string Done => Path.Combine(Root, "Logs", "bot.done");
        static string LogPath => Path.Combine(Root, "Logs", "bot.txt");
        static ArcadeBot instance;

        int phase;
        ArcadeFlow flow;
        FirstPersonController player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (instance != null || !Application.isBatchMode || !File.Exists(Flag)) return;
            var go = new GameObject("ArcadeBot");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ArcadeBot>();
            File.WriteAllText(LogPath, "bot started\n");
            Directory.CreateDirectory(Path.Combine(Root, "Docs", "Play"));
            SceneManager.sceneLoaded += (scene, mode) => instance.StartCoroutine(instance.Run());
            instance.StartCoroutine(instance.Run());
        }

        static void Log(string line) => File.AppendAllText(LogPath, Time.time.ToString("0.0") + "  " + line + "\n");

        IEnumerator Run()
        {
            yield return null;
            flow = FindFirstObjectByType<ArcadeFlow>();
            player = flow.player;
            GameInput.AutoMove = null;
            GameInput.AutoSprint = false;
            IEnumerator work = phase == 0 ? Opening() : phase == 1 ? FullRun() : phase == 2 ? AfterReplay() : AfterBossRetry();
            phase++;
            float limit = Time.realtimeSinceStartup + 170f;
            // (run the step with a time limit, so a stuck bot still reports and lets the editor close)
            var routine = StartCoroutine(Guarded(work));
            while (routine != null && !stepDone && Time.realtimeSinceStartup < limit) yield return null;
            if (!stepDone)
            {
                Log("TIMEOUT in phase " + (phase - 1) + " at " + player.transform.position.ToString("0.0") + " stage " + flow.Stage);
                Finish();
            }
        }

        bool stepDone;

        IEnumerator Guarded(IEnumerator work)
        {
            stepDone = false;
            yield return work;
            stepDone = true;
        }

        void Finish()
        {
            GameInput.AutoMove = null;
            GameInput.AutoSprint = false;
            Log("END");
            File.WriteAllText(Done, "done");
        }

        // ---------------------------------------------------------------- phase 0: the first seconds, the disguise, getting caught
        IEnumerator Opening()
        {
            yield return new WaitForSeconds(1.2f);
            Log("objective '" + flow.hud.commsBody.text + "' hint '" + flow.arcadeHud.hintText.text + "' control " + player.ControlEnabled);
            yield return Shot("p01_start", true);
            yield return Shot("p02_start_no_hud", false);

            // the disguise: on for five, off for five
            var disguise = flow.disguise;
            GameInput.AutoTapMask();
            yield return null;
            yield return null;
            float on = Time.time;
            Log("disguise on " + disguise.Active);
            yield return new WaitForSeconds(2f);
            yield return Shot("p03_disguised", true);
            while (disguise.Active) yield return null;
            float off = Time.time;
            Log("disguise held " + (off - on).ToString("0.00") + " s");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("p04_recharging", true);
            // E does nothing while it recharges
            while (!disguise.Active && Time.time - off < 8f)
            {
                GameInput.AutoTapMask();
                yield return new WaitForSeconds(0.25f);
            }
            Log("disguise usable again " + (Time.time - off).ToString("0.00") + " s after it dropped (on=" + disguise.Active + ")");
            if (flow.Stage == ArcadeStage.Lost && flow.detection.Catcher != null)
                Log("CAUGHT WHILE STANDING AT THE START by " + flow.detection.Catcher.name + " at " + flow.detection.Catcher.transform.position.ToString("0.0"));
            while (disguise.Active) yield return null;

            // walk up the lane as a human until a patrol takes notice
            float started = Time.time, suspectAt = -1f, alarmAt = -1f;
            bool shot = false;
            while (flow.Stage == ArcadeStage.Yard && Time.time - started < 40f)
            {
                var ahead = new Vector3(0f, 1.6f, player.transform.position.z + 6f);
                player.LookAt(ahead);
                GameInput.AutoMove = player.transform.position.z < 2f ? new Vector2(0f, 1f) : Vector2.zero;
                if (suspectAt < 0f && flow.detection.AnyWatching) { suspectAt = Time.time; Log("seen at z " + player.transform.position.z.ToString("0.0")); }
                if (alarmAt < 0f && flow.detection.AnyChasing) { alarmAt = Time.time; }
                if (alarmAt > 0f && !shot && Time.time - alarmAt > 0.25f) { shot = true; yield return Shot("p05_chased", true); }
                yield return null;
            }
            GameInput.AutoMove = null;
            Log("stage " + flow.Stage + ": ? after " + (suspectAt - started).ToString("0.0") + " s, chase " + (alarmAt - suspectAt).ToString("0.00")
                + " s later, caught " + (Time.time - alarmAt).ToString("0.00") + " s after that; loss card '" + flow.arcadeHud.lossTitle.text + "'");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("p06_caught", true);
            Log("retrying");
            flow.Retry();
        }

        // ---------------------------------------------------------------- phase 1: the whole game
        IEnumerator FullRun()
        {
            float began = Time.time;
            yield return new WaitForSeconds(0.6f);
            Log("after retry: stage " + flow.Stage + " at " + player.transform.position.ToString("0.0") + " carried " + flow.Carried);
            flow.detection.Paused = true;           // the route itself is what is being checked here, not the bot's stealth
            // battery 1, south-west
            yield return Go(-13.5f, -12f);
            yield return Shot("p07_arrow_round_the_corner", true);
            yield return Go(-13.5f, -5.5f);
            Log("battery 1: carried " + flow.Carried + " objective '" + flow.hud.commsBody.text + "'");
            // a hiding place on the way north
            yield return Go(-13.05f, 5.5f);
            yield return Go(-13.05f, 8.3f, 0.3f);
            player.LookAt(new Vector3(-14.9f, 0.8f, 8.3f));
            yield return new WaitForSeconds(0.3f);
            string prompt = flow.hud.promptText.text;
            yield return Shot("p08_hide_prompt", true);
            yield return Use();
            yield return new WaitForSeconds(1.4f);
            Log("hide prompt '" + prompt + "' hidden " + flow.identity.Hidden);
            yield return Use();
            yield return new WaitForSeconds(1.4f);
            Log("out again: hidden " + flow.identity.Hidden + " control " + player.ControlEnabled);
            // battery 3, north-west
            yield return Go(-13.05f, 10.6f);
            yield return Go(-13.5f, 13f);
            Log("battery 3: carried " + flow.Carried);
            // battery 4, by the gate
            yield return Go(-12.5f, 10.8f);
            yield return Go(4.2f, 10.8f);
            yield return Shot("p09_gate_side_on", true);
            yield return Go(5.2f, 13.6f);
            Log("battery 4: carried " + flow.Carried);
            // battery 2, in the east gap
            yield return Go(4.0f, 10.4f);
            yield return Go(4.0f, 5.2f);
            yield return Go(9f, 4f);
            Log("battery 2: carried " + flow.Carried + " objective '" + flow.hud.commsBody.text + "'");
            // the gate
            yield return Go(1.5f, 5f);
            yield return Go(0f, 11.5f);
            yield return Shot("p10_gate_with_four", true);
            yield return Go(0f, 14.3f, 0.3f);
            player.LookAt(new Vector3(0f, 1.4f, 16f));
            yield return new WaitForSeconds(0.3f);
            Log("gate prompt '" + flow.hud.promptText.text + "'");
            yield return Use();
            yield return new WaitForSeconds(0.7f);
            yield return Shot("p11_inserting", true);
            float waited = 0f;
            while (!flow.gate.IsOpen && waited < 8f) { waited += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(1.2f);
            Log("gate open " + flow.gate.IsOpen + " inserted " + flow.gate.Inserted + ", " + (Time.time - began).ToString("0") + " s into the run");
            yield return Shot("p12_gate_open", true);

            // through, and the colossus
            yield return Go(0f, 19.3f, 0.3f);
            Log("stage " + flow.Stage + " tokens " + flow.fight.Tokens.ToString("0.00") + " hearts " + flow.fight.Hearts);
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                player.LookAt(new Vector3(0f, 2.6f, 30f));
                yield return null;
            }
            yield return Shot("p13_boss_entrance_no_hud", false);
            yield return Shot("p13_boss_entrance", true);
            float fightBegan = -1f;
            int moments = 0;
            bool shotWindUp = false;
            while (!flow.fight.Over && flow.Stage == ArcadeStage.Fight)
            {
                if (flow.fight.Active && fightBegan < 0f) fightBegan = Time.time;
                var boss = flow.fight.boss;
                player.LookAt(boss.transform.position + Vector3.up * 2.6f);
                if (boss.Threatening && !shotWindUp && boss.WindUpProgress > 0.5f) { shotWindUp = true; yield return Shot("p14_wind_up", true); }
                if (flow.fight.InDodge)
                {
                    moments++;
                    float start = Time.unscaledTime;
                    bool press = moments != 1;          // the first one is let through, to see a hit
                    float at = flow.fight.dodgeSeconds * (0.88f - flow.fight.windowWidth * 0.5f);
                    bool pressed = false, pictured = false;
                    int heartsBefore = flow.fight.Hearts;
                    while (flow.fight.InDodge)
                    {
                        float t = Time.unscaledTime - start;
                        if (!pictured && t > at * 0.6f) { pictured = true; yield return Shot("p15_dodge_bar_" + moments, true); }
                        if (press && !pressed && t >= at) { pressed = true; GameInput.AutoTapJump(); }
                        yield return null;
                    }
                    yield return new WaitForSeconds(0.5f);
                    Log("dodge moment " + moments + (press ? " (pressed)" : " (left alone)") + ": hearts " + heartsBefore + " -> " + flow.fight.Hearts
                        + ", dodged so far " + flow.fight.Dodged + ", tokens " + flow.fight.Tokens.ToString("0.00"));
                    if (moments == 2) yield return Shot("p16_after_a_dodge", true);
                }
                yield return null;
            }
            Log("fight over after " + (Time.time - fightBegan).ToString("0.0") + " s: stage " + flow.Stage + " hearts " + flow.fight.Hearts + " dodged " + flow.fight.Dodged);
            if (flow.Stage == ArcadeStage.Lost) { yield return Shot("p17_flattened", true); Finish(); yield break; }
            yield return new WaitForSeconds(2.2f);
            yield return Shot("p17_powering_down", true);
            while (flow.Stage != ArcadeStage.Plug) yield return null;
            Log("boss down: state " + flow.fight.boss.Current + ", plug locked " + flow.plug.Locked + ", objective '" + flow.hud.commsBody.text + "'");
            yield return Shot("p18_boss_sitting", true);

            // round it to the plug
            float side = flow.fight.boss.transform.position.x > 0f ? -10f : 10f;
            yield return Go(side, player.transform.position.z);
            yield return Go(side, 40.5f);
            yield return Go(0f, 41.4f, 0.35f);
            player.LookAt(new Vector3(0f, 1.4f, 44f));
            yield return new WaitForSeconds(0.4f);
            Log("plug prompt '" + flow.hud.promptText.text + "'");
            yield return Shot("p19_plug", true);
            yield return Use();
            yield return new WaitForSeconds(0.75f);
            yield return Shot("p20_plug_out", true);
            float ending = Time.time;
            int stills = 0;
            while (!flow.arcadeHud.winGroup.blocksRaycasts && Time.time - ending < 30f)
            {
                if (Time.time - ending > 3.2f + stills * 1.7f && stills < 4) { stills++; yield return Shot("p21_ending_" + stills, false); }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1.0f);
            Log("win card after " + (Time.time - ending).ToString("0.0") + " s; whole run " + (Time.time - began).ToString("0") + " s");
            yield return Shot("p22_the_last_human_hero", true);
            flow.Replay();
        }

        // ---------------------------------------------------------------- phase 2: Replay starts the whole thing over
        IEnumerator AfterReplay()
        {
            yield return new WaitForSeconds(0.8f);
            Log("after replay: stage " + flow.Stage + " at " + player.transform.position.ToString("0.0") + " gate open " + flow.gate.IsOpen + " control " + player.ControlEnabled);

            // now lose to the colossus on purpose (the yard is skipped: it was checked above)
            flow.detection.Paused = true;
            flow.gate.OpenAtOnce();
            player.Teleport(new Vector3(0f, 0.05f, 14.5f), 0f);
            yield return Go(0f, 19.3f, 0.3f);
            float began = Time.time;
            while (flow.Stage == ArcadeStage.Fight && Time.time - began < 60f)
            {
                player.LookAt(flow.fight.boss.transform.position + Vector3.up * 2.6f);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.6f);
            Log("standing still in the fight: stage " + flow.Stage + " after " + (Time.time - began).ToString("0.0") + " s, hearts " + flow.fight.Hearts
                + ", loss card '" + flow.arcadeHud.lossTitle.text + "' / '" + flow.arcadeHud.lossBody.text + "', time scale " + Time.timeScale.ToString("0.00"));
            yield return Shot("p23_flattened", true);
            flow.Retry();
        }

        // ---------------------------------------------------------------- phase 3: a retry after the colossus starts at the gate, not in the yard
        IEnumerator AfterBossRetry()
        {
            yield return new WaitForSeconds(0.5f);
            Log("after the retry: stage " + flow.Stage + " at " + player.transform.position.ToString("0.0") + " control " + player.ControlEnabled
                + " hearts " + flow.fight.Hearts + " tokens " + flow.fight.Tokens.ToString("0.00"));
            yield return Shot("p24_retry_at_the_gate", true);
            // and the fight is a real one again: dodge the first blow
            float began = Time.time;
            while (!flow.fight.InDodge && Time.time - began < 20f)
            {
                player.LookAt(flow.fight.boss.transform.position + Vector3.up * 2.6f);
                yield return null;
            }
            float start = Time.unscaledTime, at = flow.fight.dodgeSeconds * (0.88f - flow.fight.windowWidth * 0.5f);
            bool pressed = false;
            while (flow.fight.InDodge)
            {
                if (!pressed && Time.unscaledTime - start >= at) { pressed = true; GameInput.AutoTapJump(); }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            Log("first blow after the retry: dodged " + flow.fight.Dodged + ", hearts " + flow.fight.Hearts + ", stage " + flow.Stage);
            Finish();
        }

        // ---------------------------------------------------------------- helpers
        /// <summary>Run to a spot on the floor.</summary>
        IEnumerator Go(float x, float z, float tolerance = 0.5f)
        {
            var target = new Vector3(x, 0f, z);
            float limit = Time.time + 14f, stuckSince = Time.time;
            var last = player.transform.position;
            GameInput.AutoSprint = true;
            while (Time.time < limit)
            {
                var gap = target - player.transform.position;
                gap.y = 0f;
                if (gap.magnitude < tolerance) break;
                player.LookAt(target + Vector3.up * 1.6f);
                GameInput.AutoMove = new Vector2(0f, 1f);
                if (Time.time - stuckSince > 0.8f)
                {
                    if ((player.transform.position - last).magnitude < 0.3f)
                    {
                        // something is in the way (a patrol, most likely): step round it
                        GameInput.AutoMove = new Vector2(1f, 0.2f);
                        yield return new WaitForSeconds(0.5f);
                    }
                    last = player.transform.position;
                    stuckSince = Time.time;
                }
                yield return null;
            }
            GameInput.AutoMove = Vector2.zero;
            GameInput.AutoSprint = false;
            if (Time.time >= limit) Log("could not reach (" + x + ", " + z + ") from " + player.transform.position.ToString("0.0"));
            yield return null;
        }

        /// <summary>Press F.</summary>
        IEnumerator Use()
        {
            GameInput.AutoHoldInteract();
            yield return null;
            yield return null;
            GameInput.AutoInteractHeld = false;
        }

        /// <summary>A picture from the camera the player is looking through, with or without the HUD.</summary>
        IEnumerator Shot(string name, bool withHud)
        {
            yield return null;          // (no end-of-frame wait: batch mode never reaches one)
            Camera cam = null;
            foreach (var c in Camera.allCameras)
                if (c.isActiveAndEnabled && c.targetTexture == null && c.name != "BodyCamera" && (cam == null || c.depth > cam.depth)) cam = c;
            if (cam == null) yield break;
            var canvas = flow.hud.GetComponent<Canvas>();
            if (withHud)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.12f;
            }
            else canvas.enabled = false;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            picture.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Root, "Docs", "Play", name + ".png"), picture.EncodeToPNG());
            Destroy(rt);
            Destroy(picture);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.enabled = true;
            yield return null;
        }
    }
}
#endif
