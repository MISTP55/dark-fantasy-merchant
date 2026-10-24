using System;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Single reader of the "CityView" action map. Turns raw input into the gestures of the city view and drops
    /// pointer input that starts over the UI: the view's own document and every other one on its panel (the HUDs).
    /// </summary>
    public sealed class CityViewInput : MonoBehaviour
    {
        private const string ActionMapName = "CityView";

        [Tooltip("The city view's document. Its panel is the one every game document shares.")]
        [SerializeField] private UIDocument document;

        [Tooltip("A press that moves farther than this before release is a drag, not a click.")]
        [SerializeField, Min(0f)] private float dragThresholdPixels = 6f;

        private InputActionMap actionMap;
        private InputAction pointAction;
        private InputAction clickAction;
        private InputAction panDragAction;
        private InputAction zoomAction;
        private InputAction rotateAction;
        private InputAction cancelAction;

        // Both actions are on the left button: the click is a press released where it started, the drag one
        // that moved past the threshold.
        private PointerGesture clickGesture;
        private PointerGesture panGesture;

        /// <summary>Raised with the screen position of a click on the grid.</summary>
        public event Action<Vector2> Clicked;

        /// <summary>Raised with the screen-pixel delta of a drag.</summary>
        public event Action<Vector2> Dragged;

        /// <summary>Raised with a step (+1 zoom in, -1 zoom out) and the screen position.</summary>
        public event Action<float, Vector2> Zoomed;

        public event Action Rotated;

        /// <summary>Raised when the right button is pressed over the grid.</summary>
        public event Action Cancelled;

        public Vector2 PointerPosition { get; private set; }

        public bool IsPointerOverUi { get; private set; }

        private void Awake()
        {
            clickGesture = new PointerGesture(dragThresholdPixels);
            panGesture = new PointerGesture(dragThresholdPixels);
        }

        private void OnEnable()
        {
            actionMap = InputSystem.actions != null ? InputSystem.actions.FindActionMap(ActionMapName) : null;

            if (actionMap == null)
            {
                Debug.LogError($"Action map '{ActionMapName}' was not found in the project-wide input actions.", this);
                enabled = false;
                return;
            }

            pointAction = actionMap.FindAction("Point", true);
            clickAction = actionMap.FindAction("Click", true);
            panDragAction = actionMap.FindAction("PanDrag", true);
            zoomAction = actionMap.FindAction("Zoom", true);
            rotateAction = actionMap.FindAction("Rotate", true);
            cancelAction = actionMap.FindAction("Cancel", true);
            actionMap.Enable();
        }

        private void OnDisable()
        {
            actionMap?.Disable();
            CancelGestures();
        }

        // A button released while the window is unfocused is never reported.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelGestures();
            }
        }

        private void Update()
        {
            PointerPosition = pointAction.ReadValue<Vector2>();
            IsPointerOverUi = IsOverUi(PointerPosition);

            // Scroll magnitude differs between devices and platforms; only its direction is used.
            float scroll = zoomAction.ReadValue<float>();

            if (clickAction.WasPressedThisFrame() && !IsPointerOverUi)
            {
                clickGesture.Press(PointerPosition);
            }

            if (panDragAction.WasPressedThisFrame() && !IsPointerOverUi)
            {
                panGesture.PressWithoutClick(PointerPosition);
            }

            // The click gesture only tells the click; the pan gesture moves the view.
            clickGesture.Move(PointerPosition);
            Vector2 delta = panGesture.Move(PointerPosition);

            if (delta != Vector2.zero)
            {
                Dragged?.Invoke(delta);
            }

            if (panDragAction.WasReleasedThisFrame())
            {
                panGesture.Release();
            }

            if (clickAction.WasReleasedThisFrame() && clickGesture.Release())
            {
                Clicked?.Invoke(PointerPosition);
            }

            if (scroll != 0f && !IsPointerOverUi)
            {
                Zoomed?.Invoke(Mathf.Sign(scroll), PointerPosition);
            }

            if (cancelAction.WasPressedThisFrame() && !IsPointerOverUi)
            {
                Cancelled?.Invoke();
            }

            if (rotateAction.WasPressedThisFrame())
            {
                Rotated?.Invoke();
            }
        }

        /// <summary>Drops the click and the drag in progress.</summary>
        public void CancelGestures()
        {
            clickGesture?.Cancel();
            panGesture?.Cancel();
        }

        /// <param name="screenPosition">Screen position with the origin at the bottom-left.</param>
        public bool IsOverUi(Vector2 screenPosition)
        {
            IPanel panel = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.panel
                : null;

            if (panel == null)
            {
                return false;
            }

            // Panel coordinates have their origin at the top-left.
            Vector2 flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, flipped);
            return panel.Pick(panelPosition) != null;
        }
    }
}
