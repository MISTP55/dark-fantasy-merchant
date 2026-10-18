using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Shows the world's date at the top of the screen, with the button that starts and
    /// stops the fast forward. Reads the world clock; knows nothing about the map.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TimeHudController : MonoBehaviour
    {
        private const string ActiveButtonClass = "time-hud__button--active";
        private const string FastForwardText = "Avance rapide";
        private const string NormalSpeedText = "Vitesse normale";

        [SerializeField] private WorldClock worldClock;

        private GameClock clock;
        private Label dateLabel;
        private Button fastForwardButton;
        private bool isBound;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || worldClock == null)
            {
                Debug.LogError("TimeHudController needs a UIDocument with a source asset and a world clock.", this);
                return;
            }

            dateLabel = root.Q<Label>("date-label");
            fastForwardButton = root.Q<Button>("fast-forward-button");

            if (dateLabel == null || fastForwardButton == null)
            {
                Debug.LogError("TimeHud.uxml is missing an expected element.", this);
                return;
            }

            // Only the button should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // Covers the screen whatever else shares the panel.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;

            if (!worldClock.IsReady)
            {
                // WorldClock already logged why time does not pass.
                root.style.display = DisplayStyle.None;
                return;
            }

            // The button is clicked, not navigated: the keyboard belongs to the map.
            fastForwardButton.focusable = false;

            clock = worldClock.Clock;
            fastForwardButton.clicked += OnFastForwardClicked;
            clock.DayStarted += ShowDate;
            clock.FastForwardChanged += ShowSpeed;
            isBound = true;

            ShowDate(clock.Date);
            ShowSpeed(clock.IsFastForward);
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            fastForwardButton.clicked -= OnFastForwardClicked;
            clock.DayStarted -= ShowDate;
            clock.FastForwardChanged -= ShowSpeed;
            isBound = false;
        }

        private void ShowDate(GameDate date)
        {
            dateLabel.text = worldClock.Calendar.Format(date);
        }

        private void ShowSpeed(bool isFastForward)
        {
            fastForwardButton.text = isFastForward ? NormalSpeedText : FastForwardText;
            fastForwardButton.EnableInClassList(ActiveButtonClass, isFastForward);
        }

        private void OnFastForwardClicked()
        {
            clock.SetFastForward(!clock.IsFastForward);
        }
    }
}
