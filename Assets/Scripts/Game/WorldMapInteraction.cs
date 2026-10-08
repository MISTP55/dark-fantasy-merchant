using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Turns the cursor and clicks into hover and selection of ships and cities, and
    /// right clicks into move orders. At most one thing is hovered, and one selected,
    /// except that a ship in port is selected together with the city it lies in.
    /// During fast forward nothing is hovered, selected or ordered, and any input goes
    /// back to normal speed, as does the arrival of the ship that was selected and under
    /// way when it started, which the camera then shows in close-up. The ship that was
    /// selected is selected again afterwards, or the city it has entered meanwhile.
    /// </summary>
    public sealed class WorldMapInteraction : MonoBehaviour
    {
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapInput input;
        [SerializeField] private WorldMapCameraController cameraController;

        [Tooltip("Optional. Without it, only cities can be hovered and selected.")]
        [SerializeField] private ShipsView shipsView;

        [Tooltip("Optional. With it, nothing can be hovered, selected or ordered during fast forward, and any input ends it.")]
        [SerializeField] private WorldClock worldClock;

        [Tooltip("How close to a city the cursor must be, in screen pixels, at any zoom level.")]
        [SerializeField, Min(0f)] private float pickRadiusPixels = 20f;

        [Tooltip("How close to a ship the cursor must be, in screen pixels, at any zoom level.")]
        [SerializeField, Min(0f)] private float shipPickRadiusPixels = 24f;

        private bool isBound;
        private GameClock clock;

        // The ship that was selected when fast forward started, to select again when it ends.
        private Ship shipBeforeFastForward;

        // That ship when it was under way: the player was waiting for it, so its arrival
        // ends the fast forward.
        private Ship awaitedShip;

        private bool IsFastForward => clock != null && clock.IsFastForward;

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

            if (worldClock != null && worldClock.IsReady)
            {
                clock = worldClock.Clock;
                clock.FastForwardChanged += OnFastForwardChanged;
                input.AnyInput += OnAnyInput;
            }

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
                input.AnyInput -= OnAnyInput;
            }

            if (mapView != null)
            {
                mapView.Selection.SelectedChanged -= OnCitySelected;
            }

            if (clock != null)
            {
                clock.FastForwardChanged -= OnFastForwardChanged;
            }
        }

        private void Update()
        {
            // A ship that enters a port has arrived too.
            if (awaitedShip != null && !awaitedShip.IsMoving)
            {
                // Read before the end of the fast forward forgets the ship.
                Ship arrivedShip = awaitedShip;
                clock.SetFastForward(false);

                // The camera has gone back to the view it had before the fast forward:
                // the player was waiting for this ship, so it shows where it arrived instead.
                cameraController.ShowCloseUp(arrivedShip.WorldPosition);
            }

            if (!CanPick())
            {
                return;
            }

            // Nothing is hovered during fast forward: the map is only watched.
            bool pointerIsBusy = input.IsPointerOverUi || input.IsDragging || IsFastForward;

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
            if (!CanPick() || IsFastForward)
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
            if (!CanPick() || !HasShips() || IsFastForward)
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
                if (!shipsView.Docking.OrderToPort(ship, city, city.MapPosition))
                {
                    // Otherwise the ship sails to the city and stays outside for no visible reason.
                    Debug.LogWarning(
                        $"The ship cannot enter the port of '{city.name}': the water nearest to the city is not water it can sail to.",
                        city);
                }
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
            if (IsFastForward)
            {
                return;
            }

            mapView.Selection.ClearSelection();
            ClearShipSelection();
        }

        // The map is only watched during fast forward: nothing stays hovered or selected,
        // and a click that was started before it is dropped.
        private void OnFastForwardChanged(bool isFastForward)
        {
            if (!isFastForward)
            {
                Ship ship = shipBeforeFastForward;
                shipBeforeFastForward = null;
                awaitedShip = null;
                SelectAgain(ship);
                return;
            }

            // Read before the selection is cleared.
            shipBeforeFastForward = shipsView != null ? shipsView.Selection.Selected : null;
            awaitedShip = shipBeforeFastForward != null && shipBeforeFastForward.IsMoving
                ? shipBeforeFastForward
                : null;

            input.CancelGestures();
            mapView.Selection.SetHovered(null);
            mapView.Selection.ClearSelection();

            if (shipsView != null)
            {
                shipsView.Selection.SetHovered(null);
                shipsView.Selection.ClearSelection();
            }
        }

        // A ship that entered a port during the fast forward is not on the map any more:
        // its city is selected in its place.
        private void SelectAgain(Ship ship)
        {
            if (ship == null || shipsView == null)
            {
                return;
            }

            if (shipsView.Docking.IsDocked(ship))
            {
                mapView.Selection.Select(shipsView.Docking.PortOf(ship));
            }
            else
            {
                shipsView.Selection.Select(ship);
            }
        }

        // Any input ends the fast forward, and does nothing else: a click on a city only
        // goes back to normal speed.
        private void OnAnyInput()
        {
            if (!IsFastForward)
            {
                return;
            }

            clock.SetFastForward(false);
            input.ConsumeInput();
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
