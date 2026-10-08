using System;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Single reader of the "WorldMap" action map. Turns raw input into map gestures
    /// and drops pointer input that starts over the UI.
    /// </summary>
    public sealed class WorldMapInput : MonoBehaviour
    {
        private const string ActionMapName = "WorldMap";

        [SerializeField] private CityInfoPanelController ui;

        [Tooltip("A press that moves farther than this before release is a drag, not a click.")]
        [SerializeField, Min(0f)] private float dragThresholdPixels = 6f;

        private InputActionMap actionMap;
        private InputAction pointAction;
        private InputAction clickAction;
        private InputAction panDragAction;
        private InputAction panMoveAction;
        private InputAction zoomAction;
        private InputAction cancelAction;
        private InputAction commandAction;

        private PointerGesture clickGesture;
        private PointerGesture panGesture;

        private bool inputConsumed;

        /// <summary>Raised with the screen position of a click on the map.</summary>
        public event Action<Vector2> Clicked;

        /// <summary>Raised with the screen-pixel delta of a drag.</summary>
        public event Action<Vector2> Dragged;

        /// <summary>Raised with a step (+1 zoom in, -1 zoom out) and the screen position.</summary>
        public event Action<float, Vector2> Zoomed;

        public event Action Cancelled;

        /// <summary>
        /// Raised with the screen position of the pointer when the right button is pressed
        /// on the map.
        /// </summary>
        public event Action<Vector2> Commanded;

        /// <summary>
        /// Raised when the player does anything but move the pointer: a keyboard key is
        /// pressed, or a mouse button is pressed or the wheel turned on the map. Raised
        /// before the other events of the frame, so that a listener can consume the input.
        /// </summary>
        public event Action AnyInput;

        public Vector2 PointerPosition { get; private set; }

        public Vector2 MoveAxis { get; private set; }

        public bool IsPointerOverUi { get; private set; }

        public bool IsDragging => clickGesture.IsDragging || panGesture.IsDragging;

        private void Awake()
        {
            clickGesture = new PointerGesture(dragThresholdPixels);
            panGesture = new PointerGesture(0f);
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
            panMoveAction = actionMap.FindAction("PanMove", true);
            zoomAction = actionMap.FindAction("Zoom", true);
            cancelAction = actionMap.FindAction("Cancel", true);
            commandAction = actionMap.FindAction("Command", true);
            actionMap.Enable();
        }

        private void OnDisable()
        {
            actionMap?.Disable();
            CancelGestures();
            MoveAxis = Vector2.zero;
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
            MoveAxis = panMoveAction.ReadValue<Vector2>();
            IsPointerOverUi = ui != null && ui.IsPointerOverUi(PointerPosition);

            // Scroll magnitude differs between devices and platforms; only its direction is used.
            float scroll = zoomAction.ReadValue<Vector2>().y;

            // The UI handles the pointer input that starts over it; the keyboard is the map's.
            bool pointerInput = clickAction.WasPressedThisFrame() || panDragAction.WasPressedThisFrame()
                || commandAction.WasPressedThisFrame() || scroll != 0f;

            inputConsumed = false;

            if (WasAnyKeyPressedThisFrame() || (pointerInput && !IsPointerOverUi))
            {
                AnyInput?.Invoke();
            }

            if (inputConsumed)
            {
                return;
            }

            UpdateGesture(clickAction, clickGesture, Clicked);
            UpdateGesture(panDragAction, panGesture, null);

            // The right button gives its order as soon as it is pressed, wherever it is
            // released: an order given while the pointer is moving must not be lost.
            if (commandAction.WasPressedThisFrame() && !IsPointerOverUi)
            {
                Commanded?.Invoke(PointerPosition);
            }

            if (scroll != 0f && !IsPointerOverUi)
            {
                Zoomed?.Invoke(Mathf.Sign(scroll), PointerPosition);
            }

            if (cancelAction.WasPressedThisFrame())
            {
                Cancelled?.Invoke();
            }
        }

        /// <summary>
        /// For a listener of <see cref="AnyInput"/> that used the input up: nothing else is
        /// raised this frame and no gesture starts, so the release of the button that was
        /// just pressed is not a click.
        /// </summary>
        public void ConsumeInput()
        {
            inputConsumed = true;
            MoveAxis = Vector2.zero;
            CancelGestures();
        }

        /// <summary>Drops the click and the drag in progress.</summary>
        public void CancelGestures()
        {
            clickGesture?.Cancel();
            panGesture?.Cancel();
        }

        // Not the keyboard's "any key" control: it stays pressed while one key is held,
        // and would miss a second key pressed meanwhile.
        private static bool WasAnyKeyPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return false;
            }

            foreach (KeyControl key in keyboard.allKeys)
            {
                if (key != null && key.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateGesture(InputAction button, PointerGesture gesture, Action<Vector2> clicked)
        {
            if (button.WasPressedThisFrame() && !IsPointerOverUi)
            {
                gesture.Press(PointerPosition);
            }

            Vector2 delta = gesture.Move(PointerPosition);

            if (delta != Vector2.zero)
            {
                Dragged?.Invoke(delta);
            }

            if (button.WasReleasedThisFrame() && gesture.Release())
            {
                clicked?.Invoke(PointerPosition);
            }
        }
    }
}
