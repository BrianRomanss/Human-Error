using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChuchuGames.UI
{
    /// <summary>
    /// Coordinates <see cref="Draggable"/>s and <see cref="DropTarget"/>s below it in the hierarchy.
    /// Works for mouse and touch through the EventSystem, and supports tap-to-select then
    /// tap-to-place for mobile. It only reports intent: listeners decide what a drop means, and
    /// the dragged visual always returns to its home slot before <see cref="Dropped"/> fires.
    /// </summary>
    public sealed class DragDropController : MonoBehaviour
    {
        [Tooltip("Top-most layer the dragged item is moved into so it draws above everything. Defaults to this transform.")]
        [SerializeField] RectTransform dragLayer;

        public RectTransform DragLayer => dragLayer != null ? dragLayer : (RectTransform)transform;

        /// <summary>The item being dragged right now, or null.</summary>
        public Draggable Current { get; private set; }

        /// <summary>The item picked by a tap (tap-to-place mode), or null.</summary>
        public Draggable Selected { get; private set; }

        public DropTarget Hovered { get; private set; }

        public event Action<Draggable> DragStarted;
        /// <summary>Target is null when the pointer leaves all targets.</summary>
        public event Action<Draggable, DropTarget> HoverChanged;
        /// <summary>Target is null when dropped on nothing. Raised for both drag-drop and tap-to-place.</summary>
        public event Action<Draggable, DropTarget> Dropped;
        /// <summary>Raised when drag ends without a drop or a tap selection is cleared.</summary>
        public event Action<Draggable> SelectionChanged;

        internal void BeginDrag(Draggable item)
        {
            ClearSelection();
            Current = item;
            DragStarted?.Invoke(item);
        }

        internal void UpdateHover(Draggable item, PointerEventData data)
        {
            var target = FindTarget(data.pointerCurrentRaycast.gameObject);
            if (target == Hovered) return;
            Hovered = target;
            HoverChanged?.Invoke(item, target);
        }

        internal void EndDrag(Draggable item, PointerEventData data)
        {
            var target = FindTarget(data.pointerCurrentRaycast.gameObject);
            Current = null;
            if (Hovered != null)
            {
                Hovered = null;
                HoverChanged?.Invoke(item, null);
            }
            item.ReturnHome();
            Dropped?.Invoke(item, target);
        }

        internal void Tap(Draggable item)
        {
            if (Selected != null && Selected != item)
            {
                // Tapping another item while one is selected means "put it there" (swap).
                var target = item.GetComponentInParent<DropTarget>();
                if (target != null)
                {
                    TapTarget(target);
                    return;
                }
            }

            Selected = Selected == item ? null : item;
            SelectionChanged?.Invoke(Selected);
        }

        internal void TapTarget(DropTarget target)
        {
            if (Selected == null) return;
            var item = Selected;
            ClearSelection();
            Dropped?.Invoke(item, target);
        }

        public void ClearSelection()
        {
            if (Selected == null) return;
            Selected = null;
            SelectionChanged?.Invoke(null);
        }

        static DropTarget FindTarget(GameObject hit) => hit == null ? null : hit.GetComponentInParent<DropTarget>();
    }
}
