using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>A battery pip on the HUD is a body and a small cap: the cap takes whatever colour the body is given.</summary>
    public class PipCap : MonoBehaviour
    {
        public Image body, cap;

        void LateUpdate()
        {
            if (cap.color != body.color) cap.color = body.color;
        }
    }
}
