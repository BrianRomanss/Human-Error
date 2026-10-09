using UnityEngine;
using UnityEngine.EventSystems;

namespace ChuchuGames.UI
{
    /// <summary>A UI element the player can drag. Attach any game object as <see cref="Payload"/>.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public object Payload;

        DragDropController _controller;
        CanvasGroup _group;
        RectTransform _rt;
        Transform _homeParent;
        int _homeSibling;
        Vector2 _homeAnchored;
        Vector3 _grabOffset;
        bool _dragging;

        public bool IsDragging => _dragging;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        DragDropController Controller => _controller != null ? _controller : (_controller = GetComponentInParent<DragDropController>());

        public void OnBeginDrag(PointerEventData data)
        {
            var c = Controller;
            if (c == null || !c.isActiveAndEnabled) return;

            _dragging = true;
            _homeParent = _rt.parent;
            _homeSibling = _rt.GetSiblingIndex();
            _homeAnchored = _rt.anchoredPosition;

            _rt.SetParent(c.DragLayer, worldPositionStays: true);
            _rt.SetAsLastSibling();
            _group.blocksRaycasts = false; // let raycasts reach the targets underneath
            _grabOffset = PointerLocal(data, c.DragLayer) is Vector3 p ? _rt.localPosition - p : Vector3.zero;
            c.BeginDrag(this);
        }

        public void OnDrag(PointerEventData data)
        {
            if (!_dragging) return;
            var c = Controller;
            if (PointerLocal(data, c.DragLayer) is Vector3 p) _rt.localPosition = p + _grabOffset;
            c.UpdateHover(this, data);
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (!_dragging) return;
            _group.blocksRaycasts = true;
            Controller.EndDrag(this, data);
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.dragging) return;
            var c = Controller;
            if (c != null) c.Tap(this);
        }

        /// <summary>Puts the visual back where the drag started. Safe to call when not dragging.</summary>
        public void ReturnHome()
        {
            if (!_dragging) return;
            _dragging = false;
            if (_homeParent != null)
            {
                _rt.SetParent(_homeParent, worldPositionStays: false);
                _rt.SetSiblingIndex(_homeSibling);
                _rt.anchoredPosition = _homeAnchored;
            }
        }

        static Vector3? PointerLocal(PointerEventData data, RectTransform space)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, data.position, data.pressEventCamera, out var local)
                ? (Vector3?)local
                : null;
        }
    }
}
