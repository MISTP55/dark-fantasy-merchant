using System.Globalization;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Shows the player's gold at the top right of the screen. Reads the player's
    /// treasury; knows nothing about the map.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TreasuryHudController : MonoBehaviour
    {
        private const string DebtClass = "treasury-hud__gold--debt";

        // Not the French culture: its separator and its minus sign depend on the platform.
        private static readonly NumberFormatInfo GoldFormat = new NumberFormatInfo { NumberGroupSeparator = "\u00A0" };

        [SerializeField] private PlayerTreasury playerTreasury;

        private Treasury treasury;
        private Label goldLabel;
        private bool isBound;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || playerTreasury == null)
            {
                Debug.LogError("TreasuryHudController needs a UIDocument with a source asset and a player treasury.", this);
                return;
            }

            goldLabel = root.Q<Label>("gold-label");

            if (goldLabel == null)
            {
                Debug.LogError("TreasuryHud.uxml is missing an expected element.", this);
                return;
            }

            // Nothing here is clicked: the map under the HUD stays reachable.
            root.pickingMode = PickingMode.Ignore;

            // Covers the screen whatever else shares the panel.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;

            if (!playerTreasury.IsReady)
            {
                // PlayerTreasury already logged why there is no treasury.
                root.style.display = DisplayStyle.None;
                return;
            }

            treasury = playerTreasury.Treasury;
            treasury.Changed += ShowGold;
            isBound = true;

            ShowGold(treasury.Gold);
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            treasury.Changed -= ShowGold;
            isBound = false;
        }

        /// <summary>A whole number as the interface writes it: "6 000", thousands separated by a no-break space.</summary>
        public static string FormatNumber(long value)
        {
            return value.ToString("N0", GoldFormat);
        }

        /// <summary>
        /// The gold as the HUD writes it: "10 000 or", or "-250 or" for a debt. Thousands
        /// are separated by a no-break space, as in French.
        /// </summary>
        public static string FormatGold(long gold)
        {
            return FormatNumber(gold) + " or";
        }

        private void ShowGold(long gold)
        {
            goldLabel.text = FormatGold(gold);
            goldLabel.EnableInClassList(DebtClass, gold < 0);
        }
    }
}
