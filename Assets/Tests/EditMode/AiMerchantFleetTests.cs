using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class AiMerchantFleetTests
    {
        private sealed class Port
        {
        }

        private const int Fish = 0;
        private const float Speed = 4f;
        private const double Stopover = 30d;

        // Ports: H the ships' home, D a final destination, S a stopover between them, S2 another one.
        private const int H = 0;
        private const int D = 1;
        private const int S = 2;
        private const int S2 = 3;

        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;
        private ShipDocking<Port> docking;
        private Port[] ports;
        private Vector2[] positions;
        private CityMarket[] markets;
        private AiMerchantFleet<Port> fleet;

        [SetUp]
        public void SetUp()
        {
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);
            docking = new ShipDocking<Port>();
        }

        /// <summary>
        /// One good, base price 10, consumed by the day, no workshop. Thresholds in days of its
        /// consumption: T0 10, T1 30, their middle 20, the target point 17.5, T3 40.
        /// </summary>
        private static CityMarket Market(double days, double dailyConsumption = 2)
        {
            return new CityMarket(
                new[] { new MarketGood(10, dailyConsumption, 0, 0) },
                TestMarkets.Thresholds,
                TestMarkets.Workforce,
                days,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));
        }

        /// <summary>Three ports: H to D 2 days, D to S 1 day, S to H 1.1 days, both ways.</summary>
        private void CreateFleet(CityMarket home, CityMarket destination, CityMarket stopover)
        {
            var days = new double[3, 3];
            days[H, D] = days[D, H] = 2d;
            days[D, S] = days[S, D] = 1d;
            days[S, H] = days[H, S] = 1.1d;

            CreateFleet(days, home, destination, stopover);
        }

        private void CreateFleet(double[,] days, params CityMarket[] cityMarkets)
        {
            markets = cityMarkets;

            var allPositions = new[]
            {
                GridAssert.CellCenter(grid, 0, 3), GridAssert.CellCenter(grid, 5, 0),
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellCenter(grid, 7, 2),
            };

            ports = new Port[markets.Length];
            positions = new Vector2[markets.Length];

            for (int i = 0; i < markets.Length; i++)
            {
                ports[i] = new Port();
                positions[i] = allPositions[i];
            }

            fleet = new AiMerchantFleet<Port>(docking, ports, positions, markets, new SupplyPlanner(markets, days), Stopover);
        }

        /// <summary>H with 60 days of fish, D with none, S with 20.</summary>
        private void CreateOutAndBack()
        {
            CreateFleet(Market(60), Market(0), Market(20));
        }

        /// <summary>H with no fish, D with 100 days of it, S with 20.</summary>
        private void CreateHomeShort()
        {
            CreateFleet(Market(0), Market(100), Market(20));
        }

        private Ship AddShip(int home)
        {
            var ship = new Ship(projection, positions[home], Speed, TestCrew.Full, navigation, new CargoHold(200));
            fleet.Add(ship, home);
            docking.Update();
            return ship;
        }

        private void Arrive(Ship ship)
        {
            ship.Advance(1000f);
            docking.Update();
        }

        private void Depart()
        {
            fleet.Advance(Stopover);
        }

        [Test]
        public void AShipAdded_EntersItsHome()
        {
            CreateOutAndBack();

            Ship ship = AddShip(H);

            Assert.AreSame(ports[H], docking.PortOf(ship));
            Assert.IsTrue(fleet.Contains(ship));
            Assert.AreEqual(1, fleet.Count);
            Assert.AreEqual(0, fleet.ProfitOf(ship));
            Assert.AreEqual(H, fleet.HomeOf(ship));
        }

        [Test]
        public void DuringTheStopover_ItStaysInPort()
        {
            CreateOutAndBack();
            Ship ship = AddShip(H);

            fleet.Advance(Stopover - 1d);

            Assert.IsTrue(docking.IsDocked(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
        }

        [Test]
        public void ALongStep_BuysOnce()
        {
            CreateOutAndBack();
            Ship ship = AddShip(H);
            int purchases = 0;
            markets[H].Changed += () => purchases++;

            fleet.Advance(Stopover * 10d);
            fleet.Advance(Stopover * 10d);

            // One good, so one purchase changes the market once.
            Assert.AreEqual(1, purchases);
            Assert.AreSame(ports[D], docking.DestinationPortOf(ship));
            Assert.AreEqual(120 - ship.Cargo.Used, markets[H].AvailableOf(Fish));
        }

        [Test]
        public void FromHome_ItSailsStraightToItsFinalDestination()
        {
            // D needs 60 (score 1.4), S 22 (0.62): D. S would be a short detour, but none is made on the way out.
            CreateFleet(Market(60), Market(0), Market(20));
            Ship ship = AddShip(H);

            Assert.AreEqual(D, fleet.FinalDestinationOf(ship));
            Depart();

            Assert.AreSame(ports[D], docking.DestinationPortOf(ship));
            Assert.AreEqual(60, ship.Cargo.UnitsOf(Fish));
            Assert.AreEqual(60, fleet.UnderWayTo(D, Fish));
            Assert.Less(fleet.ProfitOf(ship), 0);
        }

        [Test]
        public void AtItsFinalDestination_ItUnloadsEverything_AndHeadsHomeByAStopover()
        {
            CreateOutAndBack();
            Ship ship = AddShip(H);
            Depart();

            Arrive(ship);

            Assert.AreSame(ports[D], docking.PortOf(ship));
            Assert.AreEqual(60, markets[D].AvailableOf(Fish));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(0, fleet.UnderWayTo(D, Fish));
            Assert.AreEqual(H, fleet.FinalDestinationOf(ship));

            // Nothing for home (H at T1: a quantity of 1.78, under a lot), 9 for S.
            Depart();

            Assert.AreSame(ports[S], docking.DestinationPortOf(ship));
            Assert.AreEqual(9, ship.Cargo.UnitsOf(Fish));
            Assert.AreEqual(0, fleet.UnderWayTo(H, Fish));
            Assert.AreEqual(9, fleet.UnderWayTo(S, Fish));
        }

        [Test]
        public void BackHome_ItUnloadsEverything_AndChoosesANewDestination()
        {
            CreateOutAndBack();
            Ship ship = AddShip(H);
            Depart();
            Arrive(ship);
            Depart();

            Arrive(ship);
            Assert.AreSame(ports[S], docking.PortOf(ship));
            Assert.AreEqual(49, markets[S].AvailableOf(Fish));

            Depart();
            Assert.AreSame(ports[H], docking.DestinationPortOf(ship));
            Assert.AreEqual(0, ship.Cargo.Used);

            Arrive(ship);

            // S, at 49, needs 5 from home's 60.
            Assert.AreSame(ports[H], docking.PortOf(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(S, fleet.FinalDestinationOf(ship));
        }

        [Test]
        public void UnderWay_IsRecordedOnLoading_AndClearedOnEnteringThePort()
        {
            // What D would bring back: 1.4.
            CreateHomeShort();
            Ship ship = AddShip(H);
            Assert.AreEqual(D, fleet.FinalDestinationOf(ship));

            Depart();
            Assert.AreEqual(0, ship.Cargo.Used);
            Arrive(ship);
            Depart();

            Assert.AreSame(ports[S], docking.DestinationPortOf(ship));
            Assert.AreEqual(82, ship.Cargo.UnitsOf(Fish));
            Assert.AreEqual(60, fleet.UnderWayTo(H, Fish));
            Assert.AreEqual(22, fleet.UnderWayTo(S, Fish));

            Arrive(ship);

            Assert.AreEqual(0, fleet.UnderWayTo(S, Fish));
            Assert.AreEqual(60, fleet.UnderWayTo(H, Fish));
        }

        [Test]
        public void AtAStopover_ItSellsOnlyUpToT1_AndKeepsWhatHomeLacks()
        {
            CreateHomeShort();
            Ship ship = AddShip(H);
            Depart();
            Arrive(ship);
            Depart();

            Arrive(ship);

            // Keeps 60 for home, sells 20.
            Assert.AreSame(ports[S], docking.PortOf(ship));
            Assert.AreEqual(60, markets[S].AvailableOf(Fish));
            Assert.AreEqual(62, ship.Cargo.UnitsOf(Fish));
        }

        [Test]
        public void AtAStopover_ItDoesNotBuyAgainWhatItCarriesForHome()
        {
            CreateHomeShort();
            Ship ship = AddShip(H);
            Depart();
            Arrive(ship);
            Depart();
            Arrive(ship);

            // H projected at 57.8 with its own 60 under way.
            Depart();

            Assert.AreSame(ports[H], docking.DestinationPortOf(ship));
            Assert.AreEqual(62, ship.Cargo.UnitsOf(Fish));
            Assert.AreEqual(60, markets[S].AvailableOf(Fish));
        }

        [Test]
        public void APartialSale_CutsWhatIsUnderWay_ToWhatIsLeftAboard()
        {
            CreateHomeShort();
            Ship ship = AddShip(H);
            Depart();
            Arrive(ship);
            Depart();
            Assert.AreEqual(82, ship.Cargo.UnitsOf(Fish));

            // Nothing lacks at home any more, and S is empty.
            markets[H].Put(Fish, 60);
            markets[S].Take(Fish, 40);
            Arrive(ship);

            Assert.AreEqual(60, markets[S].AvailableOf(Fish));
            Assert.AreEqual(22, ship.Cargo.UnitsOf(Fish));
            Assert.AreEqual(22, fleet.UnderWayTo(H, Fish));
        }

        [Test]
        public void ARemovedShip_NoLongerCountsAsUnderWay()
        {
            CreateHomeShort();
            Ship ship = AddShip(H);
            Depart();
            Arrive(ship);
            Depart();

            fleet.Remove(ship);

            Assert.AreEqual(0, fleet.UnderWayTo(H, Fish));
            Assert.AreEqual(0, fleet.UnderWayTo(S, Fish));
        }

        [Test]
        public void TwoShips_DoNotSupplyTheSameLack()
        {
            CreateOutAndBack();
            Ship first = AddShip(H);
            Depart();
            Assert.AreSame(ports[D], docking.DestinationPortOf(first));
            Assert.AreEqual(60, first.Cargo.UnitsOf(Fish));

            Ship second = AddShip(H);

            // D projected at 56 with the first ship's 60.
            Assert.AreEqual(S, fleet.FinalDestinationOf(second));
        }

        [Test]
        public void AtHomeWithNoDestination_ItWaitsAndTriesAgain()
        {
            // Every port at 15 days, under the middle of T0 and T1: no port gives anything.
            CreateFleet(Market(15), Market(15), Market(15));
            Ship ship = AddShip(H);
            Assert.AreEqual(-1, fleet.FinalDestinationOf(ship));

            Depart();
            Assert.IsTrue(docking.IsDocked(ship));

            // A surplus appears: not acted on before the next stopover has passed.
            markets[H].Put(Fish, 200);
            fleet.Advance(Stopover - 1d);
            Assert.IsTrue(docking.IsDocked(ship));

            fleet.Advance(1d);
            Assert.IsFalse(docking.IsDocked(ship));
            Assert.Greater(ship.Cargo.Used, 0);
        }

        [Test]
        public void WithAQuarterOfTheHoldOrLessFree_ItDropsTheStopover()
        {
            // H consumes 6 a day (T1 180). At D, S is the stopover (it needs 60), but home takes 180 of the 200 barrels.
            CreateFleet(Market(0, 6), Market(120), Market(0));
            Ship ship = AddShip(H);
            Depart();
            Arrive(ship);

            Depart();

            Assert.AreSame(ports[H], docking.DestinationPortOf(ship));
            Assert.AreEqual(180, ship.Cargo.UnitsOf(Fish));
            Assert.AreEqual(0, fleet.UnderWayTo(S, Fish));
            Assert.AreEqual(0, markets[S].AvailableOf(Fish));
        }

        [Test]
        public void ACity_IsAStopoverOnlyOncePerReturnTrip()
        {
            var days = new double[4, 4];
            days[H, D] = days[D, H] = 4d;
            days[D, S] = days[S, D] = 1d;
            days[D, S2] = days[S2, D] = 1d;
            days[S, S2] = days[S2, S] = 0.5d;
            days[S, H] = days[H, S] = 3.5d;
            days[S2, H] = days[H, S2] = 3.5d;
            CreateFleet(days, Market(20), Market(100), Market(0), Market(0));
            Ship ship = AddShip(H);
            Assert.AreEqual(D, fleet.FinalDestinationOf(ship));
            Depart();
            Arrive(ship);

            Depart();
            Assert.AreSame(ports[S], docking.DestinationPortOf(ship));
            Arrive(ship);

            // S sold up to its T1 (60), then gave 20 of them for S2.
            Depart();
            Assert.AreSame(ports[S2], docking.DestinationPortOf(ship));
            Assert.AreEqual(40, markets[S].AvailableOf(Fish));

            // S empty again is worth a stopover from S2 (1.4) and passes the detour (0.5 + 3.5 < 4.2).
            markets[S2].Put(Fish, 200);
            markets[S].Take(Fish, 40);
            Arrive(ship);
            Assert.AreSame(ports[S2], docking.PortOf(ship));

            Depart();

            Assert.AreSame(ports[H], docking.DestinationPortOf(ship));
        }

        [Test]
        public void ShipsHomedAt_CountsTheShipsOfEachHome()
        {
            CreateOutAndBack();
            Ship first = AddShip(H);
            AddShip(H);
            AddShip(D);

            Assert.AreEqual(2, fleet.ShipsHomedAt(H));
            Assert.AreEqual(1, fleet.ShipsHomedAt(D));
            Assert.AreEqual(0, fleet.ShipsHomedAt(S));

            fleet.Remove(first);

            Assert.AreEqual(1, fleet.ShipsHomedAt(H));
        }

        [Test]
        public void AShipThatIsNotOfTheFleet_IsLeftAlone()
        {
            CreateOutAndBack();
            var stranger = new Ship(projection, positions[H], Speed, TestCrew.Full, navigation, new CargoHold(200));
            stranger.Cargo.TryAdd(Fish, 10);

            docking.OrderToPort(stranger, ports[D], positions[D]);
            Arrive(stranger);
            fleet.Advance(Stopover * 3d);

            Assert.IsFalse(fleet.Contains(stranger));
            Assert.AreEqual(0, fleet.ProfitOf(stranger));
            Assert.AreEqual(-1, fleet.HomeOf(stranger));
            Assert.AreEqual(-1, fleet.FinalDestinationOf(stranger));
            Assert.AreEqual(10, stranger.Cargo.Used);
            Assert.IsTrue(docking.IsDocked(stranger));
        }

        [Test]
        public void AShipIdleAtSea_IsSentBackToThePortItLeft()
        {
            CreateOutAndBack();
            Ship ship = AddShip(H);
            fleet.Advance(Stopover);

            // An order that is not the fleet's leaves it at sea, with no port to enter.
            docking.OrderToPoint(ship, GridAssert.CellCenter(grid, 2, 3));
            ship.Advance(1000f);
            docking.Update();
            Assert.IsFalse(docking.IsDocked(ship));

            fleet.Advance(1d);

            Assert.AreSame(ports[H], docking.DestinationPortOf(ship));
        }

        [Test]
        public void ARemovedShip_NoLongerTrades()
        {
            CreateOutAndBack();
            Ship ship = AddShip(H);

            Assert.IsTrue(fleet.Remove(ship));
            fleet.Advance(Stopover * 3d);

            Assert.IsFalse(fleet.Contains(ship));
            Assert.AreEqual(0, fleet.Count);
            Assert.IsTrue(docking.IsDocked(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(120, markets[H].AvailableOf(Fish));
        }

        [Test]
        public void ARemovedShip_NoLongerSellsWhenItDocks()
        {
            CreateOutAndBack();
            Ship ship = AddShip(H);
            ship.Cargo.TryAdd(Fish, 5);

            fleet.Remove(ship);
            docking.OrderToPort(ship, ports[D], positions[D]);
            Arrive(ship);

            Assert.AreSame(ports[D], docking.PortOf(ship));
            Assert.AreEqual(5, ship.Cargo.Used);
            Assert.AreEqual(0, markets[D].AvailableOf(Fish));
            Assert.AreEqual(0, fleet.ProfitOf(ship));
        }

        [Test]
        public void Remove_OfAShipThatIsNotOfTheFleet_IsRefused()
        {
            CreateOutAndBack();
            var stranger = new Ship(projection, positions[H], Speed, TestCrew.Full, navigation, new CargoHold(200));

            Assert.IsFalse(fleet.Remove(stranger));
            Assert.IsFalse(fleet.Remove(null));
        }
    }
}
