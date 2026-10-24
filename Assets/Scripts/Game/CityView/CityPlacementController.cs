using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Places the building the player chose in the build bar: its ghost follows the cell under the pointer, green
    /// where it can stand and red where it cannot or the player cannot pay for it, hidden over the UI and off the
    /// grid; R turns it, a click builds it through <see cref="PlayerBuilder"/> and keeps placing the same building,
    /// the right button stops. Placing also stops when <see cref="Refresh"/> finds the building cannot be paid for.
    /// </summary>
    public sealed class CityPlacementController : MonoBehaviour
    {
        [SerializeField] private CityViewInput input;
        [SerializeField] private CityViewCamera viewCamera;
        [SerializeField] private CityGridView gridView;

        private readonly PlacementCursor cursor = new PlacementCursor();

        private CityDefinition city;

        // One of the two while placing, the other null.
        private HouseOffer house;
        private WorkshopOffer workshop;

        private Vector2 lastPointer;
        private bool hasPointer;

        public bool IsPlacing => cursor.IsPlacing;

        public BuildingOrientation Orientation => cursor.Orientation;

        private void OnEnable()
        {
            if (input == null || viewCamera == null || gridView == null)
            {
                Debug.LogError("CityPlacementController needs the view's input, camera and grid view.", this);
                enabled = false;
                return;
            }

            input.Clicked += OnClicked;
            input.Rotated += Rotate;
            input.Cancelled += Cancel;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Clicked -= OnClicked;
                input.Rotated -= Rotate;
                input.Cancelled -= Cancel;
            }

            Cancel();
        }

        // The ghost follows the pointer, and the grid under it as the camera moves.
        private void Update()
        {
            if (cursor.IsPlacing)
            {
                Hover(input.PointerPosition);
            }
        }

        /// <summary>Places in this city from now on; whatever was being placed is dropped.</summary>
        public void Show(CityDefinition shownCity)
        {
            Cancel();
            city = shownCity;
        }

        /// <summary>Starts placing the house, in place of what was being placed.</summary>
        public void StartPlacing(HouseOffer offer)
        {
            if (offer == null || !TryStart(offer.Footprint, offer.Name))
            {
                return;
            }

            house = offer;
            workshop = null;
        }

        /// <summary>Starts placing the workshop, in place of what was being placed.</summary>
        public void StartPlacing(WorkshopOffer offer)
        {
            if (offer == null || !TryStart(offer.Footprint, offer.Name))
            {
                return;
            }

            house = null;
            workshop = offer;
        }

        /// <summary>Moves the ghost to the cell under the screen position (origin at the bottom-left).</summary>
        public void Hover(Vector2 screenPosition)
        {
            lastPointer = screenPosition;
            hasPointer = true;

            if (!TryGetHoveredCell(screenPosition, out Vector2Int cell, out CityMarket market))
            {
                gridView.HideGhost();
                return;
            }

            GridPlace place = cursor.PlaceAt(cell);
            GhostState state = !Construction.CanStand(market, cursor.Footprint, place) || !CanPay() ? GhostState.Impossible
                : gridView.Grid.WouldConnect(cursor.Footprint, place) ? GhostState.Connected
                : GhostState.Unconnected;
            gridView.ShowGhost(cell, cursor.Footprint.SizeFor(cursor.Orientation), state);
        }

        /// <summary>
        /// Stops placing when what is being placed cannot be paid for any more (gold or materials), so that the
        /// player sees its offer disabled; called whenever the build bar quotes its offers again.
        /// </summary>
        public void Refresh()
        {
            if (cursor.IsPlacing && !CanPay())
            {
                Cancel();
            }
        }

        /// <summary>Builds what is being placed at the cell under the screen position, and keeps placing it.</summary>
        /// <returns>True when it was built; nothing was paid otherwise.</returns>
        public bool Click(Vector2 screenPosition)
        {
            if (!TryGetHoveredCell(screenPosition, out Vector2Int cell, out _))
            {
                return false;
            }

            PlayerBuilder builder = SimulationServices.Current != null ? SimulationServices.Current.PlayerBuilder : null;

            if (builder == null)
            {
                return false;
            }

            GridPlace place = cursor.PlaceAt(cell);
            bool built = house != null
                ? builder.TryBuildHouse(city, house, place)
                : builder.TryBuildWorkshop(city, workshop, place);

            // The place is taken now: the ghost turns red.
            Hover(screenPosition);
            return built;
        }

        /// <summary>Turns what is being placed; nothing for a square footprint or when not placing.</summary>
        public void Rotate()
        {
            cursor.Rotate();

            if (cursor.IsPlacing && hasPointer)
            {
                Hover(lastPointer);
            }
        }

        /// <summary>Stops placing.</summary>
        public void Cancel()
        {
            cursor.Stop();
            house = null;
            workshop = null;

            if (gridView != null)
            {
                gridView.HideGhost();
            }
        }

        private void OnClicked(Vector2 screenPosition)
        {
            Click(screenPosition);
        }

        // Whether the player can pay now for what is being placed, as its offer in the build bar is quoted.
        private bool CanPay()
        {
            PlayerBuilder builder = SimulationServices.Current != null ? SimulationServices.Current.PlayerBuilder : null;

            if (builder == null || city == null)
            {
                return false;
            }

            ConstructionQuote quote = house != null
                ? builder.QuoteHouse(city, house)
                : workshop != null ? builder.QuoteWorkshop(city, workshop) : default;
            return quote.IsPossible;
        }

        private bool TryStart(Footprint footprint, string offerName)
        {
            // Only the world economy's offers carry their footprint; one made without it is 0 by 0.
            if (footprint.Width < 1 || footprint.Depth < 1)
            {
                Debug.LogError($"The offer '{offerName}' has no footprint; it cannot be placed.", this);
                return false;
            }

            cursor.Start(footprint);

            if (hasPointer)
            {
                Hover(lastPointer);
            }

            return true;
        }

        private bool TryGetHoveredCell(Vector2 screenPosition, out Vector2Int cell, out CityMarket market)
        {
            cell = default;
            market = null;
            CityGrid grid = gridView.Grid;
            WorldEconomy economy = SimulationServices.Current != null ? SimulationServices.Current.WorldEconomy : null;

            if (!cursor.IsPlacing || city == null || grid == null || economy == null || input.IsOverUi(screenPosition))
            {
                return false;
            }

            market = economy.MarketOf(city);
            Vector2 world = gridView.transform.InverseTransformPoint(viewCamera.ScreenToWorld(screenPosition));
            cell = IsoProjection.CellAt(world);
            return market != null && grid.Contains(cell);
        }
    }
}
