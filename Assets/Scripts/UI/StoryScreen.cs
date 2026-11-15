using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// A plain full-screen card for the story lines between stages. Show() fades it in (unless it
    /// is already covering the screen), types the line, and waits for the player; Hide() fades back
    /// to the game.
    /// </summary>
    public class StoryScreen : MonoBehaviour
    {
        public CanvasGroup group;
        public Text body;
        public Text hint;
        public float fadeSeconds = 0.7f;
        public float charactersPerSecond = 30f;   // a pace that can be read as it appears
        public float autoContinueSeconds = 12f;   // moves on by itself if nothing is pressed

        public bool Showing => group.alpha > 0.01f;

        /// <summary>Cover the screen straight away (used for the very first card).</summary>
        public void Cover()
        {
            gameObject.SetActive(true);
            group.alpha = 1f;
            body.text = "";
            hint.enabled = false;
        }

        public IEnumerator Show(string line)
        {
            gameObject.SetActive(true);
            body.text = "";
            hint.enabled = false;
            for (float a = group.alpha; a < 1f; a += Time.deltaTime / fadeSeconds)
            {
                group.alpha = a;
                yield return null;
            }
            group.alpha = 1f;
            yield return new WaitForSeconds(0.25f);

            // type the line; a press finishes it at once
            float shown = 0f;
            while (shown < line.Length)
            {
                shown += charactersPerSecond * Time.deltaTime;
                if (GameInput.ContinuePressed) shown = line.Length;
                body.text = line.Substring(0, Mathf.Min(line.Length, Mathf.CeilToInt(shown)));
                yield return null;
            }
            body.text = line;

            yield return new WaitForSeconds(0.35f);
            hint.enabled = true;
            for (float t = 0; t < autoContinueSeconds; t += Time.deltaTime)
            {
                if (GameInput.ContinuePressed) break;
                var c = hint.color;
                c.a = 0.45f + 0.35f * Mathf.Sin(Time.time * 3f);
                hint.color = c;
                yield return null;
            }
            hint.enabled = false;
        }

        public IEnumerator Hide()
        {
            for (float a = group.alpha; a > 0f; a -= Time.deltaTime / fadeSeconds)
            {
                group.alpha = a;
                yield return null;
            }
            group.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
