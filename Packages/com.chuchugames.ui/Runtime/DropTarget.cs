using UnityEngine;
using UnityEngine.EventSystems;

namespace ChuchuGames.UI
{
    /// <summary>Somewhere a <see cref="Draggable"/> can be dropped. Needs a raycast-target Graphic on it or a child.</summary>
    public sealed class DropTarget : MonoBehaviour, IPointerClickHandler
    {
        public object Payload;

        public void OnPointerClick(PointerEventData data)
        {
            if (data.dragging) return;
            var c = GetComponentInParent<DragDropController>();
            if (c != null) c.TapTarget(this);
        }
    }
}
