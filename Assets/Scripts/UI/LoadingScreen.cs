using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The card shown while travelling between worlds: the objective, a progress bar, and the next
    /// scene loading behind it. The scene that is left calls <see cref="Travel"/>; the scene that
    /// is entered starts with the same card already up and calls <see cref="Reveal"/>, so the card
    /// appears to stay on screen across the load.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public const string Objective = "Reach the 'Monolith Data Center' and activate the robots' kill switch to save human freedom again.";

        public CanvasGroup group;
        public Text body;
        public Text status;
        public Image barFill;
        public float fadeSeconds = 0.6f;
        public float charactersPerSecond = 28f;
        public float minimumSeconds = 5f;         // long enough to read the objective

        /// <summary>Fade the card in, type the objective, load the scene behind it, then switch.</summary>
        public IEnumerator Travel(string scene, string destinationName)
        {
            gameObject.SetActive(true);
            body.text = "";
            SetBar(0f);
            status.text = "LOADING " + destinationName;
            for (float a = group.alpha; a < 1f; a += Time.unscaledDeltaTime / fadeSeconds)
            {
                group.alpha = a;
                yield return null;
            }
            group.alpha = 1f;

            var load = SceneManager.LoadSceneAsync(scene);
            load.allowSceneActivation = false;
            float shown = 0f, t = 0f;
            while (t < minimumSeconds || load.progress < 0.9f)
            {
                t += Time.unscaledDeltaTime;
                shown += charactersPerSecond * Time.unscaledDeltaTime;
                body.text = Objective.Substring(0, Mathf.Min(Objective.Length, Mathf.CeilToInt(shown)));
                float p = Mathf.Min(load.progress / 0.9f, t / minimumSeconds);
                SetBar(p);
                status.text = "LOADING " + destinationName + "  " + Mathf.RoundToInt(p * 100f) + "%";
                yield return null;
            }
            body.text = Objective;
            SetBar(1f);
            status.text = "LOADING " + destinationName + "  100%";
            yield return null;
            load.allowSceneActivation = true;
        }

        /// <summary>In the new world: the card is already up. Hold it a moment, then fade to the game.</summary>
        public IEnumerator Reveal(float holdSeconds, string arrivedLine)
        {
            gameObject.SetActive(true);
            group.alpha = 1f;
            body.text = Objective;
            SetBar(1f);
            status.text = arrivedLine;
            yield return new WaitForSeconds(holdSeconds);
            for (float a = 1f; a > 0f; a -= Time.deltaTime / fadeSeconds)
            {
                group.alpha = a;
                yield return null;
            }
            group.alpha = 0f;
            gameObject.SetActive(false);
        }

        void SetBar(float value) => barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
    }
}
