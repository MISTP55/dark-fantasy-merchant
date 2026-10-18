using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The route of one ship on the world map: a solid line over what it has sailed,
    /// a dashed one over what is left, and a marker where the route ends.
    /// </summary>
    public sealed class ShipRouteView : MonoBehaviour
    {
        [SerializeField] private LineRenderer sailedLine;
        [SerializeField] private LineRenderer remainingLine;
        [SerializeField] private SpriteRenderer destinationMarker;
        [SerializeField] private Color color = new Color(0.96f, 0.93f, 0.86f);

        [Tooltip("On-screen width of the lines, in pixels, at any zoom level.")]
        [SerializeField, Min(0f)] private float lineWidthPixels = 3f;

        [Tooltip("On-screen length, in pixels, of one dash and the gap after it.")]
        [SerializeField, Min(1f)] private float dashPeriodPixels = 14f;

        [Tooltip("On-screen size, in pixels, of one world unit of marker sprite.")]
        [SerializeField, Min(0f)] private float markerScreenPixelsPerUnit = 24f;

        private void Awake()
        {
            sailedLine.startColor = color;
            sailedLine.endColor = color;
            remainingLine.startColor = color;
            remainingLine.endColor = color;
            destinationMarker.color = color;
            Hide();
        }

        /// <summary>Shows the route of a ship under way; hides it for an idle ship.</summary>
        /// <param name="showDestinationMarker">False when what is at the end of the route marks it already.</param>
        public void Show(Ship ship, MapProjection projection, bool showDestinationMarker)
        {
            if (ship == null || !ship.IsMoving)
            {
                Hide();
                return;
            }

            // The dashes are laid out from the start of the line: from the destination,
            // so that they stay where they are while the ship sails towards it.
            SetPoints(sailedLine, projection, ship.SailedWaypoints, ship.Position, reversed: false);
            SetPoints(remainingLine, projection, ship.RemainingWaypoints, ship.Position, reversed: true);

            Vector2 destination = projection.NormalizedToWorld(ship.Destination.Value);
            Transform marker = destinationMarker.transform;
            marker.position = new Vector3(destination.x, destination.y, marker.position.z);
            destinationMarker.enabled = showDestinationMarker;
        }

        public void Hide()
        {
            sailedLine.enabled = false;
            remainingLine.enabled = false;
            destinationMarker.enabled = false;
        }

        /// <summary>Keeps the lines and the marker at a constant on-screen size.</summary>
        public void SetScale(float worldUnitsPerPixel)
        {
            // The negated comparison also rejects NaN.
            if (!(worldUnitsPerPixel > 0f))
            {
                return;
            }

            float width = lineWidthPixels * worldUnitsPerPixel;
            sailedLine.widthMultiplier = width;
            remainingLine.widthMultiplier = width;

            // A tiled line repeats its texture once per world unit.
            remainingLine.textureScale = new Vector2(1f / (dashPeriodPixels * worldUnitsPerPixel), 1f);

            float markerScale = markerScreenPixelsPerUnit * worldUnitsPerPixel;
            destinationMarker.transform.localScale = new Vector3(markerScale, markerScale, 1f);
        }

        // Both lines end on the ship: the waypoints come first, in their order or reversed.
        private static void SetPoints(
            LineRenderer line,
            MapProjection projection,
            IReadOnlyList<Vector2> waypoints,
            Vector2 shipPosition,
            bool reversed)
        {
            line.enabled = true;
            line.positionCount = waypoints.Count + 1;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector2 waypoint = waypoints[reversed ? waypoints.Count - 1 - i : i];
                line.SetPosition(i, ToWorld(line, projection, waypoint));
            }

            line.SetPosition(waypoints.Count, ToWorld(line, projection, shipPosition));
        }

        private static Vector3 ToWorld(LineRenderer line, MapProjection projection, Vector2 normalized)
        {
            Vector2 world = projection.NormalizedToWorld(normalized);

            return new Vector3(world.x, world.y, line.transform.position.z);
        }
    }
}
