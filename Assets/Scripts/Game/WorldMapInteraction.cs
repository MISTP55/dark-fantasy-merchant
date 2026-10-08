using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Turns the cursor and clicks into hover and selection of ships and cities, and
    /// right clicks into move orders. At most one thing is hovered, and one selected,
    /// except that a ship in port is selected together with the city it lies in.
    /// </summary>
    public sealed class WorldMapInteraction : MonoBehaviour
    {
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapInput input;
        [SerializeField] private WorldMapCameraController cameraController;

        [Tooltip("Optional. Without it, only cities can be hovered and selected.")]
        [SerializeField] private ShipsView shipsView;

        [Tooltip("How close to a city the cursor must be, in screen pixels, at any zoom level.")]
        [SerializeField, Min(0f)] private float pickRadiusPixels = 20f;

        [Tooltip("How close to a ship the cursor must be, in screen pixels, at any zoom level.")]
        [SerializeField, Min(0f)] private float shipPickRadiusPixels = 24f;

        private bool isBound;

        private void Start()
        {
            if (mapView == null || input == null || cameraController == null)
            {
                Debug.LogError("WorldMapInteraction needs a map view, an input component and a camera controller.", this);
                enabled = false;
                return;
            }

            input.Clicked += OnClicked;
            input.Commanded += OnCommanded;
            input.Cancelled += OnCancelled;
            mapView.Selection.SelectedChanged += OnCitySelected;
            isBound = true;
        }

        private void OnDestroy()
        {
            if (!isBound)
            {
                return;
            }

            if (input != null)
            {
                input.Clicked -= OnClicked;
                input.Commanded -= OnCommanded;
                input.Cancelled -= OnCancelled;
            }

            if (mapView != null)
            {
                mapView.Selection.SelectedChanged -= OnCitySelected;
            }
        }

        private void Update()
        {
            if (!CanPick())
            {
                return;
            }

            bool pointerIsBusy = input.IsPointerOverUi || input.IsDragging;

            // A ship hides the city it sails over.
            Ship ship = pointerIsBusy ? null : PickShip(input.PointerPosition);
            CityDefinition city = pointerIsBusy || ship != null ? null : PickCity(input.PointerPosition);

            if (shipsView != null)
            {
                shipsView.Selection.SetHovered(ship);
            }

            mapView.Selection.SetHovered(city);
        }

        private void LateUpdate()
        {
            if (!CanPick())
            {
                return;
            }

            mapView.SetMarkerScale(cameraController.WorldUnitsPerPixel);

            if (HasShips())
            {
                shipsView.SetShipScale(cameraController.WorldUnitsPerPixel);
            }
        }

        private void OnClicked(Vector2 screenPosition)
        {
            if (!CanPick())
            {
                return;
            }

            Ship ship = PickShip(screenPosition);

            if (ship != null)
            {
                mapView.Selection.ClearSelection();
                shipsView.Selection.Select(ship);
                return;
            }

            ClearShipSelection();

            CityDefinition city = PickCity(screenPosition);

            if (city != null)
            {
                mapView.Selection.Select(city);
            }
            else
            {
                mapView.Selection.ClearSelection();
            }
        }

        private void OnCommanded(Vector2 screenPosition)
        {
            if (!CanPick() || !HasShips())
            {
                return;
            }

            Ship ship = shipsView.Selection.Selected;

            if (ship == null)
            {
                return;
            }

            CityDefinition city = PickCity(screenPosition);

            if (city != null)
            {
                // The ship enters the city's port when it arrives.
                shipsView.Docking.OrderToPort(ship, city, city.MapPosition);
            }
            else
            {
                // The ship clamps the point to the map and ignores an unusable one.
                Vector2 worldPosition = cameraController.ScreenToWorld(screenPosition);
                shipsView.Docking.OrderToPoint(ship, mapView.Projection.WorldToNormalized(worldPosition));
            }

            // A ship that left its port is on the map again: its city's panel closes,
            // which leaves the ship alone selected.
            if (!shipsView.Docking.IsDocked(ship))
            {
                mapView.Selection.ClearSelection();
            }
        }

        // A ship in port is only selected while the panel of its city is open.
        private void OnCitySelected(CityDefinition city)
        {
            if (shipsView == null)
            {
                return;
            }

            Ship ship = shipsView.Selection.Selected;

            if (ship != null && shipsView.Docking.IsDocked(ship)
                && !ReferenceEquals(shipsView.Docking.PortOf(ship), city))
            {
                shipsView.Selection.ClearSelection();
            }
        }

        private void OnCancelled()
        {
            mapView.Selection.ClearSelection();
            ClearShipSelection();
        }

        private void ClearShipSelection()
        {
            if (shipsView != null)
            {
                shipsView.Selection.ClearSelection();
            }
        }

        // Screen height is zero while the window is minimized.
        private bool CanPick()
        {
            return mapView.IsReady && cameraController.IsReady && Screen.height > 0;
        }

        private bool HasShips()
        {
            return shipsView != null && shipsView.IsReady;
        }

        private Ship PickShip(Vector2 screenPosition)
        {
            if (!HasShips())
            {
                return null;
            }

            int index = Pick(shipsView.ShipWorldPositions, screenPosition, shipPickRadiusPixels);

            return index >= 0 ? shipsView.Ships[index] : null;
        }

        private CityDefinition PickCity(Vector2 screenPosition)
        {
            int index = Pick(mapView.CityWorldPositions, screenPosition, pickRadiusPixels);

            return index >= 0 ? mapView.Cities[index] : null;
        }

        private int Pick(IReadOnlyList<Vector2> worldPositions, Vector2 screenPosition, float radiusPixels)
        {
            Vector2 worldPosition = cameraController.ScreenToWorld(screenPosition);
            float worldRadius = radiusPixels * cameraController.WorldUnitsPerPixel;

            return CityPicker.PickNearest(worldPositions, worldPosition, worldRadius);
        }
    }
}
