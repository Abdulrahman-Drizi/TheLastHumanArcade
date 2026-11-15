using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TheLastHuman
{
    /// <summary>Main menu: the looping video behind real buttons. Play loads the workplace.</summary>
    public class MainMenuController : MonoBehaviour
    {
        public VideoPlayer video;
        public RawImage videoImage;       // sits over a still frame, shown once the video is ready
        public string videoFile = "MenuLoop.mp4";   // inside StreamingAssets (required for web builds)
        public Button playButton;
        public Button quitButton;
        public Image fade;

        RenderTexture target;
        bool loading;

        void Start()
        {
            FirstPersonController.CaptureCursor(false);
            videoImage.enabled = false;
            target = new RenderTexture(1280, 720, 0) { name = "MenuVideo" };
            video.source = VideoSource.Url;
            video.url = Path.Combine(Application.streamingAssetsPath, videoFile);
            video.renderMode = VideoRenderMode.RenderTexture;
            video.targetTexture = target;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.isLooping = true;
            video.playOnAwake = false;
            video.prepareCompleted += OnPrepared;
            video.Prepare();
            if (Application.platform == RuntimePlatform.WebGLPlayer) quitButton.gameObject.SetActive(false);
            StartCoroutine(FadeTo(1f, 0f, 0.6f));
        }

        void OnPrepared(VideoPlayer player)
        {
            videoImage.texture = target;
            videoImage.enabled = true;
            player.Play();
        }

        void Update()
        {
            if (GameInput.ConfirmPressed) Play();
        }

        public void Play()
        {
            if (loading) return;
            loading = true;
            StartCoroutine(LoadGame());
        }

        IEnumerator LoadGame()
        {
            yield return FadeTo(0f, 1f, 0.45f);
            SceneManager.LoadScene(GameFlow.GameScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        IEnumerator FadeTo(float from, float to, float seconds)
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                SetFade(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetFade(to);
        }

        void SetFade(float a)
        {
            var c = fade.color;
            c.a = a;
            fade.color = c;
            fade.raycastTarget = a > 0.5f;
        }

        void OnDestroy()
        {
            if (video != null) video.targetTexture = null;
            if (target != null) target.Release();
        }
    }
}
