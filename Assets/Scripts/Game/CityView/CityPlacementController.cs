using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Places the building the player chose in the build bar: its ghost follows the cell under the pointer, green
    /// where it can stand connected to the road network, yellow where it can stand unconnected, red where it cannot
    /// or the player cannot pay for it, hidden over the UI and off the grid; R turns it, a click builds it through
    /// <see cref="PlayerBuilder"/> and keeps placing the same building, the right button stops. The road is traced
    /// instead: the left button pressed, dragged and released lays the L from the cell pressed to the cell released,
    /// and keeps tracing. Placing also stops when <see cref="Refresh"/> finds what is placed cannot be paid for.
    /// </summary>
    public sealed class CityPlacementController : MonoBehaviour
    {
        [SerializeField] private CityViewInput input;
        [SerializeField] private CityViewCamera viewCamera;
        [SerializeField] private CityGridView gridView;

        // The road is placed as a 1 by 1 footprint, which never turns.
        private static readonly Footprint RoadCell = new Footprint(1, 1);

        private readonly PlacementCursor cursor = new PlacementCursor();

        // The cells of the trace shown or laid; reused.
        private readonly List<Vector2Int> traceCells = new List<Vector2Int>();

        private CityDefinition city;

        // One of the three while placing, the others null.
        private HouseOffer house;
        private WorkshopOffer workshop;
        private RoadOffer road;

        private Vector2 lastPointer;
        private bool hasPointer;

        // From the press of a trace to its release: the cell pressed and the last cell of the grid the pointer was on.
        private bool isTracing;
        private Vector2Int traceStart;
        private Vector2Int traceEnd;

        public bool IsPlacing => cursor.IsPlacing;

        /// <summary>
        /// Raised with the screen position of a click of the view's input while nothing is placed: such a click
        /// builds nothing and is the view's to use (selecting a ship). While placing, a click places and is not raised.
        /// </summary>
        public event Action<Vector2> ClickedWithoutPlacing;

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
            input.TraceStarted += TraceStart;
            input.TraceMoved += TraceMove;
            input.TraceEnded += OnTraceEnded;
            input.TraceCancelled += OnTraceCancelled;
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Clicked -= OnClicked;
                input.Rotated -= Rotate;
                input.Cancelled -= Cancel;
                input.TraceStarted -= TraceStart;
                input.TraceMoved -= TraceMove;
                input.TraceEnded -= OnTraceEnded;
                input.TraceCancelled -= OnTraceCancelled;
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

        /// <summary>Starts tracing the road, in place of what was being placed: the left button traces from now on.</summary>
        public void StartPlacing(RoadOffer offer)
        {
            if (offer == null)
            {
                return;
            }

            house = null;
            workshop = null;
            road = offer;
            isTracing = false;
            input.TracesWithLeftButton = true;
            cursor.Start(RoadCell);

            if (hasPointer)
            {
                Hover(lastPointer);
            }
        }

        /// <summary>
        /// Moves the ghost to the cell under the screen position (origin at the bottom-left); while a road is traced,
        /// shows the trace from its first cell to that one, or to the last cell of the grid the pointer was on.
        /// </summary>
        public void Hover(Vector2 screenPosition)
        {
            lastPointer = screenPosition;
            hasPointer = true;

            // A build may stop placing (Refresh finds the next one cannot be paid for): no ghost is left behind.
            if (!cursor.IsPlacing)
            {
                gridView.HideGhost();
                return;
            }

            bool onGrid = TryGetHoveredCell(screenPosition, out Vector2Int cell, out CityMarket market);

            if (road != null)
            {
                HoverRoad(onGrid, cell);
                return;
            }

            if (!onGrid)
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
        /// Stops placing when what is being placed cannot be paid for any more (gold or materials; for the road, a
        /// single cell), so that the player sees its offer disabled; called whenever the build bar quotes its offers again.
        /// </summary>
        public void Refresh()
        {
            if (cursor.IsPlacing && !CanPay())
            {
                Cancel();
            }
        }

        /// <summary>Builds what is being placed at the cell under the screen position, and keeps placing it.</summary>
        /// <returns>True when it was built; nothing was paid otherwise, nor for the road, which is traced.</returns>
        public bool Click(Vector2 screenPosition)
        {
            if (road != null || !TryGetHoveredCell(screenPosition, out Vector2Int cell, out _))
            {
                return false;
            }

            PlayerBuilder builder = Builder();

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

        /// <summary>Starts a trace of the road at the cell under the screen position; nothing off the grid or over the UI.</summary>
        public void TraceStart(Vector2 screenPosition)
        {
            if (road == null || !TryGetHoveredCell(screenPosition, out Vector2Int cell, out _))
            {
                return;
            }

            isTracing = true;
            traceStart = cell;
            traceEnd = cell;
            Hover(screenPosition);
        }

        /// <summary>Follows the trace in progress to the cell under the screen position.</summary>
        public void TraceMove(Vector2 screenPosition)
        {
            if (isTracing)
            {
                Hover(screenPosition);
            }
        }

        /// <summary>
        /// Ends the trace in progress at the cell under the screen position, or the last cell of the grid the pointer
        /// was on, and lays its roads; the road is still traced afterwards.
        /// </summary>
        /// <returns>True when roads were laid; nothing was paid otherwise.</returns>
        public bool TraceEnd(Vector2 screenPosition)
        {
            if (!isTracing)
            {
                return false;
            }

            // A release over the UI (the build bar, say) builds nothing.
            if (input != null && input.IsOverUi(screenPosition))
            {
                isTracing = false;
                Hover(screenPosition);
                return false;
            }

            Hover(screenPosition);
            isTracing = false;
            RoadTrace.Of(traceStart, traceEnd, traceCells);

            PlayerBuilder builder = Builder();
            bool built = builder != null && builder.TryBuildRoads(city, road, traceCells);

            // The trace is laid: the ghost is one cell again.
            Hover(screenPosition);
            return built;
        }

        /// <summary>Turns what is being placed; nothing for a square footprint, the road, or when not placing.</summary>
        public void Rotate()
        {
            cursor.Rotate();

            if (cursor.IsPlacing && hasPointer)
            {
                Hover(lastPointer);
            }
        }

        /// <summary>Stops placing; the left button clicks and pans again.</summary>
        public void Cancel()
        {
            cursor.Stop();
            house = null;
            workshop = null;
            road = null;
            isTracing = false;

            if (input != null)
            {
                input.TracesWithLeftButton = false;
            }

            if (gridView != null)
            {
                gridView.HideGhost();
            }
        }

        private void OnClicked(Vector2 screenPosition)
        {
            if (!cursor.IsPlacing)
            {
                ClickedWithoutPlacing?.Invoke(screenPosition);
                return;
            }

            Click(screenPosition);
        }

        private void OnTraceEnded(Vector2 screenPosition)
        {
            TraceEnd(screenPosition);
        }

        // The press was lost (focus, mode changed): nothing is laid, the ghost is one cell again.
        private void OnTraceCancelled()
        {
            if (!isTracing)
            {
                return;
            }

            isTracing = false;

            if (hasPointer)
            {
                Hover(lastPointer);
            }
        }

        private void HoverRoad(bool onGrid, Vector2Int cell)
        {
            if (isTracing)
            {
                // Off the grid or over the UI, the trace stays at the last cell of the grid the pointer was on.
                if (onGrid)
                {
                    traceEnd = cell;
                }

                RoadTrace.Of(traceStart, traceEnd, traceCells);
            }
            else if (onGrid)
            {
                traceCells.Clear();
                traceCells.Add(cell);
            }
            else
            {
                gridView.HideGhost();
                return;
            }

            PlayerBuilder builder = Builder();
            GhostState state = builder == null || !builder.QuoteRoads(city, road, traceCells).IsPossible ? GhostState.Impossible
                : JoinsNetwork(traceCells) ? GhostState.Connected
                : GhostState.Unconnected;
            gridView.ShowRoadGhost(traceCells, state);
        }

        private bool JoinsNetwork(List<Vector2Int> cells)
        {
            CityGrid grid = gridView.Grid;

            foreach (Vector2Int cell in cells)
            {
                if (grid.IsNetworkRoad(cell) || grid.JoinsNetwork(cell))
                {
                    return true;
                }
            }

            return false;
        }

        // Whether the player can pay now for what is being placed, as its offer in the build bar is quoted.
        private bool CanPay()
        {
            PlayerBuilder builder = Builder();

            if (builder == null || city == null)
            {
                return false;
            }

            ConstructionQuote quote = house != null ? builder.QuoteHouse(city, house)
                : workshop != null ? builder.QuoteWorkshop(city, workshop)
                : road != null ? builder.QuoteRoadCell(city, road)
                : default;
            return quote.IsPossible;
        }

        private static PlayerBuilder Builder()
        {
            return SimulationServices.Current != null ? SimulationServices.Current.PlayerBuilder : null;
        }

        private bool TryStart(Footprint footprint, string offerName)
        {
            // Only the world economy's offers carry their footprint; one made without it is 0 by 0.
            if (footprint.Width < 1 || footprint.Depth < 1)
            {
                Debug.LogError($"The offer '{offerName}' has no footprint; it cannot be placed.", this);
                return false;
            }

            road = null;
            isTracing = false;
            input.TracesWithLeftButton = false;
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
