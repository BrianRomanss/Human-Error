using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChuchuGames.UI
{
    /// <summary>
    /// Hold-to-confirm for destructive or high-stakes actions: the press must be held for
    /// <see cref="HoldSeconds"/> while a fill bar grows; releasing or sliding off early cancels.
    /// Works with mouse and touch. Add it next to a <see cref="Button"/> built by <see cref="UiBuild"/>
    /// via <see cref="Attach"/> — the Button's own onClick is left unused.
    /// </summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float HoldSeconds = 0.5f;
        public Image FillBar;
        public event Action Confirmed;

        bool _holding;
        float _held;
        Selectable _selectable;

        /// <summary>Adds a hold behaviour and a fill bar (drawn behind the label) to an existing button.</summary>
        public static HoldButton Attach(Button button, Color fillColor, float holdSeconds, Action onConfirmed)
        {
            var hold = button.gameObject.AddComponent<HoldButton>();
            hold.HoldSeconds = holdSeconds;
            hold._selectable = button;
            var bar = UiBuild.Panel("HoldFill", button.transform, fillColor, raycastTarget: false);
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillOrigin = 0;
            bar.fillAmount = 0;
            bar.rectTransform.Fill();
            bar.transform.SetSiblingIndex(0);
            hold.FillBar = bar;
            if (onConfirmed != null) hold.Confirmed += onConfirmed;
            return hold;
        }

        /// <summary>0–1 progress of the current hold.</summary>
        public float Progress => HoldSeconds <= 0 ? 1 : Mathf.Clamp01(_held / HoldSeconds);

        public void OnPointerDown(PointerEventData e)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;
            _holding = true;
            _held = 0;
        }

        public void OnPointerUp(PointerEventData e) => Cancel();
        public void OnPointerExit(PointerEventData e) => Cancel();

        void Cancel()
        {
            _holding = false;
            _held = 0;
            if (FillBar != null) FillBar.fillAmount = 0;
        }

        void Update()
        {
            if (!_holding) return;
            _held += Time.unscaledDeltaTime;
            if (FillBar != null) FillBar.fillAmount = Progress;
            if (_held < HoldSeconds) return;
            Cancel();
            Confirmed?.Invoke();
        }

        void OnDisable() => Cancel();
    }
}
