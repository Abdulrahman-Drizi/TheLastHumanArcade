using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// Menu entry feedback, for the mouse only: while the pointer is over the entry its label
    /// eases to amber and the shared crosshair marker fades in beside it; when the pointer leaves,
    /// both ease back and the crosshair is gone. Nothing is marked unless something is hovered.
    /// </summary>
    public class MenuItemHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Text label;
        public RectTransform marker;      // shared crosshair (optional)
        public Color normal = new Color32(244, 236, 214, 255);
        public Color highlighted = new Color32(255, 168, 40, 255);

        static MenuItemHighlight hovered;     // the entry the marker currently belongs to
        CanvasGroup markerGroup;
        float glow;
        bool over;

        void Awake()
        {
            normal = label.color;             // whatever colour the entry was given is its resting colour
            if (marker == null) return;
            markerGroup = marker.GetComponent<CanvasGroup>();
            if (markerGroup == null) markerGroup = marker.gameObject.AddComponent<CanvasGroup>();
            markerGroup.alpha = 0f;
            markerGroup.blocksRaycasts = false;
        }

        void OnDisable()
        {
            over = false;
            glow = 0f;
            if (label != null) label.color = normal;
            if (hovered == this) hovered = null;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            if (marker == null) return;
            hovered = this;
            var p = marker.position;
            p.y = label.rectTransform.position.y;
            marker.position = p;
        }

        public void OnPointerExit(PointerEventData e)
        {
            over = false;
            if (hovered == this) hovered = null;
        }

        void Update()
        {
            glow = Mathf.MoveTowards(glow, over ? 1f : 0f, Time.unscaledDeltaTime * 9f);
            label.color = Color.Lerp(normal, highlighted, glow);
            // whichever entry is hovered drives the marker; with none hovered, any entry may fade it out
            if (markerGroup != null && (hovered == this || hovered == null))
                markerGroup.alpha = Mathf.MoveTowards(markerGroup.alpha, hovered == this ? 1f : 0f, Time.unscaledDeltaTime * 9f);
        }
    }
}
