using UnityEngine;
using UnityEngine.EventSystems;

namespace TheLastHuman
{
    /// <summary>One line of the pause menu: the mouse over it marks it (and leaving unmarks it), a click picks it.</summary>
    public class PauseMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public PauseMenu menu;
        public int index;

        public void OnPointerEnter(PointerEventData eventData) => menu.Select(index);
        public void OnPointerExit(PointerEventData eventData) => menu.Leave(index);
        public void OnPointerClick(PointerEventData eventData) => menu.Pick(index);
    }
}
