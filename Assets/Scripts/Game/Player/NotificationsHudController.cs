using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Shows the player's notifications under the treasury, the newest at the top. Reads
    /// the player's notification feed; knows nothing about what posts to it.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class NotificationsHudController : MonoBehaviour
    {
        private const string ItemClass = "notifications-hud__item";

        [SerializeField] private PlayerNotifications playerNotifications;

        private readonly Dictionary<Notification, Label> rows = new Dictionary<Notification, Label>();

        private NotificationFeed feed;
        private VisualElement list;
        private bool isBound;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || playerNotifications == null)
            {
                Debug.LogError(
                    "NotificationsHudController needs a UIDocument with a source asset and the player's notifications.",
                    this);
                return;
            }

            list = root.Q<VisualElement>("notifications-list");

            if (list == null)
            {
                Debug.LogError("NotificationsHud.uxml is missing an expected element.", this);
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

            if (!playerNotifications.IsReady)
            {
                // PlayerNotifications already logged why there is no feed.
                root.style.display = DisplayStyle.None;
                return;
            }

            feed = playerNotifications.Feed;
            feed.Posted += AddRow;
            feed.Removed += RemoveRow;
            isBound = true;

            // The oldest first: each row goes above the ones before it.
            for (int i = feed.Notifications.Count - 1; i >= 0; i--)
            {
                AddRow(feed.Notifications[i]);
            }
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            feed.Posted -= AddRow;
            feed.Removed -= RemoveRow;
            isBound = false;

            // OnEnable makes them again from the feed. Removed here: the document keeps
            // its tree when only this component is disabled.
            foreach (Label row in rows.Values)
            {
                row.RemoveFromHierarchy();
            }

            rows.Clear();
        }

        // LateUpdate, not Update: the feed has aged this frame, whatever the order of the components.
        private void LateUpdate()
        {
            if (!isBound)
            {
                return;
            }

            foreach (KeyValuePair<Notification, Label> row in rows)
            {
                // Only a fading row changes: the others are left alone.
                if (row.Key.Opacity < 1f)
                {
                    row.Value.style.opacity = row.Key.Opacity;
                }
            }
        }

        private void AddRow(Notification notification)
        {
            var row = new Label(notification.Text) { pickingMode = PickingMode.Ignore };
            row.AddToClassList(ItemClass);
            row.style.opacity = notification.Opacity;

            list.Insert(0, row);
            rows[notification] = row;
        }

        private void RemoveRow(Notification notification)
        {
            if (rows.TryGetValue(notification, out Label row))
            {
                row.RemoveFromHierarchy();
                rows.Remove(notification);
            }
        }
    }
}
