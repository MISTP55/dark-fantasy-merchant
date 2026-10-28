using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The catalogue of the buildings the player can build: exactly one house per class, which
    /// also makes the houses the cities build of their own, and one workshop per good at most.
    /// Holds no runtime state: what a city has built is in its <see cref="CityMarket"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Buildings", menuName = "Dark Fantasy Merchant/Buildings")]
    public sealed class BuildingsDefinition : ScriptableObject
    {
        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

        [Tooltip("The houses, exactly one per class.")]
        [SerializeField] private List<HouseDefinition> houses = new List<HouseDefinition>();

        [Tooltip("The workshops, one per good at most. A workshop of a good that is not active is ignored.")]
        [SerializeField] private List<WorkshopDefinition> workshops = new List<WorkshopDefinition>();

        [Tooltip("The road, which the player and the cities lay cell by cell.")]
        [SerializeField] private RoadDefinition road;

        [Tooltip("The town hall every city has, placed by its layout.")]
        [SerializeField] private TownHallDefinition townHall;

        [Tooltip("The port every city has, placed by its layout.")]
        [SerializeField] private PortDefinition port;

        [Tooltip("The trading post the player builds on the site reserved in each city.")]
        [SerializeField] private TradingPostDefinition tradingPost;

        [Tooltip("The wall of every city's walls, placed by its layout.")]
        [SerializeField] private WallDefinition wall;

        [Tooltip("How the buildings choose their place on a city's grid, for the whole world.")]
        [SerializeField] private PlacementStrategyDefinition placementStrategy;

        [Tooltip("When a road touches a building or another road, in every city: by a side or a corner, or by a side only. Read when a game starts, and by the layout tools.")]
        [SerializeField] private RoadContact roadContact = RoadContact.SidesAndCorners;

        public PlacementStrategyDefinition PlacementStrategy => placementStrategy;

        /// <summary>When a road touches a building or another road, for every city's grid.</summary>
        public RoadContact RoadContact => roadContact;

        public RoadDefinition Road => road;

        public TownHallDefinition TownHall => townHall;

        public PortDefinition Port => port;

        public WallDefinition Wall => wall;

        public TradingPostDefinition TradingPost => tradingPost;

        public IReadOnlyList<HouseDefinition> Houses => houses;

        public IReadOnlyList<WorkshopDefinition> Workshops => workshops;

        /// <returns>The house of a class, or null when the catalogue has none.</returns>
        public HouseDefinition HouseOf(SocialClass housedClass)
        {
            return houses.Find(house => house != null && house.SocialClass == housedClass);
        }

        /// <returns>The workshop of a good, or null when the catalogue has none.</returns>
        public WorkshopDefinition WorkshopOf(GoodDefinition good)
        {
            return good == null ? null : workshops.Find(workshop => workshop != null && workshop.Good == good);
        }

        /// <returns>The definition of a building on a grid, or null when the catalogue has none.</returns>
        /// <param name="economy">Gives the good of a workshop by its index among the active goods.</param>
        public BuildingDefinition DefinitionOf(BuildingKind kind, EconomyDefinition economy)
        {
            if (kind.IsHouse)
            {
                return HouseOf(kind.HousedClass);
            }

            if (kind.IsWorkshop)
            {
                bool known = economy != null && kind.Good >= 0 && kind.Good < economy.Goods.Count;
                return known ? WorkshopOf(economy.Goods[kind.Good]) : null;
            }

            return kind.IsRoad ? road : kind.IsPort ? port : kind.IsWall ? wall : kind.IsTradingPost ? tradingPost : townHall;
        }

        /// <summary>The places of the house of each class. Only for a catalogue that <see cref="TryValidate"/> accepts.</summary>
        public PerClass<int> HouseRooms => new PerClass<int>(
            HouseOf(SocialClass.Rich).Room, HouseOf(SocialClass.Wealthy).Room, HouseOf(SocialClass.Poor).Room);

        /// <summary>
        /// Whether the buildings can be used at all: every definition is usable, each class has
        /// exactly one house, which also sets the houses of every city at the start, and each good
        /// one workshop at most, active or not. A cost that cannot be worked out does not count
        /// here: it only makes its building unavailable (<see cref="BuildingDefinition.TryCreateCost"/>).
        /// </summary>
        /// <param name="problem">What is wrong, for the console; null when nothing is.</param>
        public bool TryValidate(out string problem)
        {
            problem = ProblemOfEntries(houses, "houses") ?? ProblemOfEntries(workshops, "workshops");

            if (problem != null)
            {
                return false;
            }

            if (road == null)
            {
                problem = "it has no road";
                return false;
            }

            if (!road.TryValidate(out string roadProblem))
            {
                problem = $"'{road.name}': {roadProblem}";
                return false;
            }

            if (townHall == null)
            {
                problem = "it has no town hall";
                return false;
            }

            if (!townHall.TryValidate(out string townHallProblem))
            {
                problem = $"'{townHall.name}': {townHallProblem}";
                return false;
            }

            if (port == null)
            {
                problem = "it has no port";
                return false;
            }

            if (!port.TryValidate(out string portProblem))
            {
                problem = $"'{port.name}': {portProblem}";
                return false;
            }

            if (wall == null)
            {
                problem = "it has no wall";
                return false;
            }

            if (!wall.TryValidate(out string wallProblem))
            {
                problem = $"'{wall.name}': {wallProblem}";
                return false;
            }

            if (tradingPost == null)
            {
                problem = "it has no trading post";
                return false;
            }

            if (!tradingPost.TryValidate(out string tradingPostProblem))
            {
                problem = $"'{tradingPost.name}': {tradingPostProblem}";
                return false;
            }

            if (placementStrategy == null)
            {
                problem = "it has no placement strategy";
                return false;
            }

            foreach (SocialClass housedClass in Classes)
            {
                List<HouseDefinition> ofClass = houses.FindAll(house => house.SocialClass == housedClass);

                if (ofClass.Count == 0)
                {
                    problem = $"the {ClassName(housedClass)} have no house";
                    return false;
                }

                if (ofClass.Count > 1)
                {
                    problem = $"the {ClassName(housedClass)} have {ofClass.Count} houses: '{ofClass[0].name}' and '{ofClass[1].name}'";
                    return false;
                }
            }

            foreach (WorkshopDefinition workshop in workshops)
            {
                List<WorkshopDefinition> ofGood = workshops.FindAll(other => other.Good == workshop.Good);

                if (ofGood.Count > 1)
                {
                    problem = $"{workshop.Good.DisplayName} has {ofGood.Count} workshops: '{ofGood[0].name}' and '{ofGood[1].name}'";
                    return false;
                }
            }

            return true;
        }

        /// <summary>The houses as the player can build them, in the catalogue's order.</summary>
        /// <param name="unavailable">Told of each house whose cost cannot be worked out, with why; may be null.</param>
        public IReadOnlyList<HouseOffer> CreateHouseOffers(EconomyDefinition economy, Action<BuildingDefinition, string> unavailable)
        {
            var offers = new List<HouseOffer>();

            foreach (HouseDefinition house in houses)
            {
                BuildingCost cost = CostOf(house, economy, unavailable);
                offers.Add(new HouseOffer(house.SocialClass, house.DisplayName, house.Room, cost, house.CostText(economy), house.Footprint));
            }

            return offers;
        }

        /// <summary>The road as the player can build it. Only for a catalogue that <see cref="TryValidate"/> accepts.</summary>
        /// <param name="unavailable">Told when the road's cost cannot be worked out, with why; may be null.</param>
        public RoadOffer CreateRoadOffer(EconomyDefinition economy, Action<BuildingDefinition, string> unavailable)
        {
            return new RoadOffer(road.DisplayName, CostOf(road, economy, unavailable), road.CostText(economy));
        }

        /// <summary>The trading post as the player can build it. Only for a catalogue that <see cref="TryValidate"/> accepts.</summary>
        /// <param name="unavailable">Told when the trading post's cost cannot be worked out, with why; may be null.</param>
        public TradingPostOffer CreateTradingPostOffer(EconomyDefinition economy, Action<BuildingDefinition, string> unavailable)
        {
            return new TradingPostOffer(
                tradingPost.DisplayName, CostOf(tradingPost, economy, unavailable), tradingPost.CostText(economy), tradingPost.Footprint);
        }

        /// <summary>
        /// The workshops as the player can build them, in the catalogue's order, but for those of
        /// a good that is not active, which are left out.
        /// </summary>
        /// <param name="unavailable">Told of each workshop whose cost cannot be worked out, with why; may be null.</param>
        public IReadOnlyList<WorkshopOffer> CreateWorkshopOffers(EconomyDefinition economy, Action<BuildingDefinition, string> unavailable)
        {
            var offers = new List<WorkshopOffer>();

            foreach (WorkshopDefinition workshop in workshops)
            {
                int good = economy.IndexOf(workshop.Good);

                if (good < 0)
                {
                    continue;
                }

                BuildingCost cost = CostOf(workshop, economy, unavailable);
                offers.Add(new WorkshopOffer(good, workshop.DisplayName, workshop.Jobs, cost, workshop.CostText(economy), workshop.Footprint));
            }

            return offers;
        }

        private static BuildingCost CostOf(BuildingDefinition building, EconomyDefinition economy, Action<BuildingDefinition, string> unavailable)
        {
            if (building.TryCreateCost(economy, out BuildingCost cost, out string problem))
            {
                return cost;
            }

            unavailable?.Invoke(building, problem);
            return null;
        }

        private static string ProblemOfEntries<T>(List<T> entries, string list) where T : BuildingDefinition
        {
            foreach (T entry in entries)
            {
                if (entry == null)
                {
                    return $"its {list} have an empty entry";
                }

                if (!entry.TryValidate(out string problem))
                {
                    return $"'{entry.name}': {problem}";
                }
            }

            return null;
        }

        private static string ClassName(SocialClass housedClass)
        {
            switch (housedClass)
            {
                case SocialClass.Rich:
                    return "rich";
                case SocialClass.Wealthy:
                    return "wealthy";
                case SocialClass.Poor:
                    return "poor";
                default:
                    throw new ArgumentOutOfRangeException(nameof(housedClass));
            }
        }
    }
}
