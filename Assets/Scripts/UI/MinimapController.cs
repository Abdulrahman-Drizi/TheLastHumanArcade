using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The live minimap (top right). A top-down camera draws only the Minimap layer (walls, cover
    /// and markers) into a texture that the HUD shows. North (+Z) is up. The texture takes the
    /// shape of the widget, so the map fills it with nothing wasted.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        public Camera mapCamera;
        public RawImage display;
        public int resolution = 512;      // longer side, pixels
        public Transform follow;          // set in large levels: the map scrolls to keep this in the middle

        RenderTexture texture;

        void LateUpdate()
        {
            if (follow == null) return;
            var p = mapCamera.transform.position;
            mapCamera.transform.position = new Vector3(follow.position.x, p.y, follow.position.z);
        }

        void OnEnable()
        {
            var size = display.rectTransform.rect.size;
            float aspect = size.y > 1f ? size.x / size.y : 1f;
            int w = aspect >= 1f ? resolution : Mathf.RoundToInt(resolution * aspect);
            int h = aspect >= 1f ? Mathf.RoundToInt(resolution / aspect) : resolution;
            texture = new RenderTexture(w, h, 16) { name = "Minimap", antiAliasing = 2 };
            mapCamera.targetTexture = texture;
            display.texture = texture;
        }

        void OnDisable()
        {
            if (mapCamera != null) mapCamera.targetTexture = null;
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
